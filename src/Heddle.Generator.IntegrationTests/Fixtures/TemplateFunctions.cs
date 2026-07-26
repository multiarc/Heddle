using System.Globalization;

// Export host function containers for generator discovery.
[assembly: Heddle.Attributes.ExportFunctions(typeof(Heddle.Generator.IntegrationTests.Fixtures.TemplateFunctions))]
// Fixture for merge-path testing; contains only eligible members to validate build-time enforcement.
[assembly: Heddle.Attributes.ExportFunctions(typeof(Heddle.Generator.IntegrationTests.Fixtures.MoreTemplateFunctions))]

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>Exported function container; function names are method names lowercased.</summary>
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
    /// <summary>Fixture for merge-path testing; defines ineligible shapes the runtime refuses to register.</summary>
    public static class MoreTemplateFunctions
    {
        /// <summary>A merged overload of the name <c>TemplateFunctions</c> also exports.</summary>
        public static string Shout(int value) => value + "!";

        /// <summary>Eligible, only in this container.</summary>
        public static string Twice(string value) => (value ?? string.Empty) + (value ?? string.Empty);

        /// <summary>HED7025: a <b>host</b> overload set with the collision shape the shipped built-in table
        /// has for <c>min</c> — <c>(int, uint)</c> ranks <c>(1, 1)</c> against both signatures, so the flat Pareto
        /// front has two members and the call is ambiguous on both tiers. This collision shape does not carry to
        /// host-registered sets, so the build error is exercised here and not only over the built-ins.
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
