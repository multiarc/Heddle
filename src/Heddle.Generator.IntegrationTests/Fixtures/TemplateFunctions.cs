using System.Globalization;

// Declaratively export the host function container (phase 6 D24). The generator discovers this over the
// compilation's reference to this test assembly and binds calls directly to the container (phase 7 D21).
[assembly: Heddle.Attributes.ExportFunctions(typeof(Heddle.Generator.IntegrationTests.Fixtures.TemplateFunctions))]
// Phase 3 (F2 / OQ2): a second container exporting one of the same names, so the merge path is differential-gated.
// It deliberately carries only ELIGIBLE members: an ineligible one would make the runtime's RegisterFrom throw,
// which is precisely why the generator now raises HED7021 for it (see ExportDiscoveryTests) rather than quietly
// excluding it — a container the host cannot register is a build error, not a silent count adjustment.
[assembly: Heddle.Attributes.ExportFunctions(typeof(Heddle.Generator.IntegrationTests.Fixtures.MoreTemplateFunctions))]

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>A host function container exported for the export-function differential (phase 7 D21). Function
    /// names are the method names lowercased: <c>titlecase</c>, <c>shout</c>.</summary>
    public static class TemplateFunctions
    {
        public static string TitleCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }

        public static string Shout(string value) => (value ?? string.Empty).ToUpperInvariant() + "!";
    }
}

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>The eligibility/merge fixture container. <c>Shout(int)</c> merges a second overload onto the name
    /// <c>TemplateFunctions.Shout(string)</c> already claims; the remaining members are exactly the shapes the
    /// runtime refuses to register, so <b>neither</b> tier may count them.</summary>
    public static class MoreTemplateFunctions
    {
        /// <summary>A merged overload of the name <c>TemplateFunctions</c> also exports.</summary>
        public static string Shout(int value) => value + "!";

        /// <summary>Eligible, only in this container.</summary>
        public static string Twice(string value) => (value ?? string.Empty) + (value ?? string.Empty);

        /// <summary>Q8.1 / HED7025: a <b>host</b> overload set with the collision shape the shipped built-in table
        /// has for <c>min</c> — <c>(int, uint)</c> ranks <c>(1, 1)</c> against both signatures, so the flat Pareto
        /// front has two members and the call is ambiguous on both tiers. Phase 3 routed arbitrary export signatures
        /// through the shared ranker, and phase 4's WI10 measurement (0-of-480 winner changes) explicitly does not
        /// carry to host-registered sets, so the build error is exercised here and not only over the built-ins.
        /// Both overloads live in one container, so no other function's manifest row is affected.</summary>
        public static string Blend(long left, long right) => "long:" + (left + right);

        /// <summary>The ambiguous twin of <see cref="Blend(long, long)"/>.</summary>
        public static string Blend(double left, double right) =>
            "double:" + (left + right).ToString(CultureInfo.InvariantCulture);

        /// <summary>Skipped as a special name — a property accessor, not a function. Both tiers must exclude it
        /// from the overload count, which the gauntlet compares exactly.</summary>
        public static string Version => "1";
    }
}
