using Heddle.Data;

namespace Heddle.Precompiled
{
    /// <summary>The typed options triple a precompiled template was built under: exactly the
    /// output-byte-changing option set that the dynamic resolver cache key uses.
    /// <c>AllowCSharp</c> is <see cref="ExpressionMode.FullCSharp"/> restated and is not duplicated;
    /// <c>Functions</c> and <c>MaxRecursionCount</c> are deliberately excluded. Compared field-wise
    /// in declaration order — the first differing field names the fallback reason detail.</summary>
    public readonly struct PrecompiledOptionsFingerprint
    {
        public PrecompiledOptionsFingerprint(OutputProfile profile, ExpressionMode expressionMode,
            bool trimDirectiveLines)
        {
            Profile = profile;
            ExpressionMode = expressionMode;
            TrimDirectiveLines = trimDirectiveLines;
        }

        public OutputProfile Profile { get; }

        public ExpressionMode ExpressionMode { get; }

        public bool TrimDirectiveLines { get; }
    }
}
