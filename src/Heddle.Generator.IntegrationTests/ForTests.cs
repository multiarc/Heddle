using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 <c>@for</c> sugar: <c>ForIndexExtension</c> bound with the loop body; each iteration re-scopes via
    /// <c>scope.Parent(i)</c> (ModelData = the enclosing model, ChainedData = the boxed index), so <c>@out()</c>
    /// splices the index. Counted (<c>@for(n)</c> / <c>@for(Count)</c>) and <c>range(...)</c> forms are all bound
    /// through the same extension. Differential-gated.
    /// </summary>
    public class ForTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Carts()
        {
            yield return new object[] { new Cart { Count = 3 } };
            yield return new object[] { new Cart { Count = 0 } };
            yield return new object[] { new Cart { Count = 5 } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Carts))]
        public void CountedLiteral(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n<ul>@for(3){{<li>@out()</li>}}</ul>\n";
            AssertParity("views/for-literal.heddle", t, typeof(Cart), model);
        }

        [Theory]
        [MemberData(nameof(Carts))]
        public void CountedMember(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n@for(Count){{[@out()]}}\n";
            AssertParity("views/for-member.heddle", t, typeof(Cart), model);
        }

        [Theory]
        [MemberData(nameof(Carts))]
        public void RangeForm(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n@for(range(2, 8, 3)){{<i>@out()</i>}}\n";
            AssertParity("views/for-range.heddle", t, typeof(Cart), model);
        }

        // Phase 6 (post-2.0) WI8: the range-for differential fixture rows through both backends — iteration,
        // the empty range, and the D4 str(range(...)) stringification (renders "range(2, 10, 2)"; verifies
        // PrecompiledFunctions.Str over a boxed Heddle.Models.Range matches the dynamic Str byte-for-byte).
        [Fact]
        public void RangeForFixtureIsByteIdenticalAcrossBackends()
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "@for(range(2, 10, 2)){{@out()}}\n" +
                    "@for(range(2, 8, 3)){{[@out()]}}\n" +
                    "@for(range(5, 2)){{x}}\n" +
                    "@(str(range(2, 10, 2)))\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/range-for.heddle", t, typeof(Cart), new Cart());
            Assert.Equal(dyn, precompiled);
            Assert.Contains("2468", precompiled);
            Assert.Contains("[2][5]", precompiled);
            Assert.Contains("range(2, 10, 2)", precompiled);
        }
    }
}
