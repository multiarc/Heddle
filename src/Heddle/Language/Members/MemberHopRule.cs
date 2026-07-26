namespace Heddle.Language.Members
{
    /// <summary>The three shapes a null-safe member hop takes.</summary>
    internal enum HopForm
    {
        /// <summary>Value-typed receiver: it can never be null, so the hop accesses directly.</summary>
        Direct,

        /// <summary>Reference receiver, reference-or-nullable property: <c>recv?.Prop</c> in text, a
        /// <c>Condition(recv == null, default(T), …)</c> in expression trees.</summary>
        NullConditional,

        /// <summary>Reference receiver, non-nullable value property: the explicit conditional. C#'s <c>?.</c> would
        /// widen the result to <c>Nullable&lt;T&gt;</c> here, where the runtime yields the boxed
        /// <c>default(T)</c> — the one case where the two encodings are not interchangeable.</summary>
        NullDefaultConditional
    }

    /// <summary>
    /// The null-safe hop decision, stated once to prevent divergence. Member hops are already null-safe, so a
    /// separate <c>?.</c> operator would be redundant. This logic used to live in two places independently —
    /// <c>MemberPathWriter</c>'s text branches and <c>ModelParameter.BuildNullSafePropertyChain</c>'s expression
    /// branches — with their only equivalence guarantee being cross-file doc comments. Now both branch on this
    /// function.
    /// </summary>
    internal static class MemberHopRule
    {
        public static HopForm Form(bool receiverIsValueType, bool propertyIsNonNullableValueType)
        {
            if (receiverIsValueType)
                return HopForm.Direct;
            return propertyIsNonNullableValueType ? HopForm.NullDefaultConditional : HopForm.NullConditional;
        }
    }
}
