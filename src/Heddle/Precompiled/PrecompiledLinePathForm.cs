namespace Heddle.Precompiled
{
    /// <summary>
    /// Which form the <c>#line</c> file names in a precompiled template's generated source are in (schema 3).
    /// Machine-readable manifest data, not a prose comment. Absent means <see cref="Unspecified"/> — no validation required.
    /// </summary>
    public enum PrecompiledLinePathForm
    {
        /// <summary>No claim — row records no form. Also used for fallback-marker entries (no generated source).</summary>
        Unspecified = 0,

        /// <summary>The <c>#line</c> file names are relative to <c>HeddleTemplateRoot</c>; join to template root before opening.</summary>
        RootRelative = 1,

        /// <summary>The <c>#line</c> file names are the template's <c>AdditionalText.Path</c> (absolute in a real build); no <c>HeddleTemplateRoot</c> anchor.</summary>
        TemplatePath = 2
    }
}
