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
    }
}
