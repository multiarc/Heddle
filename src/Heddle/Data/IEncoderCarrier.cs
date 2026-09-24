namespace Heddle.Data
{
    /// <summary>Seam for renderers to discover the configured <c>TemplateOptions.Encoder</c> without
    /// threading through the hot <see cref="Scope"/> struct; <c>null</c> selects legacy <c>WebUtility.HtmlEncode</c>.</summary>
    internal interface IEncoderCarrier
    {
        System.Text.Encodings.Web.TextEncoder Encoder { get; }
    }
}
