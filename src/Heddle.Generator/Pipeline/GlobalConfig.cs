using System;

namespace Heddle.Generator.Pipeline
{
    /// <summary>The compilation-wide build options (phase 7 D16 step 2), a value-equatable model of primitives —
    /// safe to hold in the incremental pipeline. Mirrors the identity-bearing <c>TemplateOptions</c> triple plus the
    /// baked <c>MaxRecursionCount</c>, the generated namespace, the template root, and the u8 toggle.</summary>
    internal sealed class GlobalConfig : IEquatable<GlobalConfig>
    {
        public GlobalConfig(string outputProfile, string expressionMode, bool trimDirectiveLines,
            int maxRecursionCount, string templateRoot, string generatedNamespace, bool emitUtf8Pieces)
        {
            OutputProfile = outputProfile;
            ExpressionMode = expressionMode;
            TrimDirectiveLines = trimDirectiveLines;
            MaxRecursionCount = maxRecursionCount;
            TemplateRoot = templateRoot;
            GeneratedNamespace = generatedNamespace;
            EmitUtf8Pieces = emitUtf8Pieces;
        }

        public string OutputProfile { get; }
        public string ExpressionMode { get; }
        public bool TrimDirectiveLines { get; }
        public int MaxRecursionCount { get; }
        public string TemplateRoot { get; }
        public string GeneratedNamespace { get; }
        public bool EmitUtf8Pieces { get; }

        public bool Equals(GlobalConfig other)
        {
            if (other is null) return false;
            return string.Equals(OutputProfile, other.OutputProfile, StringComparison.Ordinal)
                   && string.Equals(ExpressionMode, other.ExpressionMode, StringComparison.Ordinal)
                   && TrimDirectiveLines == other.TrimDirectiveLines
                   && MaxRecursionCount == other.MaxRecursionCount
                   && string.Equals(TemplateRoot, other.TemplateRoot, StringComparison.Ordinal)
                   && string.Equals(GeneratedNamespace, other.GeneratedNamespace, StringComparison.Ordinal)
                   && EmitUtf8Pieces == other.EmitUtf8Pieces;
        }

        public override bool Equals(object obj) => Equals(obj as GlobalConfig);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (OutputProfile?.GetHashCode() ?? 0);
                hash = hash * 31 + (ExpressionMode?.GetHashCode() ?? 0);
                hash = hash * 31 + TrimDirectiveLines.GetHashCode();
                hash = hash * 31 + MaxRecursionCount;
                hash = hash * 31 + (TemplateRoot?.GetHashCode() ?? 0);
                hash = hash * 31 + (GeneratedNamespace?.GetHashCode() ?? 0);
                hash = hash * 31 + EmitUtf8Pieces.GetHashCode();
                return hash;
            }
        }
    }
}
