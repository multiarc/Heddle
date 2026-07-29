using System.Collections.Generic;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Generated code is compiled by the consumer's project, under the consumer's settings — including
    /// <c>&lt;CheckForOverflowUnderflow&gt;</c>, which a template knows nothing about and cannot influence. The engine's
    /// arithmetic is built from <c>Expression.Add</c> and friends, which are the unchecked factories, so it wraps
    /// whatever the host does. The emitter has to wrap too, or the same template renders a number in one project and
    /// throws <c>OverflowException</c> in the next.
    /// </summary>
    public class CheckedOverflowContextTests
    {
        private const string Key = "views/checked-overflow.heddle";

        private const string Template =
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Order}}@(Count * Count * Count)";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeOverflowWrapsWhicheverWayTheHostCompiles(bool hostChecksOverflow)
        {
            var model = new Order { Count = 100000 };

            var (precompiled, dyn) = DifferentialHarness.Render(
                Key, Template, typeof(Order), model, checkOverflow: hostChecksOverflow);

            Assert.Equal(dyn, precompiled);
        }

        private const string CSharpKey = "views/checked-overflow-cs.heddle";

        private const string CSharpTemplate =
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Order}}@(@model.Count * model.Count * model.Count)";

        private static readonly Dictionary<string, string> FullCSharpBuild =
            new Dictionary<string, string> { ["build_property.HeddleExpressionMode"] = "FullCSharp" };

        /// <summary>
        /// The verbatim C# tier pastes the author's expression into the consumer's assembly, where the consumer's
        /// overflow setting applies to it. The engine compiles the same text into an assembly of its own with
        /// overflow checking off, unconditionally — so without a wrapper the two tiers disagree the moment a host
        /// turns checking on, and the template that renders a wrapped number in one project throws in the next.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EmbeddedCSharpOverflowWrapsWhicheverWayTheHostCompiles(bool hostChecksOverflow)
        {
            var model = new Order { Count = 100000 };
            var runtime = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

            var (precompiled, dyn) = DifferentialHarness.Render(
                CSharpKey, CSharpTemplate, typeof(Order), model, FullCSharpBuild, runtime,
                checkOverflow: hostChecksOverflow);

            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// A <b>constant</b> overflow is checked by the C# language whatever the compilation is configured to do,
        /// so it is the one case a compilation option cannot settle and only a syntactic <c>unchecked</c> can. Both
        /// tiers compile the author's text as C#, so if only one of them wraps it, one renders a number and the
        /// other refuses the template. The native tier already wraps its constants; this is the same answer for the
        /// text the C# tier hands to Roslyn verbatim.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AConstantOverflowInEmbeddedCSharpWrapsOnBothTiers(bool hostChecksOverflow)
        {
            const string key = "views/checked-overflow-const.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Order}}@(@100000 * 100000 * 100000)";
            var runtime = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

            var (precompiled, dyn) = DifferentialHarness.Render(
                key, template, typeof(Order), new Order(), FullCSharpBuild, runtime,
                checkOverflow: hostChecksOverflow);

            Assert.Equal(dyn, precompiled);
            Assert.Equal(unchecked(100000 * 100000 * 100000).ToString(), precompiled);
        }
    }
}
