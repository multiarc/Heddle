using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI5a differential (D20, OQ1/D21): built-in function calls inside <c>@(...)</c> bind through the public
    /// <c>PrecompiledFunctions</c> shim, and the consumer compiler resolves the overload over the same closed
    /// candidate set as the runtime registry — rendered byte-identically to the dynamic engine, including the
    /// never-throw/invariant-culture contract on null arguments.
    /// </summary>
    public class FunctionTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        public static IEnumerable<object[]> CartModels()
        {
            yield return new object[] { new Cart { Count = 5, Quantity = 12, Price = 3.14159m, Name = "Widget" } };
            yield return new object[] { new Cart { Count = -7, Quantity = 0, Price = 2.5m, Name = null } };
            yield return new object[] { null };
        }

        private static void AssertParity(string key, string content, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), model);
            Assert.Equal(dyn, precompiled);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void StringFunctions(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "Upper: @(upper(Name)); len @(len(Name)); trimmed [@(trim(Name))].\n";
            AssertParity("views/fn-string.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void NumericFunctions(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "Abs: @(abs(Count)); max @(max(Count, Quantity)); round @(round(Price, 2)).\n";
            AssertParity("views/fn-numeric.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void FunctionWithLiteralAndMemberArgs(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "Contains: @(contains(Name, \"id\")); sub @(substr(Name, 0, 3)).\n";
            AssertParity("views/fn-mixed.heddle", t, model);
        }
    }
}
