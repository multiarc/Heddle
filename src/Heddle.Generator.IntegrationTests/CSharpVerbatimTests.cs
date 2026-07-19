using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 — the FullCSharp verbatim tier (generated-code.md example 3): the parser-captured C# expression pastes
    /// verbatim into the generated body against a local named <c>model</c>, exactly as the runtime's
    /// <c>CSharpClassTemplate</c> compiles it. Emitted only under <c>ExpressionMode.FullCSharp</c>; differential-gated.
    /// </summary>
    public class CSharpVerbatimTests
    {
        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";

        private static readonly Dictionary<string, string> FullCSharp =
            new Dictionary<string, string> { ["build_property.HeddleExpressionMode"] = "FullCSharp" };

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var runtime = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model, FullCSharp, runtime);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Catalogs()
        {
            yield return new object[] { new Catalog { Title = "Store", Products = new List<Product>
            {
                new Product { Name = "Cheap", Manufacturer = new Manufacturer { Name = "Acme" } },
                new Product { Name = "Pricey", Manufacturer = null },
            } } };
            yield return new object[] { new Catalog { Title = "Empty", Products = new List<Product>() } };
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void ListOverLinqExpression(Catalog model)
        {
            // generated-code.md example 3 shape: an embedded LINQ expression drives @list.
            var t = "@using(){{System.Linq}}@\\\n" +
                    "@model(){{" + CatalogType + "}}@\\\n" +
                    "@list(@model.Products.Where(p => p.Name.Length > 5)){{ <div>@(Name)</div> }}\n";
            AssertParity("views/cs-list.heddle", t, typeof(Catalog), model);
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void UnnamedCSharpOutput(Catalog model)
        {
            // A bare @(@ expr) unnamed output: the verbatim C# value stringifies through EmptyExtension.
            var t = "@model(){{" + CatalogType + "}}@\\\n" +
                    "Count: @(@model.Products.Count)\n";
            AssertParity("views/cs-count.heddle", t, typeof(Catalog), model);
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void CSharpInsideIfCondition(Catalog model)
        {
            var t = "@model(){{" + CatalogType + "}}@\\\n" +
                    "@using(){{System.Linq}}@\\\n" +
                    "@if(@model.Products.Any()){{has @(@model.Products.Count) items}}@else(){{empty}}\n";
            AssertParity("views/cs-if.heddle", t, typeof(Catalog), model);
        }
    }
}
