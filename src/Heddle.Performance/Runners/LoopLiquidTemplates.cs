namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The Liquid source shared by the Fluid and DotLiquid large-loop twins — both consume the
    /// same dialect, so the loop is authored once. One <c>{% for %}</c> over the 5,000-row model,
    /// two scalar members per row, no whitespace between tags (the parity normalizer is a
    /// functional no-op).
    /// </summary>
    internal static class LoopLiquidTemplates
    {
        public const string LoopTemplate =
            "{% for item in items %}<tr><td>{{ item.name }}</td><td>{{ item.value }}</td></tr>{% endfor %}";
    }
}
