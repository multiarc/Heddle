using System.Globalization;

// Declaratively export the host function container (phase 6 D24). The generator discovers this over the
// compilation's reference to this test assembly and binds calls directly to the container (phase 7 D21).
[assembly: Heddle.Attributes.ExportFunctions(typeof(Heddle.Generator.IntegrationTests.Fixtures.TemplateFunctions))]

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
