using System.Collections.Generic;
using System.Linq;
using System.Text;
using Heddle.Generator.Binding;
using Heddle.Generator.Diagnostics;
using Heddle.Generator.Pipeline;
using Heddle.Language;
using Heddle.Language.Expressions;
using Heddle.Precompiled;
using Heddle.Strings.Core;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// The per-template structural emitter (phase 7 WI4/WI6): turns a parsed <see cref="ParseContext"/> into the
    /// <c>{SanitizedName}.g.cs</c> compilation unit — a public entry class, the piece table, the pre-constructed
    /// bound extension instances, and one <c>IProcessStrategy</c> body per compiled body — in the exact shape the
    /// runtime funnel (<c>PrecompiledRuntime</c>) renders, so both backends produce byte-identical output
    /// (D20). Constructs it can not yet emit make the template "unsupported": no <c>.g.cs</c>, no manifest entry,
    /// the render takes the unchanged dynamic path (safe incremental growth of the supported family).
    /// </summary>
    internal sealed class TemplateEmitter
    {
        private readonly string _key;
        private readonly string _sanitizedName;
        private readonly string _namespace;
        private readonly string _cleanDocument;
        private readonly string _originalDocument;
        private readonly string _sourcePath;
        private readonly ParseContext _parse;
        private readonly GlobalConfig _config;
        private readonly LineMapper _map;
        private readonly SymbolTypeResolver _resolver;
        private readonly FunctionExportResolver _exports;
        private readonly ExtensionBinder _extensionBinder;

        // Directives extracted from the document.
        private string _modelTypeText;   // trimmed @model body; null when no @model
        private BlockPosition _modelDirectivePosition;   // the @model(){{…}} span (for HED7007)
        private ITypeSymbol _modelSymbol;
        private readonly List<string> _usings = new List<string>();

        // Manifest accumulation.
        private readonly List<(string Name, string Type, string Assembly)> _extensionBindings =
            new List<(string, string, string)>();
        private readonly List<(string Name, string Target, int OverloadCount)> _functionBindings =
            new List<(string, string, int)>();

        // Extension-field allocation, document order.
        private readonly List<string> _extensionFields = new List<string>();
        private readonly StringBuilder _fieldDecls = new StringBuilder();
        private readonly StringBuilder _methodDecls = new StringBuilder();   // dynamic prop-arg evaluators (phase 5 D8)
        private int _extensionCounter;
        private int _dynEvalCounter;
        private int _dynSettersCounter;

        // OQ1 delegate-only remainder (D21): function names resolvable at build from neither the default table nor
        // a referenced export. Their presence degrades the template to a HED7014 fallback-marker entry.
        private readonly List<(string Name, BlockPosition Position)> _unresolvableFunctions =
            new List<(string, BlockPosition)>();

        // Emitter-produced Roslyn diagnostics (HED7005 surrogate warning, HED7006/HED7015 extension binding) with
        // their .heddle span; the generator reports each via ToLocation. Collected regardless of the emit outcome.
        private readonly List<EmitDiagnostic> _diagnostics = new List<EmitDiagnostic>();

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
            FunctionExportResolver exports = null, string sourcePath = null)
        {
            _key = key;
            _sanitizedName = sanitizedName;
            _namespace = generatedNamespace;
            _cleanDocument = cleanDocument;
            _originalDocument = originalDocument;
            _sourcePath = sourcePath;
            _parse = parse;
            _config = config;
            _map = new LineMapper(originalDocument);
            _resolver = new SymbolTypeResolver(compilation);
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
        }

        private bool IsHtml => string.Equals(_config.OutputProfile, "Html", System.StringComparison.Ordinal);

        // The running output profile (phase 2 D4): starts at the compile-time OutputProfile and flips in document
        // order at each @profile(){{html|text}} directive, per compile context (body). Drives which unnamed carrier
        // (EmptyExtension / EmptyHtmlExtension) subsequent output binds — the emitter's reimplementation of
        // ProfileExtension.InitStart's context.OutputProfile flip (README D22). The options fingerprint keeps the
        // compile-time profile (never the post-flip value).
        private bool _profileHtml;

        public Result Emit(string contentHash)
        {
            _profileHtml = IsHtml;
            ExtractDirectives();
            bool isDynamic = _modelTypeText == null ||
                             string.Equals(_modelTypeText, "dynamic", System.StringComparison.Ordinal);
            string modelType = isDynamic ? "object" : _modelTypeText;
            if (!isDynamic)
            {
                _modelSymbol = _resolver.ResolveModelType(_modelTypeText, _usings);
                if (_modelSymbol != null)
                    modelType = SymbolTypeResolver.FullyQualified(_modelSymbol);
                else if (IsPlainTypeName(_modelTypeText) && !_resolver.TypeNameExistsAnywhere(_modelTypeText))
                    // Milestone 2 (HED7007): a declared @model type that resolves as no symbol AND matches no type name
                    // anywhere in the reference closure is a genuine typo/unresolvable symbol. The existence guard is
                    // the reconciliation the milestone requires: a bare name the runtime resolves by assembly scan
                    // (namespace-less, no @using) still degrades safely to the dynamic path rather than false-erroring.
                    // Gated to plain type-name syntax so exotic forms (open generics, arrays) never false-error either.
                    _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.UnresolvableModelType,
                        _modelDirectivePosition, _modelTypeText));
            }

            // Build the body tree (Body0 = document root; extension bodies become nested bodies).
            var rootCtx = new BodyContext(isDynamic ? null : "(" + modelType + ")", _modelSymbol, isDynamic);
            var root = BuildBody(_cleanDocument, _parse, rootCtx, out var reason);
            if (root == null)
            {
                // OQ1 remainder (D21): the build failed on a function resolvable from neither the default table nor a
                // referenced export — degrade to a HED7014 fallback marker (warning + marker manifest entry, no code).
                if (_unresolvableFunctions.Count != 0)
                {
                    return new Result
                    {
                        Emitted = false, IsMarker = true, IsDynamic = isDynamic, ModelTypeText = modelType,
                        UnresolvableFunctions = DedupeUnresolvable(), Diagnostics = _diagnostics,
                        ManifestEntry = BuildMarkerManifestEntry(contentHash, isDynamic, modelType)
                    };
                }

                return new Result { Emitted = false, UnsupportedReason = reason, Diagnostics = _diagnostics };
            }

            // D15 / HED7005: a static piece with an unpaired surrogate stays legal for string output (the char is
            // preserved), but the compiler rejects it in a "…"u8 literal — warn and suppress the u8 twin (PieceWriter
            // already omits it). Validation runs regardless of the HeddleEmitUtf8Pieces toggle. Position = the first
            // lone surrogate in the source document (static text is preserved verbatim into the pieces).
            if (_pieces.Any(PieceWriter.HasLoneSurrogate))
            {
                var idx = IndexOfFirstLoneSurrogate(_originalDocument);
                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.SurrogatePiece,
                    new BlockPosition(idx < 0 ? 0 : idx, 1), _sourcePath ?? _key));
            }

            var source = RenderFile(modelType, root);
            var manifest = BuildManifestEntry(contentHash, isDynamic, modelType);
            return new Result
            {
                Emitted = true, Source = source, ManifestEntry = manifest, Diagnostics = _diagnostics,
                IsDynamic = isDynamic, ModelTypeText = modelType
            };
        }

        private static bool IsDirectiveName(string name) =>
            name == "model" || name == "using" || name == "import" || name == "profile";

        private bool IsZeroOutput(OutputChain chain)
        {
            var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
            return leftmost != null && IsDirectiveName(leftmost.ExtensionName);
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

        // ---- Body construction (recursive: Body0 = document root; extension bodies become nested bodies) ----

        private readonly struct BodyContext
        {
            public BodyContext(string modelCast, ITypeSymbol modelSymbol, bool isDynamic, PropLayoutInfo props = null,
                bool inSlot = false)
            {
                ModelCast = modelCast;
                ModelSymbol = modelSymbol;
                IsDynamic = isDynamic;
                Props = props;
                InSlot = inSlot;
            }

            public string ModelCast { get; }          // "(global::T)" or null for the dynamic tier
            public ITypeSymbol ModelSymbol { get; }    // for member-path typing; null on the dynamic tier
            public bool IsDynamic { get; }

            /// <summary>The active prop layout (phase 5 D9): a body prop read wins over the model on the first path
            /// segment. Null outside a definition body with props. Prop-first resolution is syntactic, so both
            /// backends resolve identically by rule (README D22, differential-gated).</summary>
            public PropLayoutInfo Props { get; }

            /// <summary>True inside a slot-declaring definition body: <c>@out(value)</c> projects the caller content
            /// (slot mode); outside a slot definition, an <c>@out</c> value is a runtime error the emitter refuses.</summary>
            public bool InSlot { get; }

            public BodyContext WithProps(PropLayoutInfo props) =>
                new BodyContext(ModelCast, ModelSymbol, IsDynamic, props, InSlot);

            public BodyContext AsSlot() =>
                new BodyContext(ModelCast, ModelSymbol, IsDynamic, Props, true);
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
            public readonly List<object> Segments = new List<object>();   // Piece | Call
        }

        private readonly List<string> _pieces = new List<string>();
        private readonly List<BodyClass> _bodies = new List<BodyClass>();
        private int _bodyCounter;

        // Definition bodies compile once into a shared body class (keyed by definition identity), referenced by every
        // call site — never inlined per call, so a self-recursive definition does not loop the generator (README
        // "Emitter completion notes": recursion). Reserved before population so a self-call during the build finds it.
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

            var shape = DocumentShaper.Shape(doc, ctx, _config.TrimDirectiveLines,
                chain => IsZeroOutput(chain), ctx.DefenitionExists, RoleOf);
            int offset = 0;
            var working = shape.WorkingDocument;

            foreach (var element in shape.Elements)
            {
                var pos = element.Position;
                if (pos.StartIndex > offset)
                    AddPiece(body, working.Substring(offset, pos.StartIndex - offset));

                if (profileByChain != null && profileByChain.TryGetValue(element.Chain, out var elementProfile))
                    _profileHtml = elementProfile;

                var leftmostName = element.Chain.Chain.Count > 0 ? element.Chain.Chain[0].ExtensionName : string.Empty;
                // Keyed on [ScopeChannel] presence (not the branch role) so bodiless custom channel extensions also
                // provision a locals frame; deliberately runs before definition resolution (harmless over-provision).
                if (_extensionBinder.TryResolve(leftmostName, out var lmInfo) && lmInfo.HasScopeChannel)
                    body.HostsParticipant = true;

                var seg = BuildCall(element.Chain, bctx, out reason);
                if (seg == null)
                {
                    _profileHtml = savedProfile;
                    return false;
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

                offset = pos.StartIndex + pos.Length;
            }

            if (working.Length > offset)
                AddPiece(body, working.Substring(offset));

            _profileHtml = savedProfile;
            return true;
        }

        /// <summary>Precomputes the running output profile at each rendering chain of this body, from the body's
        /// inherited profile, flipped in document order by every <c>@profile(){{html|text}}</c> directive (which is a
        /// zero-output chain removed from the piece stream but still document-ordered). Keyed by chain identity so the
        /// element walk can look up the profile that was active where each call sits.</summary>
        private Dictionary<OutputChain, bool> MapProfilePerChain(ParseContext ctx)
        {
            if (ctx?.OutputChains == null || ctx.OutputChains.Count == 0)
                return null;

            Dictionary<OutputChain, bool> map = null;
            bool running = _profileHtml;
            bool sawFlip = false;
            foreach (var chain in ctx.OutputChains)
            {
                var lm = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
                if (lm != null && lm.ExtensionName == "profile")
                {
                    var v = (lm.ParameterTemplate ?? string.Empty).Trim();
                    if (string.Equals(v, "html", System.StringComparison.OrdinalIgnoreCase)) { running = true; sawFlip = true; }
                    else if (string.Equals(v, "text", System.StringComparison.OrdinalIgnoreCase)) { running = false; sawFlip = true; }
                    // An unknown value is a runtime error → the template falls back; leave the running profile as-is.
                }
                else
                {
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

        /// <summary>True when the piece has an emitted <c>PnU8</c> twin (phase 8 D7): the opt-in is on and the piece
        /// carries no unpaired surrogate (the HED7005 downgrade, per-piece, mirroring <see cref="PieceWriter"/>).</summary>
        private bool HasU8Twin(int pieceIndex) =>
            _config.EmitUtf8Pieces && !PieceWriter.HasLoneSurrogate(_pieces[pieceIndex]);

        /// <summary>The char index of the first unpaired UTF-16 surrogate in <paramref name="text"/>, or -1.</summary>
        private static int IndexOfFirstLoneSurrogate(string text)
        {
            if (text == null)
                return -1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                        return i;
                    i++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The shared branch-role source: strip machine and emitter both read roles through the single
        /// <see cref="ExtensionBinder"/>, so classification can never drift between them.</summary>
        private BranchRole? RoleOf(string name)
            => _extensionBinder.TryResolve(name, out var i) ? i.Role : null;

        /// <summary>Removes the <c>global::</c> prefix from a fully-qualified name (the binder's <c>GlobalName</c>),
        /// yielding the bare <c>Ns.Type</c> the manifest binding row and body-extension type name expect.</summary>
        private static string StripGlobal(string globalName)
            => globalName != null && globalName.StartsWith("global::", System.StringComparison.Ordinal)
                ? globalName.Substring("global::".Length)
                : globalName;

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

        private object BuildCall(OutputChain chain, BodyContext bctx, out string reason)
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
                // Unnamed carrier: EmptyExtension (Text) / EmptyHtmlExtension (Html, encode) for a bodiless call.
                if (!string.IsNullOrEmpty(item.ParameterTemplate))
                {
                    reason = "bodied unnamed carrier";
                    return null;
                }

                if (!BuildParamExpr(cp, bctx, out var uParam, out var uUses, out var uCs, out reason))
                    return null;
                var uField = AllocateEmptyExtension(item.Position);
                return MakeCall(uField, uParam, uUses, item.Position, uCs);
            }

            // Definition invocation wins over extension/function name resolution (matches HeddleCompiler.CompileItem's
            // definition-first precedence), so a definition may shadow a branch keyword.
            if (name.Length != 0 && _parse.DefenitionExists(name))
                return BuildDefinitionCall(_parse.GetDefenition(name), item, cp, bctx, out reason);

            if (name == "out")
                return BuildOutCall(item, cp, bctx, out reason);

            if (name == "partial")
                return BuildPartialCall(item, cp, bctx, out reason);

            // Engine-assembly branch-role extensions (@if/@ifnot/@elif/@elseif/@else) → pinned branch emission (§6.3.1):
            // the emitter's parent-model body typing is the built-ins' verified contract, so bytes are unchanged.
            // Non-engine role extensions deliberately fall through to the generic custom path (§6.3.2).
            if (_extensionBinder.TryResolve(name, out var branchInfo) && branchInfo.Role.HasValue &&
                branchInfo.IsEngineAssembly)
            {
                // Branch bodies execute under scope.Parent(): the model stays the enclosing body's model
                // (phase 3; generated-code body-model-typing rule). The condition evaluates against that model.
                if (!BuildParamExpr(cp, bctx, out var bParam, out var bUses, out var bCs, out reason))
                    return null;

                BodyClass branchBody = null;
                if (!string.IsNullOrEmpty(item.ParameterTemplate) && item.Context != null)
                {
                    branchBody = BuildBody(item.ParameterTemplate, item.Context, bctx, out reason);
                    if (branchBody == null)
                        return null;
                }

                bool needsLocals = branchBody != null && branchBody.HostsParticipant;
                var field = AllocateBodyExtension(name, branchInfo.GlobalName, StripGlobal(branchInfo.GlobalName),
                    branchBody?.Name, needsLocals, item.Position);
                var call = MakeCall(field, bParam, bUses, item.Position, bCs);
                return call;
            }

            if (name == "list")
            {
                // The collection is evaluated against the enclosing model; the element body is on the dynamic
                // tier (body model-typing rule — the element type is discoverable only by reflection, which the
                // runtime backend does via ListExtension.InitStart; the C# runtime binder resolves the same
                // members against the same runtime element type, differential-gated). generated-code.md example 3.
                if (!BuildParamExpr(cp, bctx, out var lParam, out var lUses, out var lCs, out reason))
                    return null;

                BodyClass itemBody = null;
                if (!string.IsNullOrEmpty(item.ParameterTemplate) && item.Context != null)
                {
                    // The element body is the dynamic tier; a slot @out(value) may still appear inside it, so the
                    // enclosing definition's slot mode propagates into the nested body.
                    var itemCtx = new BodyContext(null, null, true);
                    if (bctx.InSlot)
                        itemCtx = itemCtx.AsSlot();
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
                // @for(n)/@for(Count)/@for(range(...)): ForIndexExtension re-scopes each iteration via
                // scope.Parent(i) — ModelData becomes the enclosing model, ChainedData the index — so the body
                // is typed by the enclosing model (like a branch body) and @out() splices the boxed index.
                if (!BuildParamExpr(cp, bctx, out var fParam, out var fUses, out var fCs, out reason))
                    return null;

                BodyClass forBody = null;
                if (!string.IsNullOrEmpty(item.ParameterTemplate) && item.Context != null)
                {
                    forBody = BuildBody(item.ParameterTemplate, item.Context, bctx, out reason);
                    if (forBody == null)
                        return null;
                }

                bool forNeedsLocals = forBody != null && forBody.HostsParticipant;
                var forField = AllocateBodyExtension("for", "global::Heddle.Extensions.ForIndexExtension",
                    "Heddle.Extensions.ForIndexExtension", forBody?.Name, forNeedsLocals, item.Position);
                return MakeCall(forField, fParam, fUses, item.Position, fCs);
            }

            // Standalone function call (@upper(x) / @(upper(x))): the runtime wraps a function CallNode in an
            // unnamed EmptyExtension carrier (HeddleCompiler.CompileItem function path). Default-function names do
            // not collide with built-in extension names, so a default-table name here is unambiguously a function.
            if ((NativeExpressionWriter.IsDefaultFunction(name) || _exports.TryGet(name, out _)) &&
                string.IsNullOrEmpty(item.ParameterTemplate))
            {
                var callNode = BuildFunctionCallNode(name, cp, item.Position);
                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, "m", _exports);
                var expr = writer.Write(callNode);
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

            // Custom extension bound from a referenced assembly (D9 / WI6): resolve the [ExtensionName] type and
            // reproduce the TemplateItem render protocol against a bound instance — never inlined.
            if (_extensionBinder.TryResolve(name, out var extInfo))
                return BuildCustomExtensionCall(name, extInfo, item, cp, bctx, out reason);

            // HED7006 — an extension-only shape (a bodied call) whose name resolves to no [ExtensionName] type in any
            // referenced assembly. A bodiless call is function-compatible (a delegate-only host registration could
            // satisfy it at run time), so it degrades quietly rather than hard-failing what the runtime might accept.
            if (!string.IsNullOrEmpty(item.ParameterTemplate))
                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.ExtensionNotBindable, item.Position, name));

            reason = "named extension '" + name + "'";
            return null;
        }

        /// <summary>Binds a custom <c>[ExtensionName]</c> extension resolved from a referenced assembly (D9). A plain
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
                // Suppressed for role extensions: a custom branch trio's InitStart override is the canonical shape
                // (R12), not an authoring error — it degrades quietly to the dynamic tier instead (§6.3.3).
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

            if (!BuildParamExpr(cp, bctx, out var paramExpr, out var uses, out var cs, out reason))
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
                .Append("(), body: null, global::Heddle.Data.RenderType.Raw, needsLocals: false, line: ")
                .Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            var bareType = info.GlobalName.StartsWith("global::", System.StringComparison.Ordinal)
                ? info.GlobalName.Substring("global::".Length)
                : info.GlobalName;
            RecordExtensionBinding(name, bareType, info.AssemblyName);
            return field;
        }

        // ---- Definition invocation (README keystone; generated-code.md examples 4/5) ----

        private Call BuildDefinitionCall(DefinitionItem def, OutputItem item, CallParameter cp, BodyContext bctx,
            out string reason)
        {
            reason = null;

            // Full overrides (<name:name>) require document-order layering: an override body's by-name call to the
            // same name resolves to the *base* layer, not the override, and the definition rendered at a call site
            // depends on which overrides are in scope there. The emitter resolves definitions flatly through
            // ParseContext.GetDefenition (always the most-derived layer), so an override calling itself would recurse
            // forever. Refuse to precompile — the dynamic backend's DefinitionResolver owns this — a safe fallback
            // (Emit/DefinitionResolver document-order layering is the reserved follow-up, README/WI6).
            if (DefinitionInvolvesOverride(def)) { reason = "definition override/layering"; return null; }

            // Slot definitions bind through the slot-mode BindDefinition overload (handled below); a default output
            // ('-> chain') renders the definition at document end via ParseContext.DefaultChains — already surfaced as
            // a document-end element by DocumentShaper, so a by-name call and the default-chain self-call both route
            // here with no special casing (the HED4002 double-render warning is the runtime's, non-fatal).
            bool slotMode = DefinitionHasSlot(def);

            var layout = ResolvePropLayout(def);
            if (layout.Failed) { reason = "unresolved prop type"; return null; }

            // The props carriage: a shared frozen object[] prototype plus, for any non-constant argument, per-call
            // dynamic setters evaluated against the caller view (phase 5 D8). The dynamic-arg expressions evaluate
            // against the enclosing body's model (the caller context, bctx).
            if (!TryBuildPropsPrototype(def, layout, cp, bctx, out var propsFieldRef, out var dynamicSettersRef, out reason))
                return null;

            // The definition body compiles once into a shared body class (supports self-recursion; carries the layout
            // so body prop reads resolve prop-first).
            var bodyInfo = GetOrBuildDefinitionBody(def, out reason);
            if (bodyInfo == null || bodyInfo.Failed)
            {
                reason = reason ?? bodyInfo?.Reason ?? "definition body";
                return null;
            }

            // The invocation value evaluates against the enclosing body's model (the caller's context, bctx).
            if (!BuildParamExpr(cp, bctx, out var paramExpr, out var usesModel, out var usesCsModel, out reason))
                return null;

            // Caller content (@name(P){{...}}) is typed by the invoked definition's :: T in the ordinary case, and by
            // the declared slot type in slot mode (README body model-typing rule; runtime CreateExtension:
            // callerModelType = slotType ?? dataType).
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
                }

                callerBody = BuildBody(item.ParameterTemplate, item.Context, callerCtx, out reason);
                if (callerBody == null)
                    return null;
            }

            bool needsLocals = bodyInfo.Body.HostsParticipant ||
                               (callerBody != null && callerBody.HostsParticipant);
            var field = AllocateDefinitionExtension(bodyInfo.Body.Name, callerBody?.Name, propsFieldRef,
                dynamicSettersRef, needsLocals, slotMode, item.Position);
            return MakeCall(field, paramExpr, usesModel, item.Position, usesCsModel);
        }

        /// <summary>@out() — the bodiless caller-content splice. Non-slot: EmptyParameter passes the current model
        /// through and OutExtension renders the chained caller content (generated-code.md example 5). Slot mode
        /// (@out(value) inside a slot-declaring definition body): the value expression becomes the projection model
        /// and OutExtension renders the caller content lazily against it (README slots; example 5's slot note).</summary>
        private Call BuildOutCall(OutputItem item, CallParameter cp, BodyContext bctx, out string reason)
        {
            reason = null;
            if (!string.IsNullOrEmpty(item.ParameterTemplate)) { reason = "bodied @out"; return null; }
            bool hasValue = !cp.IsModelTypeParameter ||
                            (cp.ModelParameter != null && cp.ModelParameter.Length > 0 &&
                             !string.IsNullOrEmpty(cp.ModelParameter[0])) ||
                            (cp.PropArguments != null && cp.PropArguments.Count != 0);

            if (hasValue)
            {
                // A value on @out is only valid inside a slot-declaring definition body (the runtime raises
                // SlotValueWithoutSlot otherwise) — refuse to precompile a value elsewhere.
                if (!bctx.InSlot) { reason = "@out with value outside a slot definition"; return null; }
                if (cp.PropArguments != null && cp.PropArguments.Count != 0) { reason = "@out prop arguments"; return null; }
                if (!BuildParamExpr(cp, bctx, out var vParam, out var vUses, out var vCs, out reason))
                    return null;
                var slotField = AllocateOutExtension(slotMode: true, item.Position);
                return MakeCall(slotField, vParam, vUses, item.Position, vCs);
            }

            // A bodiless valueless @out inside a slot definition is the SlotValueRequired error at runtime — refuse.
            if (bctx.InSlot) { reason = "@out() without a slot value"; return null; }

            var field = AllocateOutExtension(slotMode: false, item.Position);
            return MakeCall(field, "scope.ModelData", false, item.Position);
        }

        /// <summary>@partial(model){{name}} — renders the named template with the parameter value as its model
        /// (README D7). The partial's strategy is resolved lazily on first render (registry first, dynamic compile
        /// second) via <c>PrecompiledRuntime.ResolvePartial</c> and memoized in a static field; the render splices the
        /// partial's output exactly as <c>PartialExtension.InnerTemplate.Generate</c> does. The name (the body) must be
        /// static text.</summary>
        private Partial BuildPartialCall(OutputItem item, CallParameter cp, BodyContext bctx, out string reason)
        {
            reason = null;
            var name = item.ParameterTemplate;
            if (string.IsNullOrWhiteSpace(name)) { reason = "empty @partial name"; return null; }

            // The name must be static text: a body carrying processors (output chains), definitions, imports, or raw
            // output is a dynamic partial name the emitter can not resolve at build.
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

            if (!BuildParamExpr(cp, bctx, out var modelExpr, out var usesModel, out var usesCs, out reason))
                return null;

            var field = "_partial" + _partialCounter++;
            _fieldDecls.Append("        private static global::Heddle.Runtime.IProcessStrategy ").Append(field)
                .Append(";\n");

            // The dynamic-compile fallback types the child by the caller-site model type (README D7); a dynamic tier
            // caller compiles the child on the dynamic tier too.
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

        private DefBodyInfo GetOrBuildDefinitionBody(DefinitionItem def, out string reason)
        {
            reason = null;
            var key = def.Name + "@" + def.Position;   // matches the runtime layout cache key (definition identity)
            if (_definitionBodies.TryGetValue(key, out var existing))
                return existing;

            var bodyCtx = DefinitionBodyContext(def, out reason);
            if (reason != null)
                return null;

            // The definition body resolves prop reads prop-first (phase 5 D9) — attach the layout.
            var layout = ResolvePropLayout(def);
            if (layout.Count > 0)
                bodyCtx = bodyCtx.WithProps(layout);

            // A slot-declaring definition body may use @out(value) to project the caller content.
            if (DefinitionHasSlot(def))
                bodyCtx = bodyCtx.AsSlot();

            var info = new DefBodyInfo { Body = NewBody(bodyCtx) };
            // Pre-mark participant hosting so a self-call encountered during population bakes the correct needsLocals.
            if (ScanHostsParticipant(def.Context))
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

        /// <summary>The body-model-typing rule for a definition: <c>:: dynamic</c> is the dynamic tier; a declared
        /// <c>:: T</c> resolves to its symbol; an undeclared (or unresolvable) model type degrades the template to the
        /// dynamic path (the runtime types such a body as <c>object</c>, whose member reads it then rejects).</summary>
        private BodyContext DefinitionBodyContext(DefinitionItem def, out string reason)
        {
            reason = null;
            var modelTypeName = def.ModelType;
            if (string.Equals(modelTypeName, "dynamic", System.StringComparison.Ordinal))
                return new BodyContext(null, null, true);

            var sym = _resolver.ResolveModelType(modelTypeName, _usings);
            if (sym == null)
            {
                reason = "unresolved definition model type '" + modelTypeName + "'";
                return default;
            }

            var fq = SymbolTypeResolver.FullyQualified(sym);
            return new BodyContext("(" + fq + ")", sym, false);
        }

        private bool ScanHostsParticipant(ParseContext ctx)
        {
            if (ctx?.OutputChains == null)
                return false;
            foreach (var chain in ctx.OutputChains)
            {
                var lm = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0].ExtensionName : null;
                if (lm != null && _extensionBinder.TryResolve(lm, out var i) && i.HasScopeChannel)
                    return true;
            }

            return false;
        }

        private static bool DefinitionHasProps(DefinitionItem def)
        {
            for (var d = def; d != null; d = d.BaseDefinition)
                if (d.PropDeclarations != null && d.PropDeclarations.Count != 0)
                    return true;
            return false;
        }

        // ---- Prop layout (phase 5 D6; generated-code.md example 5) ----

        private sealed class PropSlotInfo
        {
            public string Name;
            public ITypeSymbol Type;
            public string TypeFq;
            public bool HasDefault;
            public object DefaultValue;   // decoded literal (pre-conversion CLR value)
            public int Index;
        }

        private sealed class PropLayoutInfo
        {
            public readonly List<PropSlotInfo> Slots = new List<PropSlotInfo>();
            public readonly Dictionary<string, PropSlotInfo> ByName =
                new Dictionary<string, PropSlotInfo>(System.StringComparer.Ordinal);
            public bool Failed;
            public int Count => Slots.Count;
        }

        private readonly Dictionary<string, PropLayoutInfo> _propLayouts =
            new Dictionary<string, PropLayoutInfo>(System.StringComparer.Ordinal);

        /// <summary>Reimplements <c>PropLayout.Resolve</c> over symbols (README D22): base-chain props outermost-first
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
                    if (sym == null)
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
        /// forbids guessing a numeric widening or a lossy conversion.</summary>
        private static bool TryFormatPropValue(ITypeSymbol targetType, bool hasDefault, object value, out string expr)
        {
            expr = null;
            if (targetType == null)
                return false;

            var underlying = targetType;
            if (targetType is INamedTypeSymbol named &&
                named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T &&
                named.TypeArguments.Length == 1)
                underlying = named.TypeArguments[0];

            if (value == null)
            {
                // A null literal is legal only for a reference/nullable target (the runtime rejects it otherwise).
                if (targetType.IsReferenceType ||
                    (targetType is INamedTypeSymbol n2 && n2.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T))
                {
                    expr = "null";
                    return true;
                }

                return false;
            }

            // Only reproduce a boxed value when the literal's CLR type already matches the target's underlying type
            // (no conversion), or the target is object/string with a matching literal. Numeric widening is deferred.
            var literal = LiteralFormatter.Format(value);
            if (literal == null)
                return false;

            if (underlying.SpecialType == SpecialType.System_Object)
            {
                expr = literal;   // boxes as the literal's own type — matches ConvertValue's pass-through
                return true;
            }

            var valueSpecial = SpecialTypeOf(value);
            if (valueSpecial != SpecialType.None && valueSpecial == underlying.SpecialType)
            {
                expr = literal;
                return true;
            }

            // Implicit numeric widening (phase 5 D10 rule 4 / PropConversion.ConvertValue): the runtime stores the
            // widened boxed value (Convert.ChangeType to the target underlying) in the prototype. A C# cast to the
            // target underlying keyword boxes to the identical CLR type/value (e.g. (double)1 == 1.0d).
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

        /// <summary>C#'s implicit numeric conversions (spec §10.2.3) over <see cref="SpecialType"/> — the exact set
        /// <c>NumericPromotion.IsImplicitNumeric</c> allows for prop-default widening (README D22, differential-gated).</summary>
        private static bool IsImplicitNumericWidening(SpecialType from, SpecialType to)
        {
            if (from == to)
                return false;
            switch (from)
            {
                case SpecialType.System_SByte:
                    return to == SpecialType.System_Int16 || to == SpecialType.System_Int32 || to == SpecialType.System_Int64 ||
                           to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Byte:
                    return to == SpecialType.System_Int16 || to == SpecialType.System_UInt16 || to == SpecialType.System_Int32 ||
                           to == SpecialType.System_UInt32 || to == SpecialType.System_Int64 || to == SpecialType.System_UInt64 ||
                           to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Int16:
                    return to == SpecialType.System_Int32 || to == SpecialType.System_Int64 ||
                           to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_UInt16:
                    return to == SpecialType.System_Int32 || to == SpecialType.System_UInt32 || to == SpecialType.System_Int64 ||
                           to == SpecialType.System_UInt64 || to == SpecialType.System_Single || to == SpecialType.System_Double ||
                           to == SpecialType.System_Decimal;
                case SpecialType.System_Int32:
                    return to == SpecialType.System_Int64 || to == SpecialType.System_Single ||
                           to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_UInt32:
                    return to == SpecialType.System_Int64 || to == SpecialType.System_UInt64 ||
                           to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return to == SpecialType.System_Single || to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Char:
                    return to == SpecialType.System_UInt16 || to == SpecialType.System_Int32 || to == SpecialType.System_UInt32 ||
                           to == SpecialType.System_Int64 || to == SpecialType.System_UInt64 || to == SpecialType.System_Single ||
                           to == SpecialType.System_Double || to == SpecialType.System_Decimal;
                case SpecialType.System_Single:
                    return to == SpecialType.System_Double;
                default:
                    return false;
            }
        }

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

        private static bool DefinitionHasSlot(DefinitionItem def)
        {
            for (var d = def; d != null; d = d.BaseDefinition)
                if (!string.IsNullOrEmpty(d.SlotTypeName))
                    return true;
            return false;
        }

        /// <summary>True when this definition participates in a full override (<c>&lt;name:name&gt;</c>) — the same
        /// name re-declared over a base layer. The emitter refuses these (document-order layering, above);
        /// name-differing inheritance (<c>&lt;child:base&gt;</c>) is not an override and stays precompilable.</summary>
        private static bool DefinitionInvolvesOverride(DefinitionItem def)
        {
            for (var d = def; d != null; d = d.BaseDefinition)
            {
                if (d.FullOverride)
                    return true;
                if (d.BaseDefinition != null &&
                    string.Equals(d.BaseDefinition.Name, d.Name, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private int _propsCounter;

        /// <summary>Builds the props carriage for a call site (phase 5 D8): a frozen <c>static readonly object[]</c>
        /// prototype (defaults + constant arguments, shared, zero per-render allocation) plus, for any non-constant
        /// argument, a <c>PrecompiledPropSetter[]</c> whose evaluators run against the caller view — exactly what the
        /// runtime <c>PropsBinder</c> produces. <paramref name="propsFieldRef"/> is <c>"null"</c> for a prop-less
        /// definition; <paramref name="dynamicSettersRef"/> is <c>"null"</c> for an all-constant call. Falls back
        /// (returns false) for a prop value the emitter can not reproduce byte-exactly or a dynamic argument it can
        /// not type (root refs, non-path native args needing conversion).</summary>
        private bool TryBuildPropsPrototype(DefinitionItem def, PropLayoutInfo layout, CallParameter cp,
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

                    // A non-constant argument becomes a dynamic setter evaluated against the caller view (phase 5 D8).
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

                if (!TryFormatPropValue(slot.Type, true, slot.DefaultValue, out var expr))
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

        /// <summary>Emits an evaluator for a non-constant prop argument (phase 5 D8): a static method computing the
        /// argument expression against the caller view's model, wrapped in a <c>PrecompiledPropSetter(index, eval)</c>.
        /// Supports a typed-caller member path (with numeric widening to the prop type) and any writable native
        /// expression when the prop type is <c>object</c>; root references and conversions the emitter can not prove
        /// fall back.</summary>
        private bool TryBuildDynamicSetter(NamedArgument arg, PropSlotInfo slot, BodyContext bctx, out string setterExpr,
            out string reason)
        {
            reason = null;
            setterExpr = null;
            if (bctx.IsDynamic || bctx.ModelSymbol == null) { reason = "dynamic arg without a typed caller model"; return false; }
            if (slot.Type == null) { reason = "dynamic arg with unresolved prop type"; return false; }

            string conversionKeyword = null;
            if (arg.Value is PathNode pn)
            {
                if (pn.RootRef) { reason = "root-reference dynamic arg"; return false; }
                var res = _resolver.ResolvePath(bctx.ModelSymbol, pn.Segments);
                if (res.Kind != SymbolTypeResolver.PathKind.Resolved) { reason = "dynamic arg path (" + res.Kind + ")"; return false; }

                if (!SymbolEqualityComparer.Default.Equals(res.ResultType, slot.Type) &&
                    slot.Type.SpecialType != SpecialType.System_Object)
                {
                    var from = res.ResultType?.SpecialType ?? SpecialType.None;
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

            var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, "m", _exports);
            var body = writer.Write(arg.Value);
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
        {
            if (type is INamedTypeSymbol named &&
                named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T &&
                named.TypeArguments.Length == 1)
                return named.TypeArguments[0].SpecialType;
            return type.SpecialType;
        }

        private string AllocateDefinitionExtension(string bodyName, string callerBodyName, string propsFieldRef,
            bool needsLocals, bool slotMode, BlockPosition position)
        {
            return AllocateDefinitionExtension(bodyName, callerBodyName, propsFieldRef, "null", needsLocals, slotMode,
                position);
        }

        private string AllocateDefinitionExtension(string bodyName, string callerBodyName, string propsFieldRef,
            string dynamicSettersRef, bool needsLocals, bool slotMode, BlockPosition position)
        {
            var field = "E" + _extensionCounter++;
            var (line, col) = _map.Map(position.StartIndex);
            var callerArg = callerBodyName != null ? "new " + callerBodyName + "()" : "null";
            _fieldDecls.Append("        private static readonly global::Heddle.Core.AbstractExtension ").Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.BindDefinition(\n");
            _fieldDecls.Append("            body: new ").Append(bodyName).Append("(), callerContent: ").Append(callerArg)
                .Append(", props: ").Append(propsFieldRef)
                .Append(", dynamicSetters: ").Append(dynamicSettersRef)
                .Append(", global::Heddle.Data.RenderType.Raw, needsLocals: ")
                .Append(needsLocals ? "true" : "false")
                .Append(", slotMode: ").Append(slotMode ? "true" : "false")
                .Append(", maxRecursionCount: ").Append(_config.MaxRecursionCount)
                .Append(", line: ").Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            return field;
        }

        /// <summary>The caller-content typing for a slot-mode definition (README body model-typing rule): the caller
        /// body executes with <c>ModelData</c> = the slot value, declared to be the slot type. <c>:: dynamic</c> is
        /// never a slot type (the runtime rejects a dynamic slot value); an unresolvable slot type degrades the
        /// template to the dynamic path.</summary>
        private BodyContext SlotBodyContext(DefinitionItem def, out string reason)
        {
            reason = null;
            string slotName = null;
            for (var d = def; d != null; d = d.BaseDefinition)
                if (!string.IsNullOrEmpty(d.SlotTypeName)) { slotName = d.SlotTypeName; break; }

            if (slotName == null) { reason = "slot definition without slot type"; return default; }
            var sym = _resolver.ResolveModelType(slotName, _usings);
            if (sym == null) { reason = "unresolved slot type '" + slotName + "'"; return default; }
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
            out string reason)
            => BuildParamExpr(cp, bctx, out paramExpr, out usesModel, out _, out reason);

        private bool BuildParamExpr(CallParameter cp, BodyContext bctx, out string paramExpr, out bool usesModel,
            out bool usesCSharpModel, out string reason)
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

                // Phase 5 D9: a body prop read wins over the model on the first segment (never for :: root refs) —
                // resolved prop-first, syntactically, so both backends agree by rule (README D22).
                if (!cp.RootReference && bctx.Props != null && bctx.Props.ByName.TryGetValue(segments[0], out var slot))
                {
                    var propRead = "global::Heddle.Precompiled.PrecompiledRuntime.Prop(in scope, " + slot.Index + ")";
                    if (segments.Length == 1)
                    {
                        paramExpr = propRead;
                        return true;   // reads scope.PropsData, not the model local
                    }

                    // Multi-hop: cast the boxed prop to its declared slot type and walk the rest hop-by-hop, the same
                    // member tier the single-hop model path uses (differential-gated). A dynamic/unresolvable prop
                    // type falls back.
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

                    var root = "((" + slot.TypeFq + ")" + propRead + ")";
                    paramExpr = "(object)(" + MemberPathWriter.Write(root, MapHops(res)) + ")";
                    return true;
                }

                if (bctx.IsDynamic)
                {
                    // Dynamic tier (generated-code.md example 4, protocol rule 6): a leading conditional guards
                    // the first hop, dynamic ?. carries the rest — exactly what DynamicParameter emits.
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
                    // Milestone 2 (HED7008): a genuine property-not-found on the typed model — the same condition the
                    // runtime raises as HED0001. Positioned by locating the dotted path in the source (the raw
                    // segments carry no BlockPosition); skipped when it cannot be located (never a false diagnostic).
                    if (resolution.Kind == SymbolTypeResolver.PathKind.Failed)
                        RecordMemberFailure(bctx.ModelSymbol, segments, resolution);
                    reason = "member path (" + resolution.Kind + ")";
                    return false;
                }

                paramExpr = "(object)(" + MemberPathWriter.Write("m", MapHops(resolution)) + ")";
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

                if (string.Equals(_config.ExpressionMode, "MemberPathsOnly", System.StringComparison.Ordinal))
                {
                    reason = "native expression under MemberPathsOnly";
                    return false;
                }

                if (bctx.IsDynamic || bctx.ModelSymbol == null)
                {
                    reason = "native expression without typed model";
                    return false;
                }

                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, "m", _exports);
                var expr = writer.Write(cp.NativeExpression);
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

            // A single-item parenthesized chain (@(upper(Name)), @(Name):... with one producer) reduces to the
            // producer's value: the runtime wraps it in EmptyExtension carriers that only pass the value through,
            // so flattening to one carrier over the producer expression is byte-identical.
            if (cp.ChainParameter != null && cp.ChainParameter.Count == 1)
                return BuildChainItemExpr(cp.ChainParameter[0], bctx, out paramExpr, out usesModel, out usesCSharpModel,
                    out reason);

            reason = "C#/chain parameter";
            return false;
        }

        /// <summary>The FullCSharp verbatim tier (generated-code.md example 3): the parser-captured C# expression is
        /// pasted verbatim, binding a local named <c>model</c> — the exact identifier the runtime's
        /// <c>CSharpClassTemplate</c> method signature declares — so a pasted expression means the same thing on both
        /// backends. Only emitted under <c>ExpressionMode.FullCSharp</c> (the generator never widens the sandbox,
        /// README D14); a typed model is required (the runtime types <c>model</c>); expressions referencing the
        /// <c>chained</c>/<c>root</c> parameters degrade to the dynamic path (their static types are not reproducible
        /// in-generator here — a reserved follow-up).</summary>
        private bool BuildCSharpExpr(string csharp, BodyContext bctx, out string paramExpr, out bool usesCSharpModel,
            out string reason)
        {
            reason = null;
            paramExpr = null;
            usesCSharpModel = false;

            if (!string.Equals(_config.ExpressionMode, "FullCSharp", System.StringComparison.Ordinal))
            {
                // The runtime rejects C# unless AllowCSharp (== FullCSharp); refuse to precompile (the dynamic path
                // will surface the engine's own C#-tier gate). HED7012 wrapping is the diagnostics work item.
                reason = "embedded C# outside FullCSharp mode";
                return false;
            }

            if (bctx.IsDynamic || bctx.ModelSymbol == null)
            {
                reason = "embedded C# without a typed model";
                return false;
            }

            // References to the chained/root parameters need their runtime static types, which the emitter cannot
            // reproduce here — fall back rather than paste an untyped/ill-typed reference.
            if (System.Text.RegularExpressions.Regex.IsMatch(csharp, @"\bchained\b") ||
                System.Text.RegularExpressions.Regex.IsMatch(csharp, @"\broot\b"))
            {
                reason = "embedded C# references chained/root";
                return false;
            }

            paramExpr = "(object)(" + csharp + ")";
            usesCSharpModel = true;
            return true;
        }

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
                    out reason);

            if (NativeExpressionWriter.IsDefaultFunction(name) || _exports.TryGet(name, out _))
            {
                var callNode = BuildFunctionCallNode(name, inner.CallParameter, inner.Position);
                var writer = new NativeExpressionWriter(_resolver, bctx.ModelSymbol, "m", _exports);
                var expr = writer.Write(callNode);
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

            // A producer name in @(name(args)) that is neither a default built-in, a discovered export, nor a
            // definition is the OQ1 delegate-only remainder (D21) — the runtime would resolve it as a function and
            // fail to find it. Record it so the template degrades to a HED7014 fallback marker (generated-code.md
            // example 7 negative half — @(shout(Name))). Function-shaped only: bodiless (guarded above) and not a
            // nested chain.
            if (inner.CallParameter.ChainParameter == null && string.IsNullOrEmpty(inner.CallParameter.CSharpExpression)
                && !_parse.DefenitionExists(name))
            {
                _unresolvableFunctions.Add((name, inner.Position));
            }

            reason = "chain item extension '" + name + "'";
            return false;
        }

        /// <summary>The dynamic-tier member access (generated-code.md example 4): <c>m == null ? (object)null :
        /// (object)(((dynamic)m).A?.B)</c> — the leading conditional guards the first hop, dynamic <c>?.</c> the
        /// rest, byte-identical to <c>DynamicParameter</c>'s per-hop <c>null</c> propagation.</summary>
        private static string WriteDynamicPath(string local, string[] segments)
        {
            var sb = new StringBuilder();
            sb.Append("((dynamic)").Append(local).Append(").").Append(segments[0]);
            for (int i = 1; i < segments.Length; i++)
                sb.Append("?.").Append(segments[i]);
            var chain = sb.ToString();
            var access = segments.Length == 1 ? "(object)" + chain : "(object)(" + chain + ")";
            return local + " == null ? (object)null : " + access;
        }

        private static IReadOnlyList<MemberPathWriter.HopEmit> MapHops(SymbolTypeResolver.PathResolution resolution)
        {
            var hops = new List<MemberPathWriter.HopEmit>(resolution.Hops.Count);
            foreach (var hop in resolution.Hops)
            {
                hops.Add(new MemberPathWriter.HopEmit(
                    hop.Receiver.IsValueType,
                    SymbolTypeResolver.IsNonNullableValueType(hop.Property),
                    SymbolTypeResolver.FullyQualified(hop.Property),
                    hop.Name));
            }

            return hops;
        }

        private string AllocateEmptyExtension(BlockPosition position)
        {
            var field = "E" + _extensionCounter++;
            // The running profile (post-@profile-flip) decides encoding — the phase 2 redirect reproduced at the
            // exact document position the carrier sits.
            bool html = _profileHtml;
            var typeName = html ? "global::Heddle.Extensions.EmptyHtmlExtension"
                                : "global::Heddle.Extensions.EmptyExtension";
            var renderType = html ? "global::Heddle.Data.RenderType.Encode"
                                  : "global::Heddle.Data.RenderType.Raw";
            var (line, col) = _map.Map(position.StartIndex);
            _fieldDecls.Append("        private static readonly ").Append(typeName).Append(' ').Append(field)
                .Append(" = global::Heddle.Precompiled.PrecompiledRuntime.Bind(\n");
            _fieldDecls.Append("            new ").Append(typeName)
                .Append("(), body: null, ").Append(renderType)
                .Append(", needsLocals: false, line: ").Append(line).Append(", column: ").Append(col).Append(");\n");
            _extensionFields.Add(field);
            // The manifest binding name must resolve (via TemplateFactory) to the bound type in the gauntlet:
            // under Html the bodiless carrier redirects to the "html" extension (EmptyHtmlExtension); under Text
            // it is the empty-named EmptyExtension. Recording "" with the Html type would resolve "" -> EmptyExtension
            // and mismatch.
            RecordExtensionBinding(html ? "html" : "", html ? "Heddle.Extensions.EmptyHtmlExtension" : "Heddle.Extensions.EmptyExtension");
            return field;
        }

        private string AllocateBodyExtension(string callName, string fqn, string typeName, string bodyName,
            bool needsLocals, BlockPosition position)
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
            RecordExtensionBinding(callName, typeName);
            return field;
        }

        private void RecordExtensionBinding(string name, string type)
        {
            if (!_extensionBindings.Any(b => b.Name == name && b.Type == type))
                _extensionBindings.Add((name, type, "Heddle"));
        }

        private void RecordExtensionBinding(string name, string type, string assembly)
        {
            if (!_extensionBindings.Any(b => b.Name == name && b.Type == type))
                _extensionBindings.Add((name, type, assembly));
        }

        private void RecordFunctionBinding(string name, string target, int overloadCount)
        {
            if (!_functionBindings.Any(b => b.Name == name && b.Target == target))
                _functionBindings.Add((name, target, overloadCount));
        }

        /// <summary>Copies the writer's recorded remainders into the template-level channels: unresolvable
        /// (delegate-only) function names turn the template into a HED7014 fallback marker (D21); genuine member-path
        /// failures become HED7008 diagnostics (milestone 2). Both are drained after every native-expression write.</summary>
        private void DrainUnresolvable(NativeExpressionWriter writer)
        {
            foreach (var fn in writer.UnresolvableFunctions)
                _unresolvableFunctions.Add(fn);
            foreach (var mf in writer.MemberFailures)
            {
                var seenKey = mf.Path + "@" + mf.Position.StartIndex;
                if (_seenMemberFailures.Add(seenKey))
                    _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.UnresolvableMember,
                        mf.Position, mf.ReceiverType, mf.Member, mf.Path));
            }
        }

        private readonly HashSet<string> _seenMemberFailures = new HashSet<string>(System.StringComparer.Ordinal);

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
                _diagnostics.Add(new EmitDiagnostic(GeneratorDiagnostics.UnresolvableMember,
                    position, SymbolTypeResolver.FullyQualified(receiver), member, display));
        }

        /// <summary>True when <paramref name="text"/> is a plain (possibly dotted, possibly <c>?</c>-suffixed) type
        /// name — the syntax the model-type resolver models. Open generics, arrays, tuples and whitespace forms are
        /// excluded so an unresolved exotic model degrades to the dynamic path rather than a false HED7007.</summary>
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

        private IReadOnlyList<(string Name, BlockPosition Position)> DedupeUnresolvable()
        {
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            var result = new List<(string, BlockPosition)>();
            foreach (var fn in _unresolvableFunctions)
                if (seen.Add(fn.Name))
                    result.Add(fn);
            return result;
        }

        /// <summary>Records one manifest <c>FunctionBindings</c> row per default/exported function the writer bound —
        /// defaults to the shim's forwarding target, exports to their discovered container (D21).</summary>
        private void RecordFunctionUses(NativeExpressionWriter writer)
        {
            foreach (var fn in writer.UsedDefaultFunctions)
                RecordFunctionBinding(fn, DefaultFunctionTable.ShimTargetTypeName,
                    NativeExpressionWriter.DefaultOverloadCount(fn));
            foreach (var ex in writer.UsedExports)
                RecordFunctionBinding(ex.Key, ex.Value.Aqn, ex.Value.OverloadCount);
        }

        // ---- File rendering ----

        private string RenderFile(string modelType, BodyClass root)
        {
            var w = new CodeWriter();
            w.Raw("// <auto-generated/>");
            w.Raw("#pragma warning disable");
            foreach (var ns in _usings)
                w.Raw("using " + ns + ";");
            w.Line();
            w.Line("namespace " + _namespace);
            w.Line("{");
            w.Indent();

            w.Line("public static class " + _sanitizedName);
            w.Line("{");
            w.Indent();

            w.Line("/// <summary>Typed entry point — the recommended host API (phase 7 D11).</summary>");
            w.Line($"public static string Generate({modelType} model, object chained = null, object callerData = null)");
            w.Indent();
            w.Line("=> global::Heddle.Precompiled.PrecompiledRuntime.GenerateString(Root, model, chained, callerData);");
            w.Outdent();
            w.Line();
            w.Line("/// <summary>Renders into a TextWriter with no full-output materialization (phase 8).</summary>");
            w.Line($"public static void Generate({modelType} model, global::System.IO.TextWriter writer, object chained = null, object callerData = null)");
            w.Indent();
            w.Line("=> global::Heddle.Precompiled.PrecompiledRuntime.GenerateToWriter(Root, model, chained, callerData, writer);");
            w.Outdent();
            w.Line();
            w.Line("/// <summary>Renders UTF-8 into an IBufferWriter&lt;byte&gt; with no full-output materialization (phase 8).</summary>");
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

            // Piece table (global, document order — D5 determinism).
            for (int i = 0; i < _pieces.Count; i++)
                PieceWriter.EmitPiece(w, i, _pieces[i], _config.EmitUtf8Pieces, utf8Supported: true);
            w.Line();

            // Extension instances.
            foreach (var line in _fieldDecls.ToString().Split('\n'))
                if (line.Length != 0)
                    w.Raw(line);
            w.Line();

            // Dynamic prop-argument evaluators (phase 5 D8) — static methods referenced by the setter arrays above.
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

            // Render
            w.Line("public void Render(in global::Heddle.Data.Scope scope)");
            w.Line("{");
            w.Indent();
            if (body.NeedsModelLocal)
                w.Line($"var m = {body.ModelCast}scope.ModelData;");
            if (body.NeedsCSharpModel)
                w.Line($"var model = {body.ModelCast}scope.ModelData;");
            foreach (var seg in body.Segments)
            {
                if (seg is Piece p)
                {
                    w.Raw("#line hidden");
                    // Phase 8 D7: when the u8 twin exists (HeddleEmitUtf8Pieces on, no lone surrogate) route the piece
                    // through the one WritePiece hook — the zero-transcode byte branch on a UTF-8 sink, the string form
                    // otherwise (including under encode proxies, which are not IUtf8ScopeRenderer). Otherwise the plain
                    // string render, unchanged (schemaVersion-1 shape).
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

            // Execute
            w.Line("public string Execute(in global::Heddle.Data.Scope scope)");
            w.Line("{");
            w.Indent();
            if (body.NeedsModelLocal)
                w.Line($"var m = {body.ModelCast}scope.ModelData;");
            if (body.NeedsCSharpModel)
                w.Line($"var model = {body.ModelCast}scope.ModelData;");
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

        /// <summary>The lazily-memoized partial-strategy resolution the generated @partial call site closes over:
        /// <c>LazyInitializer.EnsureInitialized(ref _partialN, () =&gt; ResolvePartial("key"))</c> — registry-first,
        /// dynamic-compile second (README D7), at-most-once per field.</summary>
        private static string PartialResolveExpr(Partial pt)
        {
            var resolve = pt.CallerModelTypeFq == null
                ? "global::Heddle.Precompiled.PrecompiledRuntime.ResolvePartial(" + PieceWriter.Escape(pt.Key) + ")"
                : "global::Heddle.Precompiled.PrecompiledRuntime.ResolvePartial(" + PieceWriter.Escape(pt.Key) +
                  ", typeof(" + pt.CallerModelTypeFq + "))";
            return "global::System.Threading.LazyInitializer.EnsureInitialized(ref " + pt.FieldName +
                   ", () => " + resolve + ")";
        }

        private void EmitLineSpanRaw(CodeWriter w, int sl, int sc, int el, int ec)
        {
            w.Raw($"#line ({sl}, {sc}) - ({el}, {ec}) \"{_key}\"");
        }

        private void EmitLineSpan(CodeWriter w, Call c)
        {
            w.Raw($"#line ({c.SpanStartLine}, {c.SpanStartCol}) - ({c.SpanEndLine}, {c.SpanEndCol}) \"{_key}\"");
        }

        // ---- Manifest entry ----

        private string BuildManifestEntry(string contentHash, bool isDynamic, string modelType)
        {
            var sb = new StringBuilder();
            var entryType = $"global::{_namespace}.{_sanitizedName}";
            sb.Append("new global::Heddle.Precompiled.PrecompiledTemplateInfo(\n");
            sb.Append($"    key: {PieceWriter.Escape(_key)},\n");
            sb.Append($"    entryPointType: typeof({entryType}),\n");
            sb.Append($"    modelType: {entryType}.__ModelType,\n");
            sb.Append($"    isDynamic: {(isDynamic ? "true" : "false")},\n");
            sb.Append($"    contentHash: {PieceWriter.Escape(contentHash)},\n");
            sb.Append("    imports: global::System.Array.Empty<global::Heddle.Precompiled.PrecompiledImport>(),\n");
            sb.Append("    optionsFingerprint: new global::Heddle.Precompiled.PrecompiledOptionsFingerprint(\n");
            sb.Append($"        global::Heddle.Data.OutputProfile.{Profile()},\n");
            sb.Append($"        global::Heddle.Data.ExpressionMode.{Mode()},\n");
            sb.Append($"        trimDirectiveLines: {(_config.TrimDirectiveLines ? "true" : "false")}),\n");
            sb.Append("    extensionBindings: " + ExtensionBindingsArray() + ",\n");
            sb.Append("    functionBindings: " + FunctionBindingsArray() + ",\n");
            sb.Append("    capabilities: " + CapabilitiesExpr() + ",\n");
            sb.Append($"    strategy: {entryType}.Root)");
            return sb.ToString();
        }

        /// <summary>The HED7014 fallback-marker manifest entry (D21, generated-code.md example 7 negative half): a
        /// <see cref="Heddle.Precompiled.PrecompiledTemplateInfo"/> with a null entry class and null strategy
        /// (<c>IsPrecompiled == false</c>) plus one null-target <c>FunctionBindings</c> row per unresolvable name —
        /// the row the gauntlet short-circuits on with <c>UnsupportedFunction</c>.</summary>
        private string BuildMarkerManifestEntry(string contentHash, bool isDynamic, string modelType)
        {
            var sb = new StringBuilder();
            sb.Append("new global::Heddle.Precompiled.PrecompiledTemplateInfo(\n");
            sb.Append($"    key: {PieceWriter.Escape(_key)},\n");
            sb.Append("    entryPointType: null,\n");
            sb.Append($"    modelType: typeof({modelType}),\n");
            sb.Append($"    isDynamic: {(isDynamic ? "true" : "false")},\n");
            sb.Append($"    contentHash: {PieceWriter.Escape(contentHash)},\n");
            sb.Append("    imports: global::System.Array.Empty<global::Heddle.Precompiled.PrecompiledImport>(),\n");
            sb.Append("    optionsFingerprint: new global::Heddle.Precompiled.PrecompiledOptionsFingerprint(\n");
            sb.Append($"        global::Heddle.Data.OutputProfile.{Profile()},\n");
            sb.Append($"        global::Heddle.Data.ExpressionMode.{Mode()},\n");
            sb.Append($"        trimDirectiveLines: {(_config.TrimDirectiveLines ? "true" : "false")}),\n");
            sb.Append("    extensionBindings: global::System.Array.Empty<global::Heddle.Precompiled.PrecompiledExtensionBinding>(),\n");
            sb.Append("    functionBindings: new global::Heddle.Precompiled.PrecompiledFunctionBinding[] { ");
            sb.Append(string.Join(", ", DedupeUnresolvable()
                .Select(fn => $"new global::Heddle.Precompiled.PrecompiledFunctionBinding({PieceWriter.Escape(fn.Name)}, null, 0)")));
            sb.Append(" },\n");
            sb.Append("    capabilities: global::Heddle.Precompiled.PrecompiledCapabilities.None,\n");
            sb.Append("    strategy: null)");
            return sb.ToString();
        }

        private string Profile() => IsHtml ? "Html" : "Text";

        private string Mode()
        {
            switch (_config.ExpressionMode)
            {
                case "MemberPathsOnly": return "MemberPathsOnly";
                case "FullCSharp": return "FullCSharp";
                default: return "Native";
            }
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
                .Select(b => $"new global::Heddle.Precompiled.PrecompiledExtensionBinding({PieceWriter.Escape(b.Name)}, {PieceWriter.Escape(b.Type + ", " + b.Assembly)})")));
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
                .Select(b => $"new global::Heddle.Precompiled.PrecompiledFunctionBinding({PieceWriter.Escape(b.Name)}, {PieceWriter.Escape(b.Target)}, {b.OverloadCount})")));
            sb.Append(" }");
            return sb.ToString();
        }

        private string CapabilitiesExpr()
        {
            var caps = "global::Heddle.Precompiled.PrecompiledCapabilities.StringOutput";
            if (_config.EmitUtf8Pieces)
                caps += " | global::Heddle.Precompiled.PrecompiledCapabilities.Utf8Pieces";
            return caps;
        }
    }
}
