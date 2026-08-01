using System.Collections.Generic;
using System.Linq;
using System.Text;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Generator.Binding;
using Heddle.Generator.Diagnostics;
using Heddle.Generator.Pipeline;
using Heddle.Language;
using Heddle.Language.Binding;
using Heddle.Language.Expressions;
using Heddle.Precompiled;
using Heddle.Strings.Core;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// The per-template structural emitter: turns a parsed <see cref="ParseContext"/> into the
    /// <c>{SanitizedName}.g.cs</c> compilation unit — a public entry class, the piece table, the pre-constructed
    /// bound extension instances, and one <c>IProcessStrategy</c> body per compiled body — in the exact shape the
    /// runtime funnel (<c>PrecompiledRuntime</c>) renders, so both backends produce byte-identical output.
    /// Constructs it cannot yet emit make the template "unsupported": no <c>.g.cs</c>, no manifest entry,
    /// the render takes the unchanged dynamic path (safe incremental growth of the supported family).
    /// </summary>
    internal sealed class TemplateEmitter
    {
        private readonly string _key;

        /// <summary>The file name written into the emitted <c>#line</c> directives. Normally equal to
        /// <see cref="_key"/> — for a path-derived key they are the same string — but the two are different concepts:
        /// an item with explicit <c>Key</c>/<c>Name</c> metadata registers under a key that names no file, and
        /// emitting that as the <c>#line</c> file pointed every mapped span at a path that does not exist. The key
        /// names the <em>registration</em>; this names the <em>file</em>. Callers that pass nothing keep the
        /// historical value, so every existing snapshot and golden is byte-identical. The separation between key
        /// and file name is necessary whenever they diverge.</summary>
        private readonly string _lineDirectiveFile;

        /// <summary>Whether <see cref="_lineDirectiveFile"/> is relative to <c>HeddleTemplateRoot</c> (for templates
        /// under the root) or the template's own path (outside the root, where no anchor exists — absolute in a real
        /// build). Something must state which, because a reader of a <c>#line</c> otherwise cannot tell and the two
        /// forms resolve against different working directories. This metadata lives in the manifest row
        /// (<c>PrecompiledTemplateInfo.LinePathForm</c>) rather than the generated file.</summary>
        private readonly bool _lineDirectiveFileIsRootRelative;

        /// <summary>
        /// Whether <see cref="_lineDirectiveFile"/> has a <c>#line</c> spelling at all. The file name of a
        /// <c>#line</c> is a <c>pp_string</c>, not a string literal: it ends at the first <c>"</c> or line
        /// terminator and no escape sequence is processed inside it. A template whose path carries either — both
        /// are ordinary file-name characters off Windows — has no writable form, and the mapping is dropped rather
        /// than the consumer's build being broken by an unterminated directive.
        /// <para>A backslash is deliberately <b>not</b> in that set. <c>pp_string</c> does not process it, so a
        /// Windows path is already correct written through verbatim.</para>
        /// </summary>
        private readonly bool _lineDirectiveFileIsWritable;

        /// <summary>The template's optional registered <c>Name</c>, already normalized, or null. Emitted onto
        /// the manifest row so the runtime registry can answer to it; it affects nothing else the emitter produces —
        /// not the entry-class identifier, not the <c>#line</c> file, not the key.</summary>
        private readonly string _registeredName;
        private readonly string _sanitizedName;
        private readonly string _namespace;
        private readonly string _cleanDocument;
        private readonly string _originalDocument;
        private readonly string _sourcePath;
        private readonly ParseContext _parse;
        private readonly GlobalConfig _config;
        private readonly LineMapper _map;
        private readonly Compilation _compilation;

        /// <summary>The generator's single Roslyn <c>ITypeFacts</c> adapter — the CLR assignability
        /// relation with its two nullable corrections, the unusable-prop-type predicate, and the shared AQN
        /// formatting, all stated once. The shared rule-cores ask their type questions through this.</summary>
        internal SymbolTypeFacts TypeFacts { get; private set; }
        private readonly SymbolTypeResolver _resolver;
        private readonly CSharpExpressionTyper _csharpTyper;
        private readonly FunctionExportResolver _exports;
        private readonly ExtensionBinder _extensionBinder;

        private string _modelTypeText;
        private BlockPosition _modelDirectivePosition;
        private ITypeSymbol _modelSymbol;
        private readonly List<string> _usings = new List<string>();

        private readonly List<(string Name, string Type, string Assembly, string PropLayout)> _extensionBindings =
            new List<(string, string, string, string)>();
        private readonly List<(string Name, string Target, int OverloadCount)> _functionBindings =
            new List<(string, string, int)>();

        private readonly List<string> _extensionFields = new List<string>();
        private readonly StringBuilder _fieldDecls = new StringBuilder();
        private readonly StringBuilder _methodDecls = new StringBuilder();
        private int _extensionCounter;

        /// <summary>Names for the locals a member path binds its receiver to. Counted across the whole generated
        /// file rather than per expression: a pattern variable belongs to the block its statement sits in, so two
        /// paths emitted into one block would collide on a per-path counter.</summary>
        private int _hopLocalCounter;

        private string AllocateHopLocal() => "__h" + _hopLocalCounter++;
        private int _dynEvalCounter;
        private int _dynSettersCounter;

        // Unresolvable functions degrade the template to a HED7014 fallback-marker entry.
        private readonly List<(string Name, BlockPosition Position)> _unresolvableFunctions =
            new List<(string, BlockPosition)>();

        // Collected regardless of emit outcome.
        private readonly List<EmitDiagnostic> _diagnostics = new List<EmitDiagnostic>();

        // The front end's own compile-channel warnings, raised here by the shared rule that raises them at run
        // time and reported under the runtime's own id, message and fix so one authoring mistake reads the same
        // on both tiers. Filled by whichever shared scan produced them, then drained by DrainLints.
        private readonly List<HeddleCompileWarning> _lints = new List<HeddleCompileWarning>();
        // The same shared scan's error arm. The engine refuses a template that raises one of these, so a body
        // that collects any is left to the dynamic tier rather than precompiled past the refusal.
        private readonly List<HeddleCompileError> _lintErrors = new List<HeddleCompileError>();

        // A body is walked more than once (a definition emitted for several call sites, a region filled twice),
        // and the second walk rediscovers the first walk's warnings at the same source offset. One mistake is one
        // squiggle, so identity is the pair the user can see: the diagnostic and where it points.
        private readonly HashSet<string> _reportedLints = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>Moves everything the shared scans collected into the reported set, in the order the shared
        /// passes produced it.</summary>
        /// <summary>The first error the shared branch scan raised, if any, and clears the channel. The engine
        /// refuses these outright, so the build tier declines the template instead of emitting past them.</summary>
        private string TakeLintRefusal()
        {
            if (_lintErrors.Count == 0)
                return null;
            var first = _lintErrors[0].Error;
            _lintErrors.Clear();
            return first;
        }

        private void DrainLints()
        {
            foreach (var lint in _lints)
            {
                if (string.IsNullOrEmpty(lint.DiagnosticId) ||
                    !_reportedLints.Add(lint.DiagnosticId + "@" + lint.Position.StartIndex))
                    continue;
                _diagnostics.Add(new EmitDiagnostic(
                    GeneratorDiagnostics.Forwarded(lint.DiagnosticId, isWarning: true), lint.Position,
                    GeneratorDiagnostics.ForwardedMessage(lint.Error, lint.Fix)));
            }

            _lints.Clear();
        }

        internal readonly struct EmitDiagnostic
        {
            public EmitDiagnostic(DiagnosticDescriptor descriptor, BlockPosition position, params object[] args)
            {
                Descriptor = descriptor;
                Position = position;
                Args = args ?? new object[0];
            }

            public DiagnosticDescriptor Descriptor { get; }
            public BlockPosition Position { get; }
            public object[] Args { get; }
        }

        public TemplateEmitter(string key, string sanitizedName, string generatedNamespace, string cleanDocument,
            string originalDocument, ParseContext parse, GlobalConfig config, Compilation compilation,
            FunctionExportResolver exports = null, string sourcePath = null, string lineDirectiveFile = null,
            bool lineDirectiveFileIsRootRelative = true, string registeredName = null)
        {
            _key = key;
            _lineDirectiveFile = lineDirectiveFile ?? key;
            _lineDirectiveFileIsRootRelative = lineDirectiveFileIsRootRelative;
            _lineDirectiveFileIsWritable = IsWritableLineDirectiveFile(_lineDirectiveFile);
            _registeredName = registeredName;
            _sanitizedName = sanitizedName;
            _namespace = generatedNamespace;
            _cleanDocument = cleanDocument;
            _originalDocument = originalDocument;
            _sourcePath = sourcePath;
            _parse = parse;
            _config = config;
            _map = new LineMapper(originalDocument);
            _compilation = compilation;
            TypeFacts = new SymbolTypeFacts(compilation);
            _resolver = new SymbolTypeResolver(compilation);
            _csharpTyper = new CSharpExpressionTyper(compilation);
            _exports = exports ?? FunctionExportResolver.Build(compilation);
            _extensionBinder = ExtensionBinder.Build(compilation);
        }

        internal sealed class Result
        {
            public bool Emitted { get; set; }
            public string UnsupportedReason { get; set; }
            public string Source { get; set; }
            public string ManifestEntry { get; set; }
            public bool IsDynamic { get; set; }
            public string ModelTypeText { get; set; }

            /// <summary>True when the template is a HED7014 fallback marker: not precompiled (no <see cref="Source"/>),
            /// but a marker manifest entry (<see cref="ManifestEntry"/>) and one warning per
            /// <see cref="UnresolvableFunctions"/> are emitted.</summary>
            public bool IsMarker { get; set; }
            public IReadOnlyList<(string Name, BlockPosition Position)> UnresolvableFunctions { get; set; }

            /// <summary>Emitter-produced Roslyn diagnostics (HED7005/HED7006/HED7015) with their <c>.heddle</c>
            /// span; reported by the generator in every result branch.</summary>
            public IReadOnlyList<EmitDiagnostic> Diagnostics { get; set; }

            /// <summary>The region-fill candidate errors this emit <b>retracted</b> — matched public
            /// fills and private-region overrides (whose HED7024 replaces the tentative error). Every other
            /// candidate error survives and the generator forwards it, exactly as the dynamic compile leaves it in
            /// its error list. Non-null only when a full body build completed.</summary>
            public IReadOnlyCollection<Heddle.Data.HeddleCompileError> RetractedCandidateErrors { get; set; }
        }

        private bool IsHtml => _config.OutputProfile == Heddle.Data.OutputProfile.Html;

        // The running output profile: starts at the compile-time OutputProfile and flips in document
        // order at each @profile(){{html|text}} directive, per compile context (body). Drives which unnamed carrier
        // (EmptyExtension / EmptyHtmlExtension) subsequent output binds. The options fingerprint keeps the
        // compile-time profile (never the post-flip value).
        private bool _profileHtml;

        /// <summary>Test-only fault injection: invoked with the template key at the top of
        /// <see cref="Emit"/> so the emitter-defect error path can be exercised without a real defect. Never
        /// assigned by the generator — the field is <c>internal</c> and only the white-box test project sets it.</summary>
        internal static System.Action<string> FaultInjector;

        public Result Emit(string contentHash)
        {
            FaultInjector?.Invoke(_key);
            _profileHtml = IsHtml;
            ExtractDirectives();
            bool isDynamic = _modelTypeText == null ||
                             string.Equals(_modelTypeText, "dynamic", System.StringComparison.Ordinal);
            string modelType = isDynamic ? "object" : _modelTypeText;
            if (!isDynamic)
            {
                _modelSymbol = _resolver.ResolveModelType(_modelTypeText, _usings);
                if (!CanWriteTypeName(_modelSymbol, _modelDirectivePosition, out var modelReason))
                    return new Result
                    {
                        Emitted = false, Diagnostics = _diagnostics, UnsupportedReason = modelReason
                    };

                if (_modelSymbol != null)
                {
                    modelType = SymbolTypeResolver.FullyQualified(_modelSymbol);
                }
                else
                {
                    if (_resolver.LastFault == Heddle.Language.Binding.TypeSpellingFault.Ambiguous)
                        // HED7023: the name resolves to SEVERAL types and the imports do not settle it.
                        // The runtime raises its "the type name is ambiguous" error for the same input, so the build
                        // must not quietly pick one and emit typed code off a type the runtime might not choose.
                        _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.AmbiguousTypeName,
                            _modelDirectivePosition, _modelTypeText));
                    else if (IsPlainTypeName(_modelTypeText) && !_resolver.TypeNameExistsAnywhere(_modelTypeText))
                        // HED7007: a declared @model type that resolves as no symbol AND matches no type name
                        // anywhere in the reference closure is a genuine typo/unresolvable symbol. A bare name the
                        // runtime resolves by assembly scan (namespace-less, no @using) still degrades safely to the
                        // dynamic path rather than false-erroring. Gated to plain type-name syntax so exotic forms
                        // (open generics, arrays) never false-error either.
                        _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.UnresolvableModelType,
                            _modelDirectivePosition, _modelTypeText));

                    // Whether or not there was anything to report, a spelling that named no symbol is not one the
                    // emitter may write. It used to be emitted as the author typed it, on the chance that the
                    // generated file meant something by it; it does not. `Fixtures.Article` and
                    // `System.Collections.Generic.List` both pass the name-existence gate and both become the entry
                    // point's parameter type, where the consumer's compiler answers CS0246 and CS0305 against a
                    // `.g.cs` no one can edit — and only a template body that degraded for some other reason ever
                    // hid it. The engine binds a model name over the assemblies actually loaded rather than the ones
                    // this compilation references, so it is also the only tier that might still serve the template.
                    return new Result
                    {
                        Emitted = false, Diagnostics = _diagnostics,
                        UnsupportedReason = "model type '" + _modelTypeText + "' resolves to no symbol"
                    };
                }
            }

            // Build the body tree (Body0 = document root; extension bodies become nested bodies).
            var rootCtx = new BodyContext(isDynamic ? null : "(" + modelType + ")", _modelSymbol, isDynamic);
            var root = BuildBody(_cleanDocument, _parse, rootCtx, out var reason);
            if (root == null)
            {
                // The build failed on a function resolvable from neither the default table nor a
                // referenced export — degrade to a HED7014 fallback marker (warning + marker manifest entry, no code).
                if (_unresolvableFunctions.Count != 0)
                {
                    return new Result
                    {
                        Emitted = false, IsMarker = true, IsDynamic = isDynamic, ModelTypeText = modelType,
                        UnresolvableFunctions = DedupeUnresolvable(), Diagnostics = _diagnostics,
                        RetractedCandidateErrors = _retractedCandidateErrors,
                        ManifestEntry = BuildManifestEntry(contentHash, isDynamic, modelType, marker: true)
                    };
                }

                return new Result { Emitted = false, UnsupportedReason = reason, Diagnostics = _diagnostics };
            }

            // An unconsumed region-fill candidate is no longer a reason to un-precompile the template silently.
            // Each candidate now gets the runtime's own reaction inside TryBuildGeneratorFillScope — matched fills
            // retract their tentative error, dangling ones keep it (and it is forwarded as a build error), a private
            // one raises HED7024 — so the two tiers report the same errors instead of one of them going quiet.

            // HED7005: a static piece with an unpaired surrogate stays legal for string output (the char is
            // preserved), but the compiler rejects it in a "…"u8 literal — warn and suppress the u8 twin (PieceWriter
            // already omits it). Validation runs regardless of the HeddleEmitUtf8Pieces toggle. Position = the first
            // lone surrogate in the source document (static text is preserved verbatim into the pieces).
            if (_pieces.Any(CSharpEscape.HasLoneSurrogate))
            {
                var idx = CSharpEscape.IndexOfLoneSurrogate(_originalDocument);
                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.SurrogatePiece,
                    new BlockPosition(idx < 0 ? 0 : idx, 1), _sourcePath ?? _key));
            }

            var source = RenderFile(modelType, root);
            var manifest = BuildManifestEntry(contentHash, isDynamic, modelType, marker: false);
            return new Result
            {
                Emitted = true, Source = source, ManifestEntry = manifest, Diagnostics = _diagnostics,
                RetractedCandidateErrors = _retractedCandidateErrors,
                IsDynamic = isDynamic, ModelTypeText = modelType
            };
        }

        /// <summary>The four built-in directive names, hard-coded. Kept as the fallback for a name the binder cannot
        /// resolve (a template compiled before its reference closure is complete, or against an engine reference
        /// predating <c>[ZeroOutput]</c>) — never as the primary rule, which is what made a CUSTOM zero-output
        /// extension diverge silently.</summary>
        private static bool IsDirectiveName(string name) =>
            name == "model" || name == "using" || name == "import" || name == "profile";

        /// <summary>An extension is zero-output when it declares <c>[ZeroOutput]</c> — the
        /// symbol-readable form of the runtime's null-<c>InitStart</c> protocol, which the generator has no way to
        /// evaluate. A block whose leftmost call is zero-output is removed from the piece stream on both tiers.</summary>
        private bool IsZeroOutput(OutputChain chain)
        {
            var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
            if (leftmost == null)
                return false;
            if (_extensionBinder.TryResolve(leftmost.ExtensionName, out var info))
                return info.IsZeroOutput;
            return IsDirectiveName(leftmost.ExtensionName);
        }

        private void ExtractDirectives()
        {
            foreach (var chain in _parse.OutputChains)
            {
                if (chain.Chain == null || chain.Chain.Count == 0)
                    continue;
                var item = chain.Chain[0];
                if (item.ExtensionName == "model")
                {
                    var text = (item.ParameterTemplate ?? string.Empty).Trim();
                    if (text.Length != 0)
                    {
                        _modelTypeText = text;
                        _modelDirectivePosition = item.Position;
                    }
                }
                else if (item.ExtensionName == "using")
                {
                    var ns = (item.ParameterTemplate ?? string.Empty).Trim();
                    if (ns.Length != 0 && !_usings.Contains(ns))
                        _usings.Add(ns);
                }
            }
        }

        private readonly struct BodyContext
        {
            public BodyContext(string modelCast, ITypeSymbol modelSymbol, bool isDynamic, PropLayoutInfo props = null,
                ITypeSymbol slotType = null, Dictionary<string, DefinitionItem> fills = null,
                PropLayoutInfo regionHostProps = null, ITypeSymbol dynamicBodyModel = null)
            {
                ModelCast = modelCast;
                ModelSymbol = modelSymbol;
                IsDynamic = isDynamic;
                Props = props;
                SlotType = slotType;
                Fills = fills;
                RegionHostProps = regionHostProps;
                DynamicBodyModel = dynamicBodyModel;
            }

            public string ModelCast { get; }          // "(global::T)" or null for the dynamic tier

            /// <summary>The model's type for member-path typing. Every construction site upholds
            /// <c>IsDynamic ⇒ ModelSymbol is null</c> — the dynamic tier has no static model to type against — so a
            /// reader wanting "the typed model here, if any" reads this alone. A body emitted on the dynamic tier
            /// over a model the <b>engine</b> has typed carries that type in <see cref="DynamicBodyModel"/>, which
            /// is a different question with a different answer.</summary>
            public ITypeSymbol ModelSymbol { get; }

            public bool IsDynamic { get; }

            /// <summary>The active prop layout: a body prop read wins over the model on the first path
            /// segment. Null outside a definition body with props. Prop-first resolution is syntactic, so both
            /// backends resolve identically.</summary>
            public PropLayoutInfo Props { get; }

            /// <summary>The declared slot parameter type inside a slot-declaring definition body, null outside one.
            /// Carried rather than a bare flag because every <c>@out(value)</c> here has to be checked against it,
            /// the way the engine checks it when it compiles the same body.</summary>
            public ITypeSymbol SlotType { get; }

            /// <summary>True inside a slot-declaring definition body: <c>@out(value)</c> projects the caller content
            /// (slot mode); outside a slot definition, an <c>@out</c> value is a runtime error the emitter refuses.</summary>
            public bool InSlot => SlotType != null;

            /// <summary>The ambient region fill scope — <c>regionName → materialized-fill DefinitionItem</c> —
            /// the generator's parallel to the dynamic tier's <c>RegionFillScope</c> on <c>CompileContext</c>.
            /// Threaded through every nested body build (branch/list/for bodies and the definition-body contexts)
            /// so a fill resolves at any depth. Null outside a filled component body.</summary>
            public Dictionary<string, DefinitionItem> Fills { get; }

            /// <summary>The enclosing component's prop layout, carried through nested bodies so a region body
            /// borrows the component's props (a region declares none of its own).</summary>
            public PropLayoutInfo RegionHostProps { get; }

            /// <summary>The model behind a body emitted on the dynamic tier, where <see cref="ModelSymbol"/> is
            /// null but the engine still has a static type in hand.
            /// <para>Two bodies set it, and both set it alongside the model they are typed by, so the two never
            /// disagree. A <c>:: dynamic</c> definition body carries the model of the one call site that built it:
            /// the engine compiles such a definition once per call site off the model that call site hands it, so
            /// the declaration does not mean "untyped", it means "whatever this caller passes". A nested
            /// <c>@list</c> body carries the <b>element</b> type for exactly the same reason. Either is on the
            /// dynamic tier only when the value <em>is</em> the compilation's <c>dynamic</c> — then the body
            /// genuinely has no static model, and that is what the <c>@out</c> check reads. Null on the typed tier
            /// and in a body with neither source.</para>
            /// <para>Two call sites that hand the same <c>:: dynamic</c> definition different models do not get
            /// different bodies — the engine gives them one, and <see cref="TryShareBodyTyping"/> is where that is
            /// decided.</para></summary>
            public ITypeSymbol DynamicBodyModel { get; }

            public BodyContext WithProps(PropLayoutInfo props) =>
                new BodyContext(ModelCast, ModelSymbol, IsDynamic, props, SlotType, Fills, RegionHostProps,
                    DynamicBodyModel);

            public BodyContext AsSlot(ITypeSymbol slotType) =>
                new BodyContext(ModelCast, ModelSymbol, IsDynamic, Props, slotType, Fills, RegionHostProps,
                    DynamicBodyModel);

            /// <summary>The same body, now typed by the model the call site actually hands it.</summary>
            public BodyContext TypedAs(ITypeSymbol model) =>
                new BodyContext("(" + SymbolTypeResolver.FullyQualified(model) + ")", model, false, Props, SlotType,
                    Fills, RegionHostProps, model);

            public BodyContext WithDynamicBodyModel(ITypeSymbol model) =>
                new BodyContext(ModelCast, ModelSymbol, IsDynamic, Props, SlotType, Fills, RegionHostProps, model);

            public BodyContext WithFills(Dictionary<string, DefinitionItem> fills, PropLayoutInfo regionHostProps) =>
                new BodyContext(ModelCast, ModelSymbol, IsDynamic, Props, SlotType, fills, regionHostProps,
                    DynamicBodyModel);
        }

        /// <summary>
        /// <para>The nested body's model context is <b>derived from</b> <see cref="BodyModelRules"/>' row for the
        /// host name, not chosen per emission branch. This is the emitter's real dependency on the table: the row
        /// decides which context the body is built in, so the table is load-bearing.</para>
        /// <list type="bullet">
        /// <item><description><see cref="BodyModelSource.Parent"/> (the branch trio, <c>@for</c>) — the body keeps
        /// the enclosing typed context, because it executes under <c>scope.Parent()</c>.</description></item>
        /// <item><description><see cref="BodyModelSource.ElementOfData"/> (<c>@list</c>) — the body is typed by the
        /// element type, because that is the type the engine compiles it against: <c>ListExtension.InitStart</c>
        /// returns the collection's <c>IEnumerable&lt;T&gt;</c> argument and the body is compiled once, in a scope of
        /// that type. Only where the host reaches no generic form does it hand back <c>ExType.Dynamic</c>, and only
        /// then is the body genuinely untyped. The enclosing fill scope, region props, slot mode and — the engine
        /// restores the prop layout around <b>definition</b> bodies only — the enclosing <b>prop layout</b> all
        /// propagate.
        /// <para>Emitting the reads on the dynamic tier regardless cost this both ways. A member the element type
        /// does <em>not</em> carry is an <c>HED0001</c> the engine raises when it compiles the template; bound
        /// dynamically it precompiled and threw <c>RuntimeBinderException</c> at render, and over an element type of
        /// <c>object</c> — a static type, not the absence of one — it precompiled and <b>rendered</b> a page the
        /// engine refuses outright. And every read the dynamic tier cannot express — a native expression over the
        /// element's own member, a function call taking one — degraded a template the engine renders.</para></description></item>
        /// </list>
        /// <para>A name with no pinned row, or a row naming a source the emitter has no emission for, returns
        /// <c>false</c>: the caller refuses the body and the template degrades, which is the safe direction.</para>
        /// </summary>
        private static bool TryNestedBodyContext(string name, BodyContext bctx, ITypeSymbol elementModel,
            out BodyContext nested)
        {
            nested = bctx;
            if (!BodyModelRules.TryGet(name, out var source, out _))
                return false;

            if (source == BodyModelSource.Parent)
                return true;

            if (source == BodyModelSource.ElementOfData)
            {
                var elementCtx = elementModel == null || elementModel.TypeKind == TypeKind.Dynamic
                    ? new BodyContext(null, null, true, props: bctx.Props,
                        fills: bctx.Fills, regionHostProps: bctx.RegionHostProps, dynamicBodyModel: elementModel)
                    : new BodyContext("(" + SymbolTypeResolver.FullyQualified(elementModel) + ")", elementModel,
                        false, props: bctx.Props, fills: bctx.Fills, regionHostProps: bctx.RegionHostProps,
                        dynamicBodyModel: elementModel);
                nested = bctx.InSlot ? elementCtx.AsSlot(bctx.SlotType) : elementCtx;
                return true;
            }

            return false;
        }

        private sealed class Piece { public int Index; }

        private sealed class Call
        {
            public string ExtensionField;
            public string ParamExpr;
            public bool UsesModelLocal;
            public bool UsesCSharpModel;
            public int SpanStartLine, SpanStartCol, SpanEndLine, SpanEndCol;
        }

        private sealed class BodyClass
        {
            public string Name;
            public string ModelCast;
            public bool IsDynamic;
            public bool NeedsModelLocal;
            public bool NeedsCSharpModel;   // FullCSharp verbatim: declare `var model = (T)scope.ModelData;`
            public bool HostsParticipant;
            public readonly List<object> Segments = new List<object>();
        }

        private readonly List<string> _pieces = new List<string>();
        private readonly List<BodyClass> _bodies = new List<BodyClass>();
        private int _bodyCounter;

        // Compiled once per identity, keyed to prevent infinite recursion during self-calls.
        // Reserved before population so a self-call during build finds it.
        private readonly Dictionary<string, DefBodyInfo> _definitionBodies =
            new Dictionary<string, DefBodyInfo>(System.StringComparer.Ordinal);

        private sealed class DefBodyInfo
        {
            public BodyClass Body;
            public bool Failed;
            public string Reason;
        }

        private BodyClass NewBody(BodyContext bctx)
        {
            var body = new BodyClass
            {
                Name = "Body" + _bodyCounter++,
                ModelCast = bctx.ModelCast,
                IsDynamic = bctx.IsDynamic
            };
            _bodies.Add(body);
            return body;
        }

        private BodyClass BuildBody(string doc, ParseContext ctx, BodyContext bctx, out string reason)
        {
            var body = NewBody(bctx);
            return PopulateBody(body, doc, ctx, bctx, out reason) ? body : null;
        }

        private bool PopulateBody(BodyClass body, string doc, ParseContext ctx, BodyContext bctx, out string reason)
        {
            reason = null;
            // The body's own profile lineage: it inherits the profile active where its parent element sits, and its
            // internal @profile flips must not leak back to the parent (save/restore around the whole body walk).
            var savedProfile = _profileHtml;
            var profileByChain = MapProfilePerChain(ctx);
            var inheritedProfile = _profileHtml;

            var shape = DocumentShaper.Shape(doc, ctx, _config.TrimDirectiveLines,
                chain => IsZeroOutput(chain), ctx.DefenitionExists, RoleOf, HasScopeChannel, _lints,
                chain => profileByChain != null && profileByChain.TryGetValue(chain, out var chainProfile)
                    ? chainProfile
                    : inheritedProfile,
                _lintErrors);
            DrainLints();
            var lintRefusal = TakeLintRefusal();
            if (lintRefusal != null)
            {
                reason = lintRefusal;
                return false;
            }
            var working = shape.WorkingDocument;

            // The piece walk itself is shared with RuntimeDocument.GetDocumentPieces, so the
            // P0..Pn constants below are the same strings the dynamic tier slices.
            // Collect refusals for multi-error reporting (sound: sibling elements are independent).
            // Still refuses the body (partial emit would be worse than sequential reporting).
            string localReason = null;
            string firstReason = null;
            var refused = false;
            var completed = DocumentShaping.SlicePieces(shape.Elements, element => element.Position, working,
                piece => AddPiece(body, piece),
                element =>
                {
                    // Empty chain: modeled for parity but emits no segment.
                    if (element.Chain.Chain == null || element.Chain.Chain.Count == 0)
                        return true;

                    if (profileByChain != null && profileByChain.TryGetValue(element.Chain, out var elementProfile))
                        _profileHtml = elementProfile;

                    // Run before definition resolution (bodiless custom channel extensions also need locals frame).
                    if (ParticipantScan.ChainHostsParticipant(element.Chain, HasScopeChannel))
                        body.HostsParticipant = true;

                    var seg = BuildCall(element.Chain, ctx, bctx, out localReason);
                    if (seg == null)
                    {
                        // Record and keep walking. The element contributes no segment; the body is discarded
                        // wholesale below, so the half-built segment list is never rendered.
                        refused = true;
                        firstReason ??= localReason;
                        return true;
                    }

                    body.Segments.Add(seg);
                    if (seg is Call call)
                    {
                        if (call.UsesModelLocal) body.NeedsModelLocal = true;
                        if (call.UsesCSharpModel) body.NeedsCSharpModel = true;
                    }
                    else if (seg is Partial partial)
                    {
                        if (partial.UsesModelLocal) body.NeedsModelLocal = true;
                        if (partial.UsesCSharpModel) body.NeedsCSharpModel = true;
                    }

                    return true;
                });

            // `completed` can still be false: SlicePieces' contract keeps the abandon channel, and a future callback
            // arm may use it. Either signal refuses the body.
            reason = firstReason ?? localReason;
            _profileHtml = savedProfile;
            return completed && !refused;
        }

        /// <summary>Precomputes the running output profile at each rendering chain of this body, from the body's
        /// inherited profile, flipped in document order by every <c>@profile(){{html|text}}</c> directive (which is a
        /// zero-output chain removed from the piece stream but still document-ordered). Keyed by chain identity so the
        /// element walk can look up the profile that was active where each call sits.</summary>
        // One HED7022 per source offset: bodies walk more than once, typos deserve separate squiggles.
        private readonly HashSet<int> _reportedUnknownProfiles = new HashSet<int>();

        private Dictionary<OutputChain, bool> MapProfilePerChain(ParseContext ctx)
        {
            if (ctx?.OutputChains == null || ctx.OutputChains.Count == 0)
                return null;

            Dictionary<OutputChain, bool> map = null;
            bool running = _profileHtml;
            bool sawFlip = false;
            // The directive only sets the profile for output compiled after it, so the runtime tracks whether a
            // bodiless unnamed carrier has already been resolved in this compile context and warns when one has.
            // The flag is per body — a nested body compiles under its own context and never sets its parent's.
            bool unnamedOutputCompiled = false;
            foreach (var chain in ctx.OutputChains)
            {
                var lm = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
                if (lm != null && lm.ExtensionName == "profile")
                {
                    // The parse is the shared OutputProfileRules rule ProfileExtension.InitStart runs.
                    var v = (lm.ParameterTemplate ?? string.Empty).Trim();
                    if (OutputProfileRules.TryParseProfile(v, out var parsed))
                    {
                        if (unnamedOutputCompiled)
                        {
                            _lints.Add(CompileWarningFactory.ProfileDirectiveAfterOutput(lm.Position));
                            DrainLints();
                        }

                        running = parsed == Heddle.Data.OutputProfile.Html;
                        sawFlip = true;
                    }
                    else if (_reportedUnknownProfiles.Add(lm.Position.StartIndex))
                    {
                        // The runtime raises HED2001 for this template and never compiles it. The emitter previously
                        // fell through silently, rendering output the dynamic tier would never produce. Now it reports
                        // HED7022 at the directive so the problem is visible.
                        _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.UnknownOutputProfile,
                            lm.Position, v));
                    }
                }
                else
                {
                    foreach (var carrier in chain.Chain)
                    {
                        if (carrier.ExtensionName.Length == 0 && string.IsNullOrEmpty(carrier.ParameterTemplate))
                        {
                            unnamedOutputCompiled = true;
                            break;
                        }
                    }

                    (map ??= new Dictionary<OutputChain, bool>())[chain] = running;
                }
            }

            return sawFlip ? map : null;   // no flip → keep the fast path (no per-chain overrides)
        }

        private void AddPiece(BodyClass body, string text)
        {
            var idx = _pieces.Count;
            _pieces.Add(text);
            body.Segments.Add(new Piece { Index = idx });
        }

        /// <summary>True when the piece has an emitted <c>PnU8</c> twin: the opt-in is on and the piece
        /// carries no unpaired surrogate (HED7005 downgrade, per-piece, mirroring <see cref="PieceWriter"/>).</summary>
        private bool HasU8Twin(int pieceIndex) =>
            _config.EmitUtf8Pieces && !CSharpEscape.HasLoneSurrogate(_pieces[pieceIndex]);

        /// <summary>The shared branch-role source: strip machine and emitter both read roles through the single
        /// <see cref="ExtensionBinder"/>, so classification can never drift between them.</summary>
        /// <summary>A by-name call to a definition that also carries a default output renders it twice — once
        /// here and once at document end. The synthetic self-call the default chain itself makes is exempt.</summary>
        private void WarnOnDoubleRender(OutputItem item, DefinitionItem definitionItem)
        {
            if (definitionItem == null || !definitionItem.HasDefaultOutput || item.IsDefaultChainSelfCall)
                return;

            _lints.Add(CompileWarningFactory.DefinitionRendersTwice(item.ExtensionName, definitionItem.Position,
                item.Position));
            DrainLints();
        }

        /// <summary>A standalone call whose name is both an extension and a registered function resolves to the
        /// extension — the precedence the shared call-target rule applies for both tiers. The build tier sees the
        /// default table and the assemblies' <c>[ExportFunctions]</c>; a registry the host builds at run time is
        /// beyond it, so this can only ever say less than the run tier, never more.</summary>
        private void WarnOnShadowedFunction(OutputItem item, CallTargetKind callTarget, DefinitionItem definitionItem)
        {
            if (string.IsNullOrEmpty(item.ExtensionName) || definitionItem != null ||
                _config.ExpressionMode == Heddle.Data.ExpressionMode.MemberPathsOnly)
                return;
            if (callTarget != CallTargetKind.Extension)
                return;
            var name = item.ExtensionName;
            if (!NativeExpressionWriter.IsDefaultFunction(name) && !_exports.TryGet(name, out _))
                return;

            _lints.Add(CompileWarningFactory.FunctionShadowedByExtension(name, item.Position));
            DrainLints();
        }

        /// <summary>A producer that encodes its own output, feeding the bodiless unnamed sink that already encodes
        /// under the Html profile, encodes the value twice. The producer is the chain parameter's first item — the
        /// one the runtime reaches as the last of its reversed execution list. At build time a name resolves
        /// straight to the extension type, which is what the runtime arrives at by unwrapping its carrier.</summary>
        private void WarnOnRedundantEncoding(CallParameter cp)
        {
            if (!_profileHtml)
                return;
            var chainParameter = cp.ChainParameter;
            if (chainParameter == null || chainParameter.Count == 0)
                return;
            var producer = chainParameter[0];
            if (!_extensionBinder.TryResolve(producer.ExtensionName, out var producerInfo) ||
                !producerInfo.HasEncodeOutput)
                return;

            _lints.Add(CompileWarningFactory.RedundantEncodingExtension(producer.ExtensionName, producer.Position));
            DrainLints();
        }

        /// <summary>A prop whose name also names a readable model member hides it: the read takes the prop, and
        /// the member stays reachable only through an explicit <c>this.</c>. The prop still wins on both tiers —
        /// this only says so out loud.</summary>
        private void WarnOnPropShadowsMember(ITypeSymbol scope, string name, BlockPosition position)
        {
            if (string.IsNullOrEmpty(name) || !SymbolTypeResolver.BindsReadableProperty(scope, name))
                return;

            _lints.Add(CompileWarningFactory.PropShadowsModelMember(name,
                SymbolTypeIdentity.FullName(scope as INamedTypeSymbol) ?? SymbolTypeResolver.FullyQualified(scope),
                position));
            DrainLints();
        }

        private BranchRole? RoleOf(string name)
            => _extensionBinder.TryResolve(name, out var i) ? i.Role : null;

        /// <summary>Whether an extension carries <c>[ScopeChannel]</c> — the branch classifier's Participant
        /// mapping, matching the runtime's <c>IsHaveAttribute&lt;ScopeChannelAttribute&gt;</c> arm.</summary>
        private bool HasScopeChannel(string name)
            => _extensionBinder.TryResolve(name, out var i) && i.HasScopeChannel;

        private sealed class Partial
        {
            public string FieldName;
            public string Key;
            public string ModelExpr;
            public string CallerModelTypeFq;   // fully-qualified caller model type for a dynamic-compiled child; null = dynamic tier
            public bool UsesModelLocal;
            public bool UsesCSharpModel;
            public int SpanStartLine, SpanStartCol, SpanEndLine, SpanEndCol;
        }

        private int _partialCounter;

        private object BuildCall(OutputChain chain, ParseContext ctx, BodyContext bctx, out string reason)
        {
            reason = null;
            if (chain.Chain.Count != 1)
            {
                reason = "chained call";
                return null;
            }

            var item = chain.Chain[0];
            var cp = item.CallParameter;
            var name = item.ExtensionName;

            if (name.Length == 0)
            {
                if (!string.IsNullOrEmpty(item.ParameterTemplate))
                {
                    reason = "bodied unnamed carrier";
                    return null;
                }

                WarnOnRedundantEncoding(cp);
                if (!BuildParamExpr(cp, bctx, out var uParam, out var uUses, out var uCs, out reason, item.Position))
                    return null;
                var uField = AllocateEmptyExtension(item.Position);
                return MakeCall(uField, uParam, uUses, item.Position, uCs);
            }

            // Definition-first precedence (matches HeddleCompiler.CompileItem) — definitions may shadow branches.
            // Ambient fill scope first (RegionFillScope precedence), then enclosing parse context.
            var resolutionCtx = chain.Context ?? ctx ?? _parse;
            // Shared CallTargetRules precedence: fill → definition → extension → function → unknown.
            // Must use this order to avoid binding function-first, causing divergence if host-exported functions share names with extensions.
            var callTarget = CallTargetRules.ResolveCallTarget(name, cp,
                n => bctx.Fills != null && bctx.Fills.ContainsKey(n),
                resolutionCtx.DefenitionExists,
                n => _extensionBinder.TryResolve(n, out _),
                n => NativeExpressionWriter.IsDefaultFunction(n) || _exports.TryGet(n, out _));

            var definitionItem = callTarget == CallTargetKind.Fill ? bctx.Fills[name]
                : callTarget == CallTargetKind.Definition ? resolutionCtx.GetDefenition(name)
                : null;
            WarnOnDoubleRender(item, definitionItem);
            WarnOnShadowedFunction(item, callTarget, definitionItem);

            if (callTarget == CallTargetKind.Fill)
                return BuildDefinitionCall(definitionItem, item, cp, bctx, isFill: true, out reason);
            if (callTarget == CallTargetKind.Definition)
                return BuildDefinitionCall(definitionItem, item, cp, bctx, isFill: false, out reason);

            if (name == "out")
                return BuildOutCall(item, cp, bctx, out reason);

            if (name == "partial")
                return BuildPartialCall(item, cp, bctx, out reason);

            // Every route below spells the bound extension's own type into the consumer's assembly, so the one
            // question that decides whether the generated file compiles at all is asked once, here, before any of
            // them allocates a field. The engine asks it nowhere: its discovery filters on the interface and the
            // name attribute alone, and Activator.CreateInstance is indifferent to accessibility and [Obsolete].
            if (_extensionBinder.TryResolve(name, out var boundInfo) &&
                !CanWriteExtensionTypeName(boundInfo.TypeSymbol, item.Position, out reason))
                return null;

            // The engine checks the call value against every type the extension declares it accepts, before it
            // compiles the call, and refuses the whole template when a statically-typed value fits none of them.
            // Without the same check here a template the engine will not compile precompiled and rendered.
            if (_extensionBinder.TryResolve(name, out var acceptInfo) &&
                !AcceptedTypeSatisfied(acceptInfo, CallSiteValueType(cp, bctx), out var acceptReason))
            {
                reason = "'" + name + "' " + acceptReason;
                return null;
            }

            // Engine-assembly branch-role extensions (@if/@ifnot/@elif/@elseif/@else) use pinned branch emission:
            // the emitter's parent-model body typing is the built-ins' verified contract, so bytes are unchanged.
            // Non-engine role extensions deliberately fall through to the generic custom path.
            if (_extensionBinder.TryResolve(name, out var branchInfo) && branchInfo.Role.HasValue &&
                branchInfo.IsEngineAssembly)
            {
                // Body model: the rule is BodyModelRules' row and it is consumed, not asserted.
                if (!TryNestedBodyContext(name, bctx, null, out var branchBodyCtx))
                {
                    reason = "no pinned body model-typing row for branch '" + name + "'";
                    return null;
                }

                if (!BuildParamExpr(cp, bctx, out var bParam, out var bUses, out var bCs, out reason, item.Position))
                    return null;

                BodyClass branchBody = null;
                if (!string.IsNullOrEmpty(item.ParameterTemplate) && item.Context != null)
                {
                    branchBody = BuildBody(item.ParameterTemplate, item.Context, branchBodyCtx, out reason);
                    if (branchBody == null)
                        return null;
                }

                bool needsLocals = branchBody != null && branchBody.HostsParticipant;
                // Use binder's BareTypeName (handles nested types with +) and AssemblyName; don't parse display name.
                var field = AllocateBodyExtension(name, branchInfo.GlobalName, branchInfo.BareTypeName,
                    branchBody?.Name, needsLocals, item.Position, branchInfo.AssemblyName);
                var call = MakeCall(field, bParam, bUses, item.Position, bCs);
                return call;
            }

            if (name == "list")
            {
                // Element body typed by the element type — the type the engine compiles it against; the enclosing
                // prop layout, slot mode and fill scope propagate, and BodyModelRules' ElementOfData row decides
                // the context.
                var elementModel = ListElementModel(cp, bctx, out var elementAmbiguous);
                if (elementAmbiguous)
                {
                    reason = "collection reaches IEnumerable<T> at more than one element type";
                    return null;
                }

                // The element type is now written into the body's own `(T)scope.ModelData`, so it passes the gate
                // every other spelled name passes before it reaches the file. It is a model position: the host hands
                // each element to `scope.Model(item, index)` boxed, which a ref struct cannot be.
                if (elementModel != null && elementModel.TypeKind != TypeKind.Dynamic &&
                    !CanWriteTypeName(elementModel, item.Position, out reason))
                    return null;

                if (!TryNestedBodyContext("list", bctx, elementModel, out var itemCtx))
                {
                    reason = "no pinned body model-typing row for 'list'";
                    return null;
                }

                if (!BuildParamExpr(cp, bctx, out var lParam, out var lUses, out var lCs, out reason, item.Position))
                    return null;

                BodyClass itemBody = null;
                if (!string.IsNullOrEmpty(item.ParameterTemplate) && item.Context != null)
                {
                    itemBody = BuildBody(item.ParameterTemplate, item.Context, itemCtx, out reason);
                    if (itemBody == null)
                        return null;
                }

                bool listNeedsLocals = itemBody != null && itemBody.HostsParticipant;
                var listField = AllocateBodyExtension("list", "global::Heddle.Extensions.ListExtension",
                    "Heddle.Extensions.ListExtension", itemBody?.Name, listNeedsLocals, item.Position);
                return MakeCall(listField, lParam, lUses, item.Position, lCs);
            }

            if (name == "for")
            {
                // Body typed by enclosing model; @out() splices the boxed index (BodyModelRules row).
                if (!TryNestedBodyContext("for", bctx, null, out var forBodyCtx))
                {
                    reason = "no pinned body model-typing row for 'for'";
                    return null;
                }

                if (!BuildParamExpr(cp, bctx, out var fParam, out var fUses, out var fCs, out reason, item.Position))
                    return null;

                BodyClass forBody = null;
                if (!string.IsNullOrEmpty(item.ParameterTemplate) && item.Context != null)
                {
                    forBody = BuildBody(item.ParameterTemplate, item.Context, forBodyCtx, out reason);
                    if (forBody == null)
                        return null;
                }

                bool forNeedsLocals = forBody != null && forBody.HostsParticipant;
                var forField = AllocateBodyExtension("for", "global::Heddle.Extensions.ForIndexExtension",
                    "Heddle.Extensions.ForIndexExtension", forBody?.Name, forNeedsLocals, item.Position);
                return MakeCall(forField, fParam, fUses, item.Position, fCs);
            }

            // Reached only when the shared classifier picked the function tier.
            if (callTarget == CallTargetKind.Function && string.IsNullOrEmpty(item.ParameterTemplate))
            {
                var callNode = BuildFunctionCallNode(name, cp, item.Position);
                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, "m", _exports, TypeFacts, AllocateHopLocal,
                    bctx.Props);
                var expr = writer.WriteRoot(callNode);
                DrainUnresolvable(writer);
                if (expr == null)
                {
                    reason = "unsupported function call '" + name + "'";
                    return null;
                }

                RecordFunctionUses(writer);
                var fField = AllocateEmptyExtension(item.Position);
                return MakeCall(fField, "(object)(" + expr + ")", writer.UsedModel, item.Position);
            }

            if (_extensionBinder.TryResolve(name, out var extInfo))
                return BuildCustomExtensionCall(name, extInfo, item, cp, bctx, out reason);

            // Runtime will find it but emitter cannot reproduce render protocol — must not fire HED7006.
            if (_extensionBinder.TryGetUnbindableReason(name, out var unbindableReason))
            {
                reason = unbindableReason;
                return null;
            }

            // HED7006 — bodied call with no bound extension. Bodiless may be function-compatible (delegate registration).
            if (!string.IsNullOrEmpty(item.ParameterTemplate))
                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.ExtensionNotBindable, item.Position, name));

            reason = "named extension '" + name + "'";
            return null;
        }

        /// <summary>Binds a custom <c>[ExtensionName]</c> extension resolved from a referenced assembly. A plain
        /// extension (no <c>InitStart</c>/<c>CompleteInit</c> override) carries exactly the base behavior
        /// <c>PrecompiledRuntime.Bind</c> reproduces, so it renders byte-identically. A non-engine hook override is
        /// refused as <c>HED7015</c>; an engine-assembly hook override the emitter has no pinned knowledge of, and a
        /// bodied custom call whose body model-typing is extension-specific, degrade to a safe dynamic fallback.</summary>
        private Call BuildCustomExtensionCall(string name, ExtensionBinder.Info info, OutputItem item,
            CallParameter cp, BodyContext bctx, out string reason)
        {
            reason = null;

            if (info.OverridesHook && !info.IsEngineAssembly && !info.Role.HasValue)
            {
                // HED7015: resolvable but unevaluable — a build error, not a silent degrade (contrast HED7014).
                // Suppressed for role extensions: a custom branch trio's InitStart override is the canonical shape,
                // not an authoring error — it degrades quietly to the dynamic tier instead.
                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.ExtensionOverridesHook,
                    item.Position, name, info.AqnSansVersion, "InitStart/CompleteInit"));
                reason = "extension <" + name + "> overrides a compile-time hook";
                return null;
            }

            if (info.OverridesHook)
            {
                reason = info.Role.HasValue
                    ? "custom branch extension <" + name + ">"
                    : "engine extension <" + name + "> with a compile-time hook (no pinned knowledge)";
                return null;
            }

            // A bodied custom extension's body model-typing is extension-specific (its InitStart would decide it) —
            // conservatively fall back rather than guess. Bodiless value transformers (@ext(x)) bind directly.
            if (!string.IsNullOrEmpty(item.ParameterTemplate))
            {
                reason = "bodied custom extension <" + name + ">";
                return null;
            }

            // Named arguments on a PARAMETER-LESS extension must not be silently dropped — the dynamic tier
            // hard-errors HED5005 for that call, so the precompiled tier degrades and lets the dynamic tier govern
            // (mirrors TryBuildPropsPrototype's prop-less-definition guard).
            if (info.Parameters.Count == 0 && cp.PropArguments != null && cp.PropArguments.Count != 0)
            {
                reason = "named arguments on parameter-less extension <" + name + ">";
                return null;
            }

            // A bodiless parameter-declaring extension binds its [Prop] layout at build time — the frozen
            // prototype + dynamic setters props already emit, installed through BindExtension.
            if (info.Parameters.Count != 0)
            {
                var extLayout = ResolveExtensionPropLayout(name, info, item.Position);
                if (extLayout == null)
                {
                    // Malformed [Prop] declaration — HED7017 recorded (once per extension type); refuse, the
                    // build fails like HED7015 rather than silently degrading.
                    reason = "malformed [Prop] declaration on extension <" + name + ">";
                    return null;
                }

                if (!TryBuildPropsPrototype(extLayout, cp, bctx, out var extPropsRef, out var extSettersRef,
                        out reason))
                    return null;   // unknown/duplicate/missing/unreproducible → safe dynamic fallback

                if (!BuildParamExpr(cp, bctx, out var extParamExpr, out var extUses, out var extCs, out reason, item.Position))
                    return null;

                var namesRef = EmitParameterNamesField(extLayout);
                var extField = AllocateParameterizedExtension(name, info, extPropsRef, extSettersRef, namesRef,
                    item.Position, extLayout);
                return MakeCall(extField, extParamExpr, extUses, item.Position, extCs);
            }

            if (!BuildParamExpr(cp, bctx, out var paramExpr, out var uses, out var cs, out reason, item.Position))
                return null;

            var field = AllocateCustomExtension(name, info, item.Position);
            return MakeCall(field, paramExpr, uses, item.Position, cs);
        }

        private string AllocateCustomExtension(string name, ExtensionBinder.Info info, BlockPosition position)
        {
            var field = "E" + _extensionCounter++;
            var (line, col) = _map.Map(position.StartIndex);
            _fieldDecls.Append("        private static readonly ").Append(info.GlobalName).Append(' ').Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.Bind(\n");
            _fieldDecls.Append("            new ").Append(info.GlobalName)
                .Append("(), body: null, ").Append(DerivedRenderTypeLiteral(info))
                .Append(", needsLocals: false, line: ")
                .Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            // The manifest type name comes from the shared AqnFormatter, not from stripping 'global::' off a
            // display string — a nested extension spells 'Ns.Outer+Inner' on both tiers.
            RecordExtensionBinding(name, info.BareTypeName, info.AssemblyName);
            return field;
        }

        /// <summary>The extension's output render type derived from its <c>[EncodeOutput]</c>/<c>[NotEncode]</c>
        /// symbols — the EXACT expression the dynamic tier's <c>InitializeTemplate</c> evaluates over the concrete
        /// instance type, emitted as the <c>global::Heddle.Data.RenderType.*</c> literal. Never hard-coded <c>Raw</c>:
        /// a plain <c>[EncodeOutput]</c> custom extension must self-encode on the precompiled tier exactly as it does
        /// on the dynamic tier; the flags degrade to false → <c>Raw</c>, the safe value, against an older engine
        /// reference.</summary>
        private static string DerivedRenderTypeLiteral(ExtensionBinder.Info info)
            => "global::Heddle.Data.RenderType." +
               RenderTypeRules.Derive(info.HasEncodeOutput, info.HasNotEncode);

        /// <summary>Allocates a parameter-declaring custom extension call site — the carrier bind through
        /// <c>PrecompiledRuntime.BindExtension</c> with the frozen props prototype, optional dynamic setters, the
        /// ordered parameter names, and the derived render type (applied to the INNER extension so an
        /// <c>[EncodeOutput]</c> inner self-encodes). Field typed <c>AbstractExtension</c> (the carrier is
        /// engine-internal), as <c>AllocateDefinitionExtension</c> does.</summary>
        private string AllocateParameterizedExtension(string name, ExtensionBinder.Info info, string propsFieldRef,
            string dynamicSettersRef, string parameterNamesRef, BlockPosition position,
            PropLayoutInfo layout = null)
        {
            var field = "E" + _extensionCounter++;
            var (line, col) = _map.Map(position.StartIndex);
            _fieldDecls.Append("        private static readonly global::Heddle.Core.AbstractExtension ").Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.BindExtension(\n");
            _fieldDecls.Append("            new ").Append(info.GlobalName)
                .Append("(), props: ").Append(propsFieldRef)
                .Append(", dynamicSetters: ").Append(dynamicSettersRef)
                .Append(", parameterNames: ").Append(parameterNamesRef)
                .Append(", ").Append(DerivedRenderTypeLiteral(info))
                .Append(", needsLocals: false, line: ")
                .Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            // The prop-layout fingerprint travels with the binding row. Built through the shared
            // PropLayout.FormatFingerprint over this side's slots and ITypeFacts, so the string the runtime
            // recomputes from the live extension type is byte-identical when the layouts agree.
            RecordExtensionBinding(name, info.BareTypeName, info.AssemblyName, FingerprintOf(layout));
            return field;
        }

        /// <summary>Emits the ordered parameter-name array field a <c>BindExtension</c> call site hands the
        /// runtime for its name→index map.</summary>
        private string EmitParameterNamesField(PropLayoutInfo layout)
        {
            var field = "ParamNames" + _paramNamesCounter++;
            var names = layout.Slots.Select(s => CSharpEscape.StringLiteral(s.Name));
            _fieldDecls.Append("        private static readonly string[] ").Append(field)
                .Append(" = new string[] { ").Append(string.Join(", ", names)).Append(" };\n");
            return field;
        }

        private int _paramNamesCounter;

        // Per-template extension [Prop] layout cache — one resolution (and one HED7017 per malformed type) per
        // extension name per template. A null value marks a malformed layout.
        private readonly Dictionary<string, PropLayoutInfo> _extensionPropLayouts =
            new Dictionary<string, PropLayoutInfo>(System.StringComparer.Ordinal);

        /// <summary>
        /// The generator's extension prop layout, sequenced by the <b>shared</b>
        /// <see cref="PropLayoutCore"/> the runtime's <c>PropLayout.ResolveFromExtension</c> also drives — one
        /// layer walk, one slot-index rule, one ordered fault vocabulary. This method is the Roslyn adapter:
        /// declarations in, <see cref="SymbolTypeFacts"/> and a <c>HED7017</c> sink alongside, slots out.
        /// <para>Two build-tier divergences disappear with the swap: the walk no longer stops at the first fault
        /// (the runtime accumulates and continues, so a two-fault declaration list now reports both, in
        /// declaration order, on both tiers), and the unusable-type predicate is the runtime's.</para>
        /// Returns <c>null</c> when any fault was reported.
        /// </summary>
        private PropLayoutInfo ResolveExtensionPropLayout(string name, ExtensionBinder.Info info,
            BlockPosition callPosition)
        {
            if (_extensionPropLayouts.TryGetValue(name, out var cached))
                return cached;

            var declarations = new List<PropDeclaration<ITypeSymbol>>(info.Parameters.Count);
            foreach (var p in info.Parameters)
                declarations.Add(new PropDeclaration<ITypeSymbol>
                {
                    Name = p.Name,
                    Type = p.Type,
                    Level = p.Level,
                    HasDefault = p.HasDefault,
                    DefaultValue = p.DefaultValue,
                    Tag = p
                });

            var sink = new EmitterPropSink(this, name, callPosition);
            var built = PropLayoutCore.Build(declarations, TypeFacts, sink, out var faulted);

            if (faulted)
            {
                _extensionPropLayouts[name] = null;
                return null;
            }

            // Deliberately no nameability gate over the slot types, unlike the definition layout: nothing on this
            // path ever writes one. The prototype stores boxed values, the parameter-name field stores strings, the
            // fingerprint is a manifest string, and the two places a conversion is emitted spell a numeric C#
            // keyword or nothing at all. A definition layout does become a body's active prop layout, where a read
            // is emitted through a cast; an extension layout never does.
            var layout = new PropLayoutInfo();
            foreach (var slot in built)
            {
                var info2 = new PropSlotInfo
                {
                    Name = slot.Name,
                    Type = slot.Type,
                    TypeFq = SymbolTypeResolver.FullyQualified(slot.Type),
                    HasDefault = slot.HasDefault,
                    DefaultValue = slot.DefaultBoxed,
                    DefaultSourceType = (slot.Declaration?.Tag as ExtensionBinder.PropParameter)?.DefaultType,
                    Index = slot.Index
                };
                layout.Slots.Add(info2);
                layout.ByName.Add(info2.Name, info2);
            }

            _extensionPropLayouts[name] = layout;
            return layout;
        }

        /// <summary>The build tier's prop-fault sink: every fault class becomes one <c>HED7017</c> at the call
        /// position, carrying the <b>shared</b> fault sentence from <c>HeddleDiagnosticCatalog.PropFaults</c> —
        /// the same words the dynamic tier's <c>HED5007</c>/<c>HED5008</c>/<c>HED5009</c>/<c>HED5010</c>/
        /// <c>HED5015</c> use for the same condition. The default conversion itself stays here: it is defined
        /// over Roslyn constants, not a layout rule.</summary>
        private sealed class EmitterPropSink : IPropLayoutSink<ITypeSymbol>
        {
            private readonly TemplateEmitter _emitter;
            private readonly string _extensionName;
            private readonly BlockPosition _position;

            internal EmitterPropSink(TemplateEmitter emitter, string extensionName, BlockPosition position)
            {
                _emitter = emitter;
                _extensionName = extensionName;
                _position = position;
            }

            public void Fault(PropFault fault, PropDeclaration<ITypeSymbol> declaration, ITypeSymbol relatedType,
                string relatedDisplay)
            {
                var message = Heddle.Data.HeddleDiagnosticCatalog.PropFaults.Message(fault, declaration.Name,
                    "extension '" + _extensionName + "'", _emitter.TypeFacts.Display(declaration.Type),
                    relatedDisplay);
                _emitter._diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.MalformedExtensionParameter,
                    _position, _extensionName, message));
            }

            public bool TryConvertDefault(PropDeclaration<ITypeSymbol> declaration, ITypeSymbol targetType,
                out object converted, out string sourceDisplay)
            {
                var parameter = (ExtensionBinder.PropParameter) declaration.Tag;
                converted = declaration.DefaultValue;

                if (declaration.DefaultValue == null)
                {
                    sourceDisplay = "null";
                    return NullDefaultLegal(targetType);
                }

                sourceDisplay = parameter.DefaultType?.Name ?? declaration.DefaultValue.GetType().Name;
                return _emitter.DefaultConvertible(parameter.DefaultType, targetType);
            }
        }

        /// <summary>
        /// The HED5009 twin — mirrors <c>PropConversion.CanConvertTypes(source, target, allowBoxToObject: true)</c>
        /// over symbols for the attribute-representable default sources (primitive, string, enum, typeof): identity;
        /// box-to-object; implicit numeric widening (incl. widen-then-lift to a nullable target); identity-lift;
        /// reference assignability.
        /// </summary>
        private bool DefaultConvertible(ITypeSymbol source, ITypeSymbol target) =>
            Convertible(source, target, allowBoxToObject: true);

        /// <summary>The symbol-side <c>PropConversion.CanConvertTypes</c>, flag and all: prop defaults ask with
        /// boxing allowed, a slot value asks without it, exactly as the two runtime callers do.</summary>
        private bool Convertible(ITypeSymbol source, ITypeSymbol target, bool allowBoxToObject)
        {
            if (source == null || target == null)
                return false;
            if (SymbolEqualityComparer.Default.Equals(source, target))
                return true;                                                     // identity
            if (allowBoxToObject && target.SpecialType == SpecialType.System_Object && source.IsValueType)
                return true;                                                     // boxing to object

            var sourceSpecial = source.SpecialType;
            if (sourceSpecial != SpecialType.None && IsImplicitNumericWidening(sourceSpecial, target.SpecialType))
                return true;                                                     // implicit numeric widening

            if (TryGetNullableUnderlying(target, out var targetUnderlying))       // target is Nullable<W>
            {
                if (SymbolEqualityComparer.Default.Equals(source, targetUnderlying))
                    return true;                                                 // identity-lift
                if (source.IsValueType &&
                    IsImplicitNumericWidening(sourceSpecial, targetUnderlying.SpecialType))
                    return true;                                                 // widen-then-lift
                // Nullable<S> converts to Nullable<W> when the underlyings are identical or implicitly-numeric-
                // widening. This row was missing and caused safe over-refusal (the template fell back), but
                // an over-refusal is still a divergence: the two tiers disagreed about what precompiles.
                if (TryGetNullableUnderlying(source, out var sourceUnderlying) &&
                    (SymbolEqualityComparer.Default.Equals(sourceUnderlying, targetUnderlying) ||
                     IsImplicitNumericWidening(sourceUnderlying.SpecialType, targetUnderlying.SpecialType)))
                    return true;                                                 // Nullable<S> -> Nullable<W>
            }

            if (!source.IsValueType && _compilation is Microsoft.CodeAnalysis.CSharp.CSharpCompilation csharp)
            {
                var conv = csharp.ClassifyConversion(source, target);
                if (conv.IsIdentity || (conv.IsImplicit && conv.IsReference))
                    return true;                                                 // reference assignability
            }

            return false;
        }

        /// <summary>The emitter's single <c>Nullable&lt;T&gt;</c> probe. The file used to carry two — one over
        /// <c>ConstructedFrom</c>, one over <c>OriginalDefinition</c> — which is a latent divergence inside one
        /// file, so both spellings are folded here onto <c>OriginalDefinition</c> (the form that is never null).
        /// </summary>
        private static bool TryGetNullableUnderlying(ITypeSymbol type, out ITypeSymbol underlying)
        {
            if (type is INamedTypeSymbol named &&
                named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                named.TypeArguments.Length == 1)
            {
                underlying = named.TypeArguments[0];
                return true;
            }

            underlying = null;
            return false;
        }

        /// <summary>The runtime null-default rule (HED5009 twin, null branch): a null default is legal only for a
        /// reference or <c>Nullable&lt;T&gt;</c> parameter type.</summary>
        private static bool NullDefaultLegal(ITypeSymbol target)
            => target.IsReferenceType || TryGetNullableUnderlying(target, out _);

        // ---- Definition invocation ----

        private Call BuildDefinitionCall(DefinitionItem def, OutputItem item, CallParameter cp, BodyContext bctx,
            bool isFill, out string reason)
        {
            reason = null;

            // Emitter resolves flatly (always most-derived), so overrides calling themselves would recurse.
            // Lifted only for materialized region fills (fill scope carries self-call→base rebind).
            if (!isFill && DefinitionInvolvesOverride(def)) { reason = "definition override/layering"; return null; }

            // Slot definitions bind through the slot-mode BindDefinition overload.
            bool slotMode = SlotRules.HasSlot(def);

            var layout = ResolvePropLayout(def);
            if (layout.Failed) { reason = "unresolved prop type"; return null; }

            // Frozen object[] prototype + dynamic setters for non-constant arguments (evaluated against caller view).
            if (!TryBuildPropsPrototype(layout, cp, bctx, out var propsFieldRef, out var dynamicSettersRef, out reason))
                return null;

            // Fill scope follows the generator's twin of the dynamic tier's CreateExtension rule.
            Dictionary<string, DefinitionItem> bodyFills;
            if (def.IsRegion)
            {
                bodyFills = isFill ? Rebound(bctx.Fills, def.Name, def.BaseDefinition) : bctx.Fills;
            }
            else if (!TryBuildGeneratorFillScope(def, item, out bodyFills, out reason))
            {
                return null;
            }

            ITypeSymbol slotType = null;
            if (slotMode)
            {
                // The slot type first, and unconditionally: it is written into the caller-content cast, so it
                // has to pass the same gate as any other type the emitter spells — including reporting HED7030
                // for one this assembly may not name, which a refusal ordered before it would swallow.
                var slotCtx = SlotBodyContext(def, out reason);
                if (reason != null)
                    return null;
                slotType = slotCtx.ModelSymbol;
            }

            // Region body borrows enclosing component's model/props; non-region keeps declared :: T + own layout.
            // Slot mode is the region's own, not the caller's: the engine swaps the slot parameter type around
            // every definition body it compiles and does not exempt a region, though it does exempt one from the
            // prop-layout swap on the line above it.
            BodyContext defBodyCtx;
            if (def.IsRegion)
            {
                if (!TryRegionBodyContext(def, cp, bctx, item.Position, out defBodyCtx, out reason))
                    return null;
                if (slotMode)
                    defBodyCtx = defBodyCtx.AsSlot(slotType);
                defBodyCtx = defBodyCtx.WithFills(bodyFills, bctx.RegionHostProps);
            }
            else
            {
                defBodyCtx = DefinitionBodyContext(def, out reason);
                if (reason != null)
                    return null;
                if (layout.Count > 0)
                    defBodyCtx = defBodyCtx.WithProps(layout);

                if (defBodyCtx.IsDynamic &&
                    !TryTypeCallSiteBody(def, cp, bctx, item.Position, ref defBodyCtx, out reason))
                    return null;

                if (slotMode)
                    defBodyCtx = defBodyCtx.AsSlot(slotType);
                defBodyCtx = defBodyCtx.WithFills(bodyFills, layout.Count > 0 ? layout : null);
            }

            if (!DeclaredModelAcceptsCallSiteValue(def, cp, bctx, out reason))
                return null;

            if (!BuildParamExpr(cp, bctx, out var paramExpr, out var usesModel, out var usesCsModel, out reason, item.Position))
                return null;

            // Caller content typed by :: T (or slot type in slot mode); ambient fill scope stays active (lexical).
            // It is built BEFORE the body, because that is the order the engine compiles the two in, and the order
            // decides which of two call sites into one definition gets to type a body they share: the first to
            // arrive. Built the other way round, a call inside this caller content reached a shared definition
            // after this call's own body had already typed it, and the emitter typed the body from the wrong one
            // of the two — precompiling a template the engine refuses at compile time and throwing the engine's
            // own InvalidCastException at render instead.
            BodyClass callerBody = null;
            if (!string.IsNullOrEmpty(item.ParameterTemplate) && item.Context != null)
            {
                BodyContext callerCtx;
                if (slotMode)
                {
                    callerCtx = SlotBodyContext(def, out var slotReason);
                    if (slotReason != null) { reason = slotReason; return null; }
                }
                else
                {
                    callerCtx = DefinitionBodyContext(def, out var ctxReason);
                    if (ctxReason != null) { reason = ctxReason; return null; }

                    // A callee that declares no model type of its own does not give its caller content an untyped
                    // model any more than it gives its body one: the engine compiles both against the value this
                    // call site passes, so `@frame("ab"){{@(Title)}}` is an HED0001 it raises at compile time.
                    // Emitted untyped, that read bound dynamically and the template precompiled and rendered what
                    // the engine will not compile. The same rule types both, so a call form the engine itself
                    // types `dynamic` still gets an untyped caller content and still matches.
                    if (callerCtx.IsDynamic &&
                        !TryTypeCallSiteBody(def, cp, bctx, item.Position, ref callerCtx, out reason))
                        return null;
                }

                // Only the MODEL changes here. The caller's prop layout and slot mode both stay active, because the
                // engine compiles caller content BEFORE it swaps either of them: the save/restore it does around a
                // definition body starts after this text is already compiled. Built without them, a first path
                // segment naming one of the caller's props silently read the member of the callee's model that
                // shares the name (or reported one that model has no member of at all), and an `@out(value)` under
                // an enclosing slot definition looked like an `@out` outside a slot.
                callerCtx = callerCtx.WithProps(bctx.Props).WithFills(bctx.Fills, bctx.RegionHostProps);
                if (bctx.InSlot)
                    callerCtx = callerCtx.AsSlot(bctx.SlotType);
                callerBody = BuildBody(item.ParameterTemplate, item.Context, callerCtx, out reason);
                if (callerBody == null)
                    return null;
            }

            // Compiled once per (definition identity, parse context); the fill scope is not part of that identity,
            // any more than it is part of the engine's.
            var bodyInfo = GetOrBuildDefinitionBody(def, defBodyCtx, out reason);
            if (bodyInfo == null || bodyInfo.Failed)
            {
                reason = reason ?? bodyInfo?.Reason ?? "definition body";
                return null;
            }

            // Each carrier uses its own body's flag (not OR'd); two different documents, separate derivation.
            bool bodyNeedsLocals = bodyInfo.Body.HostsParticipant;
            bool callerContentNeedsLocals = callerBody != null && callerBody.HostsParticipant;
            var field = AllocateDefinitionExtension(bodyInfo.Body.Name, callerBody?.Name, propsFieldRef,
                dynamicSettersRef, bodyNeedsLocals, callerContentNeedsLocals, slotMode, item.Position);
            return MakeCall(field, paramExpr, usesModel, item.Position, usesCsModel);
        }

        /// <summary>The bodiless caller-content splice. Non-slot: passes current model. Slot mode: value becomes projection model.</summary>
        private Call BuildOutCall(OutputItem item, CallParameter cp, BodyContext bctx, out string reason)
        {
            reason = null;
            if (!string.IsNullOrEmpty(item.ParameterTemplate)) { reason = "bodied @out"; return null; }
            // Use the canonical five-way test (OutExtension.InitStart), not a simplified approximation.
            bool hasValue = SlotRules.HasOutValue(cp);

            if (hasValue)
            {
                // Value on @out is only valid inside a slot-declaring definition body.
                if (!bctx.InSlot) { reason = "@out with value outside a slot definition"; return null; }
                if (cp.PropArguments != null && cp.PropArguments.Count != 0) { reason = "@out prop arguments"; return null; }
                if (!SlotValueAssignable(cp, bctx, out reason))
                    return null;
                if (!BuildParamExpr(cp, bctx, out var vParam, out var vUses, out var vCs, out reason, item.Position))
                    return null;
                var slotField = AllocateOutExtension(slotMode: true, item.Position);
                return MakeCall(slotField, vParam, vUses, item.Position, vCs);
            }

            // Bodiless valueless @out inside slot definition is a SlotValueRequired error at runtime.
            if (bctx.InSlot) { reason = "@out() without a slot value"; return null; }

            var field = AllocateOutExtension(slotMode: false, item.Position);
            return MakeCall(field, "scope.ModelData", false, item.Position);
        }

        /// <summary>
        /// The HED5014 twin: the engine refuses a template whose <c>@out</c> value is not assignable to the declared
        /// slot type — at compile time, with an id and a position — and the emitter precompiled the same template
        /// and rendered it, or threw <c>InvalidCastException</c> at render where the caller's content casts the
        /// value. Same rule, same conversion table (<c>OutExtension.InitStart</c> asks
        /// <c>PropConversion.CanConvert(…, allowBoxToObject: false)</c>), so the slot value the engine will not take
        /// is one the emitter refuses to precompile and the engine's refusal is what the reader gets.
        /// <para>What this cannot answer is a value whose type is not statically known here — a chained call, or an
        /// expression outside what the shared operator tables decide. Those keep precompiling: the emitter has
        /// nothing to check, and refusing every one of them would take working slot projections off the precompiled
        /// tier to catch a template the caller's cast already throws on. An <c>@out(this)</c> inside an <c>@list</c>
        /// body is <b>not</b> one of them any more: the element type is the collection's <c>IEnumerable&lt;T&gt;</c>
        /// argument, which is a static type and gets checked like any other.</para>
        /// </summary>
        private bool SlotValueAssignable(CallParameter cp, BodyContext bctx, out string reason)
        {
            reason = null;
            var valueType = CallSiteValueType(cp, bctx);
            if (valueType == null)
                return true;

            // The engine's first HED5014 arm, before the conversion table is consulted at all: a slot value with no
            // static type is refused outright. Asking C#'s table instead would say yes to every one of them —
            // `dynamic` converts implicitly to anything — and precompile a template the engine will not compile.
            if (valueType.TypeKind == TypeKind.Dynamic)
            {
                reason = "slot value is dynamic under a dynamic definition model";
                return false;
            }

            if (Convertible(valueType, bctx.SlotType, allowBoxToObject: false))
                return true;

            reason = "slot value '" + SymbolTypeResolver.FullyQualified(valueType) + "' is not assignable to slot type '" +
                     SymbolTypeResolver.FullyQualified(bctx.SlotType) + "'";
            return false;
        }

        /// <summary>The static type of the value a call site passes, or null where the emitter has none. Null is
        /// "cannot say", never "no type"; the compilation's <c>dynamic</c> is the opposite, a definite "no static
        /// type", which is what the engine refuses as a slot value and hands a <c>:: dynamic</c> body as its
        /// model.</summary>
        private ITypeSymbol CallSiteValueType(CallParameter cp, BodyContext bctx)
        {
            var model = bctx.IsDynamic ? bctx.DynamicBodyModel : bctx.ModelSymbol;

            // `this`, which is the enclosing body's own model. As a whole expression it is the model passthrough and
            // keeps the scope's type even where that type is dynamic.
            if (cp.NativeExpression is ThisNode)
                return model;

            // A literal is not an estimate: the parser already decoded it to a CLR value, and the engine types the
            // same literal the same way when it checks it against the slot type. Taking the "cannot say" exit here
            // let `@out(5)` into an `Article` slot precompile and render, where the engine refuses the template.
            if (cp.NativeExpression is LiteralNode literal)
                return LiteralType(literal);

            if (cp.NativeExpression != null)
                return ComputedValueType(cp.NativeExpression, model, bctx.Props).Symbol;

            // Embedded C#. The engine sends this same text to Roslyn and reads the semantic type back, so it is the
            // most definite type it ever has for a call-site value — and answering "cannot say" for it exempted the
            // value from every gate that reads this, which is how `@out(@model.Products.Count)` into a `string` slot
            // and `@list(@model.Products.Count)` pre-compiled and rendered where the engine refuses the template.
            // Asked only under FullCSharp: in any other mode the expression is not emitted at all.
            if (cp.CSharpExpression != null)
                return _config.ExpressionMode == Heddle.Data.ExpressionMode.FullCSharp
                    ? _csharpTyper.TypeOf(cp.CSharpExpression, model, _usings)
                    : null;

            // A chain call-parameter's value is the chain's render type, not the producer's own type: the engine
            // reads `callParameter.RenderType`, which is the last item's InitStart return, and every chain the
            // emitter can flatten ends in a plain carrier whose InitStart is the base one — `typeof(string)`.
            // Answering "cannot say" here let a non-enumerable producer past the @list gate.
            if (cp.ChainParameter != null && cp.ChainParameter.Count != 0)
                return _compilation.GetSpecialType(SpecialType.System_String);

            if (bctx.IsDynamic && model == null)
                return null;

            // A root reference is "cannot say" and cannot become a divergence: BuildParamExpr refuses every one of
            // them outright, so an `@out(::X)` degrades before anything is emitted whatever this answers.
            if (!cp.IsModelTypeParameter || cp.RootReference)
                return null;

            var segments = cp.ModelParameter;
            if (segments == null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
                return model;

            if (bctx.Props != null && bctx.Props.ByName.TryGetValue(segments[0], out var slot))
            {
                if (segments.Length == 1)
                    return slot.Type;
                var rest = new string[segments.Length - 1];
                System.Array.Copy(segments, 1, rest, 0, rest.Length);
                return ResolvedTypeOf(slot.Type, rest);
            }

            // A member path read off a `dynamic` model is `dynamic` in the engine too — the accessor takes its
            // dynamic exit before it resolves a segment — and a dynamic value is one the engine will not accept.
            if (model.TypeKind == TypeKind.Dynamic)
                return model;

            return ResolvedTypeOf(model, segments);
        }

        /// <summary>
        /// The model an <c>@list</c> body runs under, which is the element type of the collection the call site
        /// hands it: the host resolves <c>IEnumerable&lt;T&gt;</c> and falls back to <c>dynamic</c> for a collection
        /// that implements no generic form. Null is "cannot say" — the data expression itself has no static type
        /// here — and is not the same answer as <c>dynamic</c>.
        /// <para>A collection reaching <c>IEnumerable&lt;T&gt;</c> at more than one <c>T</c> is a third answer and
        /// sets <paramref name="ambiguous"/>. The host picks one of them by reflection order, which is not an order
        /// this can reproduce — but it does pick one, so the engine has an element type here and compiles the whole
        /// body against it. Treating that as an ordinary "cannot say" put the body on the dynamic tier with no
        /// model behind it, which every gate downstream exempts: the template precompiled and rendered where the
        /// engine refuses it at compile time. The emitter not being able to name the type the engine chose is a
        /// reason to leave the body to the dynamic tier, not to emit one against no type at all.</para>
        /// </summary>
        private ITypeSymbol ListElementModel(CallParameter cp, BodyContext bctx, out bool ambiguous)
        {
            ambiguous = false;
            var dataType = CallSiteValueType(cp, bctx);
            if (dataType == null)
                return null;
            if (dataType.TypeKind == TypeKind.Dynamic)
                return dataType;

            ITypeSymbol element = null;
            foreach (var candidate in SelfAndInterfaces(dataType))
            {
                if (!(candidate is INamedTypeSymbol named) ||
                    named.ConstructedFrom?.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T ||
                    named.TypeArguments.Length != 1)
                    continue;
                if (element != null && !SymbolEqualityComparer.Default.Equals(element, named.TypeArguments[0]))
                {
                    ambiguous = true;
                    return null;
                }

                element = named.TypeArguments[0];
            }

            return element ?? _compilation.DynamicType;
        }

        /// <summary>
        /// Whether the engine's accepted-type check would let this value reach this extension. It asks whether
        /// the value's static type is assignable to any type the extension declares with <c>[DataType]</c>, after
        /// unwrapping a nullable and with a value that has no static type exempt — a <c>dynamic</c> value is decided
        /// at render, not at compile. An extension declaring no <c>[DataType]</c> accepts anything.
        /// <para>The rule is read off the attribute rather than written out per extension, so it covers every
        /// built-in that declares one — <c>@list</c>'s <c>IEnumerable</c> and <c>@for</c>'s <c>Range</c>/<c>int</c> —
        /// and any host extension that declares one, without a list here to keep in step.</para>
        /// <para>"Assignable" is the <b>CLR</b> relation and is asked of the one adapter that answers it, not
        /// re-derived here. Reproducing it as nominal identity over the base chain and the interface set answers
        /// no to every relation the CLR admits that identity does not — generic and array covariance, and the
        /// CLR's own <c>Nullable&lt;T&gt;</c> treatment, which alone made <c>[DataType(typeof(int?))]</c> accept
        /// nothing whatever, not even an <c>int?</c>.</para>
        /// <para>"Cannot say" is exempt for the same reason it is everywhere else: refusing on a type the emitter
        /// never established would cost the precompiled tier over templates that are fine.</para>
        /// </summary>
        private bool AcceptedTypeSatisfied(ExtensionBinder.Info info, ITypeSymbol valueType, out string reason)
        {
            reason = null;
            var accepted = info.AcceptedDataTypes;
            if (accepted.Count == 0)
                return true;
            if (valueType == null || valueType.TypeKind == TypeKind.Dynamic || valueType.TypeKind == TypeKind.Error)
                return true;
            if (TryGetNullableUnderlying(valueType, out var underlying))
                valueType = underlying;

            foreach (var candidate in accepted)
                if (candidate == null || candidate.TypeKind == TypeKind.Dynamic ||
                    TypeFacts.IsAssignableFrom(candidate, valueType))
                    return true;

            var names = new List<string>(accepted.Count);
            foreach (var candidate in accepted)
                names.Add(candidate == null ? "?" : SymbolTypeResolver.FullyQualified(candidate));
            reason = "value type '" + SymbolTypeResolver.FullyQualified(valueType) +
                     "' is not one of the accepted types [" + string.Join(", ", names) + "]";
            return false;
        }

        private static IEnumerable<ITypeSymbol> SelfAndInterfaces(ITypeSymbol type)
        {
            yield return type;
            foreach (var iface in type.AllInterfaces)
                yield return iface;
        }

        /// <summary>A computed native expression's static type together with the shared tables' descriptor of it.
        /// The two travel as a pair because neither reconstructs the other: the null literal has a type
        /// (<c>System.Object</c>) and a descriptor that is not that type's, and a promotion result has a descriptor
        /// with no identity to rebuild a reference or enum type from.</summary>
        private readonly struct ComputedValue
        {
            public ComputedValue(ITypeSymbol symbol, OperandKind kind) { Symbol = symbol; Kind = kind; }
            public ITypeSymbol Symbol { get; }
            public OperandKind Kind { get; }
            public static ComputedValue None => new ComputedValue(null, OperandKind.Unknown);
        }

        /// <summary>
        /// The static type the engine's native-expression compiler gives a computed call-site value — an
        /// arithmetic, comparison, ternary or coalesce expression — in the caller's own scope.
        /// <para>The operand descriptors and the promotion arithmetic are the shared operator tables', the same ones
        /// the expression writer consults before it emits anything, so a shape this types is a shape the two tiers
        /// already agree about. Everything the tables leave undecided — a function call, an indexer, a user type's
        /// operator, an operand read off a <c>dynamic</c> receiver — is "cannot say", and the caller degrades rather
        /// than guessing at a model the engine has typed statically.</para>
        /// </summary>
        private ComputedValue ComputedValueType(ExprNode node, ITypeSymbol model, PropLayoutInfo props)
        {
            switch (node)
            {
                case LiteralNode literal:
                {
                    if (literal.LiteralError != null)
                        return ComputedValue.None;
                    var symbol = LiteralType(literal);
                    return new ComputedValue(symbol,
                        literal.Value == null ? OperandKind.Null : SymbolFacts.Classify(symbol));
                }

                // `this` INSIDE a computed value is the enclosing body's own model as an operand, which the engine's
                // compiler types statically and refuses outright where the scope has no static type. That is not the
                // same question as `this` standing alone as the whole call parameter, which is the model passthrough
                // and keeps its meaning on the dynamic tier — see CallSiteValueType.
                case ThisNode _:
                    return model == null || model.TypeKind == TypeKind.Dynamic
                        ? ComputedValue.None
                        : new ComputedValue(model, SymbolFacts.Classify(model));

                case PathNode path:
                {
                    // A body prop read wins over the model on the first segment, the way the engine's own compiler
                    // tries the active layout before it ever looks at the scope type. Typing this off the model
                    // instead gave a shadowed name the shadowed member's type, and the value the call site was
                    // judged by was not the value it passes.
                    var propType = PropRootType(path, props);
                    if (propType != null)
                        return new ComputedValue(propType, SymbolFacts.Classify(propType));

                    // A `::`-rooted path, a path over a scope with no static type, and a path hung off any target
                    // but `this` are where the engine's own compiler stops resolving — it refuses the expression
                    // rather than typing it. `this.` roots at the model, so it types like a bare path.
                    if ((path.Target != null && !(path.Target is ThisNode)) || path.RootRef || model == null ||
                        model.TypeKind == TypeKind.Dynamic || IsPropName(path, props))
                        return ComputedValue.None;
                    var resolved = ResolvedTypeOf(model, path.Segments);
                    return resolved == null
                        ? ComputedValue.None
                        : new ComputedValue(resolved, SymbolFacts.Classify(resolved));
                }

                case UnaryNode unary:
                {
                    var operand = ComputedValueType(unary.Operand, model, props);
                    return FromKind(NativeOperatorRules.UnaryResult(unary.Operator, operand.Kind));
                }

                case BinaryNode binary:
                {
                    var left = ComputedValueType(binary.Left, model, props);
                    var right = ComputedValueType(binary.Right, model, props);
                    var result = NativeOperatorRules.BinaryResult(binary.Operator, left.Kind, right.Kind);
                    // `x ?? null` is `x`'s own type, identity included — the one supported result the descriptor
                    // alone could not name.
                    if (binary.Operator == ExprOperator.Coalesce && right.Kind.Category == OperandCategory.NullLiteral &&
                        result.Category != OperandCategory.Unknown)
                        return left;
                    return FromKind(result);
                }

                case TernaryNode ternary:
                {
                    var condition = ComputedValueType(ternary.Condition, model, props);
                    var whenTrue = ComputedValueType(ternary.WhenTrue, model, props);
                    var whenFalse = ComputedValueType(ternary.WhenFalse, model, props);
                    if (NativeOperatorRules.ClassifyTernary(condition.Kind, whenTrue.Kind, whenFalse.Kind) !=
                        OperatorVerdict.Supported)
                        return ComputedValue.None;
                    return whenTrue;
                }

                case CallNode call:
                {
                    // A call in this position is the engine's own value, statically typed by the function's return
                    // type — `@list(min(1, 2))` is a template it refuses with that type in the message. Leaving it
                    // "cannot say" exempted every such call from the checks the other shapes go through.
                    //
                    // This writer is thrown away undrained, and the refusals it may record with it. That costs
                    // nothing: a call the ranker refuses has no return type either, so the value stays "cannot say",
                    // no gate can refuse the template on account of it, and the writer that emits the same
                    // expression reaches it and reports what this one saw.
                    var writer = new NativeExpressionWriter(_resolver, model, "m", _exports, TypeFacts,
                        AllocateHopLocal, props);
                    var kind = writer.EstimateCallReturn(call);

                    // The chosen overload's DECLARED return type, not the shared descriptor of it. The descriptor
                    // names the primitives and nothing else, so typing the call through it left `range(1, 3)` — the
                    // one built-in returning neither string, bool nor numeric, and the one a reader reaches for to
                    // iterate — and every host export returning a date or a class of its own indistinguishable from
                    // a call the generator cannot type at all. `dynamic` is the exception: the engine reads a
                    // MethodInfo, where a `dynamic` return is `System.Object`, and that is the type it checks.
                    var returned = writer.CallReturnType(call, _compilation);
                    if (returned == null)
                        return FromKind(kind);
                    if (returned.TypeKind == TypeKind.Dynamic)
                        returned = _compilation.GetSpecialType(SpecialType.System_Object);
                    return new ComputedValue(returned, kind);
                }

                default:
                    return ComputedValue.None;
            }
        }

        /// <summary>True when this path resolves prop-first: no target, no <c>::</c> root, and a first segment the
        /// active layout carries. Once that holds the model is out of the picture — a first segment naming a prop
        /// never falls back to the member it shadows.</summary>
        private static bool IsPropName(PathNode path, PropLayoutInfo props) =>
            path.Target == null && !path.RootRef && props != null && path.Segments.Count != 0 &&
            props.ByName.ContainsKey(path.Segments[0]);

        /// <summary>The static type of a prop-rooted path, or null when the path is not prop-rooted or its remaining
        /// segments do not resolve off the slot's declared type.</summary>
        private ITypeSymbol PropRootType(PathNode path, PropLayoutInfo props)
        {
            if (!IsPropName(path, props))
                return null;
            var slot = props.ByName[path.Segments[0]];
            if (slot.Type == null)
                return null;
            if (path.Segments.Count == 1)
                return slot.Type;
            if (slot.Type.TypeKind == TypeKind.Dynamic)
                return null;
            var rest = new string[path.Segments.Count - 1];
            for (int i = 1; i < path.Segments.Count; i++)
                rest[i - 1] = path.Segments[i];
            return ResolvedTypeOf(slot.Type, rest);
        }

        /// <summary>Names the type a shared result descriptor stands for. Only the categories the descriptor
        /// identifies completely — the numeric primitives, <c>bool</c>, <c>string</c> — can be named; a reference,
        /// enum or other struct result carries no identity and stays "cannot say".</summary>
        private ComputedValue FromKind(OperandKind kind)
        {
            switch (kind.Category)
            {
                case OperandCategory.String:
                    return new ComputedValue(_compilation.GetSpecialType(SpecialType.System_String), kind);
                case OperandCategory.Bool:
                    return new ComputedValue(
                        Lift(_compilation.GetSpecialType(SpecialType.System_Boolean), kind.IsNullable), kind);
                case OperandCategory.Numeric:
                {
                    var special = SymbolFacts.ToSpecialType(kind.Kind);
                    if (special == SpecialType.None)
                        return ComputedValue.None;
                    return new ComputedValue(Lift(_compilation.GetSpecialType(special), kind.IsNullable), kind);
                }
                default:
                    return ComputedValue.None;
            }
        }

        private ITypeSymbol Lift(ITypeSymbol type, bool nullable) =>
            nullable ? _compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(type) : type;

        /// <summary>
        /// Types a definition body that declares no model type of its own by the model the engine compiles it
        /// against at <b>this</b> call site.
        /// <para><c>:: dynamic</c> does not declare an untyped body. The engine compiles the body once per call site
        /// off the value that call site passes, so a caller handing it a static value gets a body bound against that
        /// value's type — and a member read the type does not have is an <c>HED0001</c> the engine raises when it
        /// compiles the template. Emitting the body untyped bound those reads dynamically instead: they threw
        /// <c>RuntimeBinderException</c> at render where the engine had refused the template outright, and where the
        /// value was <c>null</c> — <c>@frame(null)</c>, whose model the engine types <c>System.Object</c> — the
        /// dynamic read yielded empty and the page RENDERED what the engine will not compile at all.</para>
        /// <para>Only a call form the engine <b>itself</b> types <c>dynamic</c> keeps an untyped body. A value the
        /// emitter merely cannot type degrades: "the emitter cannot say" and "the engine has no type" are different
        /// answers, and treating the first as the second is how a dynamically-bound body got emitted where the
        /// engine had bound one statically.</para>
        /// <para><c>:: object</c> reaches here for the same reason and takes a different model rule. Only
        /// <c>:: dynamic</c> makes the engine's model accessor take its dynamic exit; under <c>:: object</c> the
        /// accessor resolves a member path statically, so <c>@frame(Name)</c> hands that body a <c>string</c> and a
        /// read the <c>string</c> does not have is a compile-time refusal rather than a render-time throw.</para>
        /// </summary>
        private bool TryTypeCallSiteBody(DefinitionItem def, CallParameter cp, BodyContext bctx,
            BlockPosition position, ref BodyContext defBodyCtx, out string reason)
        {
            reason = null;
            var model = DeclaresDynamicModel(def)
                ? DynamicDefinitionBodyModel(cp, bctx)
                : ObjectDefinitionBodyModel(cp, bctx);
            if (model == null)
            {
                reason = "definition body over a caller value this call site cannot type";
                return false;
            }

            if (model.TypeKind == TypeKind.Dynamic)
            {
                defBodyCtx = defBodyCtx.WithDynamicBodyModel(model);
                return true;
            }

            if (!CanWriteTypeName(model, position, out reason))
                return false;

            defBodyCtx = defBodyCtx.TypedAs(model);
            return true;
        }

        /// <summary>
        /// The model type the engine compiles a <c>:: dynamic</c> definition's body against at <b>this</b> call site,
        /// which is what the body's <c>@out</c> values are then checked against. It mirrors the engine's model
        /// accessor: a native expression — <c>this</c>, a literal, a computed one — is compiled in the caller's own
        /// scope and keeps its static type, and so does a read of the caller's own prop, which is tried first of all.
        /// Everything else — a member path, a root reference, a bare <c>@name()</c> — reaches the accessor's
        /// <c>dynamic</c> exit before it resolves anything, and the body gets a <c>dynamic</c> model. That is not a
        /// refusal on its own: an <c>@out</c> of a literal or of the definition's own prop is still statically typed
        /// and still checked. It is only an <c>@out</c> that reads the model which the engine then refuses.
        /// <para>Null is "cannot say" — a computed expression outside what the shared operator tables decide, a
        /// chained value, embedded C#. See <see cref="TryTypeCallSiteBody"/> for what becomes of it.</para>
        /// </summary>
        private ITypeSymbol DynamicDefinitionBodyModel(CallParameter cp, BodyContext bctx)
        {
            if (cp.NativeExpression != null)
                return CallSiteValueType(cp, bctx);

            // A chain never reaches the model accessor at all: the engine compiles the chain and hands the body its
            // render type, which is text. Answering "cannot say" here degraded `@frame(len(Name))` — a template the
            // engine compiles with a `string` body model — off the precompiled tier entirely.
            if (cp.ChainParameter != null && cp.ChainParameter.Count != 0)
                return CallSiteValueType(cp, bctx);

            if (!cp.IsModelTypeParameter)
                return null;

            var segments = cp.ModelParameter;
            bool propRead = !cp.RootReference && segments != null && segments.Length > 0 &&
                            !string.IsNullOrEmpty(segments[0]) &&
                            bctx.Props != null && bctx.Props.ByName.ContainsKey(segments[0]);
            return propRead ? CallSiteValueType(cp, bctx) : _compilation.DynamicType;
        }

        /// <summary>
        /// The model type the engine compiles an <c>:: object</c> definition's body against at <b>this</b> call
        /// site. Unlike <c>:: dynamic</c> the accessor takes no dynamic exit here, so the value's own static type
        /// stands whatever shape the call parameter took — including a member path, which is why
        /// <c>@frame(Name)</c> hands such a body a <c>string</c>.
        /// <para>The one exception is a caller whose own scope has no static type: the accessor resolves nothing
        /// against it and the bare-call arm reads that same dynamic scope type, so the body's model is
        /// <c>dynamic</c>. <see cref="CallSiteValueType"/> answers "cannot say" there, which is the answer a check
        /// that must be conservative wants and the wrong one for a question about what the engine did — a
        /// model-less document is nothing but this shape, and refusing it took every one of them off the
        /// precompiled tier.</para>
        /// </summary>
        private ITypeSymbol ObjectDefinitionBodyModel(CallParameter cp, BodyContext bctx)
        {
            if (cp.IsModelTypeParameter && bctx.IsDynamic && bctx.DynamicBodyModel == null)
                return _compilation.DynamicType;

            return CallSiteValueType(cp, bctx);
        }

        /// <summary>The symbol for a decoded literal, or null where the emitter must not claim one — an out-of-range
        /// numeric the compiler is about to reject anyway.
        /// <para>The null literal is not one of those. The engine types it <c>System.Object</c> and checks it like
        /// any other value, which is why <c>@out(null)</c> into a <c>string</c> slot is a template it refuses; taking
        /// the "cannot say" exit for it here precompiled and rendered that template instead.</para>
        /// </summary>
        private ITypeSymbol LiteralType(LiteralNode literal)
        {
            if (literal.LiteralError != null)
                return null;
            if (literal.Value == null)
                return _compilation.GetSpecialType(SpecialType.System_Object);
            switch (literal.Value)
            {
                case bool _: return _compilation.GetSpecialType(SpecialType.System_Boolean);
                case string _: return _compilation.GetSpecialType(SpecialType.System_String);
                case char _: return _compilation.GetSpecialType(SpecialType.System_Char);
                case int _: return _compilation.GetSpecialType(SpecialType.System_Int32);
                case uint _: return _compilation.GetSpecialType(SpecialType.System_UInt32);
                case long _: return _compilation.GetSpecialType(SpecialType.System_Int64);
                case ulong _: return _compilation.GetSpecialType(SpecialType.System_UInt64);
                case float _: return _compilation.GetSpecialType(SpecialType.System_Single);
                case double _: return _compilation.GetSpecialType(SpecialType.System_Double);
                case decimal _: return _compilation.GetSpecialType(SpecialType.System_Decimal);
                default: return null;
            }
        }

        private ITypeSymbol ResolvedTypeOf(ITypeSymbol start, IReadOnlyList<string> segments)
        {
            if (start == null || start.TypeKind == TypeKind.Dynamic)
                return null;
            var resolution = _resolver.ResolvePath(start, segments);
            return resolution.Kind == SymbolTypeResolver.PathKind.Resolved ? resolution.ResultType : null;
        }

        /// <summary>Renders the named template with the parameter as its model. Strategy resolved lazily on first render (registry first, dynamic compile second) and memoized. The name must be static text.</summary>
        private Partial BuildPartialCall(OutputItem item, CallParameter cp, BodyContext bctx, out string reason)
        {
            reason = null;
            var name = item.ParameterTemplate;
            if (string.IsNullOrWhiteSpace(name)) { reason = "empty @partial name"; return null; }

            // Name must be static text; bodies with output chains, definitions, or imports are dynamic.
            var ctx = item.Context;
            if (ctx != null &&
                ((ctx.OutputChains != null && ctx.OutputChains.Count != 0) ||
                 (ctx.RawOutputItems != null && ctx.RawOutputItems.Count != 0) ||
                 (ctx.DefinitionsBlock != null && ctx.DefinitionsBlock.Positions != null && ctx.DefinitionsBlock.Positions.Count != 0)))
            {
                reason = "dynamic @partial name";
                return null;
            }

            if (!TemplateKey.TryNormalize(name.Trim(), out var key))
            {
                reason = "unnormalizable @partial name '" + name.Trim() + "'";
                return null;
            }

            if (!BuildParamExpr(cp, bctx, out var modelExpr, out var usesModel, out var usesCs, out reason, item.Position))
                return null;

            var field = "_partial" + _partialCounter++;
            _fieldDecls.Append("        private static global::Heddle.Runtime.IProcessStrategy ").Append(field)
                .Append(";\n");

            // Dynamic-compile fallback types child by caller-site model type.
            string callerModelFq = (!bctx.IsDynamic && bctx.ModelSymbol != null)
                ? SymbolTypeResolver.FullyQualified(bctx.ModelSymbol)
                : null;

            var (sl, sc) = _map.Map(item.Position.StartIndex);
            var (el, ec) = _map.Map(item.Position.StartIndex + item.Position.Length);
            return new Partial
            {
                FieldName = field, Key = key, ModelExpr = modelExpr, CallerModelTypeFq = callerModelFq,
                UsesModelLocal = usesModel, UsesCSharpModel = usesCs,
                SpanStartLine = sl, SpanStartCol = sc, SpanEndLine = el, SpanEndCol = ec
            };
        }

        /// <summary>
        /// The body a call site gets, built once per body identity. There is exactly one such identity and both the
        /// sharing rule and the emitted-code cache are keyed by it, because they are two halves of one question:
        /// which call sites the engine gives one compiled body to.
        /// <para>That identity is the definition's own parse context, and nothing else — in particular <b>not</b>
        /// the fill scope. The engine memoizes each item of a body by the parsed <c>OutputItem</c> it came from, and
        /// saves and restores the region fill scope around the body compile without putting it in that memo, so a
        /// second call site filling a region differently still gets the first site's body, fills included. Keyed with
        /// the fill scope on this side, the two call sites got two bodies and the page rendered each site's own fill
        /// where the engine renders the first site's twice.</para>
        /// <para>The name and the declaration span are not terms of it either, and not because nothing tests them:
        /// they cannot decide anything a definition's context has not already decided. A <c>DefinitionItem</c> gets a
        /// fresh <c>ParseContext</c> in its constructor, and every path that hands two live items one context —
        /// the copy constructor, <c>OverrideWith</c>, and the region-fill materializer, which takes its name and its
        /// span from the same candidate — carries the name and the span across with it. The one path that does not,
        /// <c>ParseContext.IsolateContext</c>, gives the copy a <em>new</em> context, which only splits further.
        /// </para>
        /// </summary>
        private DefBodyInfo GetOrBuildDefinitionBody(DefinitionItem def, BodyContext bodyCtx, out string reason)
        {
            reason = null;
            var key = ParseContextId(def.Context).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!TryShareBodyTyping(key, ref bodyCtx, out reason))
                return null;

            if (_definitionBodies.TryGetValue(key, out var existing))
            {
                reason = existing.Failed ? existing.Reason : null;
                return existing;
            }

            var info = new DefBodyInfo { Body = NewBody(bodyCtx) };
            // Pre-mark to bake correct needsLocals when self-call is encountered during population.
            if (ParticipantScan.BodyHostsParticipant(def.Context, HasScopeChannel))
                info.Body.HostsParticipant = true;
            _definitionBodies[key] = info;

            if (!PopulateBody(info.Body, def.ParameterTemplate, def.Context, bodyCtx, out var populateReason))
            {
                info.Failed = true;
                info.Reason = populateReason;
                reason = populateReason;
            }

            return info;
        }

        /// <summary>
        /// The engine compiles a definition body once per <see cref="ParseContext"/> the definition is reached
        /// through — not once per call site. Every item it compiles is memoized for the whole compile, so a second
        /// call site into one definition re-uses the code the first one produced, and its own value is simply cast
        /// to the model the first one typed that code against. Two call sites reach two contexts, and so two
        /// compiles, only where the parser isolated the definition tree between them: a document-scope output chain
        /// and a subtemplate outside a definition body both isolate, and inside a definition body nothing does — one
        /// body there serves every call, however many models the calls hand it.
        /// <para>So the emitter shares by the same measure, and the call site that arrives second is re-typed to the
        /// body that already exists. Where that body is typed, its <c>(T)scope.ModelData</c> is the engine's cast and
        /// reproduces it exactly, failure included. Where it is on the dynamic tier there is no cast to reproduce —
        /// its reads bind to whatever they are handed — so a later call site of another model degrades instead of
        /// reading members off a value the engine would have refused to cast.</para>
        /// <para>Two typings are the same when they agree on the tier and on the model symbol. A third term for
        /// <c>DynamicBodyModel</c> would decide nothing: every context that reaches here got its model from
        /// <see cref="DefinitionBodyContext"/> or <see cref="TryTypeCallSiteBody"/>, and both leave the two in
        /// step — a typed body carries its own model in both, and an untyped one is untyped precisely because the
        /// model is the compilation's <c>dynamic</c>.</para>
        /// </summary>
        private bool TryShareBodyTyping(string key, ref BodyContext bodyCtx, out string reason)
        {
            reason = null;
            if (!_sharedBodyTyping.TryGetValue(key, out var first))
            {
                _sharedBodyTyping[key] = bodyCtx;
                return true;
            }

            if (first.IsDynamic == bodyCtx.IsDynamic &&
                SymbolEqualityComparer.Default.Equals(first.ModelSymbol, bodyCtx.ModelSymbol))
                return true;

            if (first.IsDynamic)
            {
                reason = "definition body already compiled untyped for a call site of another model";
                return false;
            }

            bodyCtx = first;
            return true;
        }

        private readonly Dictionary<string, BodyContext> _sharedBodyTyping =
            new Dictionary<string, BodyContext>(System.StringComparer.Ordinal);

        // ParseContext declares no equality of its own, so the dictionary keys by reference — which is the
        // question being asked: whether the two call sites reached the same parsed body or an isolated copy of it.
        private readonly Dictionary<ParseContext, int> _parseContextIds = new Dictionary<ParseContext, int>();

        private int ParseContextId(ParseContext context)
        {
            if (context == null)
                return 0;
            if (!_parseContextIds.TryGetValue(context, out var id))
                _parseContextIds[context] = id = _parseContextIds.Count + 1;
            return id;
        }

        /// <summary>The rebind for the generator's fill scope: while building region
        /// <paramref name="name"/>'s own fill body, the name resolves to <paramref name="target"/> (its base
        /// default) so a self-call terminates; a null target removes the entry.</summary>
        private static Dictionary<string, DefinitionItem> Rebound(Dictionary<string, DefinitionItem> fills,
            string name, DefinitionItem target)
        {
            var copy = fills == null
                ? new Dictionary<string, DefinitionItem>(System.StringComparer.Ordinal)
                : new Dictionary<string, DefinitionItem>(fills, System.StringComparer.Ordinal);
            if (target == null)
                copy.Remove(name);
            else
                copy[name] = target;
            return copy.Count == 0 ? null : copy;
        }

        // Unconsumed candidates (private/dangling) un-precompile the template so dynamic tier raises the error.
        private readonly HashSet<RegionFillCandidate> _consumedCandidates = new HashSet<RegionFillCandidate>();

        /// <summary>
        /// The generator's call-site fill step is a thin adapter over
        /// the shared <see cref="RegionFillResolver"/> — the same four-step decision (origin filter → region lookup
        /// → public gate → region-default fetch) the runtime's <c>HeddleCompiler.BuildRegionFillScope</c> drives —
        /// and reacts to each verdict <b>exactly as the runtime reacts</b>:
        /// <list type="bullet">
        /// <item><description><c>Matched</c> → retract the candidate's parse-emitted base-not-found error and
        /// materialize the fill through the shared <see cref="DefinitionMaterializer"/>.</description></item>
        /// <item><description><c>Dangling</c>/<c>DefaultMissing</c> → skip, leaving the parse-emitted error in
        /// place to surface. Un-precompiling the whole template <em>silently</em> instead would make the
        /// generator looser than the engine it must match.</description></item>
        /// <item><description><c>Private</c> → reproduce the retract-and-raise: the base-not-found error is
        /// retracted and HED7024, the twin of the runtime's HED5019, is raised once per candidate at the override
        /// declaration.</description></item>
        /// </list>
        /// Only the reactions are per-side: the channels differ (a compile-error list vs Roslyn diagnostics), the
        /// semantics do not. Always returns true — no verdict is a refusal any more.
        /// </summary>
        private bool TryBuildGeneratorFillScope(DefinitionItem def, OutputItem item,
            out Dictionary<string, DefinitionItem> fills, out string reason)
        {
            fills = null;
            reason = null;
            var callerContext = item.Context;
            var candidates = callerContext?.RegionFillCandidates;
            if (candidates == null || candidates.Count == 0)
                return true;

            Dictionary<string, DefinitionItem> matched = null;
            RegionFillResolver.Resolve(candidates, callerContext.OriginIdentity,
                (string name, out bool isPublic) =>
                {
                    foreach (var region in def.Regions)
                    {
                        if (string.Equals(region.Name, name, System.StringComparison.Ordinal))
                        {
                            isPublic = region.IsPublic;
                            return true;
                        }
                    }

                    isPublic = false;
                    return false;
                },
                def,
                (candidate, verdict, materialized) =>
                {
                    switch (verdict)
                    {
                        case RegionFillVerdict.Dangling:
                        case RegionFillVerdict.DefaultMissing:
                            break;

                        case RegionFillVerdict.Private:
                            RetractCandidateError(candidate);
                            if (!candidate.PrivateOverrideReported)
                            {
                                candidate.PrivateOverrideReported = true;
                                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.RegionNotPublic,
                                    candidate.Position, candidate.Name, def.Name));
                            }

                            break;

                        default: // Matched
                            RetractCandidateError(candidate);
                            matched = matched ??
                                      new Dictionary<string, DefinitionItem>(System.StringComparer.Ordinal);
                            matched[candidate.Name] = materialized;
                            _consumedCandidates.Add(candidate);
                            break;
                    }
                });

            fills = matched;
            return true;
        }

        /// <summary>The build tier's half of the retract: the candidate's tentative base-not-found error is
        /// removed from the shared parse-side list (as the runtime removes it from both of its lists) and recorded
        /// so the generator's forwarding pass knows not to report it.</summary>
        private void RetractCandidateError(RegionFillCandidate candidate)
        {
            _retractedCandidateErrors.Add(candidate.Error);
            candidate.Origin.Errors.Remove(candidate.Error);
        }

        private readonly HashSet<Heddle.Data.HeddleCompileError> _retractedCandidateErrors =
            new HashSet<Heddle.Data.HeddleCompileError>();

        /// <summary>
        /// The body context of a region: the declared <c>:: T</c> where there is one, otherwise the model this call
        /// site hands it. Either way the body borrows the enclosing component's prop layout, because a region
        /// declares none of its own.
        /// <para>Which of the two it is turns on the type the declaration <b>resolves to</b>, never on how it is
        /// spelled. The engine asks <c>ResolveType(ModelType) != typeof(object)</c>, and <c>dynamic</c>,
        /// <c>object</c>, <c>System.Object</c> and a region that declares nothing all answer the same — every one
        /// of them leaves the body to be typed by the call site. Keyed on the spelling instead, two of the four
        /// went to the declared-type arm and the body came out untyped with no model behind it: its reads bound
        /// dynamically and threw at render where the engine, which had typed that body from the call site, refused
        /// the template outright — and where both tiers did render, a body shared by two call sites printed the
        /// members of the wrong one of the two models.</para>
        /// <para>The call-site typing is <see cref="TryTypeCallSiteBody"/>, the same rule and the same code a
        /// non-region definition takes, so <c>:: dynamic</c> keeps sending the engine's model accessor down its
        /// dynamic exit here exactly as it does there.</para>
        /// </summary>
        private bool TryRegionBodyContext(DefinitionItem def, CallParameter cp, BodyContext bctx,
            BlockPosition position, out BodyContext ctx, out string reason)
        {
            ctx = DefinitionBodyContext(def, out reason);
            if (reason != null)
            {
                ctx = default;
                return false;
            }

            ctx = ctx.WithProps(bctx.RegionHostProps);
            if (ctx.IsDynamic && !TryTypeCallSiteBody(def, cp, bctx, position, ref ctx, out reason))
            {
                ctx = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Whether a definition declaring <c>:: T</c> accepts the value its call site passes. The engine compares
        /// the two, but only where the value came out of its model accessor with a static type in hand — a read of
        /// a body prop or a member path it resolved. A literal, <c>this</c>, a computed expression, a chain and a
        /// path that ends in a dynamic hop all leave the accessor with no input type, and the engine then compares
        /// the declared type with itself, which always passes.
        /// <para>That asymmetry is the rule, not an accident of it: checking every call form would refuse
        /// <c>@frame(5)</c> and <c>@frame(this)</c> against a <c>:: string</c> the engine compiles without
        /// complaint. "Cannot say" is exempt for the usual reason — the emitter not having established a type is
        /// not the engine not having one.</para>
        /// </summary>
        private bool DeclaredModelAcceptsCallSiteValue(DefinitionItem def, CallParameter cp, BodyContext bctx,
            out string reason)
        {
            reason = null;
            var modelTypeName = def.ModelType;
            if (string.IsNullOrEmpty(modelTypeName) || DeclaresDynamicModel(def) ||
                string.Equals(modelTypeName, "object", System.StringComparison.Ordinal))
                return true;

            // Only a member path is compared. A native expression (where a literal, `this` and every computed form
            // arrive), a chain and embedded C# are the three shapes that are not one, and for every call the
            // grammar produces they also leave no path behind — so the two halves of this test agree on every input
            // and neither carries it alone. Both are here because each states a condition this comparison needs,
            // not because the second covers a case the first misses.
            var segments = cp.ModelParameter;
            if (!cp.IsModelTypeParameter || segments == null || segments.Length == 0 ||
                string.IsNullOrEmpty(segments[0]))
                return true;

            var declared = _resolver.ResolveModelType(modelTypeName, _usings);
            if (declared == null || declared.SpecialType == SpecialType.System_Object)
                return true;

            var valueType = CallSiteValueType(cp, bctx);
            if (valueType == null || valueType.TypeKind == TypeKind.Dynamic || valueType.TypeKind == TypeKind.Error)
                return true;
            if (TryGetNullableUnderlying(valueType, out var underlying))
                valueType = underlying;

            if (TypeFacts.IsAssignableFrom(declared, valueType))
                return true;

            reason = "value type '" + SymbolTypeResolver.FullyQualified(valueType) +
                     "' is not accepted by the definition model '" +
                     SymbolTypeResolver.FullyQualified(declared) + "'";
            return false;
        }

        /// <summary>
        /// The model a definition's body runs under. A declared <c>:: T</c> resolves to its symbol and the body is
        /// typed by it; anything the engine resolves to <c>System.Object</c> — <c>:: dynamic</c>, <c>:: object</c>,
        /// and a definition that declares no model at all — leaves here needing the call site, and
        /// <see cref="TryTypeCallSiteBody"/> supplies it.
        /// <para>Which of the three it was still matters at the call site, so the caller passes the definition on
        /// rather than the answer: <c>:: dynamic</c> sends the engine's model accessor down its dynamic exit and
        /// the other two do not.</para>
        /// </summary>
        private BodyContext DefinitionBodyContext(DefinitionItem def, out string reason)
        {
            reason = null;
            var modelTypeName = def.ModelType;
            if (DeclaresDynamicModel(def))
                return new BodyContext(null, null, true);

            var sym = _resolver.ResolveModelType(modelTypeName, _usings);
            if (sym == null)
            {
                reason = "unresolved definition model type '" + modelTypeName + "'";
                return default;
            }

            if (!CanWriteTypeName(sym, def.Position, out reason))
                return default;

            // The engine's own predicate for "compile this body against the value the call site passes" is
            // `acceptType == typeof(object)`, and `:: object` satisfies it exactly as `:: dynamic` does. Typing the
            // body `object` instead made the emitter's model a supertype of the engine's at every call site: an
            // `@for(this)` or `@list(this)` in the body was refused against a model the engine had typed `int` or
            // `string`, and in slot mode an `@out(this)` looked like the identity `object → object` where the
            // engine saw the box it refuses.
            if (sym.SpecialType == SpecialType.System_Object)
                return new BodyContext(null, null, true);

            var fq = SymbolTypeResolver.FullyQualified(sym);
            return new BodyContext("(" + fq + ")", sym, false);
        }

        /// <summary>Whether the definition declares <c>dynamic</c>, as opposed to the <c>object</c> the engine also
        /// resolves it to. The two share a body model — whatever the call site passes — and differ in how the
        /// engine's model accessor reads a member path at that call site.</summary>
        private static bool DeclaresDynamicModel(DefinitionItem def) =>
            string.Equals(def.ModelType, "dynamic", System.StringComparison.Ordinal);

        private static bool DefinitionHasProps(DefinitionItem def)
            => AnyLayer(def, d => d.PropDeclarations != null && d.PropDeclarations.Count != 0);

        /// <summary>The one base-chain walk over definition layers (outermost-first). Avoids five near-identical loops; two moved to <see cref="SlotRules"/>.</summary>
        private static bool AnyLayer(DefinitionItem def, System.Func<DefinitionItem, bool> predicate)
        {
            for (var d = def; d != null; d = d.BaseDefinition)
                if (predicate(d))
                    return true;
            return false;
        }

        internal sealed class PropSlotInfo
        {
            public string Name;
            public ITypeSymbol Type;
            public string TypeFq;
            public bool HasDefault;
            public object DefaultValue;   // decoded literal (pre-conversion CLR value)

            /// <summary>The default's own declared type, where the declaration named one. Metadata hands an enum
            /// constant over as its underlying primitive, so the value alone cannot say which of the two the
            /// runtime will box. Null for a template-declared prop, whose default is a parsed literal.</summary>
            public ITypeSymbol DefaultSourceType;
            public int Index;
        }

        internal sealed class PropLayoutInfo
        {
            public readonly List<PropSlotInfo> Slots = new List<PropSlotInfo>();
            public readonly Dictionary<string, PropSlotInfo> ByName =
                new Dictionary<string, PropSlotInfo>(System.StringComparer.Ordinal);
            public bool Failed;
            public int Count => Slots.Count;
        }

        private readonly Dictionary<string, PropLayoutInfo> _propLayouts =
            new Dictionary<string, PropLayoutInfo>(System.StringComparer.Ordinal);

        /// <summary>Reimplements <c>PropLayout.Resolve</c> over symbols: base-chain props outermost-first
        /// (base slots keep their index in every descendant), re-declarations reuse the base index and re-default.</summary>
        private PropLayoutInfo ResolvePropLayout(DefinitionItem def)
        {
            var key = def.Name + "@" + def.Position;
            if (_propLayouts.TryGetValue(key, out var cached))
                return cached;

            var layout = new PropLayoutInfo();
            var layers = new List<DefinitionItem>();
            for (var d = def; d != null; d = d.BaseDefinition)
                layers.Add(d);
            layers.Reverse();

            foreach (var layer in layers)
            {
                if (layer.PropDeclarations == null)
                    continue;
                foreach (var decl in layer.PropDeclarations)
                {
                    var sym = _resolver.ResolveModelType(decl.TypeName, _usings);
                    if (sym == null || !CanWriteTypeName(sym, layer.Position, out _))
                    {
                        layout.Failed = true;
                        continue;
                    }

                    if (layout.ByName.TryGetValue(decl.Name, out var existing))
                    {
                        existing.Type = sym;
                        existing.TypeFq = SymbolTypeResolver.FullyQualified(sym);
                        existing.HasDefault = decl.HasDefault;
                        existing.DefaultValue = decl.DefaultValue;
                    }
                    else
                    {
                        var slot = new PropSlotInfo
                        {
                            Name = decl.Name, Type = sym, TypeFq = SymbolTypeResolver.FullyQualified(sym),
                            HasDefault = decl.HasDefault, DefaultValue = decl.DefaultValue, Index = layout.Slots.Count
                        };
                        layout.Slots.Add(slot);
                        layout.ByName.Add(decl.Name, slot);
                    }
                }
            }

            _propLayouts[key] = layout;
            return layout;
        }

        /// <summary>Formats a decoded prop literal as the exact boxed C# value the runtime prototype stores. Returns
        /// false (→ fall back) whenever reproducing the boxing is not trivially safe — the byte-for-byte contract
        /// forbids guessing a numeric widening or a lossy conversion.
        /// <para><paramref name="sourceType"/> is the default's own declared type where the declaration named one.
        /// It is not redundant with <paramref name="value"/>: metadata represents an enum constant as its underlying
        /// primitive, while the runtime reads the same attribute through reflection and boxes the enum. Only the
        /// declared type distinguishes the two, and the prototype has to hold whichever one the runtime holds.</para>
        /// </summary>
        private bool TryFormatPropValue(ITypeSymbol targetType, bool hasDefault, object value, out string expr,
            ITypeSymbol sourceType = null)
        {
            expr = null;
            if (targetType == null)
                return false;

            // One nullable probe for the whole file (was ConstructedFrom here, OriginalDefinition below).
            var underlying = TryGetNullableUnderlying(targetType, out var lifted) ? lifted : targetType;

            if (value == null)
            {
                // A null literal is legal only for a reference/nullable target (the runtime rejects it otherwise).
                if (NullDefaultLegal(targetType))
                {
                    expr = "null";
                    return true;
                }

                return false;
            }

            // An enum default reaches the layout only by identity, by lift, or boxed into `object` — no conversion
            // the runtime performs turns it into anything else — so the box it stores is always the enum itself.
            if (sourceType != null && sourceType.TypeKind == TypeKind.Enum)
            {
                var text = IntegralText(value);
                // The enum's name goes into the emitted source, so one this assembly cannot spell degrades here
                // rather than emitting a file the consumer's build rejects.
                if (text == null || _resolver.ClassifyModelType(sourceType, out _) != SymbolTypeResolver.NameFault.None)
                    return false;
                expr = "(" + SymbolTypeResolver.FullyQualified(sourceType) + ")(" + text + ")";
                return true;
            }

            // Reproduce only when literal CLR type matches target's underlying (no conversion) or target is object/string.
            var valueSpecial = SpecialTypeOf(value);
            var literal = LiteralFormatter.Format(value) ?? NarrowIntegralLiteral(valueSpecial, value);
            if (literal == null)
                return false;

            if (underlying.SpecialType == SpecialType.System_Object)
            {
                expr = literal;   // boxes as the literal's own type — matches ConvertValue's pass-through
                return true;
            }

            if (valueSpecial != SpecialType.None && valueSpecial == underlying.SpecialType)
            {
                expr = literal;
                return true;
            }

            // Runtime stores widened boxed value; cast to target keyword boxes to identical CLR type/value.
            if (valueSpecial != SpecialType.None &&
                IsImplicitNumericWidening(valueSpecial, underlying.SpecialType))
            {
                var keyword = NumericKeyword(underlying.SpecialType);
                if (keyword != null)
                {
                    expr = "(" + keyword + ")(" + literal + ")";
                    return true;
                }
            }

            return false;
        }

        /// <summary>The invariant decimal text of an integral box, for the inside of a cast. Null for anything an
        /// enum cannot be built on — which is what both callers are asking about, so <c>char</c> is not among the
        /// cases: C# admits no enum over it, and the narrow-literal caller has no arm for it either.</summary>
        private static string IntegralText(object value)
        {
            switch (value)
            {
                case sbyte v: return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case byte v: return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case short v: return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case ushort v: return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case int v: return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case uint v: return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case long v: return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case ulong v: return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default: return null;
            }
        }

        /// <summary>C# has no literal suffix for the four integral types narrower than <c>int</c>, so a value of one
        /// of them is written as a cast. <see cref="LiteralFormatter"/> stays out of it: its own contract is that a
        /// literal it writes round-trips through the expression parser, which has no such form to parse.</summary>
        private static string NarrowIntegralLiteral(SpecialType special, object value)
        {
            switch (special)
            {
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                    var text = IntegralText(value);
                    return text == null ? null : "(" + NumericKeyword(special) + ")(" + text + ")";
                default:
                    return null;
            }
        }

        /// <summary>C#'s implicit numeric conversions (spec §10.2.3). Adapter over shared <see cref="NumericTable.IsImplicit"/>.</summary>
        internal static bool IsImplicitNumericWidening(SpecialType from, SpecialType to) =>
            NumericTable.IsImplicit(SymbolFacts.ToNumericKind(from), SymbolFacts.ToNumericKind(to));

        private static string NumericKeyword(SpecialType special)
        {
            switch (special)
            {
                case SpecialType.System_SByte: return "sbyte";
                case SpecialType.System_Byte: return "byte";
                case SpecialType.System_Int16: return "short";
                case SpecialType.System_UInt16: return "ushort";
                case SpecialType.System_Int32: return "int";
                case SpecialType.System_UInt32: return "uint";
                case SpecialType.System_Int64: return "long";
                case SpecialType.System_UInt64: return "ulong";
                case SpecialType.System_Single: return "float";
                case SpecialType.System_Double: return "double";
                case SpecialType.System_Decimal: return "decimal";
                default: return null;
            }
        }

        private static SpecialType SpecialTypeOf(object value)
        {
            switch (value)
            {
                case string _: return SpecialType.System_String;
                case bool _: return SpecialType.System_Boolean;
                case char _: return SpecialType.System_Char;
                case sbyte _: return SpecialType.System_SByte;
                case byte _: return SpecialType.System_Byte;
                case short _: return SpecialType.System_Int16;
                case ushort _: return SpecialType.System_UInt16;
                case int _: return SpecialType.System_Int32;
                case long _: return SpecialType.System_Int64;
                case uint _: return SpecialType.System_UInt32;
                case ulong _: return SpecialType.System_UInt64;
                case double _: return SpecialType.System_Double;
                case float _: return SpecialType.System_Single;
                case decimal _: return SpecialType.System_Decimal;
                default: return SpecialType.None;
            }
        }

        /// <summary>True when this definition participates in a full override (<c>&lt;name:name&gt;</c>) — the same
        /// name re-declared over a base layer. The emitter refuses these (document-order layering, above);
        /// name-differing inheritance (<c>&lt;child:base&gt;</c>) is not an override and stays precompilable.</summary>
        private static bool DefinitionInvolvesOverride(DefinitionItem def)
            => AnyLayer(def, d => d.FullOverride ||
                                  (d.BaseDefinition != null &&
                                   string.Equals(d.BaseDefinition.Name, d.Name, System.StringComparison.Ordinal)));

        private int _propsCounter;

        /// <summary>Builds props carriage: frozen prototype + dynamic setters for non-constant arguments.
        /// <paramref name="propsFieldRef"/> is "null" for prop-less; <paramref name="dynamicSettersRef"/> is "null" for all-constant.
        /// Falls back for unreproducible values or untypeable dynamic arguments.</summary>
        private bool TryBuildPropsPrototype(PropLayoutInfo layout, CallParameter cp,
            BodyContext bctx, out string propsFieldRef, out string dynamicSettersRef, out string reason)
        {
            reason = null;
            propsFieldRef = "null";
            dynamicSettersRef = "null";
            if (layout.Count == 0)
            {
                if (cp.PropArguments != null && cp.PropArguments.Count != 0)
                {
                    reason = "named arguments on prop-less definition";
                    return false;
                }

                return true;
            }

            var values = new string[layout.Count];
            var bound = new bool[layout.Count];
            var setters = new List<string>();

            if (cp.PropArguments != null)
            {
                var seen = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (var arg in cp.PropArguments)
                {
                    if (!seen.Add(arg.Name)) { reason = "duplicate prop argument"; return false; }
                    if (!layout.ByName.TryGetValue(arg.Name, out var slot)) { reason = "unknown prop '" + arg.Name + "'"; return false; }

                    if (arg.Value is LiteralNode lit && lit.LiteralError == null)
                    {
                        if (!TryFormatPropValue(slot.Type, true, lit.Value, out var expr))
                        {
                            reason = "unreproducible prop value '" + arg.Name + "'";
                            return false;
                        }

                        values[slot.Index] = expr;
                        bound[slot.Index] = true;
                        continue;
                    }

                    // A non-constant argument becomes a dynamic setter evaluated against the caller view.
                    if (!TryBuildDynamicSetter(arg, slot, bctx, out var setterExpr, out reason))
                        return false;
                    setters.Add(setterExpr);
                    values[slot.Index] = "null";   // placeholder — the setter overwrites this slot per invocation
                    bound[slot.Index] = true;
                }
            }

            foreach (var slot in layout.Slots)
            {
                if (bound[slot.Index])
                    continue;
                if (!slot.HasDefault)
                {
                    reason = "missing required prop '" + slot.Name + "'";
                    return false;
                }

                if (!TryFormatPropValue(slot.Type, true, slot.DefaultValue, out var expr, slot.DefaultSourceType))
                {
                    reason = "unreproducible prop default '" + slot.Name + "'";
                    return false;
                }

                values[slot.Index] = expr;
            }

            var propsField = "Props" + _propsCounter++;
            _fieldDecls.Append("        private static readonly object[] ").Append(propsField).Append(" = new object[] { ")
                .Append(string.Join(", ", values)).Append(" };\n");
            propsFieldRef = propsField;

            if (setters.Count != 0)
            {
                var settersField = "DynSetters" + _dynSettersCounter++;
                _fieldDecls.Append("        private static readonly global::Heddle.Precompiled.PrecompiledPropSetter[] ")
                    .Append(settersField).Append(" = new global::Heddle.Precompiled.PrecompiledPropSetter[] { ")
                    .Append(string.Join(", ", setters)).Append(" };\n");
                dynamicSettersRef = settersField;
            }

            return true;
        }

        /// <summary>Emits an evaluator for non-constant prop arguments. Supports typed-caller member paths and native expressions.</summary>
        private bool TryBuildDynamicSetter(NamedArgument arg, PropSlotInfo slot, BodyContext bctx, out string setterExpr,
            out string reason)
        {
            reason = null;
            setterExpr = null;
            var callerModel = bctx.ModelSymbol;
            // A prop-rooted argument needs no caller model — it reads the scope's props — so the absence of one is
            // only fatal when there is no layout to read either.
            if (callerModel == null && bctx.Props == null)
            {
                reason = "dynamic arg without a typed caller model";
                return false;
            }

            if (slot.Type == null) { reason = "dynamic arg with unresolved prop type"; return false; }

            string conversionKeyword = null;
            if (arg.Value is PathNode pn)
            {
                if (pn.RootRef) { reason = "root-reference dynamic arg"; return false; }

                // Prop-first, like every other reader of a path's first segment — and like the writer three lines
                // below, which has had the layout since it was given one. Typed off the model instead, the check and
                // the emission disagreed about which value this argument even is: the writer emitted the caller's
                // prop while the check approved the shadowed member's type, so a string went into an int-declared
                // slot the engine refuses outright.
                ITypeSymbol argType;
                if (IsPropName(pn, bctx.Props))
                {
                    argType = PropRootType(pn, bctx.Props);
                    if (argType == null) { reason = "dynamic arg reads a prop this call site cannot type"; return false; }
                }
                else
                {
                    if (callerModel == null) { reason = "model-rooted dynamic arg without a typed caller model"; return false; }
                    var res = _resolver.ResolvePath(callerModel, pn.Segments);
                    if (res.Kind != SymbolTypeResolver.PathKind.Resolved) { reason = "dynamic arg path (" + res.Kind + ")"; return false; }
                    argType = res.ResultType;
                }

                if (!SymbolEqualityComparer.Default.Equals(argType, slot.Type) &&
                    slot.Type.SpecialType != SpecialType.System_Object)
                {
                    var from = argType?.SpecialType ?? SpecialType.None;
                    var to = UnderlyingSpecial(slot.Type);
                    if (from != SpecialType.None && IsImplicitNumericWidening(from, to))
                        conversionKeyword = NumericKeyword(to);
                    else
                    {
                        reason = "dynamic arg needs an unprovable conversion";
                        return false;
                    }
                }
            }
            else if (slot.Type.SpecialType != SpecialType.System_Object)
            {
                // A non-path native argument: only safe without a conversion, i.e. when the prop takes object.
                reason = "non-path dynamic arg needing a typed prop";
                return false;
            }

            var writer = new NativeExpressionWriter(_resolver, callerModel, "m", _exports, TypeFacts, AllocateHopLocal,
                bctx.Props);
            var body = writer.WriteRoot(arg.Value);
            DrainUnresolvable(writer);
            if (body == null) { reason = "unwritable dynamic arg"; return false; }
            RecordFunctionUses(writer);

            var inner = conversionKeyword != null ? "(" + conversionKeyword + ")(" + body + ")" : body;
            var evalName = "DynEval" + _dynEvalCounter++;
            _methodDecls.Append("        private static object ").Append(evalName)
                .Append("(in global::Heddle.Data.Scope scope)\n        {\n");
            if (writer.UsedModel)
                _methodDecls.Append("            var m = ").Append(bctx.ModelCast).Append("scope.ModelData;\n");
            _methodDecls.Append("            return (object)(").Append(inner).Append(");\n        }\n");

            setterExpr = "new global::Heddle.Precompiled.PrecompiledPropSetter(" + slot.Index + ", " + evalName + ")";
            return true;
        }

        private static SpecialType UnderlyingSpecial(ITypeSymbol type)
            => TryGetNullableUnderlying(type, out var underlying) ? underlying.SpecialType : type.SpecialType;

        private string AllocateDefinitionExtension(string bodyName, string callerBodyName, string propsFieldRef,
            string dynamicSettersRef, bool bodyNeedsLocals, bool callerContentNeedsLocals, bool slotMode,
            BlockPosition position)
        {
            var field = "E" + _extensionCounter++;
            var (line, col) = _map.Map(position.StartIndex);
            var callerArg = callerBodyName != null ? "new " + callerBodyName + "()" : "null";
            _fieldDecls.Append("        private static readonly global::Heddle.Core.AbstractExtension ").Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.BindDefinition(\n");
            _fieldDecls.Append("            body: new ").Append(bodyName).Append("(), callerContent: ").Append(callerArg)
                .Append(", props: ").Append(propsFieldRef)
                .Append(", dynamicSetters: ").Append(dynamicSettersRef)
                .Append(", global::Heddle.Data.RenderType.Raw");
            // Per-carrier overload gated on schema (PrecompiledSchema.EmitsPerCarrierLocals).
            if (PrecompiledSchema.EmitsPerCarrierLocals)
                _fieldDecls.Append(", bodyNeedsLocals: ").Append(bodyNeedsLocals ? "true" : "false")
                    .Append(", callerContentNeedsLocals: ").Append(callerContentNeedsLocals ? "true" : "false");
            else
                _fieldDecls.Append(", needsLocals: ")
                    .Append(bodyNeedsLocals || callerContentNeedsLocals ? "true" : "false");
            _fieldDecls
                .Append(", slotMode: ").Append(slotMode ? "true" : "false")
                .Append(", maxRecursionCount: ").Append(_config.MaxRecursionCount)
                .Append(", line: ").Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            return field;
        }

        /// <summary>Caller-content typing for slot-mode definitions: ModelData = slot value (declared slot type).
        /// Dynamic slot types and unresolvable types degrade the template.</summary>
        private BodyContext SlotBodyContext(DefinitionItem def, out string reason)
        {
            reason = null;
            // The shared base-chain walk (SlotRules), not this file's second copy of it.
            var slotName = SlotRules.SlotTypeName(def);
            if (slotName == null) { reason = "slot definition without slot type"; return default; }
            var sym = _resolver.ResolveModelType(slotName, _usings);
            if (sym == null) { reason = "unresolved slot type '" + slotName + "'"; return default; }
            if (!CanWriteTypeName(sym, def.Position, out reason)) return default;
            var fq = SymbolTypeResolver.FullyQualified(sym);
            return new BodyContext("(" + fq + ")", sym, false);
        }

        private string AllocateOutExtension(bool slotMode, BlockPosition position)
        {
            var field = "E" + _extensionCounter++;
            var (line, col) = _map.Map(position.StartIndex);
            _fieldDecls.Append("        private static readonly global::Heddle.Extensions.OutExtension ").Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.BindOut(\n");
            _fieldDecls.Append("            new global::Heddle.Extensions.OutExtension(), slotMode: ")
                .Append(slotMode ? "true" : "false")
                .Append(", line: ").Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            RecordExtensionBinding("out", "Heddle.Extensions.OutExtension");
            return field;
        }

        private static CallNode BuildFunctionCallNode(string name, CallParameter cp, BlockPosition position)
        {
            var args = new List<ExprNode>();
            if (cp.NativeExpression != null)
            {
                args.Add(cp.NativeExpression);
            }
            else if (cp.ModelParameter != null && cp.ModelParameter.Length != 0 &&
                     !string.IsNullOrEmpty(cp.ModelParameter[0]))
            {
                args.Add(new PathNode(cp.RootReference, cp.ModelParameter, null, position));
            }

            return new CallNode(name, args, position);
        }

        private Call MakeCall(string field, string paramExpr, bool usesModel, BlockPosition position,
            bool usesCSharpModel = false)
        {
            var (sl, sc) = _map.Map(position.StartIndex);
            var (el, ec) = _map.Map(position.StartIndex + position.Length);
            return new Call
            {
                ExtensionField = field, ParamExpr = paramExpr, UsesModelLocal = usesModel,
                UsesCSharpModel = usesCSharpModel,
                SpanStartLine = sl, SpanStartCol = sc, SpanEndLine = el, SpanEndCol = ec
            };
        }

        private bool BuildParamExpr(CallParameter cp, BodyContext bctx, out string paramExpr, out bool usesModel,
            out string reason, BlockPosition callPosition)
            => BuildParamExpr(cp, bctx, out paramExpr, out usesModel, out _, out reason, callPosition);

        /// <param name="callPosition">The call this parameter belongs to — where the runtime positions a
        /// prop-shadowing warning raised off the same read.</param>
        private bool BuildParamExpr(CallParameter cp, BodyContext bctx, out string paramExpr, out bool usesModel,
            out bool usesCSharpModel, out string reason, BlockPosition callPosition)
        {
            reason = null;
            usesModel = false;
            usesCSharpModel = false;
            paramExpr = null;

            if (cp.IsModelTypeParameter)
            {
                var segments = cp.ModelParameter;
                bool empty = segments == null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]);
                if (empty)
                {
                    paramExpr = "scope.ModelData";
                    return true;
                }

                // A body prop read wins over the model on the first segment (never for :: root refs) —
                // resolved prop-first, syntactically, so both backends agree by rule.
                if (!cp.RootReference && bctx.Props != null && bctx.Props.ByName.TryGetValue(segments[0], out var slot))
                {
                    WarnOnPropShadowsMember(bctx.ModelSymbol, slot.Name, callPosition);
                    var propRead = "global::Heddle.Precompiled.PrecompiledRuntime.Prop(in scope, " + slot.Index + ")";
                    if (segments.Length == 1)
                    {
                        paramExpr = propRead;
                        return true;   // reads scope.PropsData, not the model local
                    }

                    // Multi-hop: cast prop to slot type and walk hop-by-hop (same tier as single-hop model path).
                    if (slot.Type == null || slot.Type.TypeKind == TypeKind.Dynamic)
                    {
                        reason = "multi-hop prop read on dynamic/unresolved type";
                        return false;
                    }

                    var rest = new string[segments.Length - 1];
                    System.Array.Copy(segments, 1, rest, 0, rest.Length);
                    var res = _resolver.ResolvePath(slot.Type, rest);
                    if (res.Kind != SymbolTypeResolver.PathKind.Resolved)
                    {
                        reason = "prop multi-hop (" + res.Kind + ")";
                        return false;
                    }

                    if (SymbolTypeResolver.EndsOnRefStruct(res))
                    {
                        reason = "prop multi-hop ends on a ref struct";
                        return false;
                    }

                    var root = "((" + slot.TypeFq + ")" + propRead + ")";
                    paramExpr = "(object)(" + MemberPathWriter.Write(root, MapHops(res), AllocateHopLocal) + ")";
                    return true;
                }

                if (bctx.IsDynamic)
                {
                    // Dynamic tier: leading conditional guards first hop, dynamic ?. carries rest (DynamicParameter emits this).
                    if (cp.RootReference) { reason = "dynamic root-reference member path"; return false; }
                    paramExpr = WriteDynamicPath("m", segments);
                    usesModel = true;
                    return true;
                }

                if (cp.RootReference) { reason = "root-reference member path"; return false; }
                if (bctx.ModelSymbol == null) { reason = "unresolved model type"; return false; }

                var resolution = _resolver.ResolvePath(bctx.ModelSymbol, segments);
                if (resolution.Kind != SymbolTypeResolver.PathKind.Resolved)
                {
                    // HED7008: property-not-found on typed model (same as runtime HED0001).
                    if (resolution.Kind == SymbolTypeResolver.PathKind.Failed ||
                        resolution.Kind == SymbolTypeResolver.PathKind.Inaccessible)
                        RecordMemberFailure(bctx.ModelSymbol, segments, resolution);
                    reason = "member path (" + resolution.Kind + ")";
                    return false;
                }

                if (SymbolTypeResolver.EndsOnRefStruct(resolution))
                {
                    reason = "member path ends on a ref struct";
                    return false;
                }

                paramExpr = "(object)(" + MemberPathWriter.Write("m", MapHops(resolution), AllocateHopLocal) + ")";
                usesModel = true;
                return true;
            }

            if (cp.NativeExpression != null)
            {
                if (cp.NativeExpression is ThisNode)
                {
                    paramExpr = "scope.ModelData";
                    return true;
                }

                if (_config.ExpressionMode == Heddle.Data.ExpressionMode.MemberPathsOnly)
                {
                    reason = "native expression under MemberPathsOnly";
                    return false;
                }

                // No pre-check for a typed model. The engine's own compiler tries the active prop layout BEFORE it
                // asks whether the scope has a static type, so an expression rooted at a prop needs no model at all
                // — and refusing every expression on the untyped tier dropped whole templates the engine renders.
                // The writer is the gate instead: given no model type it refuses any path that reads one, which is
                // the same answer by the same rule, and it never claims to use a model local it has not been given.
                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, "m",
                    _exports, TypeFacts, AllocateHopLocal, bctx.Props);
                var expr = writer.WriteRoot(cp.NativeExpression);
                DrainUnresolvable(writer);
                if (expr == null)
                {
                    reason = "unsupported native expression";
                    return false;
                }

                RecordFunctionUses(writer);
                paramExpr = "(object)(" + expr + ")";
                usesModel = writer.UsedModel;
                return true;
            }

            if (!string.IsNullOrEmpty(cp.CSharpExpression))
                return BuildCSharpExpr(cp.CSharpExpression, bctx, out paramExpr, out usesCSharpModel, out reason);

            // A single-item chain (@card((Cols)), @list(upper(Name))) reduces to its producer's expression — but not
            // to its producer's VALUE. The carrier the runtime wraps it in renders what it is given, so the value
            // that reaches the consuming extension is the carrier's text; that is why the engine types every chain
            // call-parameter `string` (the chain's render type is the last item's InitStart return, and the default
            // is typeof(string)). Flattened to the raw producer expression it was not byte-identical at all — only
            // invisible while the consumer printed it. See CarrierValue for what the two tiers disagreed on.
            if (cp.ChainParameter != null && cp.ChainParameter.Count == 1)
            {
                if (!BuildChainItemExpr(cp.ChainParameter[0], bctx, out var chainExpr, out usesModel,
                        out usesCSharpModel, out reason))
                    return false;
                paramExpr = "global::Heddle.Precompiled.PrecompiledRuntime.CarrierValue(" + chainExpr + ")";
                return true;
            }

            reason = "C#/chain parameter";
            return false;
        }

        /// <summary>FullCSharp tier: C# expression pasted verbatim with local <c>model</c> bound (same name as runtime).
        /// Only under FullCSharp mode; requires typed model. Chained/root references degrade (types not reproducible).</summary>
        private bool BuildCSharpExpr(string csharp, BodyContext bctx, out string paramExpr, out bool usesCSharpModel,
            out string reason)
        {
            reason = null;
            paramExpr = null;
            usesCSharpModel = false;

            if (_config.ExpressionMode != Heddle.Data.ExpressionMode.FullCSharp)
            {
                // Runtime rejects C# unless FullCSharp mode; dynamic path surfaces engine's gate.
                reason = "embedded C# outside FullCSharp mode";
                return false;
            }

            if (bctx.IsDynamic || bctx.ModelSymbol == null)
            {
                reason = "embedded C# without a typed model";
                return false;
            }

            // References to the chained/root parameters need their runtime static types, which the emitter cannot
            // reproduce here — fall back rather than paste an untyped/ill-typed reference. Which identifiers are
            // those two parameters is the binder's answer, not a word search's: a lambda parameter of the same name
            // shadows them, a member can be called either, and a string literal is not an identifier at all.
            if (_csharpTyper.ReferencesChainedOrRoot(csharp, bctx.ModelSymbol, _usings))
            {
                reason = "embedded C# references chained/root";
                return false;
            }

            // Every collected @using becomes a `using` directive of the code the ENGINE compiles for this
            // expression, so a body naming no namespace makes the engine refuse the whole template — and a body that
            // is not a name at all stops its compilation unit from parsing. Neither is a refusal this tier can
            // position against the .heddle file, so the template goes back to the tier whose diagnostic is the
            // contract instead of pre-compiling something the engine would never run.
            foreach (var ns in _usings)
            {
                if (_resolver.UsingDirectiveCompiles(ns))
                    continue;
                reason = "embedded C# under a @using naming no namespace ('" + ns + "')";
                return false;
            }

            // Does the expression compile at all? The engine asks Roslyn exactly this and refuses the template on
            // any error, and nothing here asked: a misspelt member, an unbalanced expression, a wrong argument
            // count, a `Where` with no `@using System.Linq` and an `[Obsolete(error: true)]` reference were all
            // pasted straight into the generated file, where they became the consumer's build errors against a
            // `.heddle` file with no Heddle diagnostic on them.
            if (!_csharpTyper.Compiles(csharp, bctx.ModelSymbol, _usings))
            {
                reason = "embedded C# the engine's compiler rejects";
                return false;
            }

            // Wrapped, for the same reason the native writer wraps: the engine compiles this very text into an
            // assembly of its own with overflow checking off, unconditionally, while this copy is compiled by the
            // consumer under whatever <CheckForOverflowUnderflow> that project happens to set. Pasted bare, the same
            // template rendered a wrapped number in one project and threw OverflowException in the next, decided by
            // an MSBuild property the template knows nothing about. The wrapper also settles the constant case,
            // which no compilation option can: C# checks a constant expression whatever the compilation says, so
            // only a syntactic unchecked makes the two tiers fold it the same way.
            paramExpr = "(object)(unchecked(" + csharp + "))";
            usesCSharpModel = true;
            _wroteEmbeddedCSharp = true;
            return true;
        }

        /// <summary>Whether any embedded C# text was pasted into this file, which is what decides whether the
        /// model's namespaces are worth importing into it.</summary>
        private bool _wroteEmbeddedCSharp;

        private bool BuildChainItemExpr(OutputItem inner, BodyContext bctx, out string paramExpr, out bool usesModel,
            out bool usesCSharpModel, out string reason)
        {
            reason = null;
            paramExpr = null;
            usesModel = false;
            usesCSharpModel = false;

            if (!string.IsNullOrEmpty(inner.ParameterTemplate))
            {
                reason = "bodied chain item";
                return false;
            }

            var name = inner.ExtensionName;
            if (name.Length == 0)
                return BuildParamExpr(inner.CallParameter, bctx, out paramExpr, out usesModel, out usesCSharpModel,
                    out reason, inner.Position);

            // Use same precedence as top-level dispatch (HeddleCompiler.CompileItem).
            var innerTarget = CallTargetRules.ResolveCallTarget(name, inner.CallParameter, null,
                _parse.DefenitionExists,
                n => _extensionBinder.TryResolve(n, out _),
                n => NativeExpressionWriter.IsDefaultFunction(n) || _exports.TryGet(n, out _));

            if (innerTarget == CallTargetKind.Function)
            {
                var callNode = BuildFunctionCallNode(name, inner.CallParameter, inner.Position);
                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, "m", _exports, TypeFacts, AllocateHopLocal,
                    bctx.Props);
                var expr = writer.WriteRoot(callNode);
                DrainUnresolvable(writer);
                if (expr == null)
                {
                    reason = "unsupported function '" + name + "'";
                    return false;
                }

                RecordFunctionUses(writer);
                paramExpr = "(object)(" + expr + ")";
                usesModel = writer.UsedModel;
                return true;
            }

            // Delegate-only remainder — record so template degrades to HED7014 fallback marker.
            // Function-shaped only: bodiless and not nested chain.
            if (innerTarget == CallTargetKind.Unknown &&
                CallTargetRules.IsFunctionCompatibleShape(inner.CallParameter))
            {
                _unresolvableFunctions.Add((name, inner.Position));
            }

            reason = "chain item extension '" + name + "'";
            return false;
        }

        /// <summary>
        /// Dynamic-tier member access, one <c>PrecompiledRuntime.DynamicMember</c> call per segment. Avoids the
        /// assembly-context divergence of an inline <c>(dynamic)</c> cast — and, because each segment is its own
        /// call, it hops per segment the way the engine does rather than letting one <c>?.</c> abandon the rest of
        /// the chain.
        /// <para>There was a pre-routing branch here for schemas below <c>DynamicMemberRoutingSchemaVersion</c>. It
        /// could not run: both sides of that comparison are constants this same assembly reads, so the branch was
        /// statically decided, and it wrote exactly the <c>?.</c> chain whose short-circuit the typed writer had to
        /// be fixed for.</para>
        /// </summary>
        private static string WriteDynamicPath(string local, string[] segments)
        {
            var expr = local;
            foreach (var segment in segments)
            {
                expr = "global::Heddle.Precompiled.PrecompiledRuntime.DynamicMember(" + expr + ", " +
                       CSharpEscape.StringLiteral(segment) + ")";
            }

            return expr;
        }

        internal static IReadOnlyList<MemberPathWriter.HopEmit> MapHops(SymbolTypeResolver.PathResolution resolution)
        {
            var hops = new List<MemberPathWriter.HopEmit>(resolution.Hops.Count);
            foreach (var hop in resolution.Hops)
            {
                hops.Add(new MemberPathWriter.HopEmit(
                    hop.Receiver.IsValueType,
                    SymbolTypeResolver.IsNonNullableValueType(hop.Property),
                    SymbolTypeResolver.FullyQualified(hop.Property),
                    hop.Name,
                    !hop.Property.IsRefLikeType,
                    SymbolTypeResolver.FullyQualified(hop.Receiver)));
            }

            return hops;
        }

        private string AllocateEmptyExtension(BlockPosition position)
        {
            var field = "E" + _extensionCounter++;
            // Running profile (post-@profile-flip) decides encoding via shared OutputProfileRules rule.
            OutputProfileRules.ResolveUnnamedCarrier(
                _profileHtml ? Heddle.Data.OutputProfile.Html : Heddle.Data.OutputProfile.Text,
                hasBody: false, out var carrierKind, out var carrierRenderType);
            bool html = carrierKind == UnnamedCarrierKind.EmptyHtml;
            var typeName = html ? "global::Heddle.Extensions.EmptyHtmlExtension"
                                : "global::Heddle.Extensions.EmptyExtension";
            var renderType = "global::Heddle.Data.RenderType." + carrierRenderType;
            var (line, col) = _map.Map(position.StartIndex);
            _fieldDecls.Append("        private static readonly ").Append(typeName).Append(' ').Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.Bind(\n");
            _fieldDecls.Append("            new ").Append(typeName)
                .Append("(), body: null, ").Append(renderType)
                .Append(", needsLocals: false, line: ").Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            // Manifest binding name must resolve to bound type: Html redirects to "html" (EmptyHtmlExtension), Text to "" (EmptyExtension).
            RecordExtensionBinding(OutputProfileRules.CarrierRegistryName(carrierKind),
                html ? "Heddle.Extensions.EmptyHtmlExtension" : "Heddle.Extensions.EmptyExtension");
            return field;
        }

        private string AllocateBodyExtension(string callName, string fqn, string typeName, string bodyName,
            bool needsLocals, BlockPosition position, string assembly = "Heddle")
        {
            var field = "E" + _extensionCounter++;
            var (line, col) = _map.Map(position.StartIndex);
            var bodyArg = bodyName != null ? "new " + bodyName + "()" : "null";
            _fieldDecls.Append("        private static readonly ").Append(fqn).Append(' ').Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.Bind(\n");
            _fieldDecls.Append("            new ").Append(fqn).Append("(), body: ").Append(bodyArg)
                .Append(", global::Heddle.Data.RenderType.Raw, needsLocals: ")
                .Append(needsLocals ? "true" : "false")
                .Append(", line: ").Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            RecordExtensionBinding(callName, typeName, assembly);
            return field;
        }

        /// <summary>The build tier's half of the prop-layout fingerprint — the shared format over
        /// this side's slots and its <c>ITypeFacts</c>.</summary>
        private string FingerprintOf(PropLayoutInfo layout)
        {
            if (layout == null || layout.Slots.Count == 0)
                return null;

            var slots = new List<PropSlot<ITypeSymbol>>(layout.Slots.Count);
            foreach (var slot in layout.Slots)
                slots.Add(new PropSlot<ITypeSymbol> { Name = slot.Name, Type = slot.Type, Index = slot.Index });
            return PropLayoutCore.Fingerprint(slots, TypeFacts);
        }

        /// <summary>Records manifest <c>ExtensionBindings</c> row (at most once per name/type).</summary>
        private void RecordExtensionBinding(string name, string type, string assembly = "Heddle",
            string propLayoutFingerprint = null)
        {
            if (!_extensionBindings.Any(b => b.Name == name && b.Type == type))
                _extensionBindings.Add((name, type, assembly, propLayoutFingerprint));
        }

        private void RecordFunctionBinding(string name, string target, int overloadCount)
        {
            if (!_functionBindings.Any(b => b.Name == name && b.Target == target))
                _functionBindings.Add((name, target, overloadCount));
        }

        /// <summary>Drains writer's recorded remainders into template-level channels (unresolvable functions, member failures, unbindable calls).</summary>
        private void DrainUnresolvable(NativeExpressionWriter writer)
        {
            foreach (var propRead in writer.PropReads)
                WarnOnPropShadowsMember(writer.ModelSymbol, propRead.Name, propRead.Position);
            foreach (var fn in writer.UnresolvableFunctions)
                _unresolvableFunctions.Add(fn);
            foreach (var mf in writer.MemberFailures)
            {
                var seenKey = mf.Path + "@" + mf.Position.StartIndex;
                if (_seenMemberFailures.Add(seenKey))
                    _diagnostics.Add(MemberDiagnostic(mf,
                        IsInaccessibleRatherThanMissing(mf.Receiver, mf.Member, mf.Inaccessible)));
            }

            foreach (var call in writer.UnnameableFunctionCalls)
            {
                var seenKey = call.Display + "@" + call.Position.StartIndex;
                if (_seenInaccessibleTypes.Add(seenKey))
                    _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.InaccessibleModelSymbol,
                        call.Position, call.Display));
            }

            // HED7025: defensive guard against duplicate reports (failure mode is asymmetric: noise vs discovery in build log).
            foreach (var call in writer.UnbindableFunctionCalls)
            {
                var seenKey = call.Name + "@" + call.Position.StartIndex;
                if (_seenUnbindableCalls.Add(seenKey))
                    _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.FunctionCallNotBindable,
                        call.Position, call.Detail, call.RuntimeDiagnosticId));
            }
        }

        private readonly HashSet<string> _seenMemberFailures = new HashSet<string>(System.StringComparer.Ordinal);

        private readonly HashSet<string> _seenUnbindableCalls = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>True when member access off <paramref name="type"/> is effectively untyped — <c>System.Object</c>
        /// or <c>dynamic</c> — where the runtime resolves members dynamically and a "missing member" is never a typo,
        /// so HED7008 must not fire.</summary>
        internal static bool IsUntypedReceiver(ITypeSymbol type) =>
            type == null || type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic;

        /// <summary>Records a HED7008 for a genuine model-parameter member-path failure (line-1398 path), positioned by
        /// locating the dotted path in the source document (raw model segments carry no <see cref="BlockPosition"/>).
        /// When the path cannot be located, no diagnostic is emitted — a missing squiggle, never a false error.</summary>
        private void RecordMemberFailure(ITypeSymbol modelSymbol, string[] segments,
            SymbolTypeResolver.PathResolution resolution)
        {
            // A member that is there and readable, whose type no generated code in any assembly could hold a value
            // of. The template degrades, but there is no author-facing fault to name: HED7030 says "this assembly
            // may not mention it", which invites an edit that would not help, and HED7008 would say the member is
            // missing when it is not.
            if (resolution.Fault == SymbolTypeResolver.NameFault.Unusable)
                return;

            var idx = resolution.DynamicIndex;
            var receiver = resolution.Hops.Count == 0
                ? modelSymbol
                : resolution.Hops[resolution.Hops.Count - 1].Property;
            // An object-typed (untyped) receiver resolves member access dynamically at runtime — never a typo. Skip.
            if (IsUntypedReceiver(receiver))
                return;
            var display = MemberPathWriter.Display(segments);
            var at = _originalDocument.IndexOf(display, System.StringComparison.Ordinal);
            if (at < 0)
                return;
            var position = new BlockPosition(at, display.Length);
            var member = idx >= 0 && idx < segments.Length ? segments[idx] : segments[segments.Length - 1];
            var seenKey = display + "@" + at;
            if (_seenMemberFailures.Add(seenKey))
                _diagnostics.Add(MemberDiagnostic(
                    new SymbolMemberResolver.MemberFailure(receiver, member, display, position),
                    IsInaccessibleRatherThanMissing(receiver, member,
                        resolution.Kind == SymbolTypeResolver.PathKind.Inaccessible)));
        }

        /// <summary>Whether the failing hop is one this compilation is merely not shown, rather than one that is not
        /// there. Asked at the one point a member failure becomes a diagnostic: the probe behind it builds a second
        /// compilation, and every other caller of the member walk throws the answer away.</summary>
        private bool IsInaccessibleRatherThanMissing(ITypeSymbol receiver, string member, bool alreadyKnown) =>
            alreadyKnown || _resolver.HiddenByAccessibility(receiver, member);

        /// <summary>HED7008 for a member that is not there, HED7030 for one this compilation merely cannot see. The
        /// second is not a template fault, so it degrades the template instead of failing the build.</summary>
        private static EmitDiagnostic MemberDiagnostic(SymbolMemberResolver.MemberFailure failure,
            bool inaccessible) =>
            inaccessible
                ? new EmitDiagnostic(GeneratorDiagnostics.InaccessibleModelSymbol, failure.Position,
                    failure.ReceiverType + "." + failure.Member)
                : new EmitDiagnostic(GeneratorDiagnostics.UnresolvableMember, failure.Position,
                    failure.ReceiverType, failure.Member, failure.Path);

        /// <summary>
        /// One HED7030 per (subject, position). Two key shapes share this set: a function call's signature display,
        /// and a type's fully-qualified name. They cannot collide for the reason that matters — a subject of one
        /// shape and a subject of the other at the same position would have to be the same string, and a method
        /// display always carries its parameter list while a type name does not. <b>A tuple type is the exception
        /// worth knowing about:</b> its fully-qualified form contains parentheses too, so the separation rests on
        /// the two never being reported at one position rather than on the spelling alone. Both arms report about
        /// the same construct, so a merge would suppress a duplicate rather than the wrong diagnostic.
        /// </summary>
        private readonly HashSet<string> _seenInaccessibleTypes = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>
        /// Whether generated code may be written against <paramref name="type"/> at all — asked of every type the
        /// emitter spells into a cast or a parameter: the model, a definition's model, a slot type, a prop's type.
        /// The whole rule lives in <see cref="SymbolTypeResolver.ClassifyModelType"/>, which is also what types a
        /// member hop; this adds only the diagnostic.
        /// <para>Accessibility and <c>[Obsolete(…, error: true)]</c> are the two the author can act on — an
        /// <c>internal</c> type in a referenced assembly resolves here and its fully-qualified name goes straight
        /// into a cast the build then rejects with CS0122, and reflection ignores <c>[Obsolete]</c> outright, so the
        /// engine renders what the consumer's compiler refuses — and they get HED7030 and a <c>.heddle</c> position
        /// saying so. Every other refusal is a property of the type itself with no remedy but a different model, so
        /// it degrades silently to the tier that can serve it.</para>
        /// </summary>
        private bool CanWriteTypeName(ITypeSymbol type, BlockPosition position, out string reason) =>
            ReportUnnameable(_resolver.ClassifyModelType(type, out reason), type, position);

        /// <summary>
        /// The same question for a type the emitter spells but never holds a <b>model</b> value of: a bound host
        /// extension, whose type is written twice at every call site — the declared type of the field the binding
        /// lives in, and the <c>new</c> that fills it.
        /// <para>The engine's discovery asks a type only whether it implements the extension interface and carries
        /// the name attribute, and <c>Activator.CreateInstance</c> instantiates a non-public type with a public
        /// constructor and ignores <c>[Obsolete]</c> — so the engine registers the extension and renders, while the
        /// consumer's build stops on a <c>.g.cs</c> they cannot edit. The ref-struct restriction is the one that
        /// does not apply here: nothing boxes the extension, and no extension could be a ref struct anyway.</para>
        /// </summary>
        private bool CanWriteExtensionTypeName(ITypeSymbol type, BlockPosition position, out string reason) =>
            ReportUnnameable(_resolver.ClassifyTypeName(type, out reason), type, position);

        private bool ReportUnnameable(SymbolTypeResolver.NameFault fault, ITypeSymbol type, BlockPosition position)
        {
            if (fault == SymbolTypeResolver.NameFault.None)
                return true;

            if (fault == SymbolTypeResolver.NameFault.Unnameable)
            {
                var fq = SymbolTypeResolver.FullyQualified(type);
                if (_seenInaccessibleTypes.Add(fq + "@" + position.StartIndex))
                    _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.InaccessibleModelSymbol, position, fq));
            }

            return false;
        }

        /// <summary>True when <paramref name="text"/> is a plain (possibly dotted, possibly <c>?</c>-suffixed) type
        /// name. Open generics, arrays, tuples and whitespace forms are excluded so an unresolved exotic model
        /// degrades to the dynamic path rather than a false HED7007.
        /// <para><c>?</c> is admitted although no tier's grammar has a nullable suffix, and precisely because of
        /// that: <c>int?</c> is a name an author wrote and got wrong, not an exotic form the emitter merely does not
        /// model, so it belongs at the HED7007 gate with a message rather than in the silent degrade.</para>
        /// </summary>
        private static bool IsPlainTypeName(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '?')
                    continue;
                return false;
            }

            return true;
        }

        private IReadOnlyList<(string Name, BlockPosition Position)> _dedupedUnresolvable;

        /// <summary>The unresolvable-function list, first-position-wins per name. Memoized.</summary>
        private IReadOnlyList<(string Name, BlockPosition Position)> DedupeUnresolvable()
        {
            if (_dedupedUnresolvable != null)
                return _dedupedUnresolvable;
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var result = new List<(string, BlockPosition)>();
            foreach (var fn in _unresolvableFunctions)
                if (seen.Add(fn.Name))
                    result.Add(fn);
            return _dedupedUnresolvable = result;
        }

        /// <summary>Records manifest <c>FunctionBindings</c> rows for bound default/exported functions.</summary>
        private void RecordFunctionUses(NativeExpressionWriter writer)
        {
            foreach (var fn in writer.UsedDefaultFunctions)
                RecordFunctionBinding(fn, DefaultFunctionTable.ShimTargetTypeName,
                    NativeExpressionWriter.DefaultOverloadCount(fn));
            foreach (var ex in writer.UsedExports)
                RecordFunctionBinding(ex.Name, ex.Aqn, ex.OverloadCount);
        }

        private string RenderFile(string modelType, BodyClass root)
        {
            var w = new CodeWriter();
            w.Raw("// <auto-generated/>");
            // No relativity comment here: the #line form is marked by the manifest row's `linePathForm`,
            // which a symbolizer/IDE/LSP can actually read, and duplicating it as prose would be two carriers
            // for one fact.
            w.Raw("#pragma warning disable");
            // A collected @using is what makes a pasted-in embedded expression bind, so the directive is written out
            // — but only where the name resolves here. To the engine a body naming nothing is never consulted at all
            // unless an embedded expression sends it to the C# compiler, and a document with none renders whatever
            // the body says; copied out as a directive the same body is CS0246, or, when it is not a name at all,
            // stops the generated file from parsing. Omitting it costs nothing: everything else generated code writes
            // is fully qualified, and a template that did need the namespace is turned down where the expression is
            // built, because the engine's own compile of the same text is what fails.
            foreach (var ns in _usings)
                if (_resolver.UsingDirectiveCompiles(ns))
                    w.Raw("using " + ns + ";");
            // The engine imports the model's own namespace — and each type argument's, when it is generic — into the
            // unit it compiles for an embedded expression. Pasted C# is the only thing here that reads a directive
            // at all, so these are written only where some expression was pasted, and only alongside it.
            if (_wroteEmbeddedCSharp && _modelSymbol != null)
            {
                foreach (var ns in Binding.CSharpExpressionTyper.ModelNamespaces(_modelSymbol))
                    if (!_usings.Contains(ns))
                        w.Raw("using " + ns + ";");
            }

            w.Line();
            w.Line("namespace " + _namespace);
            w.Line("{");
            w.Indent();

            w.Line("public static class " + _sanitizedName);
            w.Line("{");
            w.Indent();

            w.Line("/// <summary>Typed entry point — the recommended host API.</summary>");
            w.Line($"public static string Generate({modelType} model, object chained = null, object callerData = null)");
            w.Indent();
            w.Line("=> global::Heddle.Precompiled.PrecompiledRuntime.GenerateString(Root, model, chained, callerData);");
            w.Outdent();
            w.Line();
            w.Line("/// <summary>Renders into a TextWriter with no full-output materialization.</summary>");
            w.Line($"public static void Generate({modelType} model, global::System.IO.TextWriter writer, object chained = null, object callerData = null)");
            w.Indent();
            w.Line("=> global::Heddle.Precompiled.PrecompiledRuntime.GenerateToWriter(Root, model, chained, callerData, writer);");
            w.Outdent();
            w.Line();
            w.Line("/// <summary>Renders UTF-8 into an IBufferWriter&lt;byte&gt; with no full-output materialization.</summary>");
            w.Line($"public static void Generate({modelType} model, global::System.Buffers.IBufferWriter<byte> writer, object chained = null, object callerData = null)");
            w.Indent();
            w.Line("=> global::Heddle.Precompiled.PrecompiledRuntime.GenerateUtf8(Root, model, chained, callerData, writer);");
            w.Outdent();
            w.Line();
            var rootExpr = root.HostsParticipant
                ? "global::Heddle.Precompiled.PrecompiledRuntime.WithLocalsFrame(new " + root.Name + "())"
                : "new " + root.Name + "()";
            w.Line($"internal static readonly global::Heddle.Runtime.IProcessStrategy Root = {rootExpr};");
            w.Line($"internal static readonly global::System.Type __ModelType = typeof({modelType});");
            w.Line();

            for (int i = 0; i < _pieces.Count; i++)
                PieceWriter.EmitPiece(w, i, _pieces[i], _config.EmitUtf8Pieces, utf8Supported: true);
            w.Line();

            foreach (var line in _fieldDecls.ToString().Split('\n'))
                if (line.Length != 0)
                    w.Raw(line);
            w.Line();

            if (_methodDecls.Length != 0)
            {
                foreach (var line in _methodDecls.ToString().Split('\n'))
                    if (line.Length != 0)
                        w.Raw(line);
                w.Line();
            }

            foreach (var body in _bodies)
            {
                EmitBodyClass(w, body);
                w.Line();
            }

            w.Outdent();
            w.Line("}");
            w.Outdent();
            w.Line("}");
            return w.ToString();
        }

        private void EmitBodyClass(CodeWriter w, BodyClass body)
        {
            w.Line($"private sealed class {body.Name} : global::Heddle.Runtime.IProcessStrategy");
            w.Line("{");
            w.Indent();

            w.Line("public void Render(in global::Heddle.Data.Scope scope)");
            w.Line("{");
            w.Indent();
            if (body.NeedsModelLocal)
                w.Line($"var m = {body.ModelCast}scope.ModelData;");
            if (body.NeedsCSharpModel)
                w.Line($"var {EmbeddedCSharpNames.Model} = {body.ModelCast}scope.ModelData;");
            foreach (var seg in body.Segments)
            {
                if (seg is Piece p)
                {
                    w.Raw("#line hidden");
                    // Route through WritePiece hook when u8 twin exists (zero-transcode byte branch on UTF-8 sink).
                    if (HasU8Twin(p.Index))
                        w.Line($"global::Heddle.Precompiled.PrecompiledRuntime.WritePiece(in scope, P{p.Index}, P{p.Index}U8);");
                    else
                        w.Line($"scope.Renderer.Render(P{p.Index});");
                }
                else if (seg is Partial pt)
                {
                    EmitLineSpanRaw(w, pt.SpanStartLine, pt.SpanStartCol, pt.SpanEndLine, pt.SpanEndCol);
                    w.Line($"scope.Renderer.Render(global::Heddle.Precompiled.PrecompiledRuntime.GenerateString({PartialResolveExpr(pt)}, {pt.ModelExpr}, scope.ChainedData, null));");
                }
                else
                {
                    var c = (Call) seg;
                    EmitLineSpan(w, c);
                    w.Line($"{c.ExtensionField}.RenderData(scope.Model({c.ParamExpr}));");
                }
            }
            w.Raw("#line default");
            w.Outdent();
            w.Line("}");
            w.Line();

            w.Line("public string Execute(in global::Heddle.Data.Scope scope)");
            w.Line("{");
            w.Indent();
            if (body.NeedsModelLocal)
                w.Line($"var m = {body.ModelCast}scope.ModelData;");
            if (body.NeedsCSharpModel)
                w.Line($"var {EmbeddedCSharpNames.Model} = {body.ModelCast}scope.ModelData;");
            var concatParts = new List<string>();
            int vIndex = 0;
            foreach (var seg in body.Segments)
            {
                if (seg is Piece p)
                {
                    concatParts.Add("P" + p.Index);
                }
                else if (seg is Partial pt)
                {
                    var v = "v" + vIndex++;
                    EmitLineSpanRaw(w, pt.SpanStartLine, pt.SpanStartCol, pt.SpanEndLine, pt.SpanEndCol);
                    w.Line($"var {v} = global::Heddle.Precompiled.PrecompiledRuntime.GenerateString({PartialResolveExpr(pt)}, {pt.ModelExpr}, scope.ChainedData, null) ?? string.Empty;");
                    concatParts.Add(v);
                }
                else
                {
                    var c = (Call) seg;
                    var v = "v" + vIndex++;
                    EmitLineSpan(w, c);
                    w.Line($"var {v} = {c.ExtensionField}.ProcessData(scope.Model({c.ParamExpr})) as string ?? string.Empty;");
                    concatParts.Add(v);
                }
            }

            w.Raw("#line hidden");
            if (concatParts.Count == 0)
                w.Line("return string.Empty;");
            else if (concatParts.Count == 1)
                w.Line($"return {concatParts[0]};");
            else
                w.Line("return string.Concat(" + string.Join(", ", concatParts) + ");");
            w.Raw("#line default");
            w.Outdent();
            w.Line("}");

            w.Outdent();
            w.Line("}");
        }

        /// <summary>Lazily-memoized partial-strategy resolution via <c>LazyInitializer.EnsureInitialized</c>.</summary>
        private static string PartialResolveExpr(Partial pt)
        {
            var resolve = pt.CallerModelTypeFq == null
                ? "global::Heddle.Precompiled.PrecompiledRuntime.ResolvePartial(" + CSharpEscape.StringLiteral(pt.Key) + ")"
                : "global::Heddle.Precompiled.PrecompiledRuntime.ResolvePartial(" + CSharpEscape.StringLiteral(pt.Key) +
                  ", typeof(" + pt.CallerModelTypeFq + "))";
            return "global::System.Threading.LazyInitializer.EnsureInitialized(ref " + pt.FieldName +
                   ", () => " + resolve + ")";
        }

        /// <summary>The line terminators C# recognises, which end a <c>#line</c> file name early, together with the
        /// quote that closes it. None of them can be escaped inside a <c>pp_string</c>.</summary>
        private static bool IsWritableLineDirectiveFile(string path)
        {
            foreach (var c in path)
            {
                if (c == '"' || c == '\n' || c == '\r' || c == '\u0085' || c == '\u2028' || c == '\u2029')
                    return false;
            }

            return true;
        }

        private void EmitLineSpanRaw(CodeWriter w, int sl, int sc, int el, int ec)
        {
            if (!_lineDirectiveFileIsWritable)
            {
                w.Raw("#line hidden");
                return;
            }

            w.Raw($"#line ({sl}, {sc}) - ({el}, {ec}) \"{_lineDirectiveFile}\"");
        }

        private void EmitLineSpan(CodeWriter w, Call c)
        {
            if (!_lineDirectiveFileIsWritable)
            {
                w.Raw("#line hidden");
                return;
            }

            w.Raw($"#line ({c.SpanStartLine}, {c.SpanStartCol}) - ({c.SpanEndLine}, {c.SpanEndCol}) \"{_lineDirectiveFile}\"");
        }

        /// <summary>Manifest-entry builder for both normal and HED7014 fallback-marker entries.
        /// Constructor-argument names stay string constants (PrecompiledTemplateInfo not linkable).</summary>
        private string BuildManifestEntry(string contentHash, bool isDynamic, string modelType, bool marker)
        {
            var entryType = $"global::{_namespace}.{_sanitizedName}";
            var sb = new StringBuilder();
            sb.Append("new global::Heddle.Precompiled.PrecompiledTemplateInfo(\n");
            sb.Append($"    key: {CSharpEscape.StringLiteral(_key)},\n");
            sb.Append(marker
                ? "    entryPointType: null,\n"
                : $"    entryPointType: typeof({entryType}),\n");
            sb.Append(marker
                ? $"    modelType: typeof({modelType}),\n"
                : $"    modelType: {entryType}.__ModelType,\n");
            sb.Append($"    isDynamic: {(isDynamic ? "true" : "false")},\n");
            sb.Append($"    contentHash: {CSharpEscape.StringLiteral(contentHash)},\n");
            sb.Append("    imports: global::System.Array.Empty<global::Heddle.Precompiled.PrecompiledImport>(),\n");
            sb.Append("    optionsFingerprint: " + FingerprintExpr() + ",\n");
            sb.Append(marker
                ? "    extensionBindings: global::System.Array.Empty<global::Heddle.Precompiled.PrecompiledExtensionBinding>(),\n"
                : "    extensionBindings: " + ExtensionBindingsArray() + ",\n");
            sb.Append(marker
                ? "    functionBindings: " + MarkerFunctionBindingsArray() + ",\n"
                : "    functionBindings: " + FunctionBindingsArray() + ",\n");
            sb.Append(marker
                ? "    capabilities: global::Heddle.Precompiled.PrecompiledCapabilities." +
                  nameof(Heddle.Precompiled.PrecompiledCapabilities.None) + ",\n"
                : "    capabilities: " + CapabilitiesExpr() + ",\n");
            sb.Append(marker ? "    strategy: null,\n" : $"    strategy: {entryType}.Root,\n");

            // The registered name, so the runtime registry answers to it as well as the build-time import map.
            sb.Append("    registeredName: " +
                (_registeredName == null ? "null" : CSharpEscape.StringLiteral(_registeredName)) + ",\n");

            // Which form the emitted #line file names are in, machine-readable instead of a comment. A marker
            // entry has no generated source and therefore no #line directives, so it claims nothing.
            sb.Append("    linePathForm: global::Heddle.Precompiled.PrecompiledLinePathForm." +
                (marker
                    ? nameof(Heddle.Precompiled.PrecompiledLinePathForm.Unspecified)
                    : _lineDirectiveFileIsRootRelative
                        ? nameof(Heddle.Precompiled.PrecompiledLinePathForm.RootRelative)
                        : nameof(Heddle.Precompiled.PrecompiledLinePathForm.TemplatePath)) + ")");
            return sb.ToString();
        }

        /// <summary>Formats the options fingerprint from build config. Arity mismatch breaks compile (safety guard).</summary>
        private string FingerprintExpr()
        {
            var fingerprint = new PrecompiledOptionsFingerprint(_config.OutputProfile, _config.ExpressionMode,
                _config.TrimDirectiveLines);
            var sb = new StringBuilder();
            sb.Append("new global::Heddle.Precompiled.PrecompiledOptionsFingerprint(\n");
            sb.Append($"        global::Heddle.Data.OutputProfile.{fingerprint.Profile},\n");
            sb.Append($"        global::Heddle.Data.ExpressionMode.{fingerprint.ExpressionMode},\n");
            sb.Append($"        trimDirectiveLines: {(fingerprint.TrimDirectiveLines ? "true" : "false")})");
            return sb.ToString();
        }

        /// <summary>The marker entry's function rows: one null-target row per unresolvable name.</summary>
        private string MarkerFunctionBindingsArray()
        {
            var sb = new StringBuilder();
            sb.Append("new global::Heddle.Precompiled.PrecompiledFunctionBinding[] { ");
            sb.Append(string.Join(", ", DedupeUnresolvable()
                .Select(fn => $"new global::Heddle.Precompiled.PrecompiledFunctionBinding({CSharpEscape.StringLiteral(fn.Name)}, null, 0)")));
            sb.Append(" }");
            return sb.ToString();
        }

        private string ExtensionBindingsArray()
        {
            if (_extensionBindings.Count == 0)
                return "global::System.Array.Empty<global::Heddle.Precompiled.PrecompiledExtensionBinding>()";
            var sb = new StringBuilder();
            sb.Append("new global::Heddle.Precompiled.PrecompiledExtensionBinding[] { ");
            sb.Append(string.Join(", ", _extensionBindings
                .OrderBy(b => b.Name, System.StringComparer.Ordinal)
                .ThenBy(b => b.Type, System.StringComparer.Ordinal)
                .Select(b => "new global::Heddle.Precompiled.PrecompiledExtensionBinding(" +
                             CSharpEscape.StringLiteral(b.Name) + ", " +
                             CSharpEscape.StringLiteral(b.Type + ", " + b.Assembly) +
                             // Prop-layout row omitted for parameter-less extensions (schema compatibility).
                             (b.PropLayout == null ? "" : ", " + CSharpEscape.StringLiteral(b.PropLayout)) + ")")));
            sb.Append(" }");
            return sb.ToString();
        }

        private string FunctionBindingsArray()
        {
            if (_functionBindings.Count == 0)
                return "global::System.Array.Empty<global::Heddle.Precompiled.PrecompiledFunctionBinding>()";
            var sb = new StringBuilder();
            sb.Append("new global::Heddle.Precompiled.PrecompiledFunctionBinding[] { ");
            sb.Append(string.Join(", ", _functionBindings
                .OrderBy(b => b.Name, System.StringComparer.Ordinal)
                .ThenBy(b => b.Target, System.StringComparer.Ordinal)
                .Select(b => $"new global::Heddle.Precompiled.PrecompiledFunctionBinding({CSharpEscape.StringLiteral(b.Name)}, {CSharpEscape.StringLiteral(b.Target)}, {b.OverloadCount})")));
            sb.Append(" }");
            return sb.ToString();
        }

        private string CapabilitiesExpr()
        {
            const string prefix = "global::Heddle.Precompiled.PrecompiledCapabilities.";
            var caps = prefix + nameof(PrecompiledCapabilities.StringOutput);
            if (_config.EmitUtf8Pieces)
                caps += " | " + prefix + nameof(PrecompiledCapabilities.Utf8Pieces);
            return caps;
        }
    }
}
