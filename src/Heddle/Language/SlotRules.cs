namespace Heddle.Language
{
    /// <summary>
    /// The two slot rules, written once. Both are pure functions of
    /// already-linked parse types, so this is genuinely shared code on both tiers rather than a pinned table:
    /// <list type="bullet">
    /// <item><description><see cref="HasOutValue"/> — the canonical five-way "does this <c>@out</c> carry a
    /// value?" test, previously private to <c>OutExtension</c> and approximated in the emitter by
    /// <c>!IsModelTypeParameter</c> plus two special cases. The approximation happened to agree today and would
    /// have stopped agreeing at the next <see cref="CallParameter"/> carrier.</description></item>
    /// <item><description><see cref="SlotTypeName"/>/<see cref="HasSlot"/> — the first declared <c>out::</c> down
    /// the base chain, which the runtime's <c>HeddleCompiler.ResolveSlotType</c> and two emitter copies each
    /// spelled out.</description></item>
    /// </list>
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
