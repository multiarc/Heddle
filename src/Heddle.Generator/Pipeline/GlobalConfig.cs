using System;
using Heddle.Data;
using Heddle.Precompiled;

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
            bool nodeFallback, ObserveMode observeMode, string observeIntermediatePath)
        {
            OutputProfile = outputProfile;
            ExpressionMode = expressionMode;
            TrimDirectiveLines = trimDirectiveLines;
            MaxRecursionCount = maxRecursionCount;
            TemplateRoot = templateRoot;
            GeneratedNamespace = generatedNamespace;
            EmitUtf8Pieces = emitUtf8Pieces;
            NodeFallback = nodeFallback;
            ObserveMode = observeMode;
            ObserveIntermediatePath = observeIntermediatePath;
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

        /// <summary>Engine observation's tri-state (<c>HeddleObserveEngine</c>). It decides which TYPING a body is
        /// emitted with and whether an unobservable build is an error, never a rendered byte, so it is not a
        /// fingerprint input.</summary>
        public ObserveMode ObserveMode { get; }

        /// <summary>Where observation may write its content-addressed intermediate assemblies; empty means
        /// nowhere, which is observation being unavailable.</summary>
        public string ObserveIntermediatePath { get; }

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
                   && ObserveMode == other.ObserveMode
                   && string.Equals(ObserveIntermediatePath, other.ObserveIntermediatePath,
                       StringComparison.Ordinal);
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
                hash = hash * 31 + (int)ObserveMode;
                hash = hash * 31 + (ObserveIntermediatePath?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
