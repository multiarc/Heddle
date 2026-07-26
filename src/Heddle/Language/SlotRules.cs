namespace Heddle.Language
{
    /// <summary>
    /// Slot rules shared between runtime and emitter tiers: <see cref="HasOutValue"/>, <see cref="SlotTypeName"/>, and <see cref="HasSlot"/>.
    /// </summary>
    internal static class SlotRules
    {
        /// <summary>
        /// True when <paramref name="callParameter"/> carries a value — any of the five parameter shapes that is
        /// not "nothing": a native expression, a chain parameter, a C# expression, named prop arguments, or a
        /// non-empty first model-path segment.
        /// </summary>
        internal static bool HasOutValue(CallParameter callParameter)
        {
            if (callParameter == null)
                return false;
            if (callParameter.NativeExpression != null)
                return true;
            if (callParameter.ChainParameter != null)
                return true;
            if (!string.IsNullOrEmpty(callParameter.CSharpExpression))
                return true;
            if (callParameter.PropArguments != null)
                return true;
            return callParameter.ModelParameter != null && callParameter.ModelParameter.Length > 0 &&
                   !string.IsNullOrEmpty(callParameter.ModelParameter[0]);
        }

        /// <summary>The slot type name a definition inherits: the first non-empty <c>SlotTypeName</c> found walking
        /// the base chain outermost-first, or <c>null</c> when no layer declares one.</summary>
        internal static string SlotTypeName(DefinitionItem definition)
        {
            for (var d = definition; d != null; d = d.BaseDefinition)
            {
                if (!string.IsNullOrEmpty(d.SlotTypeName))
                    return d.SlotTypeName;
            }

            return null;
        }

        /// <summary>True when <paramref name="definition"/> or any of its base layers declares an <c>out::</c>
        /// slot — i.e. the definition compiles in slot mode.</summary>
        internal static bool HasSlot(DefinitionItem definition) => SlotTypeName(definition) != null;
    }
}
