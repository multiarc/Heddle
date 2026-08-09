using System;
using Heddle.Data;

namespace Heddle.Generator.Pipeline
{
    /// <summary>The compilation-wide build options, a value-equatable model of primitives —
    /// safe to hold in the incremental pipeline. Mirrors the identity-bearing <c>TemplateOptions</c> triple plus the
    /// baked <c>MaxRecursionCount</c>, the generated namespace, the template root, and the u8 toggle.
    /// <para>The identity-bearing pair are the runtime's own <see cref="Data.OutputProfile"/>/
    /// <see cref="Data.ExpressionMode"/> enums (linked source), not strings — so the emitter cannot bake a profile
    /// or mode the runtime does not have, and the hand-rolled ordinal string compares collapse to enum compares
    /// (strictly stronger for the incremental pipeline's equality).</para></summary>
    internal sealed class GlobalConfig : IEquatable<GlobalConfig>
    {
        public GlobalConfig(OutputProfile outputProfile, ExpressionMode expressionMode, bool trimDirectiveLines,
            int maxRecursionCount, string templateRoot, string generatedNamespace, bool emitUtf8Pieces,
            bool nodeFallback, bool probeExtensionHooks, string packageFolders)
        {
            OutputProfile = outputProfile;
            ExpressionMode = expressionMode;
            TrimDirectiveLines = trimDirectiveLines;
            MaxRecursionCount = maxRecursionCount;
            TemplateRoot = templateRoot;
            GeneratedNamespace = generatedNamespace;
            EmitUtf8Pieces = emitUtf8Pieces;
            NodeFallback = nodeFallback;
            ProbeExtensionHooks = probeExtensionHooks;
            PackageFolders = packageFolders;
        }

        public OutputProfile OutputProfile { get; }
        public ExpressionMode ExpressionMode { get; }
        public bool TrimDirectiveLines { get; }
        public int MaxRecursionCount { get; }
        public string TemplateRoot { get; }
        public string GeneratedNamespace { get; }
        public bool EmitUtf8Pieces { get; }

        /// <summary>Per-node engine-accessor fallback (<c>HeddleNodeFallback</c>). Decides whether a template
        /// precompiles, never a rendered byte, so it is not a fingerprint input.</summary>
        public bool NodeFallback { get; }

        /// <summary>Extension hook probing (<c>HeddleProbeExtensionHooks</c>), off by default. Like
        /// <see cref="NodeFallback"/> it decides whether a template precompiles, never a rendered byte, so it is
        /// not a fingerprint input — but unlike it, turning it on makes the answer depend on the behaviour of a
        /// referenced assembly rather than only on its metadata.</summary>
        public bool ProbeExtensionHooks { get; }

        /// <summary>The restore's <c>$(NuGetPackageFolders)</c>, verbatim. The only directories
        /// <see cref="Probe.ProbeAssemblyLoader"/> may load from; empty means no probing at all.</summary>
        public string PackageFolders { get; }

        public bool Equals(GlobalConfig other)
        {
            if (other is null) return false;
            return OutputProfile == other.OutputProfile
                   && ExpressionMode == other.ExpressionMode
                   && TrimDirectiveLines == other.TrimDirectiveLines
                   && MaxRecursionCount == other.MaxRecursionCount
                   && string.Equals(TemplateRoot, other.TemplateRoot, StringComparison.Ordinal)
                   && string.Equals(GeneratedNamespace, other.GeneratedNamespace, StringComparison.Ordinal)
                   && EmitUtf8Pieces == other.EmitUtf8Pieces
                   && NodeFallback == other.NodeFallback
                   && ProbeExtensionHooks == other.ProbeExtensionHooks
                   && string.Equals(PackageFolders, other.PackageFolders, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as GlobalConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (int)OutputProfile;
                hash = hash * 31 + (int)ExpressionMode;
                hash = hash * 31 + TrimDirectiveLines.GetHashCode();
                hash = hash * 31 + MaxRecursionCount;
                hash = hash * 31 + (TemplateRoot?.GetHashCode() ?? 0);
                hash = hash * 31 + (GeneratedNamespace?.GetHashCode() ?? 0);
                hash = hash * 31 + EmitUtf8Pieces.GetHashCode();
                hash = hash * 31 + NodeFallback.GetHashCode();
                hash = hash * 31 + ProbeExtensionHooks.GetHashCode();
                hash = hash * 31 + (PackageFolders?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
