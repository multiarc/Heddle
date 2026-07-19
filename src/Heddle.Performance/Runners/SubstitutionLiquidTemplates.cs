namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The Liquid source shared by the Fluid and DotLiquid trivial-substitution twins — both
    /// consume the same dialect, so the card is authored once. Ten scalar member substitutions,
    /// no composition, no whitespace between tags (the parity normalizer is a functional no-op).
    /// </summary>
    internal static class SubstitutionLiquidTemplates
    {
        public const string CardTemplate =
            "<article><h1>{{ title }}</h1><p class=\"sku\">{{ sku }}</p><p class=\"price\">{{ price }}</p>" +
            "<p class=\"brand\">{{ brand }}</p><p class=\"cat\">{{ category }}</p><p class=\"avail\">{{ availability }}</p>" +
            "<a class=\"link\" href=\"{{ url }}\"><img src=\"{{ image_url }}\"></a>" +
            "<p class=\"sum\">{{ summary }}</p><p class=\"rating\">{{ rating }}</p></article>";
    }
}
