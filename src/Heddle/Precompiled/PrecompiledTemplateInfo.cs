using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Data;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;

namespace Heddle.Precompiled
{
    /// <summary>One precompiled template as recorded in a generated manifest. Immutable; safe to share
    /// across threads. A <b>fallback-marker entry</b> (<see cref="IsPrecompiled"/> == false) carries the
    /// key, hashes, fingerprint and a null-target <see cref="FunctionBindings"/> row but no entry class or strategy —
    /// the template is known but not precompiled, and the per-request gauntlet short-circuits it with
    /// <see cref="PrecompiledFallbackReason.UnsupportedFunction"/>.</summary>
    public sealed class PrecompiledTemplateInfo
    {
        private static readonly IReadOnlyList<PrecompiledImport> NoImports = Array.Empty<PrecompiledImport>();
        private static readonly IReadOnlyList<PrecompiledExtensionBinding> NoExtensions =
            Array.Empty<PrecompiledExtensionBinding>();
        private static readonly IReadOnlyList<PrecompiledFunctionBinding> NoFunctions =
            Array.Empty<PrecompiledFunctionBinding>();
        private static readonly IReadOnlyList<PrecompiledInitSite> NoInitSites =
            Array.Empty<PrecompiledInitSite>();
        private static readonly IReadOnlyList<PrecompiledRefusalSite> NoRefusals =
            Array.Empty<PrecompiledRefusalSite>();
        private static readonly IReadOnlyList<CompiledMemberRow> NoMembers =
            Array.Empty<CompiledMemberRow>();

        /// <summary>The eleven-value shape. Retained as a real constructor to preserve the shorter signature in
        /// metadata (optional parameters remove it), which past versions relied on. Hand-written manifests use this.</summary>
        public PrecompiledTemplateInfo(
            string key,
            Type entryPointType,
            Type modelType,
            bool isDynamic,
            string contentHash,
            IReadOnlyList<PrecompiledImport> imports,
            PrecompiledOptionsFingerprint optionsFingerprint,
            IReadOnlyList<PrecompiledExtensionBinding> extensionBindings,
            IReadOnlyList<PrecompiledFunctionBinding> functionBindings,
            PrecompiledCapabilities capabilities,
            IProcessStrategy strategy)
            : this(key, entryPointType, modelType, isDynamic, contentHash, imports, optionsFingerprint,
                extensionBindings, functionBindings, capabilities, strategy,
                registeredName: null, linePathForm: PrecompiledLinePathForm.Unspecified)
        {
        }

        /// <summary>The schema 3 shape: the eleven values plus optional <see cref="RegisteredName"/> and
        /// <see cref="LinePathForm"/>.</summary>
        public PrecompiledTemplateInfo(
            string key,
            Type entryPointType,
            Type modelType,
            bool isDynamic,
            string contentHash,
            IReadOnlyList<PrecompiledImport> imports,
            PrecompiledOptionsFingerprint optionsFingerprint,
            IReadOnlyList<PrecompiledExtensionBinding> extensionBindings,
            IReadOnlyList<PrecompiledFunctionBinding> functionBindings,
            PrecompiledCapabilities capabilities,
            IProcessStrategy strategy,
            string registeredName,
            PrecompiledLinePathForm linePathForm)
            : this(key, entryPointType, modelType, isDynamic, contentHash, imports, optionsFingerprint,
                extensionBindings, functionBindings, capabilities, strategy, registeredName, linePathForm,
                modelTypeIsAmbient: false)
        {
        }

        /// <summary>The schema 3 shape plus <see cref="ModelTypeIsAmbient"/>. A real constructor rather than an
        /// optional parameter on the shorter one, which would remove that one from metadata and fault every
        /// already-built consumer assembly whose manifest calls it.</summary>
        public PrecompiledTemplateInfo(
            string key,
            Type entryPointType,
            Type modelType,
            bool isDynamic,
            string contentHash,
            IReadOnlyList<PrecompiledImport> imports,
            PrecompiledOptionsFingerprint optionsFingerprint,
            IReadOnlyList<PrecompiledExtensionBinding> extensionBindings,
            IReadOnlyList<PrecompiledFunctionBinding> functionBindings,
            PrecompiledCapabilities capabilities,
            IProcessStrategy strategy,
            string registeredName,
            PrecompiledLinePathForm linePathForm,
            bool modelTypeIsAmbient)
            : this(key, entryPointType, modelType, isDynamic, contentHash, imports, optionsFingerprint,
                extensionBindings, functionBindings, capabilities, strategy, registeredName, linePathForm,
                modelTypeIsAmbient, initSites: null)
        {
        }

        /// <summary>The shape carrying <see cref="InitSites"/>. A real constructor rather than an optional
        /// parameter for the reason every widening here has been one: an optional parameter removes the narrower
        /// signature from metadata and faults every already-built consumer assembly whose manifest calls it.</summary>
        public PrecompiledTemplateInfo(
            string key,
            Type entryPointType,
            Type modelType,
            bool isDynamic,
            string contentHash,
            IReadOnlyList<PrecompiledImport> imports,
            PrecompiledOptionsFingerprint optionsFingerprint,
            IReadOnlyList<PrecompiledExtensionBinding> extensionBindings,
            IReadOnlyList<PrecompiledFunctionBinding> functionBindings,
            PrecompiledCapabilities capabilities,
            IProcessStrategy strategy,
            string registeredName,
            PrecompiledLinePathForm linePathForm,
            bool modelTypeIsAmbient,
            IReadOnlyList<PrecompiledInitSite> initSites)
        {
            InitSites = initSites ?? NoInitSites;
            ModelTypeIsAmbient = modelTypeIsAmbient;
            Key = key ?? throw new ArgumentNullException(nameof(key));
            RegisteredName = registeredName;
            LinePathForm = linePathForm;
            EntryPointType = entryPointType;
            ModelType = modelType;
            IsDynamic = isDynamic;
            ContentHash = contentHash;
            Imports = imports ?? NoImports;
            OptionsFingerprint = optionsFingerprint;
            ExtensionBindings = extensionBindings ?? NoExtensions;
            FunctionBindings = functionBindings ?? NoFunctions;
            Capabilities = capabilities;
            _strategy = strategy;
            RefusalSites = NoRefusals;
            MemberRows = NoMembers;
        }

        /// <summary>The loader constructor: one row decoded from a compiled-form artifact. Takes no strategy;
        /// <see cref="Strategy"/> materializes on first read over the kept artifact bytes and is memoized, and a
        /// materialization fault is memoized with it. Type identities resolve nominally against assemblies
        /// already loaded — nothing is loaded here.</summary>
        internal PrecompiledTemplateInfo(Assembly registeringAssembly, byte[] artifactImage,
            CompiledArtifact artifact, int rowIndex)
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

            _isLoaderRow = true;
            _artifactImage = artifactImage;
            _rowIndex = rowIndex;

            Key = row.Key ?? throw new InvalidOperationException("A compiled-form template row has no key.");
            RegisteredName = row.RegisteredName;
            LinePathForm = PrecompiledLinePathForm.Unspecified;
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
            Capabilities = PrecompiledCapabilities.StringOutput;
            InitSites = NoInitSites;
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

        /// <summary>Which form this template's generated <c>#line</c> file names are in. Machine-readable
        /// here rather than a comment in the generated file, which no symbolizer could act on.
        /// <see cref="PrecompiledLinePathForm.Unspecified"/> for a fallback-marker entry, which has no generated
        /// source.</summary>
        public PrecompiledLinePathForm LinePathForm { get; }

        /// <summary>The generated static entry class; null iff <see cref="IsPrecompiled"/> is false.</summary>
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

        public PrecompiledCapabilities Capabilities { get; }

        /// <summary>The extension call sites this template binds through <see cref="PrecompiledRuntime.Init"/>,
        /// carried so the gauntlet can read the answers those hooks gave at registration. A site whose
        /// <see cref="PrecompiledInitSite.Fault"/> reaches template scope — the hook reported compile errors, or its
        /// answer about body typing contradicts what the build assumed — takes the request to the dynamic tier
        /// before any render. Empty for a template whose calls need no hook run, and for every manifest written
        /// before the seam existed.</summary>
        public IReadOnlyList<PrecompiledInitSite> InitSites { get; }

        /// <summary>The generated root body; null iff <see cref="IsPrecompiled"/> is false.
        /// On a loader row materializes on first read and is memoized (thread-safe); null when
        /// materialization faulted — the fault is memoized and reported by the gauntlet, never thrown here.
        /// Internal from phase 4.</summary>
        public IProcessStrategy Strategy
        {
            get
            {
                if (!_isLoaderRow)
                    return _strategy;
                lock (_materializeLock)
                {
                    if (!_materialized)
                    {
                        try
                        {
                            _materializedStrategy = MaterializeNow();
                        }
                        catch (MaterializationFaultException e)
                        {
                            _materializationReason = e.Reason;
                            _materializationDetail = e.Detail;
                            _materializedStrategy = null;
                        }
                        _materialized = true;
                    }

                    return _materializedStrategy;
                }
            }
        }

        /// <summary>False for a fallback-marker entry (a build-degraded template, HED7014): the gauntlet
        /// short-circuits it with <see cref="PrecompiledFallbackReason.UnsupportedFunction"/> and
        /// <see cref="Strategy"/> is null. True for every loader row; reading it never materializes.</summary>
        public bool IsPrecompiled => _isLoaderRow || _strategy != null;

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

        private readonly IProcessStrategy _strategy;

        private readonly bool _isLoaderRow;
        private readonly byte[] _artifactImage;
        private readonly int _rowIndex;
        private readonly CompiledTypeRef _modelTypeRef;
        private readonly string _modelTypeNominal;
        private readonly bool _modelTypeUnresolved;

        private readonly object _materializeLock = new object();
        private bool _materialized;
        private IProcessStrategy _materializedStrategy;
        private PrecompiledFallbackReason? _materializationReason;
        private string _materializationDetail;

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

        /// <summary>Runs the P1-W3 materialization entry over the kept bytes. Called once under
        /// <see cref="_materializeLock"/>; every failure mode becomes a
        /// <see cref="MaterializationFaultException"/> in gauntlet taxonomy, never a raw exception.</summary>
        private IProcessStrategy MaterializeNow()
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

            var options = new TemplateOptions(row.Key);
            var fingerprint = ToFingerprint(row.Options);
            options.OutputProfile = fingerprint.Profile;
            options.ExpressionMode = fingerprint.ExpressionMode;
            options.TrimDirectiveLines = fingerprint.TrimDirectiveLines;

            RuntimeDocument document;
            CompileScope scope;
            try
            {
                var context = new CompileContext(options, ModelType ?? typeof(object));
                scope = new CompileScope(context);
                document = HeddleCompiler.Materialize(artifact, row, scope);
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
            return document.Strategy;
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
        internal static Type FindLoadedType(CompiledTypeRef typeRef)
        {
            try
            {
                var named = typeRef as NamedTypeRef;
                if (named != null)
                    return FindLoadedNamedType(named.FullName, named.AssemblySimpleName);
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

        private static Type FindLoadedNamedType(string fullName, string assemblySimpleName)
        {
            if (string.IsNullOrEmpty(fullName))
                return null;
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

                if (!string.Equals(name, assemblySimpleName, StringComparison.Ordinal))
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
