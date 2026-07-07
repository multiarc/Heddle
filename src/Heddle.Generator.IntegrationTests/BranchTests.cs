using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI6/WI6a differential (D20): branch sets (generated-code.md example 6). Bound If/IfNot/Elif/Else
    /// instances (never inlined), branch bodies executing under the enclosing model, the strip machine removing
    /// inter-block whitespace, and the ScopeLocals frame provisioning for participant-hosting bodies. Rendered
    /// byte-identically to the dynamic engine across true/false/null models.
    /// </summary>
    public class BranchTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        public static IEnumerable<object[]> CartModels()
        {
            yield return new object[] { new Cart { Count = 3, IsArchived = false, IsFeatured = true } };
            yield return new object[] { new Cart { Count = 0, IsArchived = true, IsFeatured = false } };
            yield return new object[] { new Cart { Count = 5, IsArchived = false, IsFeatured = false } };
            yield return new object[] { null };
        }

        private static void AssertParity(string key, string content, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), model);
            Assert.Equal(dyn, precompiled);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void LoneIf(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\nItems check: @if(Count > 0){{ has items }}done\n";
            AssertParity("views/branch-if.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void LoneIfNot(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n@ifnot(IsArchived){{ still active }}\n";
            AssertParity("views/branch-ifnot.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void IfElifElse(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "@if(IsFeatured){{ <b>Featured</b> }}\n" +
                    "@elif(IsArchived){{ <i>Archived</i> }}\n" +
                    "@else(){{ Regular }}\n";
            AssertParity("views/branch-set.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void IfElse(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "@if(Count > 0){{ some }}\n" +
                    "@else(){{ none }}\n";
            AssertParity("views/branch-ifelse.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void MemberConditionAndBodyReads(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "@if(IsFeatured){{ featured with count @(Count) }}\n" +
                    "@else(){{ plain @(Name) }}\n";
            AssertParity("views/branch-body-reads.heddle", t, model);
        }
    }
}
