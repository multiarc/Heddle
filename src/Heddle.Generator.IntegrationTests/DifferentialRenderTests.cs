using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI9 / D20 — the full-corpus differential gate, grown family by family. Each fixture is rendered by the
    /// compiled precompiled backend and the dynamic engine and asserted byte-identical. Family 1: static text and
    /// reference-typed member paths (generated-code.md example 1).
    /// </summary>
    public class DifferentialRenderTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        public static IEnumerable<object[]> ProductModels()
        {
            yield return new object[] { new Product { Name = "Widget", Description = "A thing", Manufacturer = new Manufacturer { Name = "Acme", Address = new Address { City = "Springfield" } } } };
            yield return new object[] { new Product { Name = null, Description = null, Manufacturer = null } };
            yield return new object[] { new Product { Name = "X", Manufacturer = new Manufacturer { Name = null, Address = null } } };
            yield return new object[] { null };
        }

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        [Theory]
        [MemberData(nameof(ProductModels))]
        public void StaticTextAndSingleMemberPath(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<h1>@(Name)</h1>\n";
            AssertParity("views/product-1.heddle", t, typeof(Product), model);
        }

        [Theory]
        [MemberData(nameof(ProductModels))]
        public void NestedMemberPaths(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "<article>\n  <h1>@(Name)</h1>\n  <p>Made by @(Manufacturer.Name)</p>\n</article>\n";
            AssertParity("views/product-2.heddle", t, typeof(Product), model);
        }

        [Theory]
        [MemberData(nameof(ProductModels))]
        public void DeepMemberPathAndMultipleReads(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "City: @(Manufacturer.Address.City); name @(Name), desc @(Description).\n";
            AssertParity("views/product-3.heddle", t, typeof(Product), model);
        }

        [Fact]
        public void PureStaticText()
        {
            var t = "Just static text\nwith multiple lines\nand no directives.\n";
            AssertParity("views/static.heddle", t, typeof(object), null);
        }

        [Theory]
        [MemberData(nameof(ProductModels))]
        public void UsingDirectiveIsStripped(Product model)
        {
            var t = "@using(){{System.Text}}@\\\n@model(){{" + ProductType + "}}@\\\nHello @(Name)!\n";
            AssertParity("views/product-using.heddle", t, typeof(Product), model);
        }
    }
}
