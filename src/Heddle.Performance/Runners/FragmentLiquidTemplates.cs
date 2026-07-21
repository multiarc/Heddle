namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The Liquid sources for the fragment-heavy twins. Unlike the other workloads, Fluid and
    /// DotLiquid cannot share one text here: the probe-E-verified include constructs differ —
    /// Fluid's <c>{% include 'tile' with item %}</c> binds the row to a variable named after the
    /// partial (<c>tile.*</c> inside it), while DotLiquid's plain <c>{% include 'tile' %}</c>
    /// shares the enclosing scope (<c>item.*</c> inside it; <c>include … with &lt;hash&gt;</c>
    /// must NOT be used — DotLiquid enumerates a Hash argument and renders once per key).
    /// </summary>
    internal static class FragmentLiquidTemplates
    {
        // ---- Fluid: `with item` binds the row as `tile` inside the partial ---------------------
        public const string FluidPageTemplate =
            "<div class=\"panel\">{% for item in items %}{% include 'tile' with item %}{% endfor %}</div>";

        public const string FluidTileTemplate =
            "<section class=\"tile\"><h3>{{ tile.name }}</h3><p class=\"v\">{{ tile.value }}</p><span class=\"badge\">{{ tile.badge }}</span></section>";

        // ---- DotLiquid: plain include shares the enclosing scope (`item` stays visible) --------
        public const string DotLiquidPageTemplate =
            "<div class=\"panel\">{% for item in items %}{% include 'tile' %}{% endfor %}</div>";

        public const string DotLiquidTileTemplate =
            "<section class=\"tile\"><h3>{{ item.name }}</h3><p class=\"v\">{{ item.value }}</p><span class=\"badge\">{{ item.badge }}</span></section>";
    }
}
