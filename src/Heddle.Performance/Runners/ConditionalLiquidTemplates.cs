namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The Liquid source shared by the Fluid and DotLiquid conditional-heavy twins — both consume
    /// the same dialect, so the matrix is authored once. Per row: one four-way
    /// <c>{% if %}/{% elsif %}/{% else %}</c> tier chain on precomputed booleans plus two
    /// independent toggles, all dense (the parity normalizer is a functional no-op).
    /// </summary>
    internal static class ConditionalLiquidTemplates
    {
        public const string PageTemplate =
            "<ul class=\"matrix\">{% for r in rows %}<li>" +
            "{% if r.is_bronze %}<span class=\"t0\">bronze</span>" +
            "{% elsif r.is_silver %}<span class=\"t1\">silver</span>" +
            "{% elsif r.is_gold %}<span class=\"t2\">gold</span>" +
            "{% else %}<span class=\"t3\">platinum</span>{% endif %}" +
            "<em>{{ r.name }}</em>" +
            "{% if r.has_note %}<small>{{ r.note }}</small>{% endif %}" +
            "{% if r.is_active %}<b>active</b>{% endif %}" +
            "</li>{% endfor %}</ul>";
    }
}
