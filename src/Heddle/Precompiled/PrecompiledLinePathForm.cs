namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>Which form the <c>#line</c> file names in a precompiled template's generated source are in
    /// (schema 3, Q8.31). Q8.27 answered the same question with a comment line under
    /// <c>// &lt;auto-generated/&gt;</c>; a reader could act on that, but a stack-trace symbolizer, an IDE or the
    /// LSP cannot. The choice is manifest data now and the comment is gone — one carrier, machine-readable, and
    /// the generated file stops carrying prose about its own layout.</para>
    /// <para>Vacuous when absent: a row that records nothing reads back as <see cref="Unspecified"/>, which asserts
    /// nothing, so no consumer can reject a manifest for lacking the value.</para>
    /// </summary>
    public enum PrecompiledLinePathForm
    {
        /// <summary>No claim — the row records no form. Also what a fallback-marker entry carries: it has no
        /// generated source, so it has no <c>#line</c> directives to describe.</summary>
        Unspecified = 0,

        /// <summary>The <c>#line</c> file names are relative to <c>HeddleTemplateRoot</c>. A consumer must join
        /// them onto the build's template root before opening them.</summary>
        RootRelative = 1,

        /// <summary>The <c>#line</c> file names are the template's own <c>AdditionalText.Path</c> — absolute in a
        /// real build — because no <c>HeddleTemplateRoot</c> anchor applied to it.</summary>
        TemplatePath = 2
    }
}
