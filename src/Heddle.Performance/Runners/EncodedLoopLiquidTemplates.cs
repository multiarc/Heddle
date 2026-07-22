namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The Liquid source shared by the Fluid and DotLiquid encoded-loop twins — both consume the
    /// same dialect, so the loop is authored once. One <c>{% for %}</c> over the 5,000-row model
    /// with every untrusted member through <c>| escape</c> (attribute-value and text positions
    /// alike — on the pinned alphabet the engines' HTML escaper matches Heddle's <c>@attr</c> and
    /// <c>@(…)</c> encoders byte-for-byte), no whitespace between tags.
    /// </summary>
    internal static class EncodedLoopLiquidTemplates
    {
        public const string LoopTemplate =
            "<table>{% for item in items %}" +
            "<tr><td data-tag=\"{{ item.tag | escape }}\">{{ item.name | escape }}</td><td>{{ item.comment | escape }}</td></tr>" +
            "{% endfor %}</table>";
    }
}
