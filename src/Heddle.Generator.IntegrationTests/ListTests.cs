using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The <c>@list</c> family: <c>ListExtension</c> bound with an element body on
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

        /// <summary>
        /// <c>@list</c> over a value that is not enumerable. The extension declares the type it accepts and the
        /// engine checks the value against it before it compiles the call, so these are templates the engine
        /// <b>refuses</b> — and the generated tier used to precompile them and render: the <c>object</c> row walked
        /// a string's characters, the <c>int</c> row rendered nothing at all.
        /// <para>The check is on the value's <em>static</em> type, so a collection whose elements happen to be
        /// <c>object</c> is unaffected — that is the pair below, and it is what keeps this a type rule rather than
        /// a refusal of anything the emitter finds hard.</para>
        /// </summary>
        [Theory]
        [InlineData("object-member", "Heddle.Generator.IntegrationTests.Fixtures.OverloadPayload", "Payload")]
        [InlineData("int-member", "Heddle.Generator.IntegrationTests.Fixtures.OverloadPayload", "Count")]
        [InlineData("bool-expression", "Heddle.Generator.IntegrationTests.Fixtures.OverloadPayload", "Count > 1")]
        [InlineData("whole-model", CatalogType, "this")]
        [InlineData("implicit-model", CatalogType, "")]
        public void ANonEnumerableValueIsRefusedByBothTiers(string name, string modelType, string value)
        {
            var key = "views/list-nonenumerable-" + name + ".heddle";
            var t = "@model(){{" + modelType + "}}@\\\n@list(" + value + "){{[@()]}}\n";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectDegrade(gen, key);

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(),
                    new Heddle.Data.ExType(Type.GetType(modelType + ", Heddle.Generator.IntegrationTests"))));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("System.Collections.IEnumerable", dynamicTemplate.CompileResult.ToString(),
                StringComparison.Ordinal);
        }

        /// <summary>The near neighbours: every enumerable shape still precompiles and still matches. An array is
        /// here because it reaches <c>IEnumerable</c> through <c>System.Array</c> rather than its own interface
        /// list, and a <c>List&lt;object&gt;</c> because an element type with no members of its own is still a
        /// static type — the engine types the body <c>object</c> and enumerates it happily.</summary>
        [Fact]
        public void EnumerableValuesStillPrecompile()
        {
            var catalog = new Catalog
            {
                Title = "ab",
                Tags = new[] { "x", "y" },
                Products = new List<Product> { new Product { Name = "p" } }
            };
            AssertParity("views/list-array.heddle",
                "@model(){{" + CatalogType + "}}@\\\n@list(Tags){{[@()]}}\n", typeof(Catalog), catalog);
            AssertParity("views/list-string.heddle",
                "@model(){{" + CatalogType + "}}@\\\n@list(Title){{[@()]}}\n", typeof(Catalog), catalog);
            AssertParity("views/list-generic.heddle",
                "@model(){{" + CatalogType + "}}@\\\n@list(Products){{[@(Name)]}}\n", typeof(Catalog), catalog);

            const string objItemsType = "Heddle.Generator.IntegrationTests.Fixtures.ObjItems";
            AssertParity("views/list-object-elements.heddle",
                "@model(){{" + objItemsType + "}}@\\\n@list(Items){{[@()]}}\n", typeof(ObjItems),
                new ObjItems { Items = new List<object> { 1, "two" } });
        }
    }
}
