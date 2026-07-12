namespace Heddle.Performance.Runners
{
    /// <summary>
    /// The Liquid source shared by the Fluid and DotLiquid twins — both consume the same dialect, so
    /// the page is authored once. The layout is an <c>{% include %}</c>, reusable sections are
    /// <c>section.*</c> members, component calls are <c>areas[...]</c> / <c>comp.*</c> lookups, and
    /// the ordered area menus are rendered with a real <c>{% for %}</c> loop.
    /// </summary>
    internal static class LiquidTemplates
    {
        public const string HomeTemplate = "{% include 'layout' %}";

        public const string LayoutTemplate =
            "{{ section.meta }}{{ section.social }}" +
            "{{ comp.assets_styles }}{{ comp.custom_styles }}{{ comp.head_scripts }}{{ comp.body_scripts }}" +
            "{% for name in area_names %}{{ areas[name] }}{% endfor %}" +
            "{{ comp.assets_scripts }}{{ section.page_scripts }}{{ section.endpage_scripts }}{{ comp.body_end_scripts }}";
    }
}
