namespace Heddle.Language
{
    /// <summary>
    /// The directive spellings the <b>language</b> reserves, as one engine-owned table.
    /// <para>These are not extension names in the sense the agnosticism gate exists to police. A build has to know
    /// the model type and the <c>@using</c> set before it can bind anything at all, and it has to know the running
    /// output profile before it can choose a carrier — so the three names below are read at <i>parse</i> level,
    /// out of the directive stream, by both tiers. Reading them is not the build predicting what an extension does;
    /// it is the build reading the language's own keywords, exactly as the parse listener reads <c>@&lt;&lt;</c>.</para>
    /// <para>They live here rather than in the emitter so there is one spelling of each, shared by every tier that
    /// reads it, and so the ledger row that records them says "grammar keyword" in the file that owns the grammar.</para>
    /// </summary>
    internal static class DirectiveNames
    {
        /// <summary>The <c>@model(){{T}}</c> directive, which types the document.</summary>
        internal const string Model = "model";

        /// <summary>The <c>@using(){{Ns}}</c> directive, which contributes to the namespace set every type
        /// spelling resolves under.</summary>
        internal const string Using = "using";

        /// <summary>The <c>@profile(){{text|html}}</c> directive, which flips the running output profile and so
        /// decides which carrier an unnamed rendering block resolves to.</summary>
        internal const string Profile = "profile";
    }
}
