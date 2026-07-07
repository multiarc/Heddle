using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 <c>@list</c> family (generated-code.md example 3): <c>ListExtension</c> bound with an element body on
    /// the dynamic tier (the element type is discoverable only by reflection in-runtime; the C# runtime binder
    /// resolves the same members, differential-gated). The collection parameter rides the typed member tier.
    /// </summary>
    public class ListTests
    {
        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Catalogs()
        {
            yield return new object[] { new Catalog { Title = "Store", Products = new List<Product>
            {
                new Product { Name = "A", Manufacturer = new Manufacturer { Name = "Acme" } },
                new Product { Name = "B", Manufacturer = null },
                new Product { Name = null, Manufacturer = new Manufacturer { Name = "Zed" } },
            } } };
            yield return new object[] { new Catalog { Title = "Empty", Products = new List<Product>() } };
            yield return new object[] { new Catalog { Title = "Null", Products = null } };
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void SimpleList(Catalog model)
        {
            var t = "@model(){{" + CatalogType + "}}@\\\n<ul>@list(Products){{<li>@(Name)</li>}}</ul>\n";
            AssertParity("views/list-simple.heddle", t, typeof(Catalog), model);
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void ListWithNestedMemberHop(Catalog model)
        {
            var t = "@model(){{" + CatalogType + "}}@\\\n@list(Products){{[@(Name) by @(Manufacturer.Name)]}}\n";
            AssertParity("views/list-nested.heddle", t, typeof(Catalog), model);
        }
    }
}
