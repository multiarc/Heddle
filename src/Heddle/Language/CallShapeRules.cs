using Heddle.Precompiled;

namespace Heddle.Language
{
    /// <summary>
    /// The one reduction of a <see cref="CallParameter"/> to the <see cref="PrecompiledCallShape"/> a call site
    /// records, shared by the emitter that writes the site and by anything that reads one back.
    /// <para>The arms are tested in <see cref="SlotRules.HasOutValue"/>'s order, so
    /// <c>Of(cp) != PrecompiledCallShape.None</c> is that predicate rather than a second copy of it — which is the
    /// property <c>PrecompiledRuntime</c>'s witness <c>OutputItem</c> depends on.</para>
    /// </summary>
    internal static class CallShapeRules
    {
        /// <summary>The positional shape of one call.</summary>
        internal static PrecompiledCallShape Of(CallParameter callParameter)
        {
            if (callParameter == null)
                return PrecompiledCallShape.None;
            if (callParameter.NativeExpression != null)
                return PrecompiledCallShape.NativeExpression;
            if (callParameter.ChainParameter != null)
                return PrecompiledCallShape.Chain;
            if (!string.IsNullOrEmpty(callParameter.CSharpExpression))
                return PrecompiledCallShape.CSharpExpression;
            if (callParameter.PropArguments != null)
                return PrecompiledCallShape.PropArguments;
            return callParameter.ModelParameter != null && callParameter.ModelParameter.Length > 0 &&
                   !string.IsNullOrEmpty(callParameter.ModelParameter[0])
                ? PrecompiledCallShape.ModelPath
                : PrecompiledCallShape.None;
        }
    }
}
