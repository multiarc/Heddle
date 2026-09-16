using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;
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

        /// <summary>The loader constructor: one row decoded from a compiled-form artifact. Takes no strategy;
        /// <see cref="Strategy"/> materializes on first read over the kept artifact bytes and is memoized, and a
        /// materialization fault is memoized with it. Type identities resolve nominally against assemblies
        /// already loaded — nothing is loaded here.</summary>
        internal PrecompiledTemplateInfo(Assembly registeringAssembly, byte[] artifactImage,
            CompiledArtifact artifact, int rowIndex, IPrecompiledSiteTable siteTable = null)
        {
            if (registeringAssembly == null)
                throw new ArgumentNullException(nameof(registeringAssembly));
            if (artifactImage == null)
                throw new ArgumentNullException(nameof(artifactImage));
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));
            if (artifact.Templates == null || rowIndex < 0 || rowIndex >= artifact.Templates.Count ||
                artifact.Templates[rowIndex] == null)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            var row = artifact.Templates[rowIndex];

            _artifactImage = artifactImage;
            _rowIndex = rowIndex;
            _siteTable = siteTable;

            Key = row.Key ?? throw new InvalidOperationException("A compiled-form template row has no key.");
            RegisteredName = row.RegisteredName;
            EntryPointType = ResolveEntryPoint(registeringAssembly, row.EntryPointTypeName);
            _modelTypeRef = row.ModelType;
            ModelType = ResolveModelType(row.ModelType, out _modelTypeNominal, out _modelTypeUnresolved);
            ModelTypeIsAmbient = row.ModelTypeIsAmbient;
            IsDynamic = row.IsDynamic;
            ContentHash = row.ContentHash ?? string.Empty;
            Imports = ToImports(row.Imports);
            OptionsFingerprint = ToFingerprint(row.Options);
            ExtensionBindings = ToExtensionBindings(artifact, row.ExtensionRefs);
            FunctionBindings = ToFunctionBindings(artifact, row.FunctionRefs);
            RefusalSites = row.RefusalSites != null
                ? (IReadOnlyList<PrecompiledRefusalSite>)new List<PrecompiledRefusalSite>(row.RefusalSites).AsReadOnly()
                : NoRefusals;
            MemberRows = artifact.Members != null
                ? (IReadOnlyList<CompiledMemberRow>)new List<CompiledMemberRow>(artifact.Members).AsReadOnly()
                : NoMembers;
        }

        public string Key { get; }

        /// <summary>Optional registered template name, normalized by <see cref="TemplateKey"/> rules. Null when
        /// the item declared no usable name. The registry answers to it after keys, never instead of them.</summary>
        public string RegisteredName { get; }

        /// <summary>The generated static entry class.</summary>
        public Type EntryPointType { get; }

        /// <summary>The type the generated code was compiled against — the declared <c>@model</c>/<c>::</c> type, the
        /// <c>ModelType</c> item metadata when the template declares neither, and <c>System.Object</c> when nothing
        /// typed it. Null only in a hand-written manifest that declines to say.</summary>
        public Type ModelType { get; }

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

        /// <summary>Materializes under one request's options and memoizes per request shape
        /// (P1-R11): a late-bound expression compiles against the materializing request's function
        /// registry, file-backed partials and composition imports resolve under its paths, and
        /// definition recursion reads its limit. A null request materializes under defaults — the
        /// <see cref="Strategy"/> path. Unbound names fail the compile (the gauntlet reports them as
        /// <c>UnsupportedFunction</c> first); deferral never re-arms at load, so a missing name cannot
        /// silently become an empty render. Thread-safe; faults memoize like strategies.</summary>
        internal IProcessStrategy GetStrategy(TemplateOptions request)
        {
            var shape = RequestShape.For(request);
            lock (_materializeLock)
            {
                IProcessStrategy strategy;
                if (_requestStrategies.TryGetValue(shape, out strategy))
                    return strategy;
                MaterializationFaultException fault = null;
                try
                {
                    strategy = MaterializeNow(request);
                }
                catch (MaterializationFaultException e)
                {
                    fault = e;
                    strategy = null;
                }
                _requestStrategies.Add(shape, strategy);
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

        /// <summary>Whether the recorded root model type resolved to no loaded type. The gate reports it;
        /// materialization memoizes the same fault.</summary>
        internal bool ModelTypeUnresolved => _modelTypeUnresolved;

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

        private readonly byte[] _artifactImage;
        private readonly int _rowIndex;
        private readonly IPrecompiledSiteTable _siteTable;
        private readonly CompiledTypeRef _modelTypeRef;
        private readonly string _modelTypeNominal;
        private readonly bool _modelTypeUnresolved;

        private readonly object _materializeLock = new object();
        private bool _materialized;
        private PrecompiledFallbackReason? _materializationReason;
        private string _materializationDetail;
        private readonly Dictionary<RequestShape, IProcessStrategy> _requestStrategies =
            new Dictionary<RequestShape, IProcessStrategy>();
        private readonly Dictionary<RequestShape, MaterializationFaultException> _requestFaults =
            new Dictionary<RequestShape, MaterializationFaultException>();

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
        /// reference identity (P1-R11: one re-compile per registry instance), the file settings file
        /// IO resolves under, the recursion limit hooks read, and the parse-shaping flags. Output
        /// profile, expression mode and directive trimming ride the row fingerprint instead — the
        /// gauntlet enforces their equality before any strategy is read.</summary>
        private sealed class RequestShape : IEquatable<RequestShape>
        {
            internal static RequestShape For(TemplateOptions request)
            {
                if (request == null)
                    return new RequestShape { IsNull = true };
                return new RequestShape
                {
                    Functions = request.Functions,
                    RootPath = request.RootPath,
                    FileNamePostfix = request.FileNamePostfix,
                    MaxRecursionCount = request.MaxRecursionCount,
                    EnableFileChangeCheck = request.EnableFileChangeCheck,
                    ProvideLanguageFeatures = request.ProvideLanguageFeatures,
                    // Strict materialization throws where a lax one succeeds: different
                    // outcomes memoize under different shapes, or a lax render would
                    // silently disarm a later strict bind of the same entry.
                    PrecompiledStrictLoad = request.PrecompiledStrictLoad
                };
            }

            /// <summary>True for the null request only: an options instance carrying defaults is its
            /// own shape, since its fields (a non-zero default recursion limit, for one) compile
            /// differently from a null request's blank slate.</summary>
            internal bool IsDefault => IsNull;

            private bool IsNull;
            private object Functions;
            private string RootPath;
            private string FileNamePostfix;
            private int MaxRecursionCount;
            private bool EnableFileChangeCheck;
            private bool ProvideLanguageFeatures;
            private bool PrecompiledStrictLoad;

            public bool Equals(RequestShape other)
            {
                if (ReferenceEquals(other, null))
                    return false;
                if (ReferenceEquals(this, other))
                    return true;
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

            public override bool Equals(object obj) => Equals(obj as RequestShape);

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

        /// <summary>Runs the P1-W3 materialization entry over the kept bytes. Called once per request
        /// shape under <see cref="_materializeLock"/>; every failure mode becomes a
        /// <see cref="MaterializationFaultException"/> in gauntlet taxonomy, never a raw exception.</summary>
        private IProcessStrategy MaterializeNow(TemplateOptions request)
        {
            CompiledArtifact artifact;
            try
            {
                artifact = CompiledFormReader.Read(_artifactImage);
            }
            catch (Exception e)
            {
                throw Fault(PrecompiledFallbackReason.ExtensionInitCompileError,
                    "Template '" + Key + "' has an unreadable artifact image: " + e.GetType().Name + ": " +
                    e.Message);
            }

            if (artifact.Templates == null || _rowIndex < 0 || _rowIndex >= artifact.Templates.Count ||
                artifact.Templates[_rowIndex] == null)
                throw Fault(PrecompiledFallbackReason.ExtensionInitCompileError,
                    "Template '" + Key + "' names no document in this artifact.");
            var row = artifact.Templates[_rowIndex];

            if (_modelTypeUnresolved)
                throw Fault(PrecompiledFallbackReason.MemberBindingMismatch,
                    "Type '" + _modelTypeNominal + "': manifest=" + AssemblyOf(row.ModelType) +
                    " live=<unresolved>");

            if (request != null && request.PrecompiledStrictLoad && row.RefusalSites != null &&
                row.RefusalSites.Count > 0)
                throw new PrecompiledStrictLoadException(Key, row.RefusalSites[0].SiteOrdinal, "RefusalSite");

            var options = new TemplateOptions(row.Key);
            var fingerprint = ToFingerprint(row.Options);
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
                var importContents = ArtifactImportContents(artifact);
                context.ImportReader = ImportMap.ReaderFor(importContents, options.RootPath);
                context.ImportIdentifier = ImportMap.IdentifierFor(importContents, options.RootPath);
                scope = new CompileScope(context);
                scope.SiteTableState = SiteTableState.Create(_siteTable, Heddle.Runtime.HeddleFeatures.UseGeneratedSites,
                    Key, row.ContentHash ?? string.Empty, _rowIndex, options.PrecompiledStrictLoad,
                    artifact.Sites, artifact);
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

            IReadOnlyDictionary<string, string> ArtifactImportContents(CompiledArtifact image)
            {
                var contents = new Dictionary<string, string>(StringComparer.Ordinal);
                if (image.Templates == null || image.Documents == null)
                    return contents;
                foreach (var template in image.Templates)
                {
                    if (template == null || template.RootDocumentRef < 0 ||
                        template.RootDocumentRef >= image.Documents.Count)
                        continue;
                    var document = image.Documents[template.RootDocumentRef];
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

        private static PrecompiledOptionsFingerprint ToFingerprint(CompiledOptionsFingerprint options)
        {
            var profile = HeddleBuildOptions.DefaultOutputProfile;
            var mode = HeddleBuildOptions.DefaultExpressionMode;
            var trim = HeddleBuildOptions.DefaultTrimDirectiveLines;
            if (options != null)
            {
                if (!string.IsNullOrEmpty(options.Profile))
                    Enum.TryParse(options.Profile, out profile);
                if (!string.IsNullOrEmpty(options.Mode))
                    Enum.TryParse(options.Mode, out mode);
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

        /// <summary>Resolves the row's wrapper type name on the registering assembly only. Null when the row
        /// names none or the name does not resolve there; never loads anything.</summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "P3-R9 (D11): resolves an identity the engine recorded itself over registered/loaded assemblies; a trimmed publish resolves only what it kept, and a miss reads as unresolved (a diagnostic or gauntlet mismatch), never a crash.")]
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

        /// <summary>Resolves a recorded model type nominally against assemblies already loaded. Never loads
        /// anything new: an unresolvable identity reports through
        /// <paramref name="unresolved"/> and becomes a memoized fault at materialization.</summary>
        private static Type ResolveModelType(CompiledTypeRef typeRef, out string nominal, out bool unresolved)
        {
            if (typeRef == null || typeRef is DynamicTypeRef)
            {
                nominal = null;
                unresolved = false;
                return null;
            }

            nominal = typeRef.Nominal();
            var resolved = FindLoadedType(typeRef);
            unresolved = resolved == null;
            return resolved;
        }

        /// <summary>Resolves a recorded type reference against assemblies already loaded into the default
        /// load context, or null when it resolves to nothing. Loads nothing; shared with the binding gate,
        /// which resolves each member row's start type by the same rule (GI-4/D11).</summary>
        [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "P3-R9 (D11): resolves an identity the engine recorded itself over registered/loaded assemblies; a trimmed publish resolves only what it kept, and a miss reads as unresolved (a diagnostic or gauntlet mismatch), never a crash.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "P3-R9 (D11): resolves an identity the engine recorded itself over registered/loaded assemblies; a trimmed publish resolves only what it kept, and a miss reads as unresolved (a diagnostic or gauntlet mismatch), never a crash.")]
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

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "P3-R9 (D11): resolves an identity the engine recorded itself over registered/loaded assemblies; a trimmed publish resolves only what it kept, and a miss reads as unresolved (a diagnostic or gauntlet mismatch), never a crash.")]
        private static Type FindLoadedNamedType(string fullName, string assemblySimpleName, bool isFramework)
        {
            if (string.IsNullOrEmpty(fullName))
                return null;
            // AC-4: a framework ref's assembly name is advisory (the writer saw System.Private.CoreLib
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
}
