using System.Collections.Generic;
using System.Linq;
using System.Text;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Generator.Binding;
using Heddle.Generator.Diagnostics;
using Heddle.Generator.Pipeline;
using Heddle.Generator.Typing;
using Heddle.Language;
using Heddle.Language.Binding;
using Heddle.Language.Expressions;
using Heddle.Precompiled;
using Heddle.Strings.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

        /// <summary>Whether the CONSUMER's compiler parses a <c>u8</c> suffix — a language-version question about
        /// the compilation being generated into, not about the Roslyn this assembly is built against. When it
        /// cannot, <see cref="PieceWriter"/> spells the same span through a constant byte array instead.</summary>
        private bool ConsumerParsesUtf8Literals() =>
            _compilation is CSharpCompilation csharp &&
#if ROSLYN_4_11_OR_GREATER
            csharp.LanguageVersion >= Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp11;
#else
            // The 4.1 floor's LanguageVersion enum predates the CSharp11 member; 1100 is its value.
            (int)csharp.LanguageVersion >= 1100;
#endif

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

        /// <summary>The <c>ModelType</c> item metadata, trimmed, or null. It types the template when no
        /// <c>@model</c> directive exists; when both exist they must name the same resolved type.</summary>
        private readonly string _metadataModelType;
        private ITypeSymbol _modelSymbol;
        private readonly List<string> _usings = new List<string>();

        private readonly List<(string Name, string Type, string Assembly, string PropLayout)> _extensionBindings =
            new List<(string, string, string, string)>();
        private readonly List<(string Name, string Target, int OverloadCount)> _functionBindings =
            new List<(string, string, int)>();

        private readonly List<string> _extensionFields = new List<string>();

        /// <summary>The <c>PrecompiledInitSite</c> field names this template emits, in emission order. They travel
        /// on the manifest row so the gauntlet can read back what each hook answered at registration.</summary>
        private readonly List<string> _initSiteFields = new List<string>();
        private int _initSiteCounter;
        private int _lateAccessorCounter;

        /// <summary>The shaped working document of the body currently being populated — what a call site's own
        /// source text is sliced out of. Saved and restored around every body walk, because a nested body's
        /// positions index its own document rather than the file.</summary>
        private string _currentDoc;

        private readonly StringBuilder _fieldDecls = new StringBuilder();

        /// <summary>Where in its chain the call currently being built sits. Ambient rather than threaded, because
        /// every route from <see cref="BuildCall"/> down to <see cref="TryPlanSite"/> would otherwise carry four
        /// more parameters it never reads. Saved and restored around each chain walk; a lone call leaves it at its
        /// default, which is the shape every non-chained call has always had.</summary>
        private ChainSlot _chainSlot;

        private struct ChainSlot
        {
            public bool HasProducerToRight;
            public bool IsChainedConsumer;

            /// <summary>The producer's site field, or <c>null</c> when the producer runs no hook.</summary>
            public string ProducerSite;

            /// <summary>The chained type to declare when there is no producer site to read it off: the base
            /// <c>InitStart</c>'s <c>typeof(string)</c> for a <c>Bind</c>ed carrier, and <c>"null"</c> otherwise.</summary>
            public string ChainedTypeExpr;

            /// <summary>The item's own source text, reconstructed as a one-call document so a faulted site's
            /// substitute compiles this item rather than the whole chain. <c>null</c> outside a multi-item chain,
            /// where the chain block's own text is already exactly this call.</summary>
            public string ItemSourceText;
        }

        /// <summary>The <c>Init</c> calls that must run <b>after</b> an enclosing one: a call site inside a
        /// type-agnostic body takes its <c>dataType</c> from the model that body's own hook answers with, and a
        /// static field initializer runs in textual order, so one tier per level of type-agnostic nesting is what
        /// makes the cascade an ordering rather than a lazy read on the render path.</summary>
        private readonly List<StringBuilder> _deferredInits = new List<StringBuilder>();
        private int _initTier;

        private StringBuilder InitDecls
        {
            get
            {
                if (_initTier == 0)
                    return _fieldDecls;
                while (_deferredInits.Count < _initTier)
                    _deferredInits.Add(new StringBuilder());
                return _deferredInits[_initTier - 1];
            }
        }

        /// <summary>Every field declaration in dependency order: the base tier, then one tier per level of
        /// type-agnostic nesting.</summary>
        private string FieldDeclarations()
        {
            if (_deferredInits.Count == 0)
                return _fieldDecls.ToString();
            var all = new StringBuilder(_fieldDecls.ToString());
            foreach (var tier in _deferredInits)
                all.Append(tier);
            return all.ToString();
        }
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
            bool lineDirectiveFileIsRootRelative = true, string registeredName = null, string modelType = null,
            Observe.ObservationSource observation = null)
        {
            _metadataModelType = string.IsNullOrWhiteSpace(modelType) ? null : modelType.Trim();
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
            _observation = observation;
        }

        /// <summary>Layer 2, when the build could build it: a real engine compile of this very template, from which
        /// the emitter takes the model type a hook chose for its body. Consulted <b>only</b> where the build would
        /// otherwise have no typing at all, so what it can change is which tier a read takes and never a rendered
        /// byte — and asked for the first time only from there, so a template the build already types costs the
        /// bundle nothing.</summary>
        private readonly Observe.ObservationSource _observation;

        private Observe.ObservedDocument _observed;

        private bool _observeAttempted;

        /// <summary>Why this template could not be observed, or <c>null</c>. The generator decides what a failure
        /// costs — an informational note under <c>auto</c>, a build error under <c>strict</c>.</summary>
        private string _observationFailure;

        /// <summary>Why this template could not be observed, or <c>null</c>. Read rather than reported here,
        /// because what a failure costs is the build option's decision and not the emitter's.</summary>
        internal string ObservationFailure => _observationFailure;

        internal sealed class Result
        {
            public bool Emitted { get; set; }

            /// <summary>The categorized refusal that declined the template, or null when it emitted (or degraded
            /// through the HED7014 marker channel). <see cref="UnsupportedReason"/> is its human-readable detail —
            /// the sentence HED7031 prints.</summary>
            public Refusal Unsupported { get; set; }

            public string UnsupportedReason => Unsupported?.Detail;
            public string Source { get; set; }
            public string ManifestEntry { get; set; }
            public bool IsDynamic { get; set; }
            public string ModelTypeText { get; set; }

            /// <summary>True when the template is a HED7014 fallback marker: not precompiled (no <see cref="Source"/>),
            /// but a marker manifest entry (<see cref="ManifestEntry"/>) and one warning per
            /// <see cref="UnresolvableFunctions"/> are emitted.</summary>
            public bool IsMarker { get; set; }
            public IReadOnlyList<(string Name, BlockPosition Position)> UnresolvableFunctions { get; set; }

            /// <summary>Emitter-produced Roslyn diagnostics (HED7005, HED7006, HED7033, the forwarded engine ids)
            /// with their <c>.heddle</c> span; reported by the generator in every result branch.</summary>
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
            // Decided on the directive alone, before the metadata merge: only an in-file @model(){{dynamic}}
            // pins the ENGINE's scope dynamic on every compile (the directive overrides a caller-supplied model
            // type, where metadata never reaches the engine at all) — and an alias named 'dynamic' re-types the
            // very same spelling, so it un-pins it.
            _modelDeclaredDynamic = string.Equals(_modelTypeText, "dynamic", System.StringComparison.Ordinal) &&
                                    !HasAliasNamed("dynamic");
            _modelDeclaredInTemplate = _modelTypeText != null;
            if (_modelTypeText == null)
            {
                // No directive: the metadata types the template, through the very pipeline the directive feeds, so
                // resolution, its diagnostics and the accessibility gate behave identically. The position stays at
                // the file start — metadata has no in-file span to point at.
                _modelTypeText = _metadataModelType;
            }
            else if (_metadataModelType != null &&
                !string.Equals(_metadataModelType, _modelTypeText, System.StringComparison.Ordinal))
            {
                var conflict = CheckMetadataModelConflict();
                if (conflict != null)
                    return conflict;
            }

            bool isDynamic = _modelTypeText == null ||
                             string.Equals(_modelTypeText, "dynamic", System.StringComparison.Ordinal);
            string modelType = isDynamic ? "object" : _modelTypeText;
            if (!isDynamic)
            {
                _modelSymbol = _resolver.ResolveModelType(_modelTypeText, _usings);
                if (!CanWriteTypeName(_modelSymbol, _modelDirectivePosition, out var modelReason))
                    return new Result
                    {
                        Emitted = false, Diagnostics = _diagnostics,
                        Unsupported = OpenGenericModelRefusal(_modelSymbol, _modelDirectivePosition) ?? modelReason
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
                    // hid it. The engine binds a model name over the assemblies actually loaded, which is the same
                    // set wherever the host declared it: `[HeddleModelAssembly(typeof(T))]` cannot be spelled
                    // without the reference this tier resolves against, and `AssemblyHelper.Register` reads it. So
                    // the tier that might still serve the template is only the one the host did not declare.
                    return new Result
                    {
                        Emitted = false, Diagnostics = _diagnostics,
                        Unsupported = new Refusal(RefusalCategory.UnnameableType,
                            "model type '" + _modelTypeText + "' resolves to no symbol", _modelDirectivePosition)
                    };
                }
            }

            // Build the body tree (Body0 = document root; extension bodies become nested bodies).
            var rootCtx = new BodyContext(isDynamic ? null : "(" + modelType + ")", _modelSymbol, isDynamic,
                root: _modelSymbol);
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

                return new Result { Emitted = false, Unsupported = reason, Diagnostics = _diagnostics };
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

        /// <summary>
        /// The agreement check for a template that carries BOTH an <c>@model</c> directive and <c>ModelType</c> item
        /// metadata with different spellings. Two spellings resolving to the same symbol agree — no diagnostic. A
        /// metadata spelling that resolves to nothing draws the same diagnostics a non-resolving directive spelling
        /// draws (HED7023/HED7007, gated the same way) and degrades the template the same way. Two different resolved
        /// types are the HED7032 conflict error. Returns null when the emit may proceed on the directive's spelling —
        /// including when the directive itself does not resolve, which the directive path reports as it always has.
        /// </summary>
        private Result CheckMetadataModelConflict()
        {
            // "dynamic" resolves as object through the shared keyword table, so an explicitly dynamic directive
            // against a typed metadata (or the reverse) lands in the different-resolved-types arm below.
            var directiveSymbol = _resolver.ResolveModelType(_modelTypeText, _usings);
            if (directiveSymbol == null)
                return null;

            var metadataSymbol = _resolver.ResolveModelType(_metadataModelType, _usings);
            if (metadataSymbol == null)
            {
                if (_resolver.LastFault == Heddle.Language.Binding.TypeSpellingFault.Ambiguous)
                    _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.AmbiguousTypeName,
                        default, _metadataModelType));
                else if (IsPlainTypeName(_metadataModelType) && !_resolver.TypeNameExistsAnywhere(_metadataModelType))
                    _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.UnresolvableModelType,
                        default, _metadataModelType));

                return new Result
                {
                    Emitted = false, Diagnostics = _diagnostics,
                    Unsupported = new Refusal(RefusalCategory.UnnameableType,
                        "ModelType metadata '" + _metadataModelType + "' resolves to no symbol")
                };
            }

            if (SymbolEqualityComparer.Default.Equals(directiveSymbol, metadataSymbol))
                return null;

            _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.ConflictingModelTypeDeclarations, default,
                _metadataModelType, _modelTypeText,
                SymbolTypeResolver.FullyQualified(metadataSymbol), SymbolTypeResolver.FullyQualified(directiveSymbol)));
            return new Result
            {
                Emitted = false, Diagnostics = _diagnostics,
                Unsupported = new Refusal(RefusalCategory.HostSetup, "ModelType metadata '" + _metadataModelType +
                    "' and the @model directive '" + _modelTypeText + "' name different types")
            };
        }

        /// <summary>An extension is zero-output when it declares <c>[ZeroOutput]</c> — the
        /// symbol-readable form of the runtime's null-<c>InitStart</c> protocol, which the generator has no way to
        /// evaluate. A block whose leftmost call is zero-output is removed from the piece stream on both tiers.</summary>
        private bool IsZeroOutput(OutputChain chain)
        {
            var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
            if (leftmost == null)
                return false;
            return _extensionBinder.TryResolve(leftmost.ExtensionName, out var info) && info.IsZeroOutput;
        }

        private bool _modelDeclaredDynamic;

        /// <summary>Whether the template's own <c>@model</c> directive named the model type. False means the
        /// recorded model type is the build's — the <c>ModelType</c> item metadata, else <c>object</c> — while the
        /// engine would take the requesting context's instead, which is the fact the manifest carries as
        /// <c>PrecompiledTemplateInfo.ModelTypeIsAmbient</c> so the gauntlet can refuse the mismatch.</summary>
        private bool _modelDeclaredInTemplate;

        /// <summary>Whether a <c>@using(){{name = …}}</c> alias claims <paramref name="name"/>.</summary>
        private bool HasAliasNamed(string name)
        {
            foreach (var import in _usings)
            {
                var eq = import.IndexOf('=');
                if (eq > 0 && string.Equals(import.Substring(0, eq).Trim(), name, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void ExtractDirectives()
        {
            foreach (var chain in _parse.OutputChains)
            {
                if (chain.Chain == null || chain.Chain.Count == 0)
                    continue;
                var item = chain.Chain[0];
                if (item.ExtensionName == DirectiveNames.Model)
                {
                    var text = (item.ParameterTemplate ?? string.Empty).Trim();
                    if (text.Length != 0)
                    {
                        _modelTypeText = text;
                        _modelDirectivePosition = item.Position;
                    }
                }
                else if (item.ExtensionName == DirectiveNames.Using)
                {
                    var ns = (item.ParameterTemplate ?? string.Empty).Trim();
                    if (ns.Length != 0 && !_usings.Contains(ns))
                        _usings.Add(ns);
                }
            }
        }

        private sealed class Piece { public int Index; }

        private sealed class Call
        {
            public string ExtensionField;
            public string ParamExpr;
            public bool UsesModelLocal;
            public int SpanStartLine, SpanStartCol, SpanEndLine, SpanEndCol;

            /// <summary>The <c>PrecompiledInitSite</c> field this call wrote, or <c>null</c> for a call bound
            /// without one — a <c>Bind</c>ed carrier. The item to this one's left in a chain names it as its
            /// producer, which is how the engine's right-to-left typing threads through generated code.</summary>
            public string SiteField;
        }

        /// <summary>A whole multi-item chain as one segment, its items in the order
        /// <c>TemplateChain</c> executes them: <b>rightmost source item first</b>. Each item is emitted as its own
        /// static helper taking the chained scope, so the item's own call parameter is evaluated against that scope
        /// rather than the ambient one — which is what <c>TemplateItem.ProcessData</c> does.</summary>
        private sealed class ChainCall
        {
            public string Local;
            public readonly List<ChainStep> Steps = new List<ChainStep>();
        }

        /// <summary>One item of a chain: the <c>ProcessData</c> helper every item has, and the <c>RenderData</c>
        /// helper only the last executed item — the leftmost in source — also has.</summary>
        private sealed class ChainStep
        {
            public string ProcessMethod;
            public string RenderMethod;
            public int SpanStartLine, SpanStartCol, SpanEndLine, SpanEndCol;
        }

        private sealed class BodyClass
        {
            public string Name;
            public string ModelCast;
            public bool IsDynamic;
            public bool NeedsModelLocal;
            public bool HostsParticipant;

            /// <summary>The body text after document shaping — what a real body compile writes back over
            /// <c>InitContext.ParameterTemplate</c>, and what a supplied body has to hand the hook in its
            /// place.</summary>
            public string ShapedDocument;
            public readonly List<object> Segments = new List<object>();

            /// <summary>Whether any output chain survived shaping — the build's side of the negation of
            /// <c>RuntimeDocument.Empty</c>. <c>HeddleCompiler.CompileBody</c> adds a document element for a chain
            /// whose composed <c>returnTypeChainedPrevious</c> is non-null and drops every other, and the shared
            /// shaper drops exactly the same ones here, so a body with no surviving element is a document the
            /// engine builds and then discards.
            /// <para>Read off the shaped element list rather than off <see cref="Segments"/>, and written before the
            /// element walk begins: a definition body is registered before it is populated so a self-call finds it,
            /// and a half-built segment list would tell that call the body was empty.</para></summary>
            public bool HasProcessors;

            /// <summary>Set when a call site was handed <c>null</c> in place of this body, so the class it would
            /// have been is never referenced and is never written.</summary>
            public bool Suppressed;
        }

        /// <summary>
        /// The strategy a call site installs for <paramref name="body"/>: the body class, or <c>null</c> where the
        /// body compiles to no processors. <c>AbstractExtension.InitSubTemplate</c> discards a document that holds
        /// none — <c>subTemplate.Empty</c> yields no result — and leaves the hook with no strategy and the shaped
        /// text as its inert <c>_innerResult</c>, which is the post-state <c>PrecompiledRuntime.Init</c> installs
        /// for a null body over a non-empty <c>RawText</c>. Handing one a real strategy instead makes
        /// <c>InnerExist</c> true where the engine leaves it false, and every hook that reads it — the raw carrier,
        /// the encoding carrier, <c>@out</c>, and any extension of anyone's — renders the other tier's bytes.
        /// </summary>
        private static string BodyStrategyName(BodyClass body)
        {
            if (body == null)
                return null;
            if (body.HasProcessors)
                return body.Name;
            body.Suppressed = true;
            return null;
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
            public Refusal Reason;
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

        private BodyClass BuildBody(string doc, ParseContext ctx, BodyContext bctx, out Refusal reason)
        {
            var body = NewBody(bctx);
            return PopulateBody(body, doc, ctx, bctx, out reason) ? body : null;
        }

        private bool PopulateBody(BodyClass body, string doc, ParseContext ctx, BodyContext bctx, out Refusal reason)
        {
            reason = null;
            // A body is its own document: nothing in it is an item of the chain the call sits in. Cleared for the
            // whole walk and restored after, so a call inside the body of a chained item is not itself read as a
            // chained consumer — which would arm @out's composed guard on an @out that composes nothing — and does
            // not take its chained type from the enclosing chain's producer.
            var savedSlot = _chainSlot;
            _chainSlot = default(ChainSlot);
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
                reason = new Refusal(RefusalCategory.EngineParity, lintRefusal);
                return false;
            }
            var working = shape.WorkingDocument;
            body.ShapedDocument = working;
            body.HasProcessors = shape.Elements.Count != 0;
            var savedDoc = _currentDoc;
            _currentDoc = working;

            // The piece walk itself is shared with RuntimeDocument.GetDocumentPieces, so the
            // P0..Pn constants below are the same strings the dynamic tier slices.
            // Collect refusals for multi-error reporting (sound: sibling elements are independent).
            // Still refuses the body (partial emit would be worse than sequential reporting).
            Refusal localReason = null;
            Refusal firstReason = null;
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
                    if (seg is Call call && call.UsesModelLocal)
                        body.NeedsModelLocal = true;

                    return true;
                });

            // `completed` can still be false: SlicePieces' contract keeps the abandon channel, and a future callback
            // arm may use it. Either signal refuses the body.
            reason = firstReason ?? localReason;
            _profileHtml = savedProfile;
            _currentDoc = savedDoc;
            _chainSlot = savedSlot;
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
                if (lm != null && lm.ExtensionName == DirectiveNames.Profile)
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

        private object BuildCall(OutputChain chain, ParseContext ctx, BodyContext bctx, out Refusal reason)
        {
            if (chain.Chain.Count == 1)
                return BuildItemCall(chain, chain.Chain[0], ctx, bctx, out reason);

            return BuildChain(chain, ctx, bctx, out reason);
        }

        /// <summary>
        /// A chain of calls, emitted as <c>TemplateChain.RenderData</c> runs it. Its items are built <b>right to
        /// left</b>, which is the order the engine executes them in and the order its typing threads in: each item
        /// takes the one to its right as its producer, and the leftmost is the one that renders.
        /// <para>Nothing here decides what a chained type is. The producer's own hook answers that at registration
        /// and the consumer's site reads the answer off it, exactly as <c>returnTypeChainedPrevious</c> carries it
        /// through <c>CompileBody</c> — except for a carrier the build <c>Bind</c>s, which runs no hook because
        /// <c>Bind</c> already stands in for the base one, whose answer is <c>string</c>.</para>
        /// </summary>
        private ChainCall BuildChain(OutputChain chain, ParseContext ctx, BodyContext bctx, out Refusal reason)
        {
            reason = null;
            var saved = _chainSlot;
            var result = new ChainCall { Local = "__c" + _chainLocalCounter++ };
            try
            {
                string producerSite = null;
                string producedType = "null";
                bool hasProducerToRight = false;
                for (int i = chain.Chain.Count - 1; i >= 0; i--)
                {
                    var item = chain.Chain[i];
                    _chainSlot = new ChainSlot
                    {
                        HasProducerToRight = hasProducerToRight,
                        IsChainedConsumer = i != 0,
                        ProducerSite = producerSite,
                        ChainedTypeExpr = producedType,
                        ItemSourceText = ChainItemSourceText(chain, item)
                    };

                    var built = BuildItemCall(chain, item, ctx, bctx, out reason) as Call;
                    if (built == null)
                    {
                        reason ??= new Refusal(RefusalCategory.ChainCarrier, "chain item", item.Position);
                        return null;
                    }

                    result.Steps.Add(NewStep(built, bctx.ModelCast, last: i == 0));
                    producerSite = built.SiteField;
                    producedType = built.SiteField == null ? "typeof(string)" : "null";
                    hasProducerToRight = true;
                }
            }
            finally
            {
                _chainSlot = saved;
            }

            return result;
        }

        /// <summary>Writes one chain item's helpers and returns the step that calls them. The helper takes the
        /// <b>chained</b> scope, so the item's call parameter is spelled against it — <c>TemplateItem.ProcessData</c>
        /// reads its parameter off the scope it is handed, and for a chain item that scope is
        /// <c>scope.Chain(result)</c>.</summary>
        private ChainStep NewStep(Call call, string modelCast, bool last)
        {
            var step = new ChainStep
            {
                SpanStartLine = call.SpanStartLine, SpanStartCol = call.SpanStartCol,
                SpanEndLine = call.SpanEndLine, SpanEndCol = call.SpanEndCol,
                ProcessMethod = "CH" + _chainMethodCounter++
            };
            EmitChainHelper(step.ProcessMethod, call, modelCast, render: false);
            if (last)
            {
                step.RenderMethod = "CH" + _chainMethodCounter++;
                EmitChainHelper(step.RenderMethod, call, modelCast, render: true);
            }

            return step;
        }

        private void EmitChainHelper(string method, Call call, string modelCast, bool render)
        {
            _methodDecls.Append("        private static ").Append(render ? "void " : "object ").Append(method)
                .Append("(in global::Heddle.Data.Scope scope)\n        {\n");
            if (call.UsesModelLocal)
                _methodDecls.Append("            var m = ").Append(modelCast).Append("scope.ModelData;\n");
            _methodDecls.Append("            ").Append(render ? string.Empty : "return ")
                .Append(call.ExtensionField).Append(render ? ".RenderData(scope.Model(" : ".ProcessData(scope.Model(")
                .Append(call.ParamExpr).Append("));\n        }\n");
        }

        /// <summary>The one-call document one chain item's own source text is. A lone call's text is the chain
        /// block itself, but a chain item's span covers only <c>name(param)</c> — no <c>@</c>, no body — and a
        /// faulted site's substitute compiles what it is given: handed the whole chain it would run the producers a
        /// second time, so the item is written back out on its own.
        /// <para>Sliced out of the block rather than out of the document, because the two coordinate systems are
        /// different — an item's position indexes the parsed document and the block's indexes the shaped one. What
        /// carries across is the item's offset <i>inside</i> the chain, which shaping never rewrites.</para></summary>
        private string ChainItemSourceText(OutputChain chain, OutputItem item)
        {
            if (chain?.Chain == null || chain.Chain.Count == 0)
                return null;
            var block = SourceTextAt(chain.BlockPosition);
            if (block == null || block.Length == 0 || block[0] != '@')
                return null;
            var offset = item.Position.StartIndex - chain.Chain[0].Position.StartIndex + 1;
            if (offset < 1 || item.Position.Length < 0 || offset + item.Position.Length > block.Length)
                return null;
            var call = block.Substring(offset, item.Position.Length);
            if (string.IsNullOrEmpty(item.ParameterTemplate))
                return "@" + call;
            return "@" + call + "{{" + item.ParameterTemplate + "}}";
        }

        private int _chainLocalCounter;
        private int _chainMethodCounter;

        /// <summary>The chain and parse context of the call being built, for the one place that needs them and
        /// cannot be handed them: a chain <b>parameter</b> item, which is reached through
        /// <see cref="BuildParamExpr"/> — a value-tier signature that carries neither.</summary>
        private OutputChain _itemChain;

        private ParseContext _itemCtx;

        private object BuildItemCall(OutputChain chain, OutputItem item, ParseContext ctx, BodyContext bctx,
            out Refusal reason)
        {
            var savedChain = _itemChain;
            var savedCtx = _itemCtx;
            _itemChain = chain;
            _itemCtx = ctx;
            try
            {
                return BuildItemCallCore(chain, item, ctx, bctx, out reason);
            }
            finally
            {
                _itemChain = savedChain;
                _itemCtx = savedCtx;
            }
        }

        private object BuildItemCallCore(OutputChain chain, OutputItem item, ParseContext ctx, BodyContext bctx,
            out Refusal reason)
        {
            reason = null;
            var cp = item.CallParameter;
            var name = item.ExtensionName;

            if (name.Length == 0)
            {
                if (!string.IsNullOrEmpty(item.ParameterTemplate))
                    return BuildBodiedUnnamedCarrier(chain, item, cp, bctx, out reason);

                WarnOnRedundantEncoding(cp);
                if (!BuildParamExpr(cp, bctx, RefStructUse.Rendered, out var uParam, out var uUses, out reason, item.Position))
                    return null;
                var uField = AllocateEmptyExtension(item.Position, out reason);
                if (uField == null)
                    return null;
                return MakeCall(uField, uParam, uUses, item.Position);
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
                return BuildDefinitionCall(definitionItem, chain, item, cp, bctx, isFill: true, out reason);
            if (callTarget == CallTargetKind.Definition)
                return BuildDefinitionCall(definitionItem, chain, item, cp, bctx, isFill: false, out reason);

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
                reason = new Refusal(RefusalCategory.EngineParity, "'" + name + "' " + acceptReason,
                    item.Position);
                return null;
            }

            // The declared roles are read off the extension's own type, never names this compiler knows. A chained
            // call, an ambient fill and a definition have all returned above, so a role is only ever read for a
            // call the extension tier owns.
            if (_extensionBinder.TryResolve(name, out var roleInfo) &&
                (roleInfo.HasSlotProjection || roleInfo.HasChildTemplateHost))
            {
                // Two roles on one type describe two incompatible call shapes — a projection takes no body and
                // the host's body is its child's name — so there is nothing to pick between and the template
                // keeps the tier whose extension declared it.
                if (roleInfo.HasSlotProjection && roleInfo.HasChildTemplateHost)
                {
                    reason = new Refusal(RefusalCategory.ExtensionBinding,
                        "extension '" + name + "' declares both the slot-projection and the child-template-host role",
                        item.Position);
                    return null;
                }

                if (roleInfo.HasSlotProjection)
                    return BuildSlotProjectionCall(name, roleInfo, chain, item, cp, bctx, out reason);

                return BuildChildTemplateCall(name, roleInfo, chain, item, cp, bctx, out reason);
            }

            // ONE arm for every bound extension. What stood here were three near-identical name-keyed arms — the
            // engine branch trio, @list, @for — and a fourth, generic one further down for everything else. They
            // differed in exactly one thing: the body's model role, which is read off a real engine compile now
            // rather than looked up by name. All four already emitted the same shape.
            if (_extensionBinder.TryResolve(name, out var bound))
                return BuildBoundExtensionCall(name, bound, chain, item, cp, bctx, out reason);

            // Reached only when the shared classifier picked the function tier.
            if (callTarget == CallTargetKind.Function && string.IsNullOrEmpty(item.ParameterTemplate))
            {
                var callNode = BuildFunctionCallNode(name, cp, item.Position);
                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, _modelSymbol, "m", _exports,
                    TypeFacts, AllocateHopLocal, bctx.Props, _modelDeclaredDynamic, AllocateFunctionSite);
                writer.TopLevelCall = callNode;
                var expr = writer.WriteRoot(callNode);
                DrainUnresolvable(writer);
                if (expr == null)
                {
                    reason = writer.RefusalReason != null
                        ? new Refusal(writer.RefusalCategory, writer.RefusalReason, item.Position)
                        : new Refusal(RefusalCategory.FunctionBinding,
                            "unsupported function call '" + name + "'", item.Position);
                    return null;
                }

                RecordFunctionUses(writer);
                var fField = AllocateEmptyExtension(item.Position, out reason);
                if (fField == null)
                    return null;
                return MakeCall(fField, "(object)(" + expr + ")", writer.UsedModel, item.Position);
            }

            // Runtime will find it but emitter cannot reproduce render protocol — must not fire HED7006.
            if (_extensionBinder.TryGetUnbindableReason(name, out var unbindableReason))
            {
                reason = new Refusal(RefusalCategory.ExtensionBinding, unbindableReason, item.Position);
                return null;
            }

            // LateBound: bodiless, function-shaped, and this compilation resolves the name to nothing at all —
            // an ambient fill, a definition, a bound extension, an extension the runtime will find, and every
            // build-visible function have each been ruled out above. The only registration left is one the host
            // makes at run time, which is exactly what a late-bound site resolves at first render, through the
            // engine's own ranker. An unregistered name still reproduces the engine's own compile error there.
            if (callTarget == CallTargetKind.Unknown && string.IsNullOrEmpty(item.ParameterTemplate) &&
                CallTargetRules.IsFunctionCompatibleShape(cp))
            {
                var lateNode = BuildFunctionCallNode(name, cp, item.Position);
                var lateWriter = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, _modelSymbol, "m",
                    _exports, TypeFacts, AllocateHopLocal, bctx.Props, _modelDeclaredDynamic,
                    AllocateFunctionSite);
                lateWriter.TopLevelCall = lateNode;
                var lateExpr = lateWriter.WriteRoot(lateNode);
                DrainUnresolvable(lateWriter);
                if (lateExpr != null)
                {
                    RecordFunctionUses(lateWriter);
                    var lateField = AllocateEmptyExtension(item.Position, out reason);
                    if (lateField == null)
                        return null;
                    return MakeCall(lateField, "(object)(" + lateExpr + ")", lateWriter.UsedModel, item.Position);
                }
            }

            // HED7006 — bodied call with no bound extension. Bodiless may be function-compatible (delegate registration).
            if (!string.IsNullOrEmpty(item.ParameterTemplate))
                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.ExtensionNotBindable, item.Position, name));

            reason = new Refusal(RefusalCategory.ExtensionBinding, "named extension '" + name + "'",
                item.Position);
            return null;
        }

        /// <summary>
        /// A bodied <c>@(x){{ … }}</c>: a raw rescoping container the profile never redirects, so the shared rule
        /// gives it the empty registry name and the binder answers which type that name resolves to here. From
        /// there it is an ordinary bound call — the carrier's own <c>InitStart</c> runs at static init and takes
        /// the body the build supplies, including the <c>null</c> a body of static text alone earns, which is what
        /// leaves <c>InnerExist</c> false and the carrier rendering its model.
        /// <para>The bodiless form keeps its own arm above: only it is redirected to the encoding carrier under the
        /// Html profile, only it records the compiled-unnamed-output flag the <c>@profile</c> lint reads, and only
        /// it can carry a producer whose encoding is redundant.</para>
        /// </summary>
        private object BuildBodiedUnnamedCarrier(OutputChain chain, OutputItem item, CallParameter cp,
            BodyContext bctx, out Refusal reason)
        {
            OutputProfileRules.ResolveUnnamedCarrier(
                _profileHtml ? Heddle.Data.OutputProfile.Html : Heddle.Data.OutputProfile.Text,
                hasBody: true, out var carrierKind, out _);
            var registryName = OutputProfileRules.CarrierRegistryName(carrierKind);
            if (!_extensionBinder.TryResolve(registryName, out var carrierInfo))
            {
                reason = new Refusal(RefusalCategory.ExtensionBinding,
                    "no extension is registered for the unnamed carrier", item.Position);
                return null;
            }

            if (!CanWriteExtensionTypeName(carrierInfo.TypeSymbol, item.Position, out reason))
                return null;

            return BuildBoundExtensionCall(registryName, carrierInfo, chain, item, cp, bctx, out reason);
        }

        /// <summary>
        /// <para>The single arm for every bound <c>[ExtensionName]</c> extension, built in or referenced, bodied or
        /// not. Its one question is whether the build knows what this extension's compile-time hook does; everything
        /// after that is the same emission the engine branch trio, <c>@list</c>, <c>@for</c> and a plain custom
        /// extension all reached separately before.</para>
        /// <para>The hook's body typing is <i>known</i> when a real engine compile of this template was observed, or
        /// when the extension's author declared it by carrying <c>[ChildTemplateHost]</c>. It is not known for
        /// anything else — no name is consulted, and a built-in's body is typed by exactly what types a third
        /// party's — and that is no longer a decline: the body is emitted type-agnostically and the extension's own
        /// hook chooses its typing at static-init, so a bodied call precompiles in a default build with no property
        /// to set and no name to be on. Only the three shapes a type-agnostic body cannot write cost anything, and
        /// they cost that one call site through the substitute.</para>
        /// </summary>
        private Call BuildBoundExtensionCall(string name, ExtensionBinder.Info info, OutputChain chain,
            OutputItem item, CallParameter cp, BodyContext bctx, out Refusal reason,
            bool hostsChildTemplate = false)
        {
            reason = null;
            bool bodied = !string.IsNullOrEmpty(item.ParameterTemplate);

            // The child-template-host role declares its own body typing: the attribute obliges the extension to
            // evaluate its body — the child's name — against the call value, which is the same value it obliges the
            // child itself to be compiled against. That is a declaration by the extension's author, not a table
            // this compiler keeps, and the hook contradicts it at registration like any other assumption.
            var bodySource = BodyModelSource.Data;
            bool known = hostsChildTemplate;

            // Observation is consulted only where the build has no typing of its own, so it can only ever ADD a
            // typed body — which is what makes coverage monotone and byte identity a property of the design rather
            // than of a comparison.
            ITypeSymbol observedModel = null;
            if (!known && bodied && TryObservedBodyModel(item, bctx, out observedModel))
            {
                known = true;
                bodySource = BodyModelSource.Observed;
            }

            // The extension's own author has declared that a static initializer cannot reproduce what its hook
            // does. Taking them at their word costs this call site and nothing else: the rest of the template
            // precompiles, and the call renders by compiling its own text at first render.
            if (info.PrecompileUnsupportedReason != null)
                return BuildUnsupportedExtensionCall(name, info, chain, item, cp, bctx, out reason);

            // Named arguments on a PARAMETER-LESS extension must not be silently dropped — the dynamic tier
            // hard-errors HED5005 for that call, so the precompiled tier degrades and lets the dynamic tier govern
            // (mirrors TryBuildPropsPrototype's prop-less-definition guard).
            if (info.Parameters.Count == 0 && cp.PropArguments != null && cp.PropArguments.Count != 0)
            {
                reason = new Refusal(RefusalCategory.EngineParity,
                    "named arguments on parameter-less extension <" + name + ">", item.Position);
                return null;
            }

            // A parameter-declaring extension binds its [Prop] layout at build time; the carrier that installs it
            // wraps the extension AFTER its hook has run, which is the order the engine's compiler uses.
            PropLayoutInfo layout = null;
            string propsRef = null, settersRef = null, namesRef = null;
            if (info.Parameters.Count != 0)
            {
                layout = ResolveExtensionPropLayout(name, info, item.Position);
                if (layout == null)
                {
                    // Malformed [Prop] declaration — HED7017 recorded (once per extension type); refuse. HED7017 is
                    // an error, so the build fails rather than silently degrading: a malformed declaration is an
                    // authoring fault.
                    reason = new Refusal(RefusalCategory.ExtensionBinding,
                        "malformed [Prop] declaration on extension <" + name + ">", item.Position);
                    return null;
                }

                if (!TryBuildPropsPrototype(layout, cp, bctx, out propsRef, out settersRef, out reason))
                    return null;   // unknown/duplicate/missing/unreproducible → safe dynamic fallback

                namesRef = EmitParameterNamesField(layout);
            }

            if (!BuildParamExpr(cp, bctx, RefStructUse.Model, out var paramExpr, out var uses, out reason,
                    item.Position))
                return null;
            if (!TryPlanSite(name, chain, item, cp, bctx, out var plan, out reason))
                return null;
            plan.HostsChildTemplate = hostsChildTemplate;

            BodyClass body = null;
            if (bodied)
            {
                if (known)
                {
                    if (!TryTypedBody(name, bodySource, observedModel, item, cp, bctx, plan, out body, out reason))
                        return null;
                }
                else if (!TryTypeAgnosticBody(item, bctx, plan, out body, out reason, out var canSubstitute))
                {
                    // The three shapes no type-agnostic emission reproduces — a computed native expression,
                    // embedded C#, and a nested call whose own typing needs one — cost THIS call site rather than
                    // the template, by rendering it through the substitute. Where the substitute could not be
                    // honest about what the fragment would see, the reason stands and the template goes instead.
                    if (!canSubstitute)
                        return null;
                    var fallbackDetail = "The body of <" + name + "> cannot be emitted without knowing the model " +
                                         "its hook chooses: " + reason.Detail + ".";
                    reason = null;
                    var fallbackSite = EmitInitSite(plan);
                    var fallbackField = AllocateSiteFallback(fallbackSite, fallbackDetail);
                    return MakeCall(fallbackField, "scope.ModelData", false, item.Position, fallbackSite);
                }

                plan.BodyShaped = body?.ShapedDocument;
                plan.BodyNeedsLocals = body != null && body.HostsParticipant;
            }

            var siteField = EmitInitSite(plan);
            var bodyRef = BodyStrategyName(body);
            var field = namesRef == null
                ? AllocateInitExtension(name, info, siteField, bodyRef)
                : AllocateParameterizedExtension(name, info, siteField, propsRef, settersRef, namesRef,
                    bodyRef, layout);
            return MakeCall(field, paramExpr, uses, item.Position, siteField);
        }

        /// <summary>
        /// A call to an extension declaring <c>[PrecompileUnsupported]</c>: the site is written, the substitute is
        /// installed against it, and <c>HED7033</c> quotes the declared reason verbatim so the template author
        /// reads the extension author's own sentence rather than a paraphrase of it.
        /// <para>Reported once per extension per template — a template calling one such extension twenty times has
        /// one fact to learn, not twenty.</para>
        /// </summary>
        private Call BuildUnsupportedExtensionCall(string name, ExtensionBinder.Info info, OutputChain chain,
            OutputItem item, CallParameter cp, BodyContext bctx, out Refusal reason)
        {
            if (!TryPlanSite(name, chain, item, cp, bctx, out var plan, out reason))
                return null;

            var stated = info.PrecompileUnsupportedReason.Length == 0
                ? "no reason stated"
                : info.PrecompileUnsupportedReason;
            if (_reportedUnsupportedExtensions.Add(name))
                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.ExtensionPrecompileUnsupported,
                    item.Position, name, info.AqnSansVersion, stated));

            var unsupportedSite = EmitInitSite(plan);
            var field = AllocateSiteFallback(unsupportedSite,
                "<" + name + "> declares [PrecompileUnsupported]: " + stated + ".");
            return MakeCall(field, "scope.ModelData", false, item.Position, unsupportedSite);
        }

        /// <summary>Extensions whose <c>[PrecompileUnsupported]</c> declaration has already been reported for this
        /// template.</summary>
        private readonly HashSet<string> _reportedUnsupportedExtensions =
            new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>The model the engine itself compiled this call's body against, when a real engine compile of
        /// this template was available and every recorded entry for the body's span agreed. False is always "no
        /// answer", never a wrong one; a <c>null</c> model with a true return is the engine's own answer that the
        /// body compiles against a dynamic scope, which the emitter reproduces rather than treating as silence.
        /// </summary>
        private bool TryObservedBodyModel(OutputItem item, BodyContext bctx, out ITypeSymbol model)
        {
            model = null;
            if (item.Context == null || item.ParameterTemplate == null)
                return false;
            var observed = Observed();
            if (observed == null)
                return false;
            if (!observed.TryBodyModel(item.Context.AbsoluteOffset, item.ParameterTemplate.Length, out model,
                    out var isDynamic))
                return false;
            // Under a DYNAMIC enclosing scope, `System.Object` is what "no model" reports rather than a model: a
            // hook that re-types its body to its caller's scope hands the body compile the document's own type, and
            // a document with no @model has that one. Casting to it binds no member and refuses every read the
            // dynamic tier would have served, so it is read as what it means there — no static model.
            // Under a TYPED enclosing scope the same answer is a real one: an `object` element of a declared
            // collection is a type, the engine refuses a member read on it, and so does this.
            if (isDynamic || model.TypeKind == TypeKind.Dynamic ||
                (bctx.IsDynamic && model.SpecialType == SpecialType.System_Object))
            {
                model = null;
                return true;
            }

            // Asked without reporting, unlike every other spelling gate here. A type generated code may not name is
            // not a fault of this template: the body simply goes out type-agnostically and the template precompiles,
            // so there is nothing for an author to act on and HED7030's sentence — "cannot be precompiled" — would
            // be false. The name is still never written, which is the whole point of asking.
            return _resolver.ClassifyModelType(model, out _) == Binding.SymbolTypeResolver.NameFault.None;
        }

        /// <summary>This template's observed compile, run on the first body the build could not type and not
        /// before: building the bundle is a whole C# compile of the compilation being built, and a template that
        /// never needs one must never pay for one.</summary>
        private Observe.ObservedDocument Observed()
        {
            if (_observeAttempted)
                return _observed;

            _observeAttempted = true;
            if (_observation == null)
                return null;

            var observation = _observation.Get(out _observationFailure);
            if (observation == null)
                return null;

            _observed = observation.Observe(_originalDocument, _modelSymbol, out _observationFailure);
            return _observed;
        }

        /// <summary>The body of a call whose hook the build has read — from an observed engine compile, or from the
        /// author's own <c>[ChildTemplateHost]</c> declaration: typed exactly as before, and carrying the typing it
        /// assumed so the hook's own answer can contradict it at registration.</summary>
        private bool TryTypedBody(string name, BodyModelSource bodySource, ITypeSymbol observedModel,
            OutputItem item, CallParameter cp,
            BodyContext bctx, SitePlan plan, out BodyClass body, out Refusal reason)
        {
            body = null;
            reason = null;
            ITypeSymbol bodyModel = observedModel;
            if (bodySource == BodyModelSource.Data)
            {
                // The host's own data value. A caller that IS typed but whose value's type cannot be said is the
                // one shape this role cannot carry: a guess there changes what the body evaluates to.
                bodyModel = CallSiteValueType(cp, bctx);
                if (bodyModel == null && !bctx.IsDynamic)
                {
                    reason = new Refusal(RefusalCategory.UnknowableValue,
                        "'" + name + "' body over a call value with no static type", item.Position);
                    return false;
                }

                if (bodyModel != null && bodyModel.TypeKind != TypeKind.Dynamic &&
                    !CanWriteTypeName(bodyModel, item.Position, out reason))
                    return false;
            }

            if (!BodyTypingRules.TryNestedBodyContext(bodySource, bctx, bodyModel, out var bodyCtx))
            {
                reason = new Refusal(RefusalCategory.HookBehavior,
                    "body model role " + bodySource + " on <" + name + "> has no emission", item.Position);
                return false;
            }

            if (item.Context != null)
            {
                body = BuildBody(item.ParameterTemplate, item.Context, bodyCtx, out reason);
                if (body == null)
                    return false;
            }

            // A body on the dynamic tier can still have been typed by the ENGINE — an `object` element, a
            // `:: dynamic` definition compiled against one call site's model — and that type is what the hook will
            // report, so it is the assumption to declare. `DynamicBodyModel` carries it; only where neither exists
            // is the claim genuinely dynamic.
            var assumed = bodyCtx.ModelSymbol ?? bodyCtx.DynamicBodyModel;
            if (!TryTypeExpr(assumed, item.Position, out var assumedExpr, out reason))
                return false;
            plan.AssumedDataTypeExpr = assumedExpr;
            return true;
        }

        /// <summary>
        /// The body of a call whose hook the build has <b>not</b> read — which is every third-party extension, and
        /// the case this whole seam exists for. Nothing here is spelled against a model type: the body carries no
        /// model cast, every member read becomes a <c>PrecompiledLateAccessor</c> bound to the engine's own walk
        /// once the hook answers, and every nested call site becomes a dependent whose typing that same answer
        /// fills in.
        /// <para>Returns false with a reason when some shape inside it cannot be written that way.
        /// <paramref name="canSubstitute"/> then says whether the per-call-site substitute may serve that shape —
        /// false means the reason stands and the whole template goes to the dynamic tier instead.</para>
        /// </summary>
        private bool TryTypeAgnosticBody(OutputItem item, BodyContext bctx, SitePlan plan, out BodyClass body,
            out Refusal reason, out bool canSubstitute)
        {
            body = null;
            reason = null;
            canSubstitute = false;
            plan.BodyTypeAgnostic = true;
            if (item.Context == null)
                return true;

            var sink = new LateBodySink();
            plan.Late = sink;
            _initTier++;
            try
            {
                body = BuildBody(item.ParameterTemplate, item.Context, bctx.AsTypeAgnostic(sink), out reason);
            }
            finally
            {
                _initTier--;
            }

            if (body != null)
                return true;

            // What the consumer's build configuration forbids, it forbids the substitute too: the substitute
            // compiles this call's own text as its own document under the request's own options, and an
            // expression-mode gate or a missing reference refuses that text wherever it is compiled. Emitted, the
            // call would raise the engine's own refusal at first render instead of at build, while the dynamic
            // tier reports it as the collected compile error it is — so the template goes there.
            if (reason != null && reason.Category == RefusalCategory.HostSetup)
                return false;

            // The substitute compiles this call's own text as its own document, so it sees no enclosing definition,
            // no ambient region fill scope and no active prop layout. A body that reaches for any of those must not
            // be handed to it — the template goes to the dynamic tier instead, where all three exist.
            var inner = reason?.Detail ?? "body of an unread hook";
            if (!SubstituteCanServe(item, bctx))
            {
                // Not the inner construct's category: what costs the TEMPLATE here is the substitute's own bound,
                // and the inner construct is only why the substitute was reached at all.
                reason = new Refusal(RefusalCategory.HookBehavior,
                    "body under an unread hook reaches an enclosing definition, fill scope or prop layout, which a " +
                    "fragment compiled as its own document would not see (" + inner + ")", item.Position);
                return false;
            }

            canSubstitute = true;
            plan.Late = null;
            return false;
        }

        /// <summary>
        /// Whether a fragment compiled as its own document would see what this body needs. It would not see the
        /// enclosing document's definitions, the ambient region fill scope, or the active prop layout — the bound
        /// <c>PrecompiledSiteFallbackExtension</c> states — so a body under any of them, or naming a definition of
        /// the enclosing document, is refused whole rather than substituted with something that resolves
        /// differently.
        /// </summary>
        private bool SubstituteCanServe(OutputItem item, BodyContext bctx)
        {
            if (bctx.Props != null || bctx.RegionHostProps != null || bctx.InSlot)
                return false;
            if (bctx.Fills != null && bctx.Fills.Count != 0)
                return false;
            return !NamesAnEnclosingDefinition(item.Context);
        }

        private bool NamesAnEnclosingDefinition(ParseContext context)
        {
            if (context == null)
                return false;
            foreach (var chain in context.OutputChains)
            {
                if (chain.Chain == null)
                    continue;
                foreach (var call in chain.Chain)
                {
                    if (!string.IsNullOrEmpty(call.ExtensionName) && _parse.DefenitionExists(call.ExtensionName))
                        return true;
                    if (call.Context != null && NamesAnEnclosingDefinition(call.Context))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Everything one call site hands <c>PrecompiledRuntime.Init</c>, gathered before any of it is
        /// written. A field-by-field mirror of <c>PrecompiledInitSite</c>; every <c>…Expr</c> member is already a
        /// C# expression, and <c>"null"</c> in a type position is the engine's <c>ExType.Dynamic</c> rather than an
        /// absent type.</summary>
        private sealed class SitePlan
        {
            public string Name;
            public BlockPosition Position;
            public BlockPosition DefinitionPosition;
            public string SourceText;
            public string BodyRaw;
            public string BodyShaped;
            public bool BodyNeedsLocals;
            public bool BodyTypeAgnostic;
            public string AssumedDataTypeExpr = "null";
            public string AssumedChainedTypeExpr = "null";
            public string DefinitionBodyRaw;
            public string DefinitionBodyShaped;
            public bool DefinitionBodyNeedsLocals;
            public string DefinitionAssumedDataTypeExpr = "null";
            public string DefinitionSlotTypeExpr = "null";
            public string DataTypeExpr = "null";
            public string ChainedTypeExpr = "null";
            public string ParentTypeExpr = "null";
            public string ModelTypeExpr = "null";
            public string RootModelTypeExpr = "null";
            public string SlotTypeExpr = "null";
            public Heddle.Precompiled.PrecompiledCallShape Shape;
            public bool HasPropArguments;
            public bool RootReference;
            public bool InsideDefinition;

            /// <summary>This call is a non-leading item of its chain (<c>OutputItem.IsChainedConsumer</c>).</summary>
            public bool IsChainedConsumer;

            /// <summary>The chain carries a producer to this call's right (the compiler's
            /// <c>hasProducerToRight</c>).</summary>
            public bool HasProducerToRight;

            /// <summary>The site field of that producer, when the producer runs a hook of its own; the runtime
            /// reads the type it returned off it rather than taking one this build predicted.</summary>
            public string ChainProducerSite;

            /// <summary>The extension bound here declares <c>[ChildTemplateHost]</c>; the runtime arms its child
            /// supply for this site's drain and gives the synthesized scope the option the hook reads to stamp
            /// its child's errors with an import origin.</summary>
            public bool HostsChildTemplate;

            public LateBodySink Late;

            /// <summary>The sink of the type-agnostic body this call sits <b>inside</b>, if any; the site registers
            /// on it so the enclosing hook's answer reaches this call's typing.</summary>
            public LateBodySink Owner;

            /// <summary>The member path whose resolved type is this call's <c>dataType</c>, for a site inside a
            /// type-agnostic body.</summary>
            public IReadOnlyList<string> DataTypePath;
        }

        /// <summary>The <c>typeof</c> expression for a type a site records, or <c>"null"</c> — which the site
        /// reads as <c>dynamic</c>, not as an absent type. A symbol this assembly may not name is refused rather
        /// than collapsed to dynamic: collapsing would hand the hook a different <c>dataType</c> than the engine
        /// hands it.</summary>
        private bool TryTypeExpr(ITypeSymbol type, BlockPosition position, out string expr, out Refusal reason)
        {
            reason = null;
            if (type == null || type.TypeKind == TypeKind.Dynamic || type.TypeKind == TypeKind.Error)
            {
                expr = "null";
                return true;
            }

            if (!CanWriteTypeName(type, position, out reason))
            {
                expr = null;
                return false;
            }

            expr = "typeof(" + SymbolTypeResolver.FullyQualified(type) + ")";
            return true;
        }

        /// <summary>The call's own source text — the span the document being walked holds at this call's position.
        /// A faulted <c>Init</c> compiles it as its own document, so it has to be the text the engine parsed rather
        /// than the file's.</summary>
        private string SourceTextAt(BlockPosition position)
        {
            var doc = _currentDoc;
            if (doc == null || position.StartIndex < 0 || position.Length < 0 ||
                position.StartIndex + position.Length > doc.Length)
                return null;
            return doc.Substring(position.StartIndex, position.Length);
        }

        /// <summary>Gathers the site for one bound-extension call: what the engine's compiler knows here, reduced
        /// to what a generated field initializer can carry.</summary>
        private bool TryPlanSite(string name, OutputChain chain, OutputItem item, CallParameter cp, BodyContext bctx,
            out SitePlan plan, out Refusal reason)
        {
            plan = null;
            var model = bctx.Late != null ? null : bctx.ModelSymbol;
            if (!TryTypeExpr(model, item.Position, out var modelExpr, out reason))
                return false;
            if (!TryTypeExpr(_modelSymbol, item.Position, out var rootExpr, out reason))
                return false;
            if (!TryTypeExpr(bctx.SlotType, item.Position, out var slotExpr, out reason))
                return false;

            var shape = CallShapeRules.Of(cp);
            var dataExpr = modelExpr;
            IReadOnlyList<string> dataPath = null;
            if (bctx.Late != null)
            {
                // Inside a type-agnostic body nothing about the model is known yet, so the only shapes that can be
                // written are the ones whose dataType is a function of it: the whole model, and a member path off
                // it, which travels as the path rather than as a type. A '::'-rooted path is not one of them — it
                // starts at the root model, which the build does know.
                if (shape == Heddle.Precompiled.PrecompiledCallShape.ModelPath && !cp.RootReference)
                {
                    dataPath = cp.ModelParameter;
                    dataExpr = "null";
                }
                else if (shape == Heddle.Precompiled.PrecompiledCallShape.ModelPath && cp.RootReference)
                {
                    if (!TryTypeExpr(CallSiteValueType(cp, bctx), item.Position, out dataExpr, out reason))
                        return false;
                }
                else if (shape != Heddle.Precompiled.PrecompiledCallShape.None &&
                         !(cp.NativeExpression is ThisNode))
                {
                    reason = new Refusal(RefusalCategory.HookBehavior,
                        "call shape " + shape + " inside a body typed by an unread hook", item.Position);
                    return false;
                }
            }
            else if (shape != Heddle.Precompiled.PrecompiledCallShape.None)
            {
                if (!TryTypeExpr(CallSiteValueType(cp, bctx), item.Position, out dataExpr, out reason))
                    return false;
            }

            plan = new SitePlan
            {
                Owner = bctx.Late,
                DataTypePath = dataPath,
                Name = name,
                // The engine's own position for this call, which is what it puts on the extension, on the witness
                // source item, and on any fault raised while compiling the item. The chain's span is a different
                // coordinate — it indexes the shaped working document, which is where the call's source text has
                // to be sliced from and nowhere a hook or a diagnostic should ever be pointed.
                Position = item.Position,
                SourceText = _chainSlot.ItemSourceText ??
                             SourceTextAt(chain != null ? chain.BlockPosition : item.Position),
                BodyRaw = item.ParameterTemplate,
                DataTypeExpr = dataExpr,
                ParentTypeExpr = modelExpr,
                ModelTypeExpr = modelExpr,
                RootModelTypeExpr = rootExpr,
                SlotTypeExpr = slotExpr,
                Shape = shape,
                HasPropArguments = cp != null && cp.PropArguments != null && cp.PropArguments.Count != 0,
                RootReference = cp != null && cp.RootReference,
                InsideDefinition = bctx.Props != null || bctx.InSlot,
                IsChainedConsumer = _chainSlot.IsChainedConsumer,
                HasProducerToRight = _chainSlot.HasProducerToRight,
                ChainProducerSite = _chainSlot.ProducerSite,
                ChainedTypeExpr = _chainSlot.ChainedTypeExpr ?? "null"
            };
            return true;
        }

        /// <summary>Writes the site object. Returns its field name.</summary>
        private string EmitInitSite(SitePlan plan)
        {
            var field = "S" + _initSiteCounter++;
            var w = _fieldDecls;
            // Internal rather than private: the manifest row names it, so the gauntlet can read what this call's
            // own hook answered at registration.
            w.Append("        internal static readonly global::Heddle.Precompiled.PrecompiledInitSite ").Append(field)
                .Append(" = new global::Heddle.Precompiled.PrecompiledInitSite\n        {\n");
            w.Append("            ExtensionName = ").Append(CSharpEscape.StringLiteral(plan.Name ?? string.Empty))
                .Append(",\n");
            w.Append("            PositionStart = ").Append(plan.Position.StartIndex)
                .Append(", PositionLength = ").Append(plan.Position.Length).Append(",\n");
            if (plan.DefinitionPosition.Length != 0)
                w.Append("            DefinitionPositionStart = ").Append(plan.DefinitionPosition.StartIndex)
                    .Append(", DefinitionPositionLength = ").Append(plan.DefinitionPosition.Length).Append(",\n");
            w.Append("            SourceText = ")
                .Append(plan.SourceText == null ? "null" : CSharpEscape.StringLiteral(plan.SourceText)).Append(",\n");
            if (plan.BodyRaw != null)
            {
                w.Append("            Body = new global::Heddle.Precompiled.PrecompiledInitBody { RawText = ")
                    .Append(CSharpEscape.StringLiteral(plan.BodyRaw)).Append(", ShapedText = ")
                    .Append(plan.BodyShaped == null ? "null" : CSharpEscape.StringLiteral(plan.BodyShaped))
                    .Append(", NeedsLocals = ").Append(plan.BodyNeedsLocals ? "true" : "false")
                    .Append(", TypeAgnostic = ").Append(plan.BodyTypeAgnostic ? "true" : "false")
                    .Append(", AssumedDataType = ").Append(plan.AssumedDataTypeExpr)
                    .Append(", AssumedChainedType = ").Append(plan.AssumedChainedTypeExpr).Append(" },\n");
            }

            if (plan.DataTypePath != null)
                w.Append("            DataTypePath = new string[] { ")
                    .Append(string.Join(", ", plan.DataTypePath.Select(CSharpEscape.StringLiteral)))
                    .Append(" },\n");
            if (plan.DefinitionBodyRaw != null)
            {
                w.Append("            DefinitionBody = new global::Heddle.Precompiled.PrecompiledInitBody { RawText = ")
                    .Append(CSharpEscape.StringLiteral(plan.DefinitionBodyRaw)).Append(", ShapedText = ")
                    .Append(plan.DefinitionBodyShaped == null
                        ? "null"
                        : CSharpEscape.StringLiteral(plan.DefinitionBodyShaped))
                    .Append(", NeedsLocals = ").Append(plan.DefinitionBodyNeedsLocals ? "true" : "false")
                    .Append(", AssumedDataType = ").Append(plan.DefinitionAssumedDataTypeExpr).Append(" },\n");
            }

            w.Append("            DefinitionSlotType = ").Append(plan.DefinitionSlotTypeExpr).Append(",\n");
            w.Append("            DataType = ").Append(plan.DataTypeExpr)
                .Append(", ChainedType = ").Append(plan.ChainedTypeExpr)
                .Append(", ParentType = ").Append(plan.ParentTypeExpr).Append(",\n");
            w.Append("            ModelType = ").Append(plan.ModelTypeExpr)
                .Append(", RootModelType = ").Append(plan.RootModelTypeExpr)
                .Append(", SlotType = ").Append(plan.SlotTypeExpr).Append(",\n");
            w.Append("            OutputProfile = global::Heddle.Data.OutputProfile.")
                .Append(_profileHtml ? "Html" : "Text")
                .Append(", ExpressionMode = global::Heddle.Data.ExpressionMode.").Append(_config.ExpressionMode)
                .Append(",\n");
            w.Append("            TrimDirectiveLines = ").Append(_config.TrimDirectiveLines ? "true" : "false")
                .Append(", MaxRecursionCount = ").Append(_config.MaxRecursionCount).Append(",\n");
            w.Append("            Namespaces = ").Append(NamespacesExpr()).Append(",\n");
            w.Append("            CallShape = global::Heddle.Precompiled.PrecompiledCallShape.").Append(plan.Shape)
                .Append(", HasPropArguments = ").Append(plan.HasPropArguments ? "true" : "false").Append(",\n");
            w.Append("            RootReference = ").Append(plan.RootReference ? "true" : "false")
                .Append(", InsideDefinition = ").Append(plan.InsideDefinition ? "true" : "false");
            if (plan.IsChainedConsumer)
                w.Append(",\n            IsChainedConsumer = true");
            if (plan.HasProducerToRight)
                w.Append(",\n            HasProducerToRight = true");
            if (plan.ChainProducerSite != null)
                w.Append(",\n            ChainProducer = ").Append(plan.ChainProducerSite);
            if (plan.HostsChildTemplate)
                w.Append(",\n            HostsChildTemplate = true");
            if (plan.Late != null && plan.Late.Accessors.Count != 0)
                w.Append(",\n            BodyAccessors = new global::Heddle.Precompiled.PrecompiledLateAccessor[] { ")
                    .Append(string.Join(", ", plan.Late.Accessors)).Append(" }");
            if (plan.Late != null && plan.Late.Dependents.Count != 0)
                w.Append(",\n            Dependents = new global::Heddle.Precompiled.PrecompiledInitSite[] { ")
                    .Append(string.Join(", ", plan.Late.Dependents)).Append(" }");
            w.Append("\n        };\n");
            _initSiteFields.Add(field);
            plan.Owner?.Dependents.Add(field);
            return field;
        }

        /// <summary>The <c>@using</c> namespace set in scope, as the array a site carries.</summary>
        private string NamespacesExpr()
        {
            if (_usings.Count == 0)
                return "null";
            return "new string[] { " + string.Join(", ", _usings.Select(CSharpEscape.StringLiteral)) + " }";
        }

        /// <summary>
        /// One bound extension's call site, bound by running the extension's <b>own</b> <c>InitStart</c> inside the
        /// consumer's assembly. Field typed <see cref="Heddle.Core.AbstractExtension"/> so a call the seam could not
        /// serve returns its substitute here and costs no per-render branch; a factory rather than a
        /// <c>new</c> argument so an extension assembly that moved faults this call site instead of the whole type
        /// initializer.
        /// </summary>
        private string AllocateInitExtension(string callName, ExtensionBinder.Info info, string siteField,
            string bodyName)
        {
            var field = "E" + _extensionCounter++;
            var bodyArg = bodyName != null ? "new " + bodyName + "()" : "null";
            var w = InitDecls;
            w.Append("        private static readonly global::Heddle.Core.AbstractExtension ").Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.Init(\n");
            w.Append("            () => new ").Append(info.GlobalName).Append("(), ").Append(siteField)
                .Append(", ").Append(bodyArg).Append(");\n");
            _extensionFields.Add(field);
            RecordExtensionBinding(callName, info.BareTypeName, info.AssemblyName);
            return field;
        }

        /// <summary>The per-call-site substitute the build writes itself: this call renders by compiling its own
        /// source text at first render while the rest of the template stays precompiled.</summary>
        private string AllocateSiteFallback(string siteField, string detail)
        {
            var field = "E" + _extensionCounter++;
            InitDecls.Append("        private static readonly global::Heddle.Core.AbstractExtension ").Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.SiteFallback(\n            ")
                .Append(siteField).Append(", ").Append(CSharpEscape.StringLiteral(detail)).Append(");\n");
            _extensionFields.Add(field);
            return field;
        }

        /// <summary>A member read of a type-agnostic body: the path only, bound to the engine's own accessor by
        /// <c>PrecompiledRuntime.Init</c> once the hook has said what the body's model is.</summary>
        private string AllocateLateAccessor(LateBodySink sink, IReadOnlyList<string> segments, bool rootRef)
        {
            var field = "LA" + _lateAccessorCounter++;
            _fieldDecls.Append("        private static readonly global::Heddle.Precompiled.PrecompiledLateAccessor ")
                .Append(field).Append(" =\n            new global::Heddle.Precompiled.PrecompiledLateAccessor(new string[] { ")
                .Append(string.Join(", ", segments.Select(CSharpEscape.StringLiteral))).Append(" }, rootRef: ")
                .Append(rootRef ? "true" : "false").Append(");\n");
            sink.Accessors.Add(field);
            return field;
        }

        /// <summary>Allocates a parameter-declaring extension's call site: the extension's own <c>InitStart</c>
        /// through <c>PrecompiledRuntime.InitExtension</c>, then the <c>ExtensionParameterCarrier</c> wrap with the
        /// frozen props prototype, the optional dynamic setters and the ordered parameter names — in that order,
        /// which is the order the engine's compiler applies them, so the render-type attributes stay on the inner
        /// extension.</summary>
        private string AllocateParameterizedExtension(string name, ExtensionBinder.Info info, string siteField,
            string propsFieldRef, string dynamicSettersRef, string parameterNamesRef, string bodyName,
            PropLayoutInfo layout)
        {
            var field = "E" + _extensionCounter++;
            var w = InitDecls;
            w.Append("        private static readonly global::Heddle.Core.AbstractExtension ").Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.InitExtension(\n");
            w.Append("            () => new ").Append(info.GlobalName).Append("(), ").Append(siteField)
                .Append(", body: ").Append(bodyName != null ? "new " + bodyName + "()" : "null")
                .Append(", props: ").Append(propsFieldRef)
                .Append(", dynamicSetters: ").Append(dynamicSettersRef)
                .Append(", parameterNames: ").Append(parameterNamesRef).Append(");\n");
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

        /// <summary>The symbol-side <c>PropConversion.CanConvertTypes</c>, flag and all. Only the boxing-allowed
        /// arm has a caller here now — prop defaults; the slot value's boxing-forbidden arm is asked by
        /// <c>OutExtension.InitStart</c> itself, against the runtime table, when it runs at registration. The flag
        /// stays because it is the runtime rule's own shape, and a rule core that drops the distinction its
        /// original makes is no longer the same rule.</summary>
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

        /// <summary>
        /// One call site of one definition. A layered definition needs nothing here: <paramref name="def"/> arrives
        /// from the same <c>ParseContext</c> lookup the engine makes, and the parser resolved the layers into that
        /// context already — a full override's body was isolated at its declaration, so a call to its own name in it
        /// carries the layer below, while every other context holds the most-derived one. Materializing is what the
        /// parser did; emitting the result is an ordinary definition call, its own body and its own position.
        /// </summary>
        private Call BuildDefinitionCall(DefinitionItem def, OutputChain chain, OutputItem item, CallParameter cp,
            BodyContext bctx, bool isFill, out Refusal reason)
        {
            reason = null;

            // Slot definitions bind through the slot-mode BindDefinition overload.
            bool slotMode = SlotRules.HasSlot(def);

            var layout = ResolvePropLayout(def);
            if (layout.Failed)
            {
                reason = new Refusal(RefusalCategory.UnnameableType, "unresolved prop type", def.Position);
                return null;
            }

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

            if (!BuildParamExpr(cp, bctx, RefStructUse.Model, out var paramExpr, out var usesModel, out reason, item.Position))
                return null;

            // Caller content typed by :: T (or slot type in slot mode); ambient fill scope stays active (lexical).
            // It is built BEFORE the body, because that is the order the engine compiles the two in, and the order
            // decides which of two call sites into one definition gets to type a body they share: the first to
            // arrive. Built the other way round, a call inside this caller content reached a shared definition
            // after this call's own body had already typed it, and the emitter typed the body from the wrong one
            // of the two — precompiling a template the engine refuses at compile time and throwing the engine's
            // own InvalidCastException at render instead.
            BodyClass callerBody = null;
            ITypeSymbol callerBodyModel = null;
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
                callerBodyModel = callerCtx.ModelSymbol ?? callerCtx.DynamicBodyModel;
            }

            // Compiled once per (definition identity, parse context); the fill scope is not part of that identity,
            // any more than it is part of the engine's.
            var bodyInfo = GetOrBuildDefinitionBody(def, defBodyCtx, out reason);
            if (bodyInfo == null || bodyInfo.Failed)
            {
                reason = reason ?? bodyInfo?.Reason ??
                    new Refusal(RefusalCategory.EngineParity, "definition body the shared piece walk abandoned",
                        item.Position);
                return null;
            }

            if (!TryPlanSite(def.Name, chain, item, cp, bctx, out var plan, out reason))
                return null;

            // The engine types the inner carrier by the definition's own model and the outer by the slot type when
            // there is one, so those are the two assumptions this site declares.
            if (!TryTypeExpr(defBodyCtx.ModelSymbol ?? defBodyCtx.DynamicBodyModel, item.Position,
                    out var innerAssumed, out reason))
                return null;
            if (!TryTypeExpr(slotType, def.Position, out var definitionSlotExpr, out reason))
                return null;

            plan.DefinitionPosition = def.Position;
            plan.DataTypeExpr = innerAssumed;
            plan.DefinitionSlotTypeExpr = definitionSlotExpr;
            plan.DefinitionBodyRaw = def.ParameterTemplate ?? string.Empty;
            plan.DefinitionBodyShaped = bodyInfo.Body.ShapedDocument;
            // Each carrier uses its own body's flag (not OR'd); two different documents, separate derivation.
            plan.DefinitionBodyNeedsLocals = bodyInfo.Body.HostsParticipant;
            plan.DefinitionAssumedDataTypeExpr = innerAssumed;
            if (callerBody != null)
            {
                plan.BodyShaped = callerBody.ShapedDocument;
                plan.BodyNeedsLocals = callerBody.HostsParticipant;
                if (!TryTypeExpr(callerBodyModel, item.Position, out var outerAssumed, out reason))
                    return null;
                plan.AssumedDataTypeExpr = outerAssumed;
            }

            var definitionSite = EmitInitSite(plan);
            var field = AllocateInitDefinition(definitionSite, BodyStrategyName(bodyInfo.Body),
                BodyStrategyName(callerBody), propsFieldRef, dynamicSettersRef);
            return MakeCall(field, paramExpr, usesModel, item.Position, definitionSite);
        }

        /// <summary>The static type of the value a call site passes, or null where the emitter has none. Null is
        /// "cannot say", never "no type"; the compilation's <c>dynamic</c> is the opposite, a definite "no static
        /// type", which is what the engine refuses as a slot value and hands a <c>:: dynamic</c> body as its
        /// model.</summary>
        private ITypeSymbol CallSiteValueType(CallParameter cp, BodyContext bctx)
        {
            // A body whose model the hook chooses has no build-time answer here, and "cannot say" is what every
            // gate reading this already treats as exempt.
            if (bctx.Late != null)
                return null;
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
                    ? _csharpTyper.TypeOf(cp.CSharpExpression, model, bctx.Root,
                        model == null ? _usings : EmbeddedExpressionUsings(model))
                    : null;

            // A chain call-parameter's value is the chain's render type, not the producer's own type: the engine
            // reads `callParameter.RenderType`, which is the last item's InitStart return, and every chain the
            // emitter can flatten ends in a plain carrier whose InitStart is the base one — `typeof(string)`.
            // Answering "cannot say" here let a non-enumerable producer past the @list gate.
            if (cp.ChainParameter != null && cp.ChainParameter.Count != 0)
                return _compilation.GetSpecialType(SpecialType.System_String);

            // A root reference resolves against the template's model on every tier — the body's typing has no
            // say — and an untyped root is the engine's dynamic accessor exit, a definite "no static type".
            if (cp.IsModelTypeParameter && cp.RootReference && cp.ModelParameter != null &&
                cp.ModelParameter.Length != 0 && !string.IsNullOrEmpty(cp.ModelParameter[0]))
                return _modelSymbol == null
                    ? _compilation.DynamicType
                    : ResolvedTypeOf(_modelSymbol, cp.ModelParameter);

            if (bctx.IsDynamic && model == null)
                return null;

            if (!cp.IsModelTypeParameter || cp.RootReference)
                return null;

            var segments = cp.ModelParameter;
            if (segments == null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
                return model;

            var slot = BodyTypingRules.PropShadowSlot(bctx.Props, segments[0]);
            if (slot != null)
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
        private bool AcceptedTypeSatisfied(ExtensionBinder.Info info, ITypeSymbol valueType, out string detail)
        {
            detail = null;
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
            detail = "value type '" + SymbolTypeResolver.FullyQualified(valueType) +
                     "' is not one of the accepted types [" + string.Join(", ", names) + "]";
            return false;
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

                    // A path hung off any target but `this` is typed only by the expression writer; this helper
                    // answers "cannot say" for it, the degrade an unproven value always gets. `this.` roots at
                    // the model, so it types like a bare path; `::` roots at the template's model whatever the
                    // body's scope is.
                    if ((path.Target != null && !(path.Target is ThisNode)) || BodyTypingRules.IsPropName(path, props))
                        return ComputedValue.None;
                    if (path.RootRef)
                    {
                        var rooted = _modelSymbol == null ? null : ResolvedTypeOf(_modelSymbol, path.Segments);
                        return rooted == null
                            ? ComputedValue.None
                            : new ComputedValue(rooted, SymbolFacts.Classify(rooted));
                    }

                    if (model == null || model.TypeKind == TypeKind.Dynamic)
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
                    var writer = new NativeExpressionWriter(_resolver, model, _modelSymbol, "m", _exports, TypeFacts,
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

        /// <summary>The static type of a prop-rooted path, or null when the path is not prop-rooted or its remaining
        /// segments do not resolve off the slot's declared type.</summary>
        private ITypeSymbol PropRootType(PathNode path, PropLayoutInfo props)
        {
            if (!BodyTypingRules.IsPropName(path, props))
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
            BlockPosition position, ref BodyContext defBodyCtx, out Refusal reason)
        {
            reason = null;
            var model = DeclaresDynamicModel(def)
                ? DynamicDefinitionBodyModel(cp, bctx)
                : ObjectDefinitionBodyModel(cp, bctx);
            if (model == null)
            {
                reason = new Refusal(RefusalCategory.UnknowableValue,
                    "definition body over a caller value this call site cannot type", position);
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
                            BodyTypingRules.PropShadowSlot(bctx.Props, segments[0]) != null;
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

        /// <summary>
        /// A call to an extension declaring the slot-projection role. Nothing about the projection is reproduced
        /// here — whether the call is in slot mode, whether a value belongs at it, and whether the value it passes
        /// is one the declared slot type can take are all read by the extension's own <c>InitStart</c> off the slot
        /// parameter type the site already carries (<c>SitePlan.SlotTypeExpr</c>, taken from the enclosing body
        /// context), run at static init through <c>PrecompiledRuntime.Init</c>. A hook that reports the engine's
        /// compile errors there faults the template, which is where the dynamic tier takes it too.
        /// <para>The one thing decided here is the shape the role cannot carry: a bodied call. Not because the hook
        /// misses it — inside a slot-declaring definition it reports the "value and a body" error for itself — but
        /// because outside one it accepts the body, and the two tiers then disagree about a body with no dynamic
        /// content in it. The engine compiles such a body to no processors at all, so the projection has no inner
        /// to render and emits the chained value alone; the build emits the same body as a real strategy, the
        /// supply installs it, and <c>[@out(){{BODY}}]</c> renders <c>[BODY]</c> where the engine renders
        /// <c>[]</c>. A rendered byte, not a preference.</para>
        /// </summary>
        private Call BuildSlotProjectionCall(string name, ExtensionBinder.Info info, OutputChain chain,
            OutputItem item, CallParameter cp, BodyContext bctx, out Refusal reason)
        {
            reason = null;
            if (!string.IsNullOrEmpty(item.ParameterTemplate))
            {
                reason = new Refusal(RefusalCategory.SlotChannel, "bodied @" + name, item.Position);
                return null;
            }

            return BuildBoundExtensionCall(name, info, chain, item, cp, bctx, out reason);
        }

        /// <summary>
        /// A call to an extension declaring the child-template-host role: its body names a second template, which
        /// it compiles and hosts. Nothing about that is reproduced here — the call takes the same route every
        /// other bound extension takes, so the name evaluation, the delayed-queue scheduling, the child's model
        /// typing and the error marking are all the hook's own, run at static init through
        /// <c>PrecompiledRuntime.Init</c>. The site is marked as hosting a child template, which is what arms the
        /// engine's child supply for the length of that call's drain.
        /// <para>The one thing decided here is the shape the role cannot carry: a call whose body names no
        /// template at all. The engine schedules no child for it either, but the whole point of the role is the
        /// child, so the template keeps the tier that has one.</para>
        /// </summary>
        private Call BuildChildTemplateCall(string name, ExtensionBinder.Info info, OutputChain chain,
            OutputItem item, CallParameter cp, BodyContext bctx, out Refusal reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(item.ParameterTemplate))
            {
                reason = new Refusal(RefusalCategory.PartialName, "empty @" + name + " name", item.Position);
                return null;
            }

            return BuildBoundExtensionCall(name, info, chain, item, cp, bctx, out reason,
                hostsChildTemplate: true);
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
        private DefBodyInfo GetOrBuildDefinitionBody(DefinitionItem def, BodyContext bodyCtx, out Refusal reason)
        {
            reason = null;
            var key = _bodyTyping.KeyOf(def.Context);
            if (!_bodyTyping.TryShareBodyTyping(key, ref bodyCtx, out var shareDetail))
            {
                reason = new Refusal(RefusalCategory.EngineParity, shareDetail, def.Position);
                return null;
            }

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

        /// <summary>The typing pass's only state — definition-body sharing keyed by parse-context identity —
        /// held as one <see cref="BodyTypingMemo"/> per emit; see it for why sharing is keyed this way.</summary>
        private readonly BodyTypingMemo _bodyTyping = new BodyTypingMemo();

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
            out Dictionary<string, DefinitionItem> fills, out Refusal reason)
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
            BlockPosition position, out BodyContext ctx, out Refusal reason)
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
            out Refusal reason)
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

            reason = new Refusal(RefusalCategory.EngineParity,
                "value type '" + SymbolTypeResolver.FullyQualified(valueType) +
                "' is not accepted by the definition model '" +
                SymbolTypeResolver.FullyQualified(declared) + "'", def.Position);
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
        private BodyContext DefinitionBodyContext(DefinitionItem def, out Refusal reason)
        {
            reason = null;
            var modelTypeName = def.ModelType;
            if (DeclaresDynamicModel(def))
                return new BodyContext(null, null, true, root: _modelSymbol);

            var sym = _resolver.ResolveModelType(modelTypeName, _usings);
            if (sym == null)
            {
                reason = new Refusal(RefusalCategory.UnnameableType,
                    "unresolved definition model type '" + modelTypeName + "'", def.Position);
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
                return new BodyContext(null, null, true, root: _modelSymbol);

            var fq = SymbolTypeResolver.FullyQualified(sym);
            return new BodyContext("(" + fq + ")", sym, false, root: _modelSymbol);
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

        private int _propsCounter;

        /// <summary>Builds props carriage: frozen prototype + dynamic setters for non-constant arguments.
        /// <paramref name="propsFieldRef"/> is "null" for prop-less; <paramref name="dynamicSettersRef"/> is "null" for all-constant.
        /// Falls back for unreproducible values or untypeable dynamic arguments.</summary>
        private bool TryBuildPropsPrototype(PropLayoutInfo layout, CallParameter cp,
            BodyContext bctx, out string propsFieldRef, out string dynamicSettersRef, out Refusal reason)
        {
            reason = null;
            propsFieldRef = "null";
            dynamicSettersRef = "null";
            if (layout.Count == 0)
            {
                if (cp.PropArguments != null && cp.PropArguments.Count != 0)
                {
                    reason = new Refusal(RefusalCategory.EngineParity, "named arguments on prop-less definition");
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
                    if (!seen.Add(arg.Name))
                    {
                        reason = new Refusal(RefusalCategory.DefinitionProps, "duplicate prop argument");
                        return false;
                    }

                    if (!layout.ByName.TryGetValue(arg.Name, out var slot))
                    {
                        reason = new Refusal(RefusalCategory.DefinitionProps, "unknown prop '" + arg.Name + "'");
                        return false;
                    }

                    if (arg.Value is LiteralNode lit && lit.LiteralError == null)
                    {
                        if (!TryFormatPropValue(slot.Type, true, lit.Value, out var expr))
                        {
                            reason = new Refusal(RefusalCategory.DefinitionProps,
                                "unreproducible prop value '" + arg.Name + "'");
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
                    reason = new Refusal(RefusalCategory.DefinitionProps,
                        "missing required prop '" + slot.Name + "'");
                    return false;
                }

                if (!TryFormatPropValue(slot.Type, true, slot.DefaultValue, out var expr, slot.DefaultSourceType))
                {
                    reason = new Refusal(RefusalCategory.DefinitionProps,
                        "unreproducible prop default '" + slot.Name + "'");
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
            out Refusal reason)
        {
            reason = null;
            setterExpr = null;
            var callerModel = bctx.ModelSymbol;
            // A prop-rooted argument needs no caller model — it reads the scope's props — so the absence of one is
            // only fatal when there is no layout to read either.
            if (callerModel == null && bctx.Props == null)
            {
                reason = new Refusal(RefusalCategory.DefinitionProps, "dynamic arg without a typed caller model");
                return false;
            }

            if (slot.Type == null)
            {
                reason = new Refusal(RefusalCategory.DefinitionProps, "dynamic arg with unresolved prop type");
                return false;
            }

            string conversionKeyword = null;
            if (arg.Value is PathNode pn)
            {
                // Prop-first, like every other reader of a path's first segment — and like the writer three lines
                // below, which has had the layout since it was given one. Typed off the model instead, the check and
                // the emission disagreed about which value this argument even is: the writer emitted the caller's
                // prop while the check approved the shadowed member's type, so a string went into an int-declared
                // slot the engine refuses outright.
                ITypeSymbol argType;
                if (pn.RootRef)
                {
                    // The engine compiles a '::' arg against RootScopeType; an untyped root is its own
                    // typed-model refusal, so degrading hands the template to the tier that raises it.
                    if (_modelSymbol == null)
                    {
                        reason = new Refusal(RefusalCategory.DefinitionProps,
                            "root-reference dynamic arg without a typed root model");
                        return false;
                    }

                    var res = _resolver.ResolvePath(_modelSymbol, pn.Segments);
                    if (res.Kind != SymbolTypeResolver.PathKind.Resolved)
                    {
                        reason = new Refusal(RefusalCategory.DefinitionProps,
                            "dynamic arg root path (" + res.Kind + ")");
                        return false;
                    }
                    argType = res.ResultType;
                }
                else if (BodyTypingRules.IsPropName(pn, bctx.Props))
                {
                    argType = PropRootType(pn, bctx.Props);
                    if (argType == null)
                    {
                        reason = new Refusal(RefusalCategory.DefinitionProps,
                            "dynamic arg reads a prop this call site cannot type");
                        return false;
                    }
                }
                else
                {
                    if (callerModel == null)
                    {
                        reason = new Refusal(RefusalCategory.DefinitionProps,
                            "model-rooted dynamic arg without a typed caller model");
                        return false;
                    }

                    var res = _resolver.ResolvePath(callerModel, pn.Segments);
                    if (res.Kind != SymbolTypeResolver.PathKind.Resolved)
                    {
                        reason = new Refusal(RefusalCategory.DefinitionProps,
                            "dynamic arg path (" + res.Kind + ")");
                        return false;
                    }
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
                        reason = new Refusal(RefusalCategory.DefinitionProps,
                            "dynamic arg needs an unprovable conversion");
                        return false;
                    }
                }
            }
            else if (slot.Type.SpecialType != SpecialType.System_Object)
            {
                // A non-path native argument: only safe without a conversion, i.e. when the prop takes object.
                reason = new Refusal(RefusalCategory.DefinitionProps, "non-path dynamic arg needing a typed prop");
                return false;
            }

            var writer = new NativeExpressionWriter(_resolver, callerModel, _modelSymbol, "m", _exports, TypeFacts,
                AllocateHopLocal, bctx.Props, _modelDeclaredDynamic, AllocateFunctionSite);
            var body = writer.WriteRoot(arg.Value);
            DrainUnresolvable(writer);
            if (body == null)
            {
                reason = new Refusal(RefusalCategory.DefinitionProps, "unwritable dynamic arg");
                return false;
            }
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

        /// <summary>
        /// A definition invocation's call site. The two carriers are built by the engine's own
        /// <c>CreateExtension</c> sequence — two <c>InitStart</c> runs, the outer over the caller content under the
        /// definition's slot type and the inner over the definition body under its own — so slot mode and the
        /// recursion limit come from the hook rather than from constants written here.
        /// <para>Both carriers are positioned at the <b>definition's declaration</b>, which is where
        /// <c>CompileFromDefenition</c> puts them and where the dynamic tier reports a fault against; the build
        /// used to position them at the call, so error coordinates move onto the engine's here.</para>
        /// </summary>
        private string AllocateInitDefinition(string siteField, string bodyName, string callerBodyName,
            string propsFieldRef, string dynamicSettersRef)
        {
            var field = "E" + _extensionCounter++;
            var bodyArg = bodyName != null ? "new " + bodyName + "()" : "null";
            var callerArg = callerBodyName != null ? "new " + callerBodyName + "()" : "null";
            var w = InitDecls;
            w.Append("        private static readonly global::Heddle.Core.AbstractExtension ").Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.InitDefinition(\n");
            w.Append("            ").Append(siteField).Append(", body: ").Append(bodyArg)
                .Append(", callerContent: ").Append(callerArg)
                .Append(", props: ").Append(propsFieldRef)
                .Append(", dynamicSetters: ").Append(dynamicSettersRef).Append(");\n");
            _extensionFields.Add(field);
            return field;
        }

        /// <summary>Caller-content typing for slot-mode definitions: ModelData = slot value (declared slot type).
        /// Dynamic slot types and unresolvable types degrade the template.</summary>
        private BodyContext SlotBodyContext(DefinitionItem def, out Refusal reason)
        {
            reason = null;
            // The shared base-chain walk (SlotRules), not this file's second copy of it.
            var slotName = SlotRules.SlotTypeName(def);
            if (slotName == null)
            {
                reason = new Refusal(RefusalCategory.SlotChannel, "slot definition without slot type", def.Position);
                return default;
            }

            var sym = _resolver.ResolveModelType(slotName, _usings);
            if (sym == null)
            {
                reason = new Refusal(RefusalCategory.UnnameableType, "unresolved slot type '" + slotName + "'",
                    def.Position);
                return default;
            }
            if (!CanWriteTypeName(sym, def.Position, out reason)) return default;
            var fq = SymbolTypeResolver.FullyQualified(sym);
            return new BodyContext("(" + fq + ")", sym, false, root: _modelSymbol);
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
            string siteField = null)
        {
            var (sl, sc) = _map.Map(position.StartIndex);
            var (el, ec) = _map.Map(position.StartIndex + position.Length);
            return new Call
            {
                ExtensionField = field, ParamExpr = paramExpr, UsesModelLocal = usesModel,
                SpanStartLine = sl, SpanStartCol = sc, SpanEndLine = el, SpanEndCol = ec,
                SiteField = siteField
            };
        }

        /// <summary>The sink a call-parameter's <b>value</b> lands in — what decides whether a ref-struct-ending
        /// path is recoverable. Only the rendered sink is: the carrier's own protocol is
        /// <c>value is string s ? s : value.ToString()</c>, so a ref-like value stringifies in place and never needs
        /// the box. Every other sink boxes, which a ref struct cannot survive, and the refusal names the sink.
        /// Expression-tier sinks (operand, function argument) are refused inside
        /// <see cref="NativeExpressionWriter"/> with their own sink-named reasons.</summary>
        internal enum RefStructUse
        {
            /// <summary>Output position: the unnamed carrier renders the value. A ref-struct-ending path is
            /// emitted as <c>.ToString()</c> in place — the exact bytes the carrier's boxed-value arm produces —
            /// composing with the carrier's encode-vs-raw rule unchanged.</summary>
            Rendered,

            /// <summary>An extension's or definition's positional value, boxed into <c>Scope.ModelData</c>
            /// (CS1503 territory). A slot projection's value lands here too: it is the projecting extension's own
            /// positional value, emitted into the same channel as every other call's.</summary>
            Model
        }

        private static string RefStructSinkReason(string pathKind) =>
            pathKind + " ends on a ref struct, which a model value cannot box (CS1503)";

        /// <param name="use">The sink the built value lands in; decides the ref-struct verdict per position.</param>
        /// <param name="callPosition">The call this parameter belongs to — where the runtime positions a
        /// prop-shadowing warning raised off the same read.</param>
        private bool BuildParamExpr(CallParameter cp, BodyContext bctx, RefStructUse use, out string paramExpr,
            out bool usesModel, out Refusal reason, BlockPosition callPosition)
        {
            reason = null;
            usesModel = false;
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
                        reason = new Refusal(RefusalCategory.MemberAccess,
                            "multi-hop prop read on dynamic/unresolved type", callPosition);
                        return false;
                    }

                    var rest = new string[segments.Length - 1];
                    System.Array.Copy(segments, 1, rest, 0, rest.Length);
                    var res = _resolver.ResolvePath(slot.Type, rest);
                    var plan = SelectValuePlan(slot.Type, rest, res, use, "prop multi-hop", callPosition);
                    if (plan.Kind == EmissionPlanKind.Refuse)
                    {
                        reason = plan.Refusal;
                        return false;
                    }

                    if (plan.Kind == EmissionPlanKind.EngineAccessor)
                    {
                        // The accessor converts the boxed prop value to the slot type itself, the engine's own
                        // first-hop conversion, so no cast is spelled here.
                        paramExpr = ExecuteValuePlan(plan,
                            AllocateMemberAccessor(slot.Type, rest) + "(" + propRead + ")");
                        return true;
                    }

                    var root = "((" + slot.TypeFq + ")" + propRead + ")";
                    paramExpr = ExecuteValuePlan(plan, MemberPathWriter.Write(root, MapHops(res), AllocateHopLocal));
                    return true;
                }

                // A '::' path roots at the TEMPLATE's model whatever body it sits in — the engine resolves it
                // against RootScopeType, which every nested and definition body inherits — so its tier follows
                // the root typing, not the body's.
                if (cp.RootReference)
                    return BuildRootRefParamExpr(segments, use, callPosition, out paramExpr, out reason);

                if (bctx.Late != null)
                {
                    // The read is written as a path and resolved by the engine's own member walk against the type
                    // the hook answers with — never through the DLR, whose visibility policy and hop rule are the
                    // dynamic tier's rather than the typed tier's the engine would take here.
                    paramExpr = "(object)" + AllocateLateAccessor(bctx.Late, segments, rootRef: false) +
                                ".Read(scope.ModelData, scope.ChainedData, " +
                                "global::Heddle.Precompiled.PrecompiledRuntime.RootModel(in scope))";
                    return true;
                }

                if (bctx.IsDynamic)
                {
                    // Dynamic tier: leading conditional guards first hop, dynamic ?. carries rest (DynamicParameter emits this).
                    paramExpr = WriteDynamicPath("m", segments);
                    usesModel = true;
                    return true;
                }

                if (bctx.ModelSymbol == null)
                {
                    reason = new Refusal(RefusalCategory.UnknowableValue, "unresolved model type", callPosition);
                    return false;
                }

                var resolution = _resolver.ResolvePath(bctx.ModelSymbol, segments);
                var pathPlan = SelectValuePlan(bctx.ModelSymbol, segments, resolution, use, "member path",
                    callPosition);
                if (pathPlan.Kind == EmissionPlanKind.Refuse)
                {
                    // HED7008: property-not-found on typed model (same as runtime HED0001).
                    if (resolution.Kind == SymbolTypeResolver.PathKind.Failed ||
                        resolution.Kind == SymbolTypeResolver.PathKind.Inaccessible)
                        RecordMemberFailure(bctx.ModelSymbol, segments, resolution);
                    reason = pathPlan.Refusal;
                    return false;
                }

                if (pathPlan.Kind == EmissionPlanKind.EngineAccessor)
                {
                    // Reads the scope channel directly — the engine's ModelParameter shape — so the model local
                    // stays unclaimed and the model cast happens inside the delegate, at the call.
                    paramExpr = ExecuteValuePlan(pathPlan,
                        AllocateMemberAccessor(bctx.ModelSymbol, segments) + "(scope.ModelData)");
                    return true;
                }

                paramExpr = ExecuteValuePlan(pathPlan,
                    MemberPathWriter.Write("m", MapHops(resolution), AllocateHopLocal));
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

                if (bctx.Late != null)
                {
                    // A computed expression's result type depends on an operand type that does not exist until the
                    // hook answers, and the DLR's numeric promotion is not the engine's for every operand pair —
                    // routing it there would be wrong rather than slow.
                    reason = new Refusal(RefusalCategory.NativeExpression,
                        "computed native expression in a body typed by an unread hook", callPosition);
                    return false;
                }

                if (_config.ExpressionMode == Heddle.Data.ExpressionMode.MemberPathsOnly)
                {
                    reason = new Refusal(RefusalCategory.HostSetup, "native expression under MemberPathsOnly",
                        callPosition);
                    return false;
                }

                // No pre-check for a typed model. The engine's own compiler tries the active prop layout BEFORE it
                // asks whether the scope has a static type, so an expression rooted at a prop needs no model at all
                // — and refusing every expression on the untyped tier dropped whole templates the engine renders.
                // The writer is the gate instead: given no model type it refuses any path that reads one, which is
                // the same answer by the same rule, and it never claims to use a model local it has not been given.
                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, _modelSymbol, "m",
                    _exports, TypeFacts, AllocateHopLocal, bctx.Props, _modelDeclaredDynamic,
                    AllocateFunctionSite);
                var expr = writer.WriteRoot(cp.NativeExpression);
                if (expr == null)
                {
                    // A path-shaped expression the writer cannot spell but the engine resolves escapes to the
                    // engine's own compilation of it, before the refusal (and its member-failure diagnostic, which
                    // would describe a node that now precompiles) is recorded. The whole expression must be the
                    // path: escaping an operand inside a larger expression would erase the static type the
                    // surrounding typed C# is built from.
                    if (writer.RefusalCategory == RefusalCategory.MemberAccess &&
                        TryNativeAccessorEscape(cp.NativeExpression, bctx, out paramExpr))
                        return true;

                    DrainUnresolvable(writer);
                    // The writer's own refusal names the construct that cost the tier; the generic phrase
                    // survives only for a bail with no specific story.
                    reason = writer.RefusalReason != null
                        ? new Refusal(writer.RefusalCategory, writer.RefusalReason, callPosition)
                        : new Refusal(RefusalCategory.NativeExpression, "unsupported native expression",
                            callPosition);
                    return false;
                }

                DrainUnresolvable(writer);

                RecordFunctionUses(writer);
                paramExpr = "(object)(" + expr + ")";
                usesModel = writer.UsedModel;
                return true;
            }

            if (!string.IsNullOrEmpty(cp.CSharpExpression))
            {
                // The expression-mode gate is asked first, inside a type-agnostic body as well as outside one: it
                // is the engine's own refusal of this text under these options rather than anything the body's
                // typing decides, and the answer below costs a call site instead of the template — which would put
                // the fragment in front of a substitute that compiles the same text under the same options and
                // meets the same gate at first render.
                if (bctx.Late != null && _config.ExpressionMode == Heddle.Data.ExpressionMode.FullCSharp)
                {
                    // The fragment's model parameter has to be SPELLED, and `object`/`dynamic` in its place changes
                    // overload resolution inside the expression.
                    reason = new Refusal(RefusalCategory.EmbeddedCSharp,
                        "embedded C# in a body typed by an unread hook", callPosition);
                    return false;
                }

                return BuildCSharpExpr(cp.CSharpExpression, bctx, out paramExpr, out reason);
            }

            // A single-item chain (@card((Cols)), @list(upper(Name))) reduces to its producer's expression — but not
            // to its producer's VALUE. The carrier the runtime wraps it in renders what it is given, so the value
            // that reaches the consuming extension is the carrier's text; that is why the engine types every chain
            // call-parameter `string` (the chain's render type is the last item's InitStart return, and the default
            // is typeof(string)). Flattened to the raw producer expression it was not byte-identical at all — only
            // invisible while the consumer printed it. See CarrierValue for what the two tiers disagreed on.
            if (cp.ChainParameter != null && cp.ChainParameter.Count == 1)
            {
                if (!BuildChainItemExpr(cp.ChainParameter[0], bctx, out var chainExpr, out usesModel, out _,
                        out reason))
                    return false;
                paramExpr = chainExpr;
                return true;
            }

            // A chain of them threads the same way an output chain does — `ChainedParameter` is
            // `TemplateChain.ProcessData`, which seeds from the chained channel and hands each item
            // `scope.Chain(running)` — except that every item processes and the LAST result is the value.
            if (cp.ChainParameter != null && cp.ChainParameter.Count > 1)
                return BuildChainParameterExpr(cp.ChainParameter, bctx, out paramExpr, out usesModel, out reason);

            reason = new Refusal(RefusalCategory.ChainCarrier, "call parameter with no producer", callPosition);
            return false;
        }

        /// <summary>A multi-item chain in call-parameter position (<c>@a(b():c())</c>), written as the engine runs
        /// it: one helper per item taking the chained scope — so an item's own parameter is read off that scope and
        /// not the ambient one — and one driver threading the running value through them left of right.</summary>
        private bool BuildChainParameterExpr(IReadOnlyList<OutputItem> items, BodyContext bctx, out string paramExpr,
            out bool usesModel, out Refusal reason)
        {
            paramExpr = null;
            usesModel = false;
            reason = null;
            var saved = _chainSlot;
            var steps = new List<string>();
            try
            {
                string producerSite = null;
                string producedType = "null";
                bool hasProducerToRight = false;
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i];
                    _chainSlot = new ChainSlot
                    {
                        HasProducerToRight = hasProducerToRight,
                        IsChainedConsumer = i != 0,
                        ProducerSite = producerSite,
                        ChainedTypeExpr = producedType,
                        ItemSourceText = ChainItemSourceText(_itemChain, item)
                    };

                    if (!BuildChainItemExpr(item, bctx, out var itemExpr, out var itemUses, out var itemSite,
                            out reason))
                        return false;

                    var method = "CH" + _chainMethodCounter++;
                    _methodDecls.Append("        private static object ").Append(method)
                        .Append("(in global::Heddle.Data.Scope scope)\n        {\n");
                    if (itemUses)
                        _methodDecls.Append("            var m = ").Append(bctx.ModelCast)
                            .Append("scope.ModelData;\n");
                    _methodDecls.Append("            return ").Append(itemExpr).Append(";\n        }\n");
                    steps.Add(method);

                    producerSite = itemSite;
                    producedType = itemSite == null ? "typeof(string)" : "null";
                    hasProducerToRight = true;
                }
            }
            finally
            {
                _chainSlot = saved;
            }

            var driver = "CH" + _chainMethodCounter++;
            _methodDecls.Append("        private static object ").Append(driver)
                .Append("(in global::Heddle.Data.Scope scope)\n        {\n");
            _methodDecls.Append("            object r = scope.ChainedData;\n");
            foreach (var step in steps)
                _methodDecls.Append("            r = ").Append(step).Append("(scope.Chain(r));\n");
            _methodDecls.Append("            return r;\n        }\n");
            paramExpr = driver + "(in scope)";
            return true;
        }

        /// <summary>Plan selection for a member-path value, from (resolution, sink) — ordered so
        /// <see cref="EmissionPlanKind.Refuse"/> is the LAST exit: a node may not refuse until every other plan
        /// has been considered. Three plans have selectors today; the seams between the others mark where each
        /// remaining plan joins the ladder when it gains one.</summary>
        private EmissionPlan SelectValuePlan(ITypeSymbol start, IReadOnlyList<string> segments,
            SymbolTypeResolver.PathResolution resolved, RefStructUse use, string pathKind, BlockPosition position)
        {
            if (resolved.Kind == SymbolTypeResolver.PathKind.Resolved)
            {
                // Direct: typed C# spells the value and the sink boxes it.
                if (!SymbolTypeResolver.EndsOnRefStruct(resolved))
                    return EmissionPlan.Direct;

                // Stringified: the rendered sink's own protocol (`value is string s ? s : value.ToString()`)
                // stringifies a ref-like value in place, so the box is never needed.
                if (use == RefStructUse.Rendered)
                    return EmissionPlan.Stringified;

                // Generic selector seam: an inference helper cannot carry a ref-like value either (CS9244).
                // LateBound is not a member-path plan: what it defers is a call TARGET, and a member path has
                // none — its selector lives where the calls are, in NativeExpressionWriter's WriteLateBoundCall.
                // EngineAccessor selector seam: the engine's accessor returns object — the very box being refused.
                return EmissionPlan.Refused(new Refusal(RefusalCategory.RefLikeSink,
                    RefStructSinkReason(pathKind), position));
            }

            // EngineAccessor: the path this compilation cannot spell — a referenced assembly's internal member
            // without IVT, an error-obsolete member, a member type with no writable name mid-chain — computed the
            // way the engine computes it, once into a static delegate. Selected only where the engine's own walk
            // proves the whole path resolves; every other non-resolution keeps degrading, which hands the template
            // to the tier whose success or failure is the contract.
            if (_config.NodeFallback && _resolver.EngineViewResolves(start, segments))
                return EmissionPlan.EngineAccessor;

            return EmissionPlan.Refused(new Refusal(RefusalCategory.MemberAccess,
                pathKind + " (" + resolved.Kind + ")", position));
        }

        /// <summary>Executes the selected plan over a built path expression — the writer half, which decides
        /// nothing: a stringified plan produces the string the carrier's boxed-value arm would have produced,
        /// handed to the same <c>is string</c> arm, so encode-vs-raw composition is untouched, and every other
        /// plan boxes the value as built.</summary>
        private static string ExecuteValuePlan(in EmissionPlan plan, string pathExpr) =>
            plan.Kind == EmissionPlanKind.Stringified
                ? "(object)((" + pathExpr + ").ToString())"
                : "(object)(" + pathExpr + ")";

        /// <summary>Accessor fields already allocated in this file, keyed by kind, start type and path — a path
        /// read from two call sites shares one delegate and one type-init construction.</summary>
        private readonly Dictionary<string, string> _accessorFields =
            new Dictionary<string, string>(System.StringComparer.Ordinal);

        private int _accessorCounter;

        /// <summary>The <see cref="EmissionPlanKind.EngineAccessor"/> field for a member-tier path: a
        /// <c>Func&lt;object, object&gt;</c> built once at type-init from the engine's own member resolution
        /// (<c>PrecompiledRuntime.MemberAccessor</c>), called with the boxed start value at the site.</summary>
        private string AllocateMemberAccessor(ITypeSymbol start, IReadOnlyList<string> segments) =>
            AllocateAccessorField("global::System.Func<object, object>",
                "MemberAccessor", start, segments, rootRefArg: null);

        /// <summary>The accessor field for a native-tier path: the engine's three-channel delegate shape
        /// (<c>model</c>, <c>chained</c>, <c>root</c>), built by <c>PrecompiledRuntime.NativeAccessor</c> the way
        /// the engine's expression compiler builds it.</summary>
        private string AllocateNativeAccessor(ITypeSymbol start, IReadOnlyList<string> segments, bool rootRef) =>
            AllocateAccessorField("global::System.Func<object, object, object, object>",
                "NativeAccessor", start, segments, rootRefArg: rootRef ? "true" : "false");

        /// <summary>
        /// The native tier's <see cref="EmissionPlanKind.EngineAccessor"/> selector: a value node that IS a member
        /// path — model-rooted, <c>this</c>-rooted or <c>::</c>-rooted, with no expression around it — whose hops
        /// the engine resolves, computed by the engine's own expression compilation into a static three-channel
        /// delegate. A prop-rooted read stays with the writer (the engine roots it at the props array, a different
        /// mechanism), and a path the engine's walk does not fully resolve keeps degrading, which reproduces the
        /// engine's compile-time refusal.
        /// </summary>
        private bool TryNativeAccessorEscape(ExprNode node, BodyContext bctx, out string paramExpr)
        {
            paramExpr = null;
            if (!_config.NodeFallback)
                return false;
            if (!(node is PathNode path) || path.Segments.Count == 0)
                return false;
            if (path.Target != null && !(path.Target is ThisNode))
                return false;
            // A body prop wins the first segment for a bare model-rooted path — the engine's own compiler asks
            // the active layout before the scope type, and a `this.`-rooted path never consults it.
            if (!path.RootRef && path.Target == null &&
                bctx.Props != null && bctx.Props.ByName.ContainsKey(path.Segments[0]))
                return false;

            var start = path.RootRef ? _modelSymbol : bctx.ModelSymbol;
            if (start == null || start.TypeKind == TypeKind.Dynamic)
                return false;
            if (!_resolver.EngineViewResolves(start, path.Segments))
                return false;

            paramExpr = "(object)(" + AllocateNativeAccessor(start, path.Segments, path.RootRef) +
                        "(scope.ModelData, scope.ChainedData, " +
                        "global::Heddle.Precompiled.PrecompiledRuntime.RootModel(in scope)))";
            return true;
        }

        private string AllocateAccessorField(string delegateType, string factory, ITypeSymbol start,
            IReadOnlyList<string> segments, string rootRefArg)
        {
            var startFq = SymbolTypeResolver.FullyQualified(start);
            var key = factory + "\0" + (rootRefArg ?? string.Empty) + "\0" + startFq + "\0" +
                      string.Join("\0", segments);
            if (_accessorFields.TryGetValue(key, out var existing))
                return existing;

            var field = "__acc" + _accessorCounter++;
            _fieldDecls.Append("        private static readonly ").Append(delegateType).Append(' ').Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.").Append(factory)
                .Append("(\n            typeof(").Append(startFq).Append("), new string[] { ");
            for (int i = 0; i < segments.Count; i++)
            {
                if (i != 0)
                    _fieldDecls.Append(", ");
                _fieldDecls.Append('"').Append(segments[i]).Append('"');
            }

            _fieldDecls.Append(" }");
            if (rootRefArg != null)
                _fieldDecls.Append(", rootRef: ").Append(rootRefArg);
            _fieldDecls.Append(");\n");
            _accessorFields[key] = field;
            return field;
        }

        /// <summary>The <c>::</c> member-path parameter: a typed root emits the null-safe hop chain off the root
        /// model read cast to the template's model (the engine's own RootScopeType conversion, InvalidCastException
        /// included), and an untyped root walks the per-segment DLR chain the engine's <c>RootDynamicParameter</c>
        /// compiles. Neither reads the model local, so the caller's <c>usesModel</c> stays false.</summary>
        private bool BuildRootRefParamExpr(string[] segments, RefStructUse use, BlockPosition callPosition,
            out string paramExpr, out Refusal reason)
        {
            reason = null;
            paramExpr = null;
            const string rootRead = "global::Heddle.Precompiled.PrecompiledRuntime.RootModel(in scope)";
            if (_modelSymbol == null)
            {
                paramExpr = WriteDynamicPath(rootRead, segments);
                return true;
            }

            var resolution = _resolver.ResolvePath(_modelSymbol, segments);
            var plan = SelectValuePlan(_modelSymbol, segments, resolution, use, "root member path", callPosition);
            if (plan.Kind == EmissionPlanKind.Refuse)
            {
                if (resolution.Kind == SymbolTypeResolver.PathKind.Failed ||
                    resolution.Kind == SymbolTypeResolver.PathKind.Inaccessible)
                    RecordMemberFailure(_modelSymbol, segments, resolution);
                reason = plan.Refusal;
                return false;
            }

            if (plan.Kind == EmissionPlanKind.EngineAccessor)
            {
                paramExpr = ExecuteValuePlan(plan,
                    AllocateMemberAccessor(_modelSymbol, segments) + "(" + rootRead + ")");
                return true;
            }

            var root = "((" + SymbolTypeResolver.FullyQualified(_modelSymbol) + ")" + rootRead + ")";
            paramExpr = ExecuteValuePlan(plan, MemberPathWriter.Write(root, MapHops(resolution), AllocateHopLocal));
            return true;
        }

        /// <summary>FullCSharp tier: the expression becomes a compiled fragment — the engine's one-line method
        /// shape (<c>CSharpClassTemplate.tcs</c>), written into this file's <c>namespace Heddle.Runtime</c> block
        /// and compiled by the consumer's own compiler. A top-level static method has exactly the engine's three
        /// parameter names in scope, so no emitter local (<c>m</c>, <c>scope</c>, <c>v0…</c>, <c>P0…</c>) can
        /// capture an identifier the engine would fail to bind — and the model cast happens at the call, the way
        /// <c>CompiledParameter</c> converts, so an <c>InvalidCastException</c> fires at the same render position,
        /// after the preceding pieces are written.</summary>
        private bool BuildCSharpExpr(string csharp, BodyContext bctx, out string paramExpr, out Refusal reason)
        {
            reason = null;
            paramExpr = null;

            if (_config.ExpressionMode != Heddle.Data.ExpressionMode.FullCSharp)
            {
                // Runtime rejects C# unless FullCSharp mode; dynamic path surfaces engine's gate.
                reason = new Refusal(RefusalCategory.HostSetup, "embedded C# outside FullCSharp mode");
                return false;
            }

            // An untyped body is spelled `dynamic` — but only where the engine's own scope is dynamic on every
            // compile, which an in-file @model (typed or `dynamic`) pins and nothing else does: with neither, the
            // engine types this template by whatever context each host hands it, a value the build does not hold.
            bool rootPinned = _modelSymbol != null || _modelDeclaredDynamic;
            if (bctx.ModelSymbol == null && !rootPinned)
            {
                reason = new Refusal(RefusalCategory.EmbeddedCSharp, "embedded C# without a typed model");
                return false;
            }

            var modelType = bctx.ModelSymbol ?? _compilation.DynamicType;

            // Every collected @using becomes a `using` directive of the code the ENGINE compiles for this
            // expression, so a body naming no namespace makes the engine refuse the whole template — and a body that
            // is not a name at all stops its compilation unit from parsing. Neither is a refusal this tier can
            // position against the .heddle file, so the template goes back to the tier whose diagnostic is the
            // contract instead of pre-compiling something the engine would never run.
            foreach (var ns in _usings)
            {
                if (_resolver.UsingDirectiveCompiles(ns))
                    continue;
                reason = new Refusal(RefusalCategory.EmbeddedCSharp,
                    "embedded C# under a @using naming no namespace ('" + ns + "')");
                return false;
            }

            var usings = EmbeddedExpressionUsings(modelType);

            // Does the expression compile at all? The engine asks Roslyn exactly this and refuses the template on
            // any error, and nothing here asked: a misspelt member, an unbalanced expression, a wrong argument
            // count, a `Where` with no `@using System.Linq` and an `[Obsolete(error: true)]` reference were all
            // pasted straight into the generated file, where they became the consumer's build errors against a
            // `.heddle` file with no Heddle diagnostic on them.
            if (!_csharpTyper.Compiles(csharp, modelType, bctx.Root, usings))
            {
                // A dynamic operation this compilation cannot compile at all is the consumer's wall, not the
                // expression's: the emitted fragment would hit the same CS0656 in the consumer's own build.
                reason = _csharpTyper.NeedsRuntimeBinder(csharp, modelType, bctx.Root, usings)
                    ? new Refusal(RefusalCategory.HostSetup,
                        "embedded C# needing a Microsoft.CSharp reference this compilation does not have")
                    : new Refusal(RefusalCategory.EmbeddedCSharp, "embedded C# the engine's compiler rejects");
                return false;
            }

            // An unpinned root is spelled `dynamic` only while the expression never reads it: read, its behaviour
            // is the host's context type, which is the value the build does not hold. The binder answers, not a
            // word search — a lambda parameter of the same name shadows the parameter and is no reference.
            if (!rootPinned && _csharpTyper.BindsRoot(csharp, modelType, bctx.Root, usings))
            {
                reason = new Refusal(RefusalCategory.EmbeddedCSharp,
                    "embedded C# reads root where no @model pins the root type");
                return false;
            }

            var method = "__cs" + _csharpFragments.Count;
            _csharpFragments.Add(new Binding.EmbeddedCSharpFragment.Method(method,
                Binding.EmbeddedCSharpFragment.TypeName(modelType),
                Binding.EmbeddedCSharpFragment.TypeName(bctx.Root), csharp));

            // The call passes the scope's three channels the way CompiledParameter.GetParameter does, casting at
            // the call site; the fragment's own body carries the engine's syntactic unchecked, so the consumer's
            // <CheckForOverflowUnderflow> cannot flip what the engine computes.
            var modelArg = bctx.ModelSymbol == null
                ? "scope.ModelData"
                : "(" + SymbolTypeResolver.FullyQualified(bctx.ModelSymbol) + ")scope.ModelData";
            var rootArg = bctx.Root == null
                ? "global::Heddle.Precompiled.PrecompiledRuntime.RootModel(in scope)"
                : "(" + SymbolTypeResolver.FullyQualified(bctx.Root) +
                  ")global::Heddle.Precompiled.PrecompiledRuntime.RootModel(in scope)";
            paramExpr = "global::Heddle.Runtime." + FragmentClassName + "." + method + "(" + modelArg +
                        ", scope.ChainedData, " + rootArg + ")";
            return true;
        }

        /// <summary>The embedded-expression fragments of this file, in allocation order; rendered by
        /// <see cref="RenderFile"/> into one <c>namespace Heddle.Runtime</c> block through the same builder the
        /// probe compiles.</summary>
        private readonly List<Binding.EmbeddedCSharpFragment.Method> _csharpFragments =
            new List<Binding.EmbeddedCSharpFragment.Method>();

        /// <summary>The namespaces the engine's <c>CSharpContext</c> accumulates across this template's embedded
        /// expressions — model and chained namespaces plus one-level generic arguments, in first-import order.</summary>
        private readonly List<string> _fragmentNamespaces = new List<string>();

        private string FragmentClassName => "__CSharp_" + _sanitizedName;

        /// <summary>The <c>using</c> set the engine compiles this expression under: every collected <c>@using</c>
        /// body, then the namespace accumulation — which this call advances with <paramref name="modelType"/>'s
        /// own namespaces first, the way <c>ParseAndGetResultType</c> imports before it compiles.</summary>
        private IReadOnlyList<string> EmbeddedExpressionUsings(ITypeSymbol modelType)
        {
            Binding.EmbeddedCSharpFragment.AddExpressionNamespaces(modelType, _fragmentNamespaces);
            var composed = new List<string>(_usings.Count + _fragmentNamespaces.Count);
            composed.AddRange(_usings);
            composed.AddRange(_fragmentNamespaces);
            return composed;
        }

        /// <summary>One item of a chain in call-parameter position, as the value the engine's
        /// <c>TemplateItem.ProcessData</c> yields for it.
        /// <para>The carrier arms — an unnamed item, a function, a late-bound name — are the engine's
        /// <c>EmptyExtension</c>, whose <c>ProcessData</c> stringifies the value it is given, so they carry
        /// <c>CarrierValue</c>. A named extension or definition is not a carrier: its own <c>ProcessData</c> is the
        /// value, boxed as it comes. Note the carrier here is the <b>unencoded</b> one whatever the profile — the
        /// engine's <c>chainParameter</c> arm takes the item's own name rather than resolving the profile's.</para>
        /// </summary>
        private bool BuildChainItemExpr(OutputItem inner, BodyContext bctx, out string paramExpr, out bool usesModel,
            out string siteField, out Refusal reason)
        {
            reason = null;
            paramExpr = null;
            usesModel = false;
            siteField = null;

            // A floor, not a live refusal: the grammar gives a `call` no subtemplate, and the one place a body is
            // attached to a chain item is the outblock's own first item — never a parameter chain's.
            if (!string.IsNullOrEmpty(inner.ParameterTemplate))
            {
                reason = new Refusal(RefusalCategory.ChainCarrier, "bodied chain item", inner.Position);
                return false;
            }

            var name = inner.ExtensionName;
            if (name.Length == 0)
            {
                if (!BuildParamExpr(inner.CallParameter, bctx, RefStructUse.Model, out var carried, out usesModel,
                        out reason, inner.Position))
                    return false;
                paramExpr = "global::Heddle.Precompiled.PrecompiledRuntime.CarrierValue(" + carried + ")";
                return true;
            }

            // Use same precedence as top-level dispatch (HeddleCompiler.CompileItem).
            var innerTarget = CallTargetRules.ResolveCallTarget(name, inner.CallParameter, null,
                _parse.DefenitionExists,
                n => _extensionBinder.TryResolve(n, out _),
                n => NativeExpressionWriter.IsDefaultFunction(n) || _exports.TryGet(n, out _));

            // A definition or a bound extension named here is compiled by the very same CompileItem the output
            // tier's items go through, so it takes the very same route — which is what makes a chain parameter's
            // items as capable as an output chain's rather than a second, poorer dispatch.
            if (innerTarget == CallTargetKind.Definition || innerTarget == CallTargetKind.Extension ||
                innerTarget == CallTargetKind.Fill)
            {
                if (!(BuildItemCall(_itemChain, inner, _itemCtx, bctx, out reason) is Call call))
                    return false;
                paramExpr = call.ExtensionField + ".ProcessData(scope.Model(" + call.ParamExpr + "))";
                usesModel = call.UsesModelLocal;
                siteField = call.SiteField;
                return true;
            }

            if (innerTarget == CallTargetKind.Function)
            {
                var callNode = BuildFunctionCallNode(name, inner.CallParameter, inner.Position);
                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, _modelSymbol, "m", _exports,
                    TypeFacts, AllocateHopLocal, bctx.Props, _modelDeclaredDynamic, AllocateFunctionSite);
                writer.TopLevelCall = callNode;
                var expr = writer.WriteRoot(callNode);
                DrainUnresolvable(writer);
                if (expr == null)
                {
                    reason = writer.RefusalReason != null
                        ? new Refusal(writer.RefusalCategory, writer.RefusalReason, inner.Position)
                        : new Refusal(RefusalCategory.FunctionBinding, "unsupported function '" + name + "'",
                            inner.Position);
                    return false;
                }

                RecordFunctionUses(writer);
                paramExpr = "global::Heddle.Precompiled.PrecompiledRuntime.CarrierValue((object)(" + expr + "))";
                usesModel = writer.UsedModel;
                return true;
            }

            // LateBound: the delegate-only remainder — a bodiless, function-shaped call this compilation
            // resolves to nothing, which is what a run-time-only registration looks like from here. The site
            // resolves it at first render through the engine's own ranker; only where that cannot be emitted
            // does the name still degrade the template to a HED7014 fallback marker.
            if (innerTarget == CallTargetKind.Unknown &&
                CallTargetRules.IsFunctionCompatibleShape(inner.CallParameter))
            {
                var lateNode = BuildFunctionCallNode(name, inner.CallParameter, inner.Position);
                var lateWriter = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, _modelSymbol, "m",
                    _exports, TypeFacts, AllocateHopLocal, bctx.Props, _modelDeclaredDynamic,
                    AllocateFunctionSite);
                lateWriter.TopLevelCall = lateNode;
                var lateExpr = lateWriter.WriteRoot(lateNode);
                DrainUnresolvable(lateWriter);
                if (lateExpr != null)
                {
                    RecordFunctionUses(lateWriter);
                    paramExpr = "global::Heddle.Precompiled.PrecompiledRuntime.CarrierValue((object)(" +
                                lateExpr + "))";
                    usesModel = lateWriter.UsedModel;
                    return true;
                }
            }

            reason = new Refusal(RefusalCategory.ChainCarrier, "chain item name '" + name + "'",
                inner.Position);
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
                    !SymbolTypeResolver.IsRefLikeOrRestricted(hop.Property),
                    SymbolTypeResolver.FullyQualified(hop.Receiver)));
            }

            return hops;
        }

        private string AllocateEmptyExtension(BlockPosition position, out Refusal reason)
        {
            reason = null;
            var field = "E" + _extensionCounter++;
            // Running profile (post-@profile-flip) decides encoding via shared OutputProfileRules rule.
            OutputProfileRules.ResolveUnnamedCarrier(
                _profileHtml ? Heddle.Data.OutputProfile.Html : Heddle.Data.OutputProfile.Text,
                hasBody: false, out var carrierKind, out var carrierRenderType);
            // The carrier is BOUND, not named: the shared rule gives the registry name the running profile
            // selects, and the binder answers which type that name resolves to in this compilation — so a host that
            // registers its own carrier under either name gets its own type spelled here.
            var registryName = OutputProfileRules.CarrierRegistryName(carrierKind);
            if (!_extensionBinder.TryResolve(registryName, out var carrierInfo))
            {
                reason = new Refusal(RefusalCategory.ExtensionBinding,
                    "no extension is registered for the unnamed carrier", position);
                return null;
            }

            if (!CanWriteExtensionTypeName(carrierInfo.TypeSymbol, position, out reason))
                return null;

            var renderType = "global::Heddle.Data.RenderType." + carrierRenderType;
            var (line, col) = _map.Map(position.StartIndex);
            _fieldDecls.Append("        private static readonly ").Append(carrierInfo.GlobalName).Append(' ')
                .Append(field).Append(" = global::Heddle.Precompiled.PrecompiledRuntime.Bind(\n");
            _fieldDecls.Append("            new ").Append(carrierInfo.GlobalName)
                .Append("(), body: null, ").Append(renderType)
                .Append(", needsLocals: false, line: ").Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            RecordExtensionBinding(registryName, carrierInfo.BareTypeName, carrierInfo.AssemblyName);
            return field;
        }

        /// <summary>
        /// The <see cref="EmissionPlanKind.LateBound"/> field for one call site: a
        /// <c>PrecompiledFunctionSite</c> constructed at type-init, which resolves the call ONCE at first render
        /// through the engine's own overload ranker and caches the bound delegate.
        /// <para>Keyed on the call's own identity — name, null-literal mask and document position — so a body
        /// emitted for two call sites shares one field and one bind, while two different calls on one name keep
        /// the separate bindings their different argument shapes earn.</para>
        /// <para>Also records the manifest's null-target <c>FunctionBindings</c> row for the name. That row is
        /// what lets the gauntlet move a request whose live registry cannot serve the name — unregistered, or
        /// registered as an extension — to the dynamic tier before any render, instead of the site meeting it.</para>
        /// </summary>
        private string AllocateFunctionSite(string name, int nullLiteralMask, bool inExpression,
            BlockPosition position)
        {
            var key = name + "\u0000" + nullLiteralMask.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      "\u0000" + (inExpression ? "e" : "t") + "\u0000" + position.StartIndex + ":" + position.Length;
            if (_functionSiteFields.TryGetValue(key, out var existing))
                return existing;

            var field = "FN" + _functionSiteCounter++;
            _fieldDecls.Append("        private static readonly global::Heddle.Precompiled.PrecompiledFunctionSite ")
                .Append(field).Append(" =\n");
            _fieldDecls.Append("            new global::Heddle.Precompiled.PrecompiledFunctionSite(")
                .Append(CSharpEscape.StringLiteral(name)).Append(", ")
                .Append(nullLiteralMask.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(", ")
                .Append(inExpression ? "true" : "false").Append(", ")
                .Append(_parse.DefenitionExists(name) ? "true" : "false").Append(", ")
                .Append(position.StartIndex).Append(", ").Append(position.Length).Append(");\n");

            _functionSiteFields[key] = field;
            return field;
        }

        /// <summary>Late-bound site fields already emitted in this file, keyed by call identity.</summary>
        private readonly Dictionary<string, string> _functionSiteFields =
            new Dictionary<string, string>(System.StringComparer.Ordinal);

        private int _functionSiteCounter;

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
        private void RecordExtensionBinding(string name, string type, string assembly,
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

            // HED1018, FORWARDED: the writer proved a constant division by zero — the very fact the engine
            // refuses at compile time under this id — so the build error carries the engine's id and its
            // catalog sentence rather than a build-tier twin. The template still degrades (the entry is never
            // emitted), which no longer matters: the error fails the build the way the engine's does.
            foreach (var division in writer.DivisionsByConstantZero)
            {
                var seenKey = division.Operator + "@" + division.Position.StartIndex;
                if (_seenZeroDivisions.Add(seenKey))
                {
                    HeddleDiagnosticCatalog.TryGet(HeddleDiagnosticIds.DivisionByConstantZero, out var info);
                    _diagnostics.Add(new EmitDiagnostic(
                        GeneratorDiagnostics.Forwarded(HeddleDiagnosticIds.DivisionByConstantZero,
                            isWarning: false),
                        division.Position,
                        string.Format(System.Globalization.CultureInfo.InvariantCulture, info.MessageFormat,
                            division.Operator)));
                }
            }

            // The HED1018 channel, generalized: refusals the writer PROVED the engine repeats at its own
            // template compile carry the engine's id and the engine's sentence forward as build errors. The
            // template still degrades (the entry is never emitted), which no longer matters: the error fails
            // the build the way the engine's fails the template compile.
            foreach (var refusal in writer.EngineRefusals)
            {
                var seenKey = refusal.Id + "@" + refusal.Position.StartIndex;
                if (_seenEngineRefusals.Add(seenKey))
                    _diagnostics.Add(new EmitDiagnostic(
                        GeneratorDiagnostics.Forwarded(refusal.Id, isWarning: false),
                        refusal.Position, refusal.Message));
            }
        }

        private readonly HashSet<string> _seenEngineRefusals = new HashSet<string>(System.StringComparer.Ordinal);

        private readonly HashSet<string> _seenZeroDivisions = new HashSet<string>(System.StringComparer.Ordinal);

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
        /// The model-type refusal for a spelling that carries a type parameter — an open generic definition such as
        /// <c>List`1</c>, a type nested in one, or an array of either. It is <b>not</b> the csc wall the general
        /// unnameable verdict describes, and a generic entry class would not recover it: the dynamic tier serves no
        /// such template <em>at all</em>, so a build that emitted one would render where the engine refuses.
        /// <para>Two independent walls prove it, and both are in the engine rather than in the C# the build would
        /// write. <c>HeddleTemplate.Generate</c> gates every render on
        /// <c>ScopeType.Type.IsInstanceOfType(data)</c>, and no value is ever an instance of a generic type
        /// <em>definition</em> — every model, including the obvious <c>List&lt;int&gt;</c>, is refused with the
        /// engine's own type-mismatch fault. And a template that reads one member never reaches render:
        /// <c>ModelParameter.GetPropertyChainAccessor</c> builds an <c>Expression.Convert</c> to the scope type,
        /// which <c>System.Linq.Expressions</c> rejects for an open generic, so the engine's compile fails with
        /// HED0005 instead.</para>
        /// <para>So the category is <see cref="RefusalCategory.EngineParity"/>: the degrade hands the template to
        /// the tier whose refusal is the contract, which is the only outcome that keeps the two tiers agreeing.
        /// Null where the fault was something else, and the general verdict stands.</para>
        /// </summary>
        private static Refusal OpenGenericModelRefusal(ITypeSymbol model, BlockPosition position)
        {
            if (!Binding.SymbolTypeFacts.ContainsGenericParameters(model))
                return null;
            return new Refusal(RefusalCategory.EngineParity,
                "model type '" + SymbolTypeResolver.FullyQualified(model) + "' carries a type parameter: the " +
                "dynamic tier accepts no model value for an open generic type and cannot build a member accessor " +
                "for one, so precompiling it would render where the engine refuses", position);
        }

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
        private bool CanWriteTypeName(ITypeSymbol type, BlockPosition position, out Refusal reason)
        {
            if (ReportUnnameable(_resolver.ClassifyModelType(type, out var detail), type, position))
            {
                reason = null;
                return true;
            }

            reason = new Refusal(RefusalCategory.UnnameableType, detail, position);
            return false;
        }

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
        private bool CanWriteExtensionTypeName(ITypeSymbol type, BlockPosition position, out Refusal reason)
        {
            if (ReportUnnameable(_resolver.ClassifyTypeName(type, out var detail), type, position))
            {
                reason = null;
                return true;
            }

            reason = new Refusal(RefusalCategory.UnnameableType, detail, position);
            return false;
        }

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

        /// <summary>Records manifest <c>FunctionBindings</c> rows for bound default/exported functions, and the
        /// null-target row every late-bound name needs so the gauntlet can check the live registry for it.</summary>
        private void RecordFunctionUses(NativeExpressionWriter writer)
        {
            foreach (var late in writer.LateBoundFunctions)
                RecordFunctionBinding(late.Name, null, 0);
            foreach (var fn in writer.UsedDefaultFunctions)
                RecordFunctionBinding(fn, DefaultFunctionTable.ShimTargetTypeName,
                    NativeExpressionWriter.DefaultOverloadCount(fn));
            foreach (var ex in writer.UsedExports)
                RecordFunctionBinding(ex.Name, ex.Aqn, ex.OverloadCount);
            // A collided name's shim row carries only the built-in overloads exact-signature exports left in
            // place — the count the live merged registry actually holds on the shim target.
            foreach (var fn in writer.UsedCollidedBuiltIns)
                RecordFunctionBinding(fn.Name, DefaultFunctionTable.ShimTargetTypeName, fn.OverloadCount);
        }

        private string RenderFile(string modelType, BodyClass root)
        {
            var w = new CodeWriter();
            w.Raw("// <auto-generated/>");
            // No relativity comment here: the #line form is marked by the manifest row's `linePathForm`,
            // which a symbolizer/IDE/LSP can actually read, and duplicating it as prose would be two carriers
            // for one fact.
            w.Raw("#pragma warning disable");
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
                PieceWriter.EmitPiece(w, i, _pieces[i], _config.EmitUtf8Pieces, utf8Supported: true,
                    utf8LiteralSyntax: ConsumerParsesUtf8Literals());
            w.Line();

            foreach (var line in FieldDeclarations().Split('\n'))
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
                // A body whose call site was handed null instead of a strategy is referenced by nothing.
                if (body.Suppressed)
                    continue;
                EmitBodyClass(w, body);
                w.Line();
            }

            w.Outdent();
            w.Line("}");
            w.Outdent();
            w.Line("}");

            // The embedded expressions, compiled the engine's way: one method per expression in the engine's
            // enclosing namespace, under the using set its CSharpContext collects, written by the same builder the
            // typer's probe compiles — so the compilation the probe answered for is the one the consumer builds.
            if (_csharpFragments.Count != 0)
            {
                w.Line();
                var usings = new List<string>(_usings.Count + _fragmentNamespaces.Count);
                usings.AddRange(_usings);
                usings.AddRange(_fragmentNamespaces);
                var block = Binding.EmbeddedCSharpFragment.Build(usings, _csharpFragments, FragmentClassName);
                foreach (var line in block.TrimEnd('\n').Split('\n'))
                    w.Raw(line);
            }

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
                else if (seg is ChainCall chain)
                {
                    EmitChainSteps(w, chain, render: true);
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
            var concatParts = new List<string>();
            int vIndex = 0;
            foreach (var seg in body.Segments)
            {
                if (seg is Piece p)
                {
                    concatParts.Add("P" + p.Index);
                }
                else if (seg is ChainCall chain)
                {
                    EmitChainSteps(w, chain, render: false);
                    var cv = "v" + vIndex++;
                    w.Raw("#line hidden");
                    w.Line($"var {cv} = {chain.Local} as string ?? string.Empty;");
                    concatParts.Add(cv);
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

        /// <summary>
        /// A chain, statement for statement as <c>TemplateChain</c> runs it: the chained channel seeds the running
        /// value, every item is handed <c>scope.Chain(running)</c>, and the last one — the leftmost in source —
        /// renders where the others process. The rendering arm is <c>RenderData</c>'s; the processing arm is
        /// <c>ProcessData</c>'s, which runs every item through <c>ProcessData</c> and keeps the last result.
        /// </summary>
        private void EmitChainSteps(CodeWriter w, ChainCall chain, bool render)
        {
            w.Raw("#line hidden");
            w.Line($"object {chain.Local} = scope.ChainedData;");
            for (int i = 0; i < chain.Steps.Count; i++)
            {
                var step = chain.Steps[i];
                EmitLineSpanRaw(w, step.SpanStartLine, step.SpanStartCol, step.SpanEndLine, step.SpanEndCol);
                if (render && i == chain.Steps.Count - 1)
                    w.Line($"{step.RenderMethod}(scope.Chain({chain.Local}));");
                else
                    w.Line($"{chain.Local} = {step.ProcessMethod}(scope.Chain({chain.Local}));");
            }
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
                        : nameof(Heddle.Precompiled.PrecompiledLinePathForm.TemplatePath)));

            // Written only when it is true, so a template that pins its own model keeps the row it has always had
            // and the wider constructor appears exactly where it says something. The init-site row is the same
            // shape of decision: it appears only for a template that binds a hook, and it forces the ambient flag
            // to be written because the two live on one constructor.
            var sites = marker ? null : InitSitesArray();
            if (sites == null)
                sb.Append(_modelDeclaredInTemplate ? ")" : ",\n    modelTypeIsAmbient: true)");
            else
                sb.Append(",\n    modelTypeIsAmbient: " + (_modelDeclaredInTemplate ? "false" : "true") +
                    ",\n    initSites: " + sites + ")");
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

        /// <summary>The call sites whose hooks ran at registration, so the gauntlet can read their answers.
        /// <c>null</c> for a template that binds none, which keeps its manifest row exactly the shape it had.</summary>
        private string InitSitesArray()
        {
            if (_initSiteFields.Count == 0)
                return null;
            return "new global::Heddle.Precompiled.PrecompiledInitSite[] { " +
                   string.Join(", ", _initSiteFields.Select(f => $"global::{_namespace}.{_sanitizedName}.{f}")) + " }";
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
                .Select(b => "new global::Heddle.Precompiled.PrecompiledFunctionBinding(" +
                             CSharpEscape.StringLiteral(b.Name) + ", " +
                             // A late-bound site records no target: the build knew the call shape and not the
                             // registration, which is exactly what a null target means to the gauntlet.
                             (b.Target == null ? "null" : CSharpEscape.StringLiteral(b.Target)) + ", " +
                             b.OverloadCount + ")")));
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
