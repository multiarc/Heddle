using Heddle.Data;

namespace Heddle.Precompiled
{
    /// <summary>The typed options triple a precompiled template was built under: exactly the
    /// output-byte-changing option set that the dynamic resolver cache key uses.
    /// <c>AllowCSharp</c> is <see cref="ExpressionMode.FullCSharp"/> restated and is not duplicated;
    /// <c>Functions</c> and <c>MaxRecursionCount</c> are deliberately excluded. Compared field-wise
    /// in declaration order — the first differing field names the fallback reason detail.
    /// <para><b>Assembly configuration is not a fingerprint input</b>, and its absence here is a rule rather than
    /// an omission. What <c>[HeddleModelAssembly]</c>, <c>@(HeddleModelAssembly)</c> and
    /// <c>@(HeddleExtensionAssembly)</c> decide is <i>whether</i> a template precompiles at all, never a byte of
    /// what it renders — a template that precompiles under one of them renders what the dynamic tier renders
    /// without it. A field added here would be a fourth constructor parameter, and this constructor is emitted
    /// into every already-built consumer assembly, so it must not gain one.</para></summary>
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
