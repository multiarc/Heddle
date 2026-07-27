using System.Linq;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The depth bound has to hold at build time too, and it matters more here: a stack overflow while parsing a
    /// template does not fail a build, it kills the compiler or the IDE process hosting the generator. The bound is
    /// shared by source-linking the parser rather than reimplemented, so what these check is that the link is real
    /// and reaches the generator's own entry point.
    /// </summary>
    public class DeepNestingGeneratorTests
    {
        private const int PastTheLimit = 1500;

        [Theory]
        [InlineData("prefix")]
        [InlineData("conditional")]
        [InlineData("coalesce")]
        [InlineData("flat")]
        [InlineData("blocks")]
        public void ADeeplyNestedTemplateFailsTheBuildInsteadOfTheCompiler(string shape)
        {
            var generated = DifferentialHarness.Generate(new[] { ("views/deep.heddle", Document(shape)) });

            Assert.NotEmpty(generated.Diagnostics);
        }

        /// <summary>Ordinary depth must still precompile, or the guard would be the worse defect.</summary>
        [Fact]
        public void AnOrdinarilyDeepTemplateStillPrecompiles()
        {
            const int realistic = 30;
            var document = "@model(){{string}}" +
                           string.Concat(Enumerable.Repeat("@if(true){{", realistic)) +
                           "@(Length)" +
                           string.Concat(Enumerable.Repeat("}}", realistic));

            var generated = DifferentialHarness.Generate(new[] { ("views/ordinary.heddle", document) });

            Assert.Empty(generated.Diagnostics);
        }

        private static string Document(string shape)
        {
            const string head = "@model(){{dynamic}}";
            switch (shape)
            {
                case "prefix":
                    return head + "@(" + new string('!', PastTheLimit) + "true)";
                case "conditional":
                    return head + "@(" + string.Concat(Enumerable.Repeat("true ? ", PastTheLimit)) + "1" +
                           string.Concat(Enumerable.Repeat(" : 2", PastTheLimit)) + ")";
                case "coalesce":
                    return head + "@(" + string.Join(" ?? ", Enumerable.Repeat("Title", PastTheLimit)) + ")";
                case "flat":
                    return head + "@(" + string.Join("+", Enumerable.Repeat("1", PastTheLimit)) + ")";
                default:
                    return head + string.Concat(Enumerable.Repeat("@if(true){{", PastTheLimit)) + "x" +
                           string.Concat(Enumerable.Repeat("}}", PastTheLimit));
            }
        }
    }
}
