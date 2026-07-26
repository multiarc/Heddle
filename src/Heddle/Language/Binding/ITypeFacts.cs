using Heddle.Language.Expressions;

namespace Heddle.Language.Binding
{
    /// <summary>
    /// The type-system seam for shared binding rules. <typeparamref name="TType"/> is opaque: reflection adapter
    /// over <c>System.Type</c>, Roslyn adapter over <c>ITypeSymbol</c>; neither appears in shared files.
    /// </summary>
    internal interface ITypeFacts<TType>
    {
        /// <summary>The <b>CLR</b> relation <c>target.IsAssignableFrom(source)</c>, exactly. The Roslyn adapter
        /// must return the CLR answer even where Roslyn's conversion classification disagrees — the two verified
        /// disagreements (<c>int → int?</c> assignable but classified <c>ImplicitNullable</c>;
        /// <c>int? → IComparable</c> classified boxing but not CLR-assignable) are corrected there, once.</summary>
        bool IsAssignableFrom(TType target, TType source);

        /// <summary>True iff <paramref name="type"/> is <c>System.Nullable&lt;T&gt;</c>; the one spelling that
        /// replaces the three divergent ones the generator carried.</summary>
        bool TryGetNullableUnderlying(TType type, out TType underlying);

        bool IsInterface(TType type);

        bool IsValueType(TType type);

        /// <summary>False for null/unresolved, open generics, pointers, and by-ref types.</summary>
        bool IsUsableAsPropType(TType type);

        /// <summary>Maps to <see cref="NumericKind"/>; <see cref="NumericKind.None"/> for
        /// anything that is not one of the twelve numeric primitives.</summary>
        NumericKind GetNumericKind(TType type);

        /// <summary>True when the type is <c>System.Object</c> — the boxing target of the default-conversion rule.</summary>
        bool IsObject(TType type);

        /// <summary>The manifest identity string, through the shared <c>AqnFormatter</c>. Both adapters produce
        /// byte-identical output for the same type.</summary>
        string FormatAqn(TType type);

        /// <summary>A human-readable spelling for diagnostic text. The two tiers' diagnostic messages quote types,
        /// so this is the seam that lets one message template serve both.</summary>
        string Display(TType type);
    }
}
