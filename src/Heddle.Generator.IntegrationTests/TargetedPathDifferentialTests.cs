using System;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Microsoft.CodeAnalysis;
using Xunit;

[assembly: Heddle.Attributes.ExportFunctions(
    typeof(Heddle.Generator.IntegrationTests.Fixtures.TargetedPathExports))]

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>Exports for the targeted-path differential corpus: a reference return walked further, one whose
    /// <see cref="Manufacturer.Address"/> stays null, a null return, a struct return, and a counting return
    /// proving the target is evaluated once per render on either tier.</summary>
    public static class TargetedPathExports
    {
        public static int Calls;

        public static Manufacturer MakerOf(string name) =>
            new Manufacturer { Name = name, Address = new Address { City = name + "ville" } };

        public static Manufacturer LostMaker(string name) => new Manufacturer { Name = name };

        public static Manufacturer Nobody() => null;

        public static Money PriceOf(int cents) => new Money(cents);

        public static Manufacturer Counted(string name)
        {
            Calls++;
            return new Manufacturer { Name = name };
        }
    }
}

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The differential corpus for member paths rooted at a target expression — a call, an index access, a
    /// literal. The engine evaluates the target once, boxes it to <c>object</c>, and walks the members null-safely
    /// off the target's static type; the generator emits the target cast to that type with the shared hop chain,
    /// which reads the same value. A target whose static type the writer cannot establish — a path off a grouped
    /// binary or ternary result — stays a degrade the engine renders through.
    /// </summary>
    public class TargetedPathDifferentialTests
    {
        private const string OrderType = "Heddle.Generator.IntegrationTests.Fixtures.Order";
        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";

        private static string Template(string modelType, string expression) =>
            "@model(){{" + modelType + "}}@\\\nvalue: @(" + expression + ")\n";

        private static TemplateOptions OptionsWithExports()
        {
            var options = new TemplateOptions();
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(TargetedPathExports).Assembly);
            options.Functions = registry;
            return options;
        }

        private static Order Host() => new Order { Name = "hi", Count = 3 };

        private static Catalog Shelf() => new Catalog
        {
            Title = "t",
            Products = new System.Collections.Generic.List<Product>
            {
                new Product { Name = "widget" }
            },
            Tags = new[] { "ab", "c" },
        };

        private static void AssertMatches(string key, string modelType, string expression, Type clrType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, Template(modelType, expression), clrType,
                model, runtimeOptions: OptionsWithExports());
            Assert.Equal(dyn, precompiled);
        }

        private static void AssertDegrades(string key, string modelType, string expression)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, Template(modelType, expression)) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.Empty(gen.TemplateSources);
        }

        [Fact]
        public void CallRootedPaths_PrecompileAndMatch()
        {
            foreach (var (key, expression) in new[]
            {
                ("targeted/call-single.heddle", "upper(Name).Length"),
                ("targeted/call-multi.heddle", "makerof(Name).Address.City"),
                ("targeted/call-struct.heddle", "priceof(Count).Amount"),
            })
                AssertMatches(key, OrderType, expression, typeof(Order), Host());
        }

        [Fact]
        public void IndexRootedPaths_PrecompileAndMatch()
        {
            foreach (var (key, expression) in new[]
            {
                ("targeted/index-name.heddle", "Products[0].Name"),
                ("targeted/index-length.heddle", "Tags[0].Length"),
            })
                AssertMatches(key, CatalogType, expression, typeof(Catalog), Shelf());
        }

        [Fact]
        public void StringLiteralRootedPath_PrecompilesAndMatches()
        {
            AssertMatches("targeted/literal-length.heddle", OrderType, "\"abc\".Length", typeof(Order), Host());
        }

        /// <summary>A null hop inside the walked chain: the engine substitutes a default at the hop that failed
        /// and keeps walking, so a null <c>Address</c> (or <c>Manufacturer</c>) yields the default of the final
        /// member rather than a NullReferenceException.</summary>
        [Fact]
        public void NullIntermediateInTheWalkedChain_YieldsTheDefaultOnBothTiers()
        {
            AssertMatches("targeted/call-null-hop.heddle", OrderType, "lostmaker(Name).Address.City",
                typeof(Order), Host());
            AssertMatches("targeted/index-null-hop.heddle", CatalogType, "Products[0].Manufacturer.Name",
                typeof(Catalog), Shelf());
        }

        /// <summary>The target itself is null: the walk yields the chain's default on both tiers, including a
        /// non-nullable value ending, which reads its default off the defaulted reference hop.</summary>
        [Fact]
        public void NullTarget_YieldsTheDefaultOnBothTiers()
        {
            AssertMatches("targeted/null-target.heddle", OrderType, "nobody().Name", typeof(Order), Host());
            AssertMatches("targeted/null-target-value-end.heddle", OrderType, "nobody().Name.Length",
                typeof(Order), Host());
        }

        [Fact]
        public void TargetedPathInsideALargerExpression_PrecompilesAndMatches()
        {
            foreach (var (key, expression) in new[]
            {
                ("targeted/in-concat.heddle", "\"n=\" + upper(Name).Length"),
                ("targeted/in-ternary.heddle", "\"abc\".Length > 2 ? upper(Name) : Name"),
            })
                AssertMatches(key, OrderType, expression, typeof(Order), Host());

            AssertMatches("targeted/in-call.heddle", CatalogType, "len(Products[0].Name)",
                typeof(Catalog), Shelf());
        }

        /// <summary>Both tiers charge the model exactly one evaluation of the target per render — the engine binds
        /// it to a local, the emitted hop forms splice the root text once.</summary>
        [Fact]
        public void TheTargetIsEvaluatedOnce()
        {
            TargetedPathExports.Calls = 0;
            AssertMatches("targeted/target-once.heddle", OrderType, "counted(Name).Name.Length",
                typeof(Order), Host());
            Assert.Equal(2, TargetedPathExports.Calls);
        }

        /// <summary>A grouped binary result has an operand descriptor but no symbol behind it, so the path off it
        /// stays a silent degrade — and the engine, which types the concat statically, still renders it.</summary>
        [Fact]
        public void GroupedBinaryRootedPath_DegradesButTheEngineRenders()
        {
            AssertDegrades("targeted/grouped-binary.heddle", OrderType, "(\"a\" + Name).Length");

            var template = new HeddleTemplate(Template(OrderType, "(\"a\" + Name).Length"),
                new CompileContext(new TemplateOptions(), typeof(Order)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Equal("value: 3\n", template.Generate(Host()));
        }
    }
}
