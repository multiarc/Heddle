using System;
using System.Collections.Generic;
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
            Strategy = strategy;
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

        /// <summary>The generated root body; null iff <see cref="IsPrecompiled"/> is false.</summary>
        public IProcessStrategy Strategy { get; }

        /// <summary>False for a fallback-marker entry (a build-degraded template, HED7014): the gauntlet
        /// short-circuits it with <see cref="PrecompiledFallbackReason.UnsupportedFunction"/> and
        /// <see cref="Strategy"/> is null.</summary>
        public bool IsPrecompiled => Strategy != null;
    }
}
