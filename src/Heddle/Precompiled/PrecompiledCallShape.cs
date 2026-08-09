namespace Heddle.Precompiled
{
    /// <summary>
    /// The positional parameter shape of one call, reduced from <c>Heddle.Language.CallParameter</c>'s arms to what
    /// a generated static field initializer can carry.
    /// <para>The members are in the precedence order <c>SlotRules.HasOutValue</c> tests them, and the first arm a
    /// call matches is its shape — so <c>Shape != None</c> reproduces that five-way test exactly. That collapse to a
    /// bool, plus the call's chained-consumer flag and its position, is everything any engine hook reads off
    /// <c>InitContext.SourceItem</c>; <c>InitSynthesisFidelityTests</c> gates the claim against the engine's
    /// source.</para>
    /// </summary>
    public enum PrecompiledCallShape
    {
        /// <summary>The call passes nothing: <c>@name</c> or <c>@name()</c>.</summary>
        None = 0,

        /// <summary>The call passes a parsed native expression (<c>CallParameter.NativeExpression</c>).</summary>
        NativeExpression = 1,

        /// <summary>The call passes a chain parameter (<c>CallParameter.ChainParameter</c>).</summary>
        Chain = 2,

        /// <summary>The call passes an embedded C# expression (<c>CallParameter.CSharpExpression</c>).</summary>
        CSharpExpression = 3,

        /// <summary>The call passes only named prop arguments (<c>CallParameter.PropArguments</c>) — orthogonal to
        /// the positional arms, and reached only when none of them matched.</summary>
        PropArguments = 4,

        /// <summary>The call passes a model member path with a non-empty first segment
        /// (<c>CallParameter.ModelParameter</c>).</summary>
        ModelPath = 5
    }
}
