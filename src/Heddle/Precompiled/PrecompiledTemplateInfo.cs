using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Heddle.Data;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;

namespace Heddle.Precompiled
{
    /// <summary>One precompiled template as recorded in a compiled-form artifact row. Immutable; safe
    /// to share across threads. A row's presence in the entries is the precompiled fact.</summary>
    public sealed class PrecompiledTemplateInfo
    {
        private static readonly IReadOnlyList<PrecompiledImport> NoImports = Array.Empty<PrecompiledImport>();
        private static readonly IReadOnlyList<PrecompiledExtensionBinding> NoExtensions =
            Array.Empty<PrecompiledExtensionBinding>();
        private static readonly IReadOnlyList<PrecompiledFunctionBinding> NoFunctions =
            Array.Empty<PrecompiledFunctionBinding>();
        private static readonly IReadOnlyList<PrecompiledRefusalSite> NoRefusals =
            Array.Empty<PrecompiledRefusalSite>();
        private static readonly IReadOnlyList<CompiledMemberRow> NoMembers =
            Array.Empty<CompiledMemberRow>();

        /// <summary>The loader constructor: one row of an artifact decoded once and shared by every row
        /// that came out of the same image. Takes no strategy; <see cref="Strategy"/> materializes on first
        /// read over that shared graph and is memoized, and a materialization fault is memoized with it.
        /// Type identities resolve nominally against assemblies already loaded — nothing is loaded here.</summary>
        internal PrecompiledTemplateInfo(Assembly registeringAssembly, LoadedArtifact loaded, int rowIndex,
            IPrecompiledSiteTable siteTable = null)
        {
            if (registeringAssembly == null)
                throw new ArgumentNullException(nameof(registeringAssembly));
            if (loaded == null)
                throw new ArgumentNullException(nameof(loaded));
            var artifact = loaded.Artifact;
            if (artifact.Templates == null || rowIndex < 0 || rowIndex >= artifact.Templates.Count ||
                artifact.Templates[rowIndex] == null)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            var row = artifact.Templates[rowIndex];

            _loaded = loaded;
            _rowIndex = rowIndex;
            _siteTable = siteTable;

            Key = row.Key ?? throw new InvalidOperationException("A compiled-form template row has no key.");
            RegisteredName = row.RegisteredName;
            EntryPointType = ResolveEntryPoint(registeringAssembly, row.EntryPointTypeName);
            _modelTypeRef = row.ModelType;
            _modelTypeNominal = NamesAType(row.ModelType) ? row.ModelType.Nominal() : null;
            _modelType = NamesAType(row.ModelType) ? FindLoadedType(row.ModelType) : null;
            ModelTypeIsAmbient = row.ModelTypeIsAmbient;
            IsDynamic = row.IsDynamic;
            ContentHash = row.ContentHash ?? string.Empty;
            Imports = ToImports(row.Imports);
            OptionsFingerprint = ToFingerprint(row.Options, out _optionsFault);
            ExtensionBindings = ToExtensionBindings(artifact, row.ExtensionRefs);
            FunctionBindings = ToFunctionBindings(artifact, row.FunctionRefs);
            RefusalSites = row.RefusalSites != null
                ? (IReadOnlyList<PrecompiledRefusalSite>)new List<PrecompiledRefusalSite>(row.RefusalSites).AsReadOnly()
                : NoRefusals;
            MemberRows = OwnedMemberRows(artifact, loaded.SitesFor(rowIndex), rowIndex);
        }

        public string Key { get; }

        /// <summary>Optional registered template name, normalized by <see cref="TemplateKey"/> rules. Null when
        /// the item declared no usable name. The registry answers to it after keys, never instead of them.</summary>
        public string RegisteredName { get; }

        /// <summary>The generated static entry class.</summary>
        public Type EntryPointType { get; }

        /// <summary>The type the generated code was compiled against — the declared <c>@model</c>/<c>::</c> type, the
        /// <c>ModelType</c> item metadata when the template declares neither, and <c>System.Object</c> when nothing
        /// typed it. Null only in a hand-written manifest that declines to say.
        /// <para>Resolved by name against the assemblies loaded so far. Registration can run before the
        /// model's assembly loads (a module initializer, a plugin host), so a miss is not final: it is looked
        /// up again on each read until it resolves, and a resolved type is kept — the read is then one field
        /// load.</para></summary>
        public Type ModelType
        {
            get
            {
                var resolved = Volatile.Read(ref _modelType);
                if (resolved != null || _modelTypeNominal == null)
                    return resolved;
                resolved = FindLoadedType(_modelTypeRef);
                if (resolved != null)
                    Volatile.Write(ref _modelType, resolved);
                return resolved;
            }
        }

        /// <summary>True when the template declares no <c>@model</c> directive, so <see cref="ModelType"/> is the
        /// <b>build's</b> answer (the <c>ModelType</c> item metadata, else <c>System.Object</c>) rather than a type
        /// the template pins on both tiers. The dynamic tier would type the same template from the requesting
        /// <c>CompileContext</c>'s model type instead, so the gauntlet requires the two to agree before it will serve
        /// such an entry, and reports <see cref="PrecompiledFallbackReason.ModelTypeMismatch"/> when they do not.
        /// <para>False — the value every manifest written before this one existed reads back — means the template's
        /// own directive decides the model type on both tiers and the host's context type does not participate.</para>
        /// </summary>
        public bool ModelTypeIsAmbient { get; }

        public bool IsDynamic { get; }

        /// <summary>SHA-256 (lowercase hex) of the template's <b>decoded text</b> re-encoded as UTF-8 without a BOM
        /// — the canonical staleness identity computed by <see cref="ContentHash.HashText"/> on both
        /// tiers. Encoding-only differences (a BOM added, a UTF-16 re-save) therefore do not read as stale.</summary>
        public string ContentHash { get; }

        public IReadOnlyList<PrecompiledImport> Imports { get; }

        public PrecompiledOptionsFingerprint OptionsFingerprint { get; }

        public IReadOnlyList<PrecompiledExtensionBinding> ExtensionBindings { get; }

        /// <summary>Function bindings; empty when the template calls no functions, never null.</summary>
        public IReadOnlyList<PrecompiledFunctionBinding> FunctionBindings { get; }

        /// <summary>The generated root body. On a loader row materializes on first read and is
        /// memoized (thread-safe); null when materialization faulted — the fault is memoized and
        /// reported by the gauntlet, never thrown here.</summary>
        internal IProcessStrategy Strategy
        {
            get { return GetStrategy(null); }
        }

        /// <summary>Materializes under one request's options and memoizes per request shape:
        /// a late-bound expression compiles against the materializing request's function
        /// registry, file-backed partials and composition imports resolve under its paths, and
        /// definition recursion reads its limit. A null request materializes under defaults — the
        /// <see cref="Strategy"/> path. Unbound names fail the compile (the gauntlet reports them as
        /// <c>UnsupportedFunction</c> first); deferral never re-arms at load, so a missing name cannot
        /// silently become an empty render. Thread-safe; faults memoize like strategies.</summary>
        internal IProcessStrategy GetStrategy(TemplateOptions request)
        {
            var shape = RequestShape.For(request);
            IProcessStrategy memoized;
            // Copy-on-write publication, so a resolution that only reads an already-materialized shape —
            // which is every resolution after the first — takes no monitor.
            if (Volatile.Read(ref _requestStrategies).TryGetValue(shape, out memoized))
                return memoized;
            lock (_materializeLock)
            {
                IProcessStrategy strategy;
                if (_requestStrategies.TryGetValue(shape, out strategy))
                    return strategy;
                MaterializationFaultException fault = null;
                if (ModelTypeUnresolved)
                {
                    // Not memoized: the model's assembly may simply not have loaded yet, and a memoized
                    // fault would keep the entry on the fallback path for the life of the process.
                    _requestFaults[shape] = Fault(PrecompiledFallbackReason.MemberBindingMismatch,
                        "Type '" + _modelTypeNominal + "': manifest=" + AssemblyOf(_modelTypeRef) +
                        " live=<unresolved>");
                    return null;
                }

                try
                {
                    strategy = MaterializeNow(request);
                }
                catch (MaterializationFaultException e)
                {
                    fault = e;
                    strategy = null;
                }
                var updated = new Dictionary<RequestShape, IProcessStrategy>(_requestStrategies);
                updated.Add(shape, strategy);
                Volatile.Write(ref _requestStrategies, updated);
                _requestFaults.Remove(shape);
                if (fault != null)
                    _requestFaults.Add(shape, fault);
                if (shape.IsDefault)
                {
                    _materialized = true;
                    _materializationReason = fault != null ? (PrecompiledFallbackReason?)fault.Reason : null;
                    _materializationDetail = fault != null ? fault.Detail : null;
                }
                return strategy;
            }
        }

        /// <summary>The memoized materialization fault for one request shape, or null when that shape
        /// was never materialized or passed. Reading this never materializes.</summary>
        internal bool TryGetRequestFault(TemplateOptions request,
            out PrecompiledFallbackReason reason, out string detail)
        {
            reason = default(PrecompiledFallbackReason);
            detail = null;
            lock (_materializeLock)
            {
                MaterializationFaultException fault;
                if (!_requestFaults.TryGetValue(RequestShape.For(request), out fault) || fault == null)
                    return false;
                reason = fault.Reason;
                detail = fault.Detail;
                return true;
            }
        }

        /// <summary>The template's refusal sites, in site-ordinal order; empty for a fully precompiled template.
        /// Available without materializing.</summary>
        public IReadOnlyList<PrecompiledRefusalSite> RefusalSites { get; }

        /// <summary>The artifact's member rows, visible to the binding gate without materializing. Empty for a
        /// hand-written manifest, which carries no member identities; the gate then passes vacuously.</summary>
        internal IReadOnlyList<CompiledMemberRow> MemberRows { get; }

        /// <summary>The row's recorded root model type reference, for the binding gate's resolve-versus-compare
        /// order: the gate reports it when it resolves to nothing. Null when the row names none (dynamic).</summary>
        internal CompiledTypeRef ModelTypeRef => _modelTypeRef;

        /// <summary>Whether the recorded root model type resolves to no loaded type <b>yet</b>. The gate
        /// reports it; materialization reports the same fault without memoizing it, so the entry serves once
        /// the type's assembly has loaded.</summary>
        internal bool ModelTypeUnresolved => _modelTypeNominal != null && ModelType == null;

        /// <summary>The memoized materialization fault reason, when <see cref="Strategy"/> read faulted.
        /// Reading this never materializes: null both before the first read and when materialization passed.</summary>
        internal PrecompiledFallbackReason? MaterializationFaultReason
        {
            get
            {
                lock (_materializeLock)
                    return _materialized ? _materializationReason : null;
            }
        }

        /// <summary>The memoized materialization fault detail, in gauntlet format. Same laziness as
        /// <see cref="MaterializationFaultReason"/>.</summary>
        internal string MaterializationFaultDetail
        {
            get
            {
                lock (_materializeLock)
                    return _materialized ? _materializationDetail : null;
            }
        }

        private readonly LoadedArtifact _loaded;
        private readonly int _rowIndex;
        private readonly IPrecompiledSiteTable _siteTable;
        private readonly CompiledTypeRef _modelTypeRef;
        private readonly string _modelTypeNominal;
        private readonly string _optionsFault;
        private Type _modelType;

        private readonly object _materializeLock = new object();
        private bool _materialized;
        private PrecompiledFallbackReason? _materializationReason;
        private string _materializationDetail;
        private Dictionary<RequestShape, IProcessStrategy> _requestStrategies =
            new Dictionary<RequestShape, IProcessStrategy>();
        private readonly Dictionary<RequestShape, MaterializationFaultException> _requestFaults =
            new Dictionary<RequestShape, MaterializationFaultException>();

        private readonly object _verdictLock = new object();
        private GauntletVerdicts _verdicts;

        /// <summary>The detail a present-but-unreadable recorded option left behind, or null when the row's
        /// options read cleanly. The gauntlet's options step reports it before it compares anything, so an
        /// unreadable value is refused rather than standing in for a default the row never asked for.</summary>
        internal string OptionsFault => _optionsFault;

        /// <summary>The request shapes whose gauntlet run passed, and the assembly generation they passed at.
        /// Immutable once published; a generation move replaces the whole instance rather than clearing it, so
        /// a reader either sees a verdict set that is current or sees one it rejects outright.</summary>
        private sealed class GauntletVerdicts
        {
            internal GauntletVerdicts(int generation, HashSet<PrecompiledGauntlet.Shape> passed)
            {
                Generation = generation;
                Passed = passed;
            }

            internal int Generation { get; }

            internal HashSet<PrecompiledGauntlet.Shape> Passed { get; }
        }

        /// <summary>Whether the ordered checks already passed for this shape at this generation. The verdict is
        /// a pure function of the row, the shape and the live type graph, and the graph only moves when the
        /// engine's assembly set does — which is what the generation counts.</summary>
        internal bool GauntletPassed(in PrecompiledGauntlet.Shape shape)
        {
            var verdicts = Volatile.Read(ref _verdicts);
            return verdicts != null && verdicts.Generation == shape.Generation && verdicts.Passed.Contains(shape);
        }

        /// <summary>Records a pass. Only passes are memoized: a failure costs the dynamic tier's own compile,
        /// which dwarfs the checks, and several failure modes (an unresolved model type, an unresolved member
        /// start type) are waiting on an assembly load that need not move the generation to happen.</summary>
        internal void RecordGauntletPass(in PrecompiledGauntlet.Shape shape)
        {
            lock (_verdictLock)
            {
                var current = Volatile.Read(ref _verdicts);
                var passed = current != null && current.Generation == shape.Generation
                    ? new HashSet<PrecompiledGauntlet.Shape>(current.Passed)
                    : new HashSet<PrecompiledGauntlet.Shape>();
                passed.Add(shape);
                Volatile.Write(ref _verdicts, new GauntletVerdicts(shape.Generation, passed));
            }
        }

        private sealed class MaterializationFaultException : Exception
        {
            internal MaterializationFaultException(PrecompiledFallbackReason reason, string detail)
                : base(detail)
            {
                Reason = reason;
                Detail = detail;
            }

            internal PrecompiledFallbackReason Reason { get; }

            internal string Detail { get; }
        }

        private static MaterializationFaultException Fault(PrecompiledFallbackReason reason, string detail) =>
            new MaterializationFaultException(reason, detail);

        /// <summary>The compile-varying slice of a request's options: the function registry by
        /// reference identity (one re-compile per registry instance), the file settings file
        /// IO resolves under, the recursion limit hooks read, and the parse-shaping flags. Output
        /// profile, expression mode and directive trimming ride the row fingerprint instead — the
        /// gauntlet enforces their equality before any strategy is read.</summary>
        private readonly struct RequestShape : IEquatable<RequestShape>
        {
            internal static RequestShape For(TemplateOptions request)
            {
                if (request == null)
                    return new RequestShape(true, null, null, null, 0, false, false, false);
                return new RequestShape(false,
                    request.Functions,
                    request.RootPath,
                    request.FileNamePostfix,
                    request.MaxRecursionCount,
                    request.EnableFileChangeCheck,
                    request.ProvideLanguageFeatures,
                    // Strict materialization throws where a lax one succeeds: different
                    // outcomes memoize under different shapes, or a lax render would
                    // silently disarm a later strict bind of the same entry.
                    request.PrecompiledStrictLoad);
            }

            private RequestShape(bool isNull, object functions, string rootPath, string fileNamePostfix,
                int maxRecursionCount, bool enableFileChangeCheck, bool provideLanguageFeatures,
                bool precompiledStrictLoad)
            {
                IsNull = isNull;
                Functions = functions;
                RootPath = rootPath;
                FileNamePostfix = fileNamePostfix;
                MaxRecursionCount = maxRecursionCount;
                EnableFileChangeCheck = enableFileChangeCheck;
                ProvideLanguageFeatures = provideLanguageFeatures;
                PrecompiledStrictLoad = precompiledStrictLoad;
            }

            /// <summary>True for the null request only: an options instance carrying defaults is its
            /// own shape, since its fields (a non-zero default recursion limit, for one) compile
            /// differently from a null request's blank slate.</summary>
            internal bool IsDefault => IsNull;

            private readonly bool IsNull;
            private readonly object Functions;
            private readonly string RootPath;
            private readonly string FileNamePostfix;
            private readonly int MaxRecursionCount;
            private readonly bool EnableFileChangeCheck;
            private readonly bool ProvideLanguageFeatures;
            private readonly bool PrecompiledStrictLoad;

            public bool Equals(RequestShape other)
            {
                if (IsNull || other.IsNull)
                    return IsNull && other.IsNull;
                return ReferenceEquals(Functions, other.Functions) &&
                    string.Equals(RootPath, other.RootPath, StringComparison.Ordinal) &&
                    string.Equals(FileNamePostfix, other.FileNamePostfix, StringComparison.Ordinal) &&
                    MaxRecursionCount == other.MaxRecursionCount &&
                    EnableFileChangeCheck == other.EnableFileChangeCheck &&
                    ProvideLanguageFeatures == other.ProvideLanguageFeatures &&
                    PrecompiledStrictLoad == other.PrecompiledStrictLoad;
            }

            public override bool Equals(object obj) => obj is RequestShape other && Equals(other);

            public override int GetHashCode()
            {
                if (IsNull)
                    return 0;
                int hash = MaxRecursionCount;
                hash = (hash * 397) ^ (Functions != null ?
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Functions) : 0);
                hash = (hash * 397) ^ (RootPath != null ? StringComparer.Ordinal.GetHashCode(RootPath) : 0);
                hash = (hash * 397) ^ (FileNamePostfix != null ?
                    StringComparer.Ordinal.GetHashCode(FileNamePostfix) : 0);
                hash = (hash * 397) ^ (EnableFileChangeCheck ? 1 : 0);
                hash = (hash * 397) ^ (ProvideLanguageFeatures ? 1 : 0);
                hash = (hash * 397) ^ (PrecompiledStrictLoad ? 1 : 0);
                return hash;
            }
        }

        /// <summary>Runs the materialization entry over the kept bytes. Called once per request
        /// shape under <see cref="_materializeLock"/>; every failure mode becomes a
        /// <see cref="MaterializationFaultException"/> in gauntlet taxonomy, never a raw exception.</summary>
        private IProcessStrategy MaterializeNow(TemplateOptions request)
        {
            var artifact = _loaded.Artifact;

            if (artifact.Templates == null || _rowIndex < 0 || _rowIndex >= artifact.Templates.Count ||
                artifact.Templates[_rowIndex] == null)
                throw Fault(PrecompiledFallbackReason.ExtensionInitCompileError,
                    "Template '" + Key + "' names no document in this artifact.");
            var row = artifact.Templates[_rowIndex];

            if (request != null && request.PrecompiledStrictLoad && row.RefusalSites != null &&
                row.RefusalSites.Count > 0)
                throw new PrecompiledStrictLoadException(Key, row.RefusalSites[0].SiteOrdinal, "RefusalSite");

            // The gauntlet refuses an unreadable recorded option before it reaches here; a direct
            // strategy read has no gauntlet in front of it, so the refusal is repeated.
            if (_optionsFault != null)
                throw Fault(PrecompiledFallbackReason.OptionsMismatch, _optionsFault);

            var options = new TemplateOptions(row.Key);
            var fingerprint = OptionsFingerprint;
            options.OutputProfile = fingerprint.Profile;
            options.ExpressionMode = fingerprint.ExpressionMode;
            options.TrimDirectiveLines = fingerprint.TrimDirectiveLines;
            if (request != null)
            {
                options.PrecompiledStrictLoad = request.PrecompiledStrictLoad;
                // Request-scoped compile inputs: the registry late-bound sites bind against, the
                // paths file-backed partials and composition imports read from, the recursion limit
                // definition hooks enforce, and the parse-shaping flags. Profile, mode and trimming
                // stay on the row fingerprint above — the gauntlet gates those before this runs.
                options.Functions = request.Functions;
                options.RootPath = request.RootPath;
                options.FileNamePostfix = request.FileNamePostfix;
                options.MaxRecursionCount = request.MaxRecursionCount;
                options.EnableFileChangeCheck = request.EnableFileChangeCheck;
                options.ProvideLanguageFeatures = request.ProvideLanguageFeatures;
            }

            RuntimeDocument document;
            CompileScope scope;
            try
            {
                // A null ModelType past the unresolved check above is a dynamic row (null ref or
                // DynamicTypeRef): the build typed member reads dynamically, so the load context must
                // too — typeof(object) would statically reject the same reads (HED0001) and trip the
                // consumed-type check against the recorded dynamic typings.
                ExType modelEx = ModelType == null ? ExType.Dynamic : new ExType(ModelType);
                var context = new CompileContext(options, modelEx);
                // The load re-parse expands composition imports from the same spellings the build
                // saw: every row's key and registered name serves its recorded root text, anything
                // else reads off disk. A named import the artifact does not carry stays a file read.
                var importContents = _loaded.ImportContents;
                context.ImportReader = ImportMap.ReaderFor(importContents, options.RootPath);
                context.ImportIdentifier = ImportMap.IdentifierFor(importContents, options.RootPath);
                scope = new CompileScope(context);
                scope.SiteTableState = SiteTableState.Create(_siteTable, Heddle.Runtime.HeddleFeatures.UseGeneratedSites,
                    Key, row.ContentHash ?? string.Empty, _rowIndex, options.PrecompiledStrictLoad,
                    _loaded.SitesFor(_rowIndex), artifact);
                document = HeddleCompiler.Materialize(artifact, row, scope);
            }
            catch (PrecompiledStrictLoadException)
            {
                throw;
            }
            catch (MaterializationFaultException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw Fault(PrecompiledFallbackReason.ExtensionInitCompileError,
                    "Template '" + Key + "' failed to materialize: " + e.GetType().Name + ": " + e.Message);
            }

            if (scope.CompileErrors.Count != 0)
                throw ClassifyCompileErrors(Key, scope.CompileErrors);

            if (document == null || document.Strategy == null)
                throw Fault(PrecompiledFallbackReason.ExtensionInitCompileError,
                    "Template '" + Key + "' materialized to no strategy.");
            // The dynamic tier provisions the root locals frame in HeddleTemplate.Render; the
            // precompiled sinks provision none (the frame rides the strategy). A needs-locals root
            // (any [ScopeChannel] participant: branches, slots) must therefore self-wrap, or its
            // opener's published state is invisible to its terminal at render.
            return document.NeedsLocals
                ? (IProcessStrategy)new LocalsFrameStrategy(document.Strategy)
                : document.Strategy;
        }

        /// <summary>The loader's <c>ScopeLocals</c>-provisioning decorator, for the one body materialization
        /// produces but no render provisions: a document root hosting branch participants. Every Render/Execute
        /// runs under a fresh frame; roots without participants stay unwrapped.</summary>
        private sealed class LocalsFrameStrategy : IProcessStrategy
        {
            private readonly IProcessStrategy _inner;

            public LocalsFrameStrategy(IProcessStrategy inner) => _inner = inner;

            public string Execute(in Scope scope) => _inner.Execute(scope.WithLocals(new ScopeLocals()));

            public void Render(in Scope scope) => _inner.Render(scope.WithLocals(new ScopeLocals()));
        }

        private static MaterializationFaultException ClassifyCompileErrors(string key,
            List<HeddleCompileError> errors)
        {
            foreach (var error in errors)
            {
                var mismatch = FindTypingMismatch(error != null ? error.Exception : null);
                if (mismatch != null)
                    return Fault(PrecompiledFallbackReason.ExtensionInitTypingMismatch, mismatch);
            }

            var first = errors.Count != 0 ? errors[0] : null;
            var detail = first != null
                ? (first.DiagnosticId != null ? first.DiagnosticId + ": " : string.Empty) + (first.Error ?? "unknown compile error")
                : "unknown compile error";
            return Fault(PrecompiledFallbackReason.ExtensionInitCompileError,
                "Template '" + key + "' reported " + errors.Count + " compile error(s); the first is " + detail + ".");
        }

        private static string FindTypingMismatch(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                var mismatch = current as PrecompiledMismatchException;
                if (mismatch != null &&
                    mismatch.Reason == PrecompiledFallbackReason.ExtensionInitTypingMismatch)
                    return mismatch.Detail;
            }

            return null;
        }

        /// <summary>The assembly half of a recorded type reference's identity: the named assembly, or the
        /// definition's for a constructed generic and the element's for an array. Structural rather than a cut
        /// on the nominal string, whose last comma sits inside the argument list for a generic.</summary>
        internal static string AssemblyOf(CompiledTypeRef typeRef)
        {
            var named = typeRef as NamedTypeRef;
            if (named != null)
                return named.AssemblySimpleName;
            var generic = typeRef as GenericTypeRef;
            if (generic != null)
                return AssemblyOf(generic.Definition);
            var array = typeRef as ArrayTypeRef;
            if (array != null)
                return AssemblyOf(array.Element);
            return AqnFormatter.Unknown;
        }

        private static IReadOnlyList<PrecompiledImport> ToImports(IList<CompiledImport> imports)
        {
            if (imports == null || imports.Count == 0)
                return Array.Empty<PrecompiledImport>();
            var converted = new PrecompiledImport[imports.Count];
            for (int i = 0; i < imports.Count; i++)
                converted[i] = new PrecompiledImport(imports[i].Key, imports[i].ContentHash);
            return converted;
        }

        /// <summary>Reads a row's recorded options. A missing value is the build's default; a value that is
        /// present and names no member is <b>refused</b> through <paramref name="fault"/> rather than guessed
        /// at, because the guess <c>Enum.TryParse</c> writes on failure is the zero member — which for
        /// <see cref="OutputProfile"/> is the one that encodes nothing. The refused row keeps the seeded
        /// defaults so nothing downstream reads a zero it never recorded, and the gauntlet's options step
        /// turns the fault into an <see cref="PrecompiledFallbackReason.OptionsMismatch"/> before it compares
        /// anything.</summary>
        private static PrecompiledOptionsFingerprint ToFingerprint(CompiledOptionsFingerprint options,
            out string fault)
        {
            fault = null;
            var profile = HeddleBuildOptions.DefaultOutputProfile;
            var mode = HeddleBuildOptions.DefaultExpressionMode;
            var trim = HeddleBuildOptions.DefaultTrimDirectiveLines;
            if (options != null)
            {
                if (!HeddleBuildOptions.TryReadEnum(options.Profile, profile, out profile))
                {
                    profile = HeddleBuildOptions.DefaultOutputProfile;
                    fault = "OutputProfile: manifest='" + options.Profile + "' names no profile; expected " +
                        HeddleBuildOptions.ExpectedValues<OutputProfile>();
                }
                else if (!HeddleBuildOptions.TryReadEnum(options.Mode, mode, out mode))
                {
                    mode = HeddleBuildOptions.DefaultExpressionMode;
                    fault = "ExpressionMode: manifest='" + options.Mode + "' names no mode; expected " +
                        HeddleBuildOptions.ExpectedValues<ExpressionMode>();
                }

                trim = options.Trim;
            }

            return new PrecompiledOptionsFingerprint(profile, mode, trim);
        }

        private static IReadOnlyList<PrecompiledExtensionBinding> ToExtensionBindings(CompiledArtifact artifact,
            IList<int> refs)
        {
            if (refs == null || refs.Count == 0)
                return Array.Empty<PrecompiledExtensionBinding>();
            var converted = new PrecompiledExtensionBinding[refs.Count];
            for (int i = 0; i < refs.Count; i++)
            {
                var row = artifact.Extensions[refs[i]];
                converted[i] = new PrecompiledExtensionBinding(
                    row.RegistryName, row.Type != null ? row.Type.Nominal() : AqnFormatter.Unknown,
                    row.Fingerprint);
            }

            return converted;
        }

        private static IReadOnlyList<PrecompiledFunctionBinding> ToFunctionBindings(CompiledArtifact artifact,
            IList<int> refs)
        {
            if (refs == null || refs.Count == 0)
                return Array.Empty<PrecompiledFunctionBinding>();
            var converted = new PrecompiledFunctionBinding[refs.Count];
            for (int i = 0; i < refs.Count; i++)
            {
                var row = artifact.Functions[refs[i]];
                converted[i] = new PrecompiledFunctionBinding(
                    row.Name, row.Target != null ? row.Target.Nominal() : null, row.OverloadCount);
            }

            return converted;
        }

        /// <summary>The member rows this template's own member-accessor sites reference. The merged
        /// artifact concatenates every template's rows, and a row another template recorded must not
        /// decide this template's gauntlet verdict. A single-template artifact owns every row, and so
        /// does a template in an artifact that carries no site table at all (hand-built rows).</summary>
        private static IReadOnlyList<CompiledMemberRow> OwnedMemberRows(CompiledArtifact artifact,
            IList<CompiledSiteRow> ownedSites, int rowIndex)
        {
            if (artifact.Members == null || artifact.Members.Count == 0)
                return NoMembers;
            if (artifact.Templates.Count == 1 || artifact.Sites == null || artifact.Sites.Count == 0)
                return new List<CompiledMemberRow>(artifact.Members).AsReadOnly();
            var owned = new List<CompiledMemberRow>();
            var seen = new HashSet<int>();
            foreach (var site in ownedSites)
            {
                if (site == null || site.TemplateIndex != rowIndex || site.Kind != CompiledSiteKind.MemberAccessor)
                    continue;
                if (site.PayloadRef < 0 || site.PayloadRef >= artifact.Members.Count || !seen.Add(site.PayloadRef))
                    continue;
                owned.Add(artifact.Members[site.PayloadRef]);
            }
            return owned.AsReadOnly();
        }

        /// <summary>Resolves the row's wrapper type name on the registering assembly only. Null when the row
        /// names none or the name does not resolve there; never loads anything.</summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Resolves an identity the engine recorded itself over registered/loaded assemblies; a trimmed publish resolves only what it kept, and a miss reads as unresolved (a diagnostic or gauntlet mismatch), never a crash.")]
        private static Type ResolveEntryPoint(Assembly registeringAssembly, string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;
            try
            {
                return registeringAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool NamesAType(CompiledTypeRef typeRef) => typeRef != null && !(typeRef is DynamicTypeRef);

        /// <summary>Resolves a recorded type reference against assemblies already loaded into the default
        /// load context, or null when it resolves to nothing. Loads nothing; shared with the binding gate,
        /// which resolves each member row's start type by the same rule.</summary>
        [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "Resolves an identity the engine recorded itself over registered/loaded assemblies; a trimmed publish resolves only what it kept, and a miss reads as unresolved (a diagnostic or gauntlet mismatch), never a crash.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Resolves an identity the engine recorded itself over registered/loaded assemblies; a trimmed publish resolves only what it kept, and a miss reads as unresolved (a diagnostic or gauntlet mismatch), never a crash.")]
        internal static Type FindLoadedType(CompiledTypeRef typeRef)
        {
            try
            {
                var named = typeRef as NamedTypeRef;
                if (named != null)
                    return FindLoadedNamedType(named.FullName, named.AssemblySimpleName, named.IsFramework);
                var generic = typeRef as GenericTypeRef;
                if (generic != null)
                {
                    var definition = FindLoadedType(generic.Definition);
                    if (definition == null || generic.Arguments == null)
                        return null;
                    var arguments = new Type[generic.Arguments.Count];
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        arguments[i] = FindLoadedType(generic.Arguments[i]);
                        if (arguments[i] == null)
                            return null;
                    }

                    return definition.MakeGenericType(arguments);
                }

                var array = typeRef as ArrayTypeRef;
                if (array != null)
                {
                    var element = FindLoadedType(array.Element);
                    if (element == null)
                        return null;
                    return array.Rank <= 1 ? element.MakeArrayType() : element.MakeArrayType(array.Rank);
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        /// <summary>The same resolution, memoized per recorded reference. A resolved type stays resolved — the
        /// default load context never gives an assembly back — so only successes are kept, and a reference that
        /// resolves to nothing is tried again on the next read, exactly as <see cref="ModelType"/> retries.
        /// The table is weak on the reference, which belongs to a decoded artifact, so a registry reset lets
        /// both go.</summary>
        internal static Type FindLoadedTypeCached(CompiledTypeRef typeRef)
        {
            if (typeRef == null || typeRef is DynamicTypeRef)
                return null;
            ResolvedType box;
            if (ResolvedTypes.TryGetValue(typeRef, out box))
            {
                var kept = Volatile.Read(ref box.Type);
                if (kept != null)
                    return kept;
            }
            else
            {
                box = ResolvedTypes.GetValue(typeRef, NewResolvedType);
            }

            var resolved = FindLoadedType(typeRef);
            if (resolved != null)
                Volatile.Write(ref box.Type, resolved);
            return resolved;
        }

        private sealed class ResolvedType
        {
            internal Type Type;
        }

        private static readonly ConditionalWeakTable<CompiledTypeRef, ResolvedType> ResolvedTypes =
            new ConditionalWeakTable<CompiledTypeRef, ResolvedType>();

        private static readonly ConditionalWeakTable<CompiledTypeRef, ResolvedType>.CreateValueCallback
            NewResolvedType = _ => new ResolvedType();

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Resolves an identity the engine recorded itself over registered/loaded assemblies; a trimmed publish resolves only what it kept, and a miss reads as unresolved (a diagnostic or gauntlet mismatch), never a crash.")]
        private static Type FindLoadedNamedType(string fullName, string assemblySimpleName, bool isFramework)
        {
            if (string.IsNullOrEmpty(fullName))
                return null;
            // A framework ref's assembly name is advisory (the writer saw System.Private.CoreLib
            // where net48 has mscorlib), so it resolves by full name alone: the core library first, then
            // every assembly in the default load context. A non-framework ref keeps its authoritative
            // simple-name match below.
            if (isFramework)
            {
                try
                {
                    var core = typeof(object).Assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (core != null)
                        return core;
                }
                catch (Exception)
                {
                }
            }
            Assembly[] loaded;
            try
            {
                loaded = AppDomain.CurrentDomain.GetAssemblies();
            }
            catch (Exception)
            {
                return null;
            }

            foreach (var assembly in loaded)
            {
                if (assembly == null || assembly.IsDynamic)
                    continue;
                string name;
                try
                {
                    name = assembly.GetName().Name;
                }
                catch (Exception)
                {
                    continue;
                }

                if (!isFramework && !string.Equals(name, assemblySimpleName, StringComparison.Ordinal))
                    continue;
                try
                {
                    var resolved = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (resolved != null)
                        return resolved;
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return null;
        }
    }

    /// <summary>One artifact image, decoded <b>once</b> and shared by every row registered from it, together
    /// with the per-artifact indexes each materialization would otherwise rebuild.
    /// <para>An assembly ships one merged artifact for all of its templates, so decoding per row meant decoding
    /// every template's strings, types, documents, definitions and expression trees once per template and again
    /// per request shape. Nothing in the materialization path writes to the graph — the form cursor, the site
    /// table state and the form compiler only read it — so one decode serves them all, concurrently included.</para>
    /// <para>Held by strong reference rather than weakly: the registry has no un-register, the raw image it
    /// replaces was held for the same lifetime, and a reclaimed graph would only be decoded again by the next
    /// request shape.</para></summary>
    internal sealed class LoadedArtifact
    {
        private static readonly CompiledSiteRow[] NoSites = Array.Empty<CompiledSiteRow>();

        private readonly IList<CompiledSiteRow>[] _sitesByTemplate;

        internal LoadedArtifact(CompiledArtifact artifact)
        {
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));
            Artifact = artifact;
            _sitesByTemplate = GroupSites(artifact);
            ImportContents = BuildImportContents(artifact);
        }

        internal CompiledArtifact Artifact { get; }

        /// <summary>The root text every row of this artifact serves to a composition import, by key and by
        /// registered name. One map per artifact: it describes the artifact, not the request.</summary>
        internal IReadOnlyDictionary<string, string> ImportContents { get; }

        /// <summary>The site rows belonging to one template, in artifact order. Grouped once, because the
        /// merged artifact concatenates every template's rows and scanning all of them per template is
        /// quadratic across an assembly.</summary>
        internal IList<CompiledSiteRow> SitesFor(int templateIndex) =>
            templateIndex >= 0 && templateIndex < _sitesByTemplate.Length
                ? _sitesByTemplate[templateIndex]
                : NoSites;

        private static IList<CompiledSiteRow>[] GroupSites(CompiledArtifact artifact)
        {
            int templates = artifact.Templates != null ? artifact.Templates.Count : 0;
            var groups = new IList<CompiledSiteRow>[templates];
            var sites = artifact.Sites;
            if (sites == null || sites.Count == 0)
            {
                for (int i = 0; i < templates; i++)
                    groups[i] = NoSites;
                return groups;
            }

            var lists = new List<CompiledSiteRow>[templates];
            foreach (var site in sites)
            {
                // A null row carries no template index, so it stays in every group — the ordinal-indexed
                // consumption state downstream is built over exactly the list it is handed.
                if (site == null)
                {
                    for (int i = 0; i < templates; i++)
                        (lists[i] ?? (lists[i] = new List<CompiledSiteRow>())).Add(null);
                    continue;
                }

                int index = site.TemplateIndex;
                if (index < 0 || index >= templates)
                    continue;
                (lists[index] ?? (lists[index] = new List<CompiledSiteRow>())).Add(site);
            }

            for (int i = 0; i < templates; i++)
                groups[i] = (IList<CompiledSiteRow>)lists[i] ?? NoSites;
            return groups;
        }

        private static IReadOnlyDictionary<string, string> BuildImportContents(CompiledArtifact artifact)
        {
            var contents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (artifact.Templates == null || artifact.Documents == null)
                return contents;
            foreach (var template in artifact.Templates)
            {
                if (template == null || template.RootDocumentRef < 0 ||
                    template.RootDocumentRef >= artifact.Documents.Count)
                    continue;
                var document = artifact.Documents[template.RootDocumentRef];
                string text = document?.RawText;
                if (string.IsNullOrEmpty(text))
                    continue;
                if (!string.IsNullOrEmpty(template.Key) && !contents.ContainsKey(template.Key))
                    contents.Add(template.Key, text);
                if (!string.IsNullOrEmpty(template.RegisteredName) &&
                    !contents.ContainsKey(template.RegisteredName))
                    contents.Add(template.RegisteredName, text);
            }

            return contents;
        }
    }
}
