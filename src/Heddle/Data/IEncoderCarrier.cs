namespace Heddle.Data
{
    /// <summary>
    /// Internal renderer capability (B2): exposes the effective output <see cref="System.Text.Encodings.Web.TextEncoder"/>
    /// carried by a renderer for this render, or <c>null</c> to select the legacy <c>WebUtility.HtmlEncode</c> path.
    /// The single seam through which the HTML-encode sites (<see cref="HtmlEncodedRenderer"/> and
    /// <see cref="Heddle.Core.AbstractHtmlExtension"/>) discover the configured <c>TemplateOptions.Encoder</c> without
    /// threading it through the hot <see cref="Scope"/> struct. Implemented by the sink adapters and by
    /// <see cref="HtmlEncodedRenderer"/> (so nested resolution keeps working). Never public — encoder selection is an
    /// engine-internal concern.
    /// </summary>
    internal interface IEncoderCarrier
    {
        System.Text.Encodings.Web.TextEncoder Encoder { get; }
    }
}
