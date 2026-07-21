namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The Liquid source shared by the Fluid and DotLiquid fortunes-encoded twins — both consume
    /// the same dialect, so the table is authored once. The TechEmpower Fortunes skeleton around
    /// one <c>{% for %}</c> over the 12 pinned rows, every message through <c>| escape</c>
    /// (the engines' HTML escaper), no whitespace between tags.
    /// </summary>
    internal static class FortunesLiquidTemplates
    {
        public const string FortunesTemplate =
            "<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>" +
            "{% for r in rows %}<tr><td>{{ r.id }}</td><td>{{ r.message | escape }}</td></tr>{% endfor %}" +
            "</table></body></html>";
    }
}
