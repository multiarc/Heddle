using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI5 differential (D20): native expressions (generated-code.md example 2) — arithmetic, comparison,
    /// coalesce, ternary, and value-typed member paths with the boxed-default null-safety (<c>@(Count)</c> on a null
    /// model renders <c>0</c>, not empty). Rendered byte-identically to the dynamic engine.
    /// </summary>
    public class NativeExpressionTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        public static IEnumerable<object[]> CartModels()
        {
            yield return new object[] { new Cart { Count = 3, IsArchived = false, IsFeatured = true, Price = 9.99m, Quantity = 4, Name = "Basket", Nested = new Nested { Amount = 7 } } };
            yield return new object[] { new Cart { Count = 0, IsArchived = true, IsFeatured = false, Price = 0m, Quantity = 0, Name = null, Nested = null } };
            yield return new object[] { null };
        }

        private static void AssertParity(string key, string content, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), model);
            Assert.Equal(dyn, precompiled);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void ValueTypedMemberPath(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\nCount is @(Count).\n";
            AssertParity("views/cart-count.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void Arithmetic(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\nTotal: @(Price * Quantity); doubled @(Count * 2).\n";
            AssertParity("views/cart-arith.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void CoalesceAndTernary(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "Author: @(Name ?? \"anonymous\"); badge @(IsFeatured ? \"featured\" : \"plain\").\n";
            AssertParity("views/cart-coalesce.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void ComparisonAndLogical(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "Has: @(Count > 0 && !IsArchived); check @(Count >= Quantity).\n";
            AssertParity("views/cart-compare.heddle", t, model);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void NestedValueHop(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\nAmount: @(Nested.Amount).\n";
            AssertParity("views/cart-nested.heddle", t, model);
        }
    }
}
