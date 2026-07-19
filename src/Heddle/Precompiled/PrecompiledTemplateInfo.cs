using System;
using System.Collections.Generic;
using Heddle.Runtime;

namespace Heddle.Precompiled
{
    /// <summary>One precompiled template as recorded in a generated manifest (phase 7 D6). Immutable; safe to share
    /// across threads. A <b>fallback-marker entry</b> (<see cref="IsPrecompiled"/> == false, README D21) carries the
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
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
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

        /// <summary>The generated static entry class; null iff <see cref="IsPrecompiled"/> is false.</summary>
        public Type EntryPointType { get; }

        /// <summary>The declared <c>@model</c>/<c>::</c> type; null when the template reads no model.</summary>
        public Type ModelType { get; }

        public bool IsDynamic { get; }

        /// <summary>SHA-256 (lowercase hex) of the template file's raw bytes.</summary>
        public string ContentHash { get; }

        public IReadOnlyList<PrecompiledImport> Imports { get; }

        public PrecompiledOptionsFingerprint OptionsFingerprint { get; }

        public IReadOnlyList<PrecompiledExtensionBinding> ExtensionBindings { get; }

        /// <summary>Function bindings (D21); empty when the template calls no functions, never null.</summary>
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
