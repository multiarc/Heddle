using System.Collections.Generic;
using System.Text;
using Heddle.Runtime.Expressions;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The result of <see cref="PrecompiledTemplates.ValidateAll"/> — the aggregate post-configuration
    /// validation pass (Q8.32 subset A). It carries <b>every</b> entry that failed the gauntlet, not the first,
    /// which is the whole difference between this pass and the per-request gate.</para>
    /// <para><b>The verdict is scoped, and the report says to what (Q8.38).</b> Four of the gauntlet's inputs are
    /// per-request, not per-configuration: the fingerprint triple <c>(OutputProfile, ExpressionMode,
    /// TrimDirectiveLines)</c> that step 1 compares, and the effective <c>Functions</c> registry that step 3
    /// compares against. A pass run once "after the host has finished configuring" can therefore only be complete
    /// with respect to one options shape, so this report records the shape it used —
    /// <see cref="ValidatedFingerprint"/>, <see cref="ValidatedFunctions"/>, <see cref="ValidatedStaleness"/> — and
    /// its green property is named <see cref="PassedForValidatedOptions"/> rather than anything that would read as
    /// a blanket all-clear. A host that renders under more than one shape (two profiles, a request-scoped
    /// <see cref="FunctionRegistry"/>) calls the pass once per shape; nothing here claims to have covered the
    /// shapes it was not given.</para>
    /// <para>The failures are ordinary <see cref="PrecompiledFallbackEvent"/>s produced by the same gauntlet the
    /// per-request path runs, with the same reasons, detail strings and <c>HED7101</c> id — so a host logs them
    /// through the machinery it already has.</para>
    /// </summary>
    public sealed class PrecompiledValidationReport
    {
        internal PrecompiledValidationReport(PrecompiledOptionsFingerprint fingerprint, FunctionRegistry functions,
            bool validatedStaleness, int entriesChecked, IReadOnlyList<PrecompiledFallbackEvent> failures)
        {
            ValidatedFingerprint = fingerprint;
            ValidatedFunctions = functions;
            ValidatedStaleness = validatedStaleness;
            EntriesChecked = entriesChecked;
            Failures = failures;
        }

        /// <summary>The options triple the pass compared every entry's fingerprint against — a snapshot taken at
        /// the call, not a live reference, so a later mutation of the caller's <c>TemplateOptions</c> cannot make
        /// the report describe a shape it did not validate.</summary>
        public PrecompiledOptionsFingerprint ValidatedFingerprint { get; }

        /// <summary>The effective function registry the pass validated function bindings against;
        /// <c>null</c> means the request-registry-less shape, which compiles against the frozen
        /// <see cref="FunctionRegistry"/> default. Reference identity is the answer: a different registry instance
        /// is a different question, even when it holds the same registrations today.</summary>
        public FunctionRegistry ValidatedFunctions { get; }

        /// <summary>Whether the staleness step ran (the options had <c>EnableFileChangeCheck</c> set). When false,
        /// nothing in this report says anything about templates having changed on disk.</summary>
        public bool ValidatedStaleness { get; }

        /// <summary>How many registered entries the pass examined — <see cref="PrecompiledTemplates.Entries"/> at
        /// the moment of the call, marker entries included.</summary>
        public int EntriesChecked { get; }

        /// <summary>Every failing entry, ordered by template key (ordinal). Empty when nothing failed.</summary>
        public IReadOnlyList<PrecompiledFallbackEvent> Failures { get; }

        /// <summary>True when no registered entry failed <b>under the validated options</b>. Deliberately not
        /// named <c>IsValid</c>: the pass cannot answer a question about options it was not given.</summary>
        public bool PassedForValidatedOptions => Failures.Count == 0;

        /// <summary>A one-line rendering that carries the scope next to the verdict, so a log line cannot be read
        /// as a blanket all-clear either.</summary>
        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append("Precompiled validation: ").Append(Failures.Count).Append(" of ").Append(EntriesChecked)
                .Append(" registered entries failed under OutputProfile=").Append(ValidatedFingerprint.Profile)
                .Append(", ExpressionMode=").Append(ValidatedFingerprint.ExpressionMode)
                .Append(", TrimDirectiveLines=").Append(ValidatedFingerprint.TrimDirectiveLines ? "true" : "false")
                .Append(", Functions=").Append(ValidatedFunctions == null ? "<default>" : "<request registry>")
                .Append(", staleness=").Append(ValidatedStaleness ? "checked" : "not checked")
                .Append(". This verdict is about these options only.");
            return text.ToString();
        }
    }
}
