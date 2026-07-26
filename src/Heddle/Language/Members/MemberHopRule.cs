namespace Heddle.Language.Members
{
    /// <summary>The three shapes a null-safe member hop takes.</summary>
    internal enum HopForm
    {
        /// <summary>Value-typed receiver: it can never be null, so the hop accesses directly.</summary>
        Direct,

        /// <summary>Reference receiver with reference-or-nullable property: null-conditional in text, conditional expression in trees.</summary>
        NullConditional,

        /// <summary>Reference receiver with non-nullable value property: explicit conditional, because <c>?.</c> would incorrectly widen to <c>Nullable&lt;T&gt;</c>.</summary>
        NullDefaultConditional
    }

    /// <summary>Centralizes the null-safe hop decision to prevent divergence between text and expression tree branches, since both implementations must always agree.</summary>
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
