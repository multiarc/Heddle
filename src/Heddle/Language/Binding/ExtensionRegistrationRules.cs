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

        /// <summary>Two unrelated types claim the same name — the runtime throws
        /// <c>TemplateOverrideException</c>.</summary>
        Conflict
    }

    /// <summary>The extension-registration precedence rule.</summary>
    internal static class ExtensionRegistrationRules
    {
        /// <summary>The registration verdict for one candidate offered under a name.</summary>
        internal static ExtensionRegistrationVerdict Resolve(bool hasIncumbent, bool candidateReplaces,
            bool incumbentAssignableFromCandidate)
        {
            if (!hasIncumbent)
                return ExtensionRegistrationVerdict.Register;
            if (candidateReplaces || incumbentAssignableFromCandidate)
                return ExtensionRegistrationVerdict.Replace;
            return ExtensionRegistrationVerdict.Conflict;
        }

        /// <summary>The runtime's pre-registration ordering key (<c>TemplateFactory.LoadExtensions</c>): candidates
        /// sort by whether any inherited <c>[DataType]</c> names an interface, then by the same test over
        /// <c>[ChainedType]</c> — <c>false</c> first, stably. Expressed as a small integer so every caller sorts
        /// by the same expression.</summary>
        internal static int OrderingKey(bool hasInterfaceDataType, bool hasInterfaceChainedType)
        {
            return (hasInterfaceDataType ? 2 : 0) + (hasInterfaceChainedType ? 1 : 0);
        }
    }
}
