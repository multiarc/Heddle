namespace Heddle.Language.Binding
{
    /// <summary>The outcome of offering one extension candidate to a registry that may already hold
    /// an incumbent under the same name.</summary>
    internal enum ExtensionRegistrationVerdict
    {
        /// <summary>The name was free — the candidate takes it.</summary>
        Register,

        /// <summary>The candidate displaces the incumbent (declared <c>[ExtensionReplace]</c>, or the incumbent
        /// type is assignable from the candidate — i.e. the candidate is the more derived of the two).</summary>
        Replace,

        /// <summary>The incumbent is already the more derived of the two, so it stays. Build-tier only: the
        /// runtime reaches this state only by enumerating the pair in the opposite order, where it produces
        /// <see cref="Replace"/>; see <see cref="ExtensionRegistrationRules.ResolveForBuild"/>.</summary>
        KeepIncumbent,

        /// <summary>Two unrelated types claim the same name — the runtime throws
        /// <c>TemplateOverrideException</c>; the build tier degrades the call to dynamic with a recorded reason
        /// (a host wiring error must not become a build failure the runtime would only hit at first render).</summary>
        Conflict
    }

    /// <summary>The extension-registration precedence rule for both tiers.</summary>
    internal static class ExtensionRegistrationRules
    {
        /// <summary>The runtime's verdict, verbatim. Never returns <see cref="ExtensionRegistrationVerdict.KeepIncumbent"/>.</summary>
        internal static ExtensionRegistrationVerdict Resolve(bool hasIncumbent, bool candidateReplaces,
            bool incumbentAssignableFromCandidate)
        {
            if (!hasIncumbent)
                return ExtensionRegistrationVerdict.Register;
            if (candidateReplaces || incumbentAssignableFromCandidate)
                return ExtensionRegistrationVerdict.Replace;
            return ExtensionRegistrationVerdict.Conflict;
        }

        /// <summary>
        /// The build tier's verdict: order-insensitive over inheritance (keep the more derived type regardless of enumeration order)
        /// to match runtime behavior; genuinely unrelated claimants still conflict.
        /// </summary>
        internal static ExtensionRegistrationVerdict ResolveForBuild(bool hasIncumbent, bool candidateReplaces,
            bool incumbentAssignableFromCandidate, bool candidateAssignableFromIncumbent)
        {
            var verdict = Resolve(hasIncumbent, candidateReplaces, incumbentAssignableFromCandidate);
            if (verdict == ExtensionRegistrationVerdict.Conflict && candidateAssignableFromIncumbent)
                return ExtensionRegistrationVerdict.KeepIncumbent;
            return verdict;
        }

        /// <summary>The runtime's pre-registration ordering key (<c>TemplateFactory.LoadExtensions</c>): candidates
        /// sort by whether any inherited <c>[DataType]</c> names an interface, then by the same test over
        /// <c>[ChainedType]</c> — <c>false</c> first, stably. Expressed as a small integer so both tiers can sort
        /// by the same expression.</summary>
        internal static int OrderingKey(bool hasInterfaceDataType, bool hasInterfaceChainedType)
        {
            return (hasInterfaceDataType ? 2 : 0) + (hasInterfaceChainedType ? 1 : 0);
        }
    }
}
