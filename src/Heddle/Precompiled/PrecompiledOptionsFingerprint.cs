using Heddle.Data;

namespace Heddle.Precompiled
{
    /// <summary>The typed options triple a precompiled template was built under (phase 7 D6): exactly the
    /// output-byte-changing option set phases 2/4 put into the dynamic resolver cache key.
    /// <c>AllowCSharp</c> is <see cref="ExpressionMode.FullCSharp"/> by phase 1's bridge and is not duplicated;
    /// <c>Functions</c> and <c>MaxRecursionCount</c> are deliberately excluded (README D21/D23). Compared field-wise
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
