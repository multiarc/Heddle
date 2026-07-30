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

        /// <summary>
        /// <c>@list</c> over a value the caller wrote as a chain — a function call, or a parenthesized path. The
        /// value that reaches the extension is not the producer's own value: a chain call-parameter is rendered by
        /// the carrier it rides in, so the engine hands <c>@list</c> the <b>text</b> and iterates its characters.
        /// Flattened to the raw producer expression, the generated tier handed <c>@list</c> an <c>int</c> and
        /// enumerated nothing at all — a page that rendered <c>&lt;4&gt;</c> on the engine and empty here, with no
        /// diagnostic anywhere.
        /// <para>The <c>upper</c> row is why this was invisible for so long: a producer that already returns a
        /// string agrees with itself, so only a type-sensitive consumer over a non-string producer can tell the two
        /// tiers apart.</para>
        /// </summary>
        [Theory]
        [InlineData("function-int", "len(Title)", "<2>\n")]
        [InlineData("function-int-literal", "len(\"abcd\")", "<4>\n")]
        [InlineData("function-string", "upper(Title)", "<A><B>\n")]
        [InlineData("paren-int", "(Products.Count)", "<1>\n")]
        [InlineData("paren-string", "(Title)", "<a><b>\n")]
        public void AChainValueReachesListAsTheTextTheCarrierRenders(string name, string value, string expected)
        {
            var t = "@model(){{" + CatalogType + "}}@\\\n@list(" + value + "){{<@()>}}\n";
            var catalog = new Catalog
            {
                Title = "ab",
                Products = new List<Product> { new Product { Name = "p" } }
            };

            var (precompiled, dyn) = DifferentialHarness.Render("views/list-chain-" + name + ".heddle", t,
                typeof(Catalog), catalog);
            Assert.Equal(dyn, precompiled);
            Assert.Equal(expected, dyn);
        }

        /// <summary>
        /// The other half of the same question, one syntax over. A multi-argument call is a native expression, not a
        /// chain, so no carrier renders it and the engine keeps the function's own return type — which makes
        /// <c>@list(min(1, 2))</c> a template it <b>refuses</b>, naming <c>System.Int32</c>. The generated tier had
        /// nothing to say about a call's type, so the enumerability gate exempted every one of them and this
        /// precompiled and rendered.
        /// <para>The second half is the near neighbour: the same syntax over a function that does return something
        /// enumerable still precompiles and still matches, so this is the return type deciding and not calls being
        /// refused wholesale.</para>
        /// </summary>
        [Fact]
        public void ANativeCallsReturnTypeDecidesWhetherListAcceptsIt()
        {
            const string refusedKey = "views/list-call-int.heddle";
            const string refused = "@model(){{" + CatalogType + "}}@\\\n@list(min(1, 2)){{<@()>}}\n";

            var gen = DifferentialHarness.Generate(new[] { (refusedKey, refused) });
            DifferentialHarness.ExpectDegrade(gen, refusedKey);

            var dynamicTemplate = new HeddleTemplate(refused,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(Catalog)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("System.Int32", dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);

            var (precompiled, dyn) = DifferentialHarness.Render("views/list-call-string.heddle",
                "@model(){{" + CatalogType + "}}@\\\n@list(substr(Title, 0, 2)){{<@()>}}\n",
                typeof(Catalog), new Catalog { Title = "abcd" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("<a><b>\n", dyn);
        }

        /// <summary>
        /// The same rule over a return type that is <em>not</em> a primitive. <c>range</c> is the one built-in whose
        /// return type is neither string, bool nor numeric, and it is the built-in a reader reaches for when they
        /// want to iterate — so <c>@list(range(1, 3))</c> is exactly the template the enumerability gate exists to
        /// catch, and it was the one shape the gate could not see. The call was typed through the shared operand
        /// <em>descriptor</em>, which names only the primitives; a <c>Range</c> came back as "some other value type",
        /// which the emitter reads as "cannot say", and every check a call-site value goes through exempted it. So
        /// this precompiled and rendered nothing at all where the engine refuses the template outright.
        /// </summary>
        [Fact]
        public void AListOverRangeIsRefusedByBothTiersBecauseARangeIsNotEnumerable()
        {
            const string key = "views/list-call-range.heddle";
            const string t = "@model(){{" + CatalogType + "}}@\\\n@list(range(1, 3)){{<@()>}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, t) }), key);

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(Catalog)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("Heddle.Models.Range", dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);
        }
    }
}
