using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 composition coverage: the shipped families nested inside one another — branches inside list element
    /// bodies (the list binding must carry <c>needsLocals</c> when the element body hosts elif/else participants),
    /// branches under a dynamic root, and nested lists. All differential-gated against the runtime backend.
    /// </summary>
    public class CompositionTests
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
            } } };
            yield return new object[] { new Catalog { Title = "Empty", Products = new List<Product>() } };
            yield return new object[] { new Catalog { Title = "Null", Products = null } };
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void BranchInsideListElement(Catalog model)
        {
            var t = "@model(){{" + CatalogType + "}}@\\\n" +
                    "@list(Products){{<li>@(Name): @if(Manufacturer){{ yes }}@else(){{ no }}</li>}}\n";
            AssertParity("views/comp-branch-in-list.heddle", t, typeof(Catalog), model);
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void BranchUnderDynamicRoot(Catalog model)
        {
            // Model-less root => dynamic tier; @if condition is a dynamic member path.
            var t = "@if(Title){{has-title:@(Title)}}@else(){{no-title}}\n";
            AssertParity("views/comp-branch-dynamic.heddle", t, typeof(Catalog), model);
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void ListUnderDynamicRoot(Catalog model)
        {
            var t = "start @list(Products){{[@(Name)]}} end\n";
            AssertParity("views/comp-list-dynamic.heddle", t, typeof(Catalog), model);
        }
    }
}
