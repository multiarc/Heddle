namespace Heddle.Language.Members
{
    /// <summary>The declared accessibility of a property getter, as both fact sources can report it (Roslyn's
    /// <c>Accessibility</c> on the generator side, <c>MethodBase</c>'s attribute bits on the runtime side).</summary>
    internal enum MemberAccess
    {
        Public,
        Internal,
        ProtectedOrInternal,
        Protected,
        ProtectedAndInternal,
        Private
    }

    /// <summary>
    /// Everything the member-tier visibility policy needs to know about one property — and nothing else. The fact
    /// sources are per-side adapters; this struct is the only shape <see cref="MemberVisibility"/> ever sees, which
    /// is what makes a divergent policy structurally impossible once both adapters feed it.
    /// </summary>
    internal readonly struct MemberFacts
    {
        public MemberFacts(bool canRead, MemberAccess access, bool hasHidden, bool isStatic)
        {
            CanRead = canRead;
            Access = access;
            HasHidden = hasHidden;
            IsStatic = isStatic;
        }

        public bool CanRead { get; }

        public MemberAccess Access { get; }

        /// <summary>Matched by <b>full metadata name</b> — <c>Heddle.Attributes.HiddenAttribute</c> — on both sides.
        /// The generator's historic unqualified-name match let a foreign <c>*.HiddenAttribute</c> hide a member the
        /// runtime happily exposed; phase 3's adoption corrects that adapter.</summary>
        public bool HasHidden { get; }

        public bool IsStatic { get; }
    }

    /// <summary>
    /// The one policy point for the documented member-tier sandbox filter
    /// (<c>docs/native-expressions.md</c>, "The sandbox"): a path segment binds to a readable, non-<c>[Hidden]</c>
    /// instance property whose getter is public-or-internal. Phase 4 D7 / OQ1 (resolved user, 2026-07-25) makes the
    /// <b>runtime's</b> observable accept/reject behavior normative and the generator conform downward — widening
    /// anything here (accepting <c>protected internal</c>, surfacing base-interface members) is a breaking-window
    /// candidate, never a drift fix.
    /// </summary>
    internal static class MemberVisibility
    {
        /// <summary>The filter for a property declared on the receiver type itself.</summary>
        public static bool IsAccessible(in MemberFacts facts) => IsAccessible(facts, declaredOnReceiver: true);

        /// <summary>
        /// The filter, parameterized by where the property was declared.
        /// <para><paramref name="declaredOnReceiver"/> is the one place reflection's <i>capability</i> becomes
        /// policy: <c>Type.GetProperty</c> never surfaces an inherited non-public property, so an
        /// <c>internal</c> getter on a base class is not-found in the runtime today. Under the OQ1 ruling that
        /// behavior is normative, so the shared walk encodes it as a rule instead of leaving it as an accident of
        /// which reflection overload each side happened to call.</para>
        /// </summary>
        public static bool IsAccessible(in MemberFacts facts, bool declaredOnReceiver)
        {
            if (!facts.CanRead)
                return false;
            if (facts.HasHidden)
                return false;
            // Error-shape fix (phase 4 D7): a static property is not-found rather than an unpositioned
            // ArgumentException out of Expression.MakeMemberAccess on one tier and consumer-side CS0176 on the
            // other. Neither tier ever rendered such a template.
            if (facts.IsStatic)
                return false;

            switch (facts.Access)
            {
                case MemberAccess.Public:
                    return true;
                case MemberAccess.Internal:
                    return declaredOnReceiver;
                default:
                    // protected / protected internal / private protected / private — all rejected, runtime-normative.
                    return false;
            }
        }
    }
}
