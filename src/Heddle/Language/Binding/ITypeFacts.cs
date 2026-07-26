using Heddle.Language.Expressions;

namespace Heddle.Language.Binding
{
    /// <summary>
    /// Phase 3 (F6): the type-system seam the shared binding rule-cores answer their questions through.
    /// <para>The assignability relation cannot be shared imperatively — it <em>is</em> the type graph, and each side
    /// already has an engine for it (reflection's <c>Type.IsAssignableFrom</c>; Roslyn's
    /// <c>Compilation.ClassifyConversion</c> plus a hierarchy walk). What drifted was never the graph but the
    /// corrections and spellings around it, which the generator carried in three places. This interface confines
    /// the graph to one adapter per side and lets the <em>rules</em> (prop-layout sequencing, discovery precedence,
    /// conversion legality) live in Roslyn-free shared files.</para>
    /// <para><typeparamref name="TType"/> is fully opaque here: the reflection adapter closes it over
    /// <c>System.Type</c> in <c>Heddle</c>, the Roslyn adapter over <c>ITypeSymbol</c> in <c>Heddle.Generator</c>,
    /// and neither adapter type appears in a shared file.</para>
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

        /// <summary>The unified unusable-prop-type predicate: false for null/unresolved, open generics
        /// (<c>ContainsGenericParameters</c> semantics — not merely an unbound definition), pointers and by-ref
        /// types. The runtime's rule at <c>PropLayout.ResolveFromExtension</c> is authoritative; the generator's
        /// local variant under-implemented it (no by-ref arm, narrower generic test).</summary>
        bool IsUsableAsPropType(TType type);

        /// <summary>Maps into phase 4's shared <see cref="NumericKind"/>; <see cref="NumericKind.None"/> for
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
