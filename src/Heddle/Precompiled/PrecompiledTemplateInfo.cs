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
        {
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

        /// <summary>The declared <c>@model</c>/<c>::</c> type; null when the template reads no model.</summary>
        public Type ModelType { get; }

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

        /// <summary>The generated root body; null iff <see cref="IsPrecompiled"/> is false.</summary>
        public IProcessStrategy Strategy { get; }

        /// <summary>False for a fallback-marker entry (a build-degraded template, HED7014): the gauntlet
        /// short-circuits it with <see cref="PrecompiledFallbackReason.UnsupportedFunction"/> and
        /// <see cref="Strategy"/> is null.</summary>
        public bool IsPrecompiled => Strategy != null;
    }
}
