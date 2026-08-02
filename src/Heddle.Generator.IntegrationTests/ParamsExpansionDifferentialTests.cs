using Heddle.Data;
using Heddle.Generator.IntegrationTests.ParamsBinding;
using Heddle.Runtime.Expressions;
using Xunit;

[assembly: Heddle.Attributes.ExportFunctions(
    typeof(Heddle.Generator.IntegrationTests.ParamsBinding.ParamsExports))]

namespace Heddle.Generator.IntegrationTests.ParamsBinding
{
    public sealed class TagModel
    {
        public string Name { get; set; }

        public string[] Tags { get; set; }

        public int Count { get; set; }
    }

    /// <summary>Exports whose binds exercise the params-expanded tier. Outputs are distinctive so byte parity
    /// proves which overload — and which bind form — a call resolved to; <c>Splice</c> distinguishes a null array
    /// from an empty one because normal-form <c>null</c> and a zero-argument expansion are different binds.</summary>
    public static class ParamsExports
    {
        public static string Splice(string head, params string[] parts) =>
            head + "[" + (parts == null ? "null" : string.Join("|", parts)) + "]";

        public static string Pick(int value) => "fixed:" + value;

        public static string Pick(params int[] values) => "params:" + string.Join(",", values);

        /// <summary>Collides with the built-in <c>ceil</c>: joins its four numeric overloads, and only the
        /// expanded tier of this signature can take a string call.</summary>
        public static string Ceil(string value, params string[] extras) =>
            "ceiled(" + value + "|" + string.Join(",", extras) + ")";
    }
}

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The differential corpus for calls the shared ranker binds on the params-expanded tier. The emitted call
    /// spells the expansion itself — the fixed arguments cast-pinned as usual, then one explicitly typed array
    /// creation holding the expanded tail — so the consumer's compiler binds the engine's winner in normal form
    /// instead of re-running its own params expansion. The engine prefers normal form whenever any candidate is
    /// applicable there (an argument already typed as the array, a bare <c>null</c>), and its expanded tier accepts
    /// zero expanded arguments, building an empty array; every lane pins that the emission agrees.
    /// </summary>
    public class ParamsExpansionDifferentialTests
    {
        private const string ModelType = "Heddle.Generator.IntegrationTests.ParamsBinding.TagModel";

        private static string Template(string expression) =>
            "@model(){{" + ModelType + "}}@\\\nvalue: @(" + expression + ")\n";

        private static TemplateOptions OptionsWithExports()
        {
            var options = new TemplateOptions();
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(ParamsExports).Assembly);
            options.Functions = registry;
            return options;
        }

        private static TagModel Host() => new TagModel { Name = "n", Tags = new[] { "x", "y" }, Count = 3 };

        private static string AssertMatches(string key, string expression)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(TagModel),
                Host(), runtimeOptions: OptionsWithExports());
            Assert.Equal(dyn, precompiled);
            return dyn;
        }

        [Fact]
        public void AFixedAndParamsMixBindsExpandedAndMatches()
        {
            var rendered = AssertMatches("params/expanded-mix.heddle", "splice(Name, \"a\", \"b\")");
            Assert.Equal("value: n[a|b]\n", rendered);
        }

        /// <summary>The engine's expanded tier accepts a call with only the fixed arguments and builds an empty
        /// array; the emission spells that same empty array creation.</summary>
        [Fact]
        public void AZeroArgumentExpansionBindsTheEmptyArrayOnBothTiers()
        {
            var rendered = AssertMatches("params/expanded-empty.heddle", "splice(Name)");
            Assert.Equal("value: n[]\n", rendered);
        }

        /// <summary>An argument already typed as the params array is an exact normal-form match, and normal form
        /// wins before the expanded tier is ever ranked — on both tiers the array passes through whole.</summary>
        [Fact]
        public void AnArgumentAlreadyTypedAsTheArrayBindsNormalFormOnBothTiers()
        {
            var rendered = AssertMatches("params/normal-array.heddle", "splice(Name, Tags)");
            Assert.Equal("value: n[x|y]\n", rendered);
        }

        /// <summary>A bare <c>null</c> converts to the array parameter itself, so normal form binds and the
        /// function receives a null array — not a one-element expansion — on both tiers.</summary>
        [Fact]
        public void ANullArgumentBindsNormalFormAsTheNullArrayOnBothTiers()
        {
            var rendered = AssertMatches("params/normal-null.heddle", "splice(Name, null)");
            Assert.Equal("value: n[null]\n", rendered);
        }

        [Fact]
        public void AFixedArityOverloadBeatsTheParamsExpansionOnBothTiers()
        {
            var rendered = AssertMatches("params/fixed-wins.heddle", "pick(Count)");
            Assert.Equal("value: fixed:3\n", rendered);
        }

        [Fact]
        public void ACallWithNoFixedArgumentsExpandsTheWholeList()
        {
            var rendered = AssertMatches("params/all-expanded.heddle", "pick(Count, Count)");
            Assert.Equal("value: params:3,3\n", rendered);
        }

        [Fact]
        public void AParamsExpansionInsideALargerExpressionPrecompilesAndMatches()
        {
            var rendered = AssertMatches("params/in-expression.heddle",
                "\"x=\" + splice(Name, \"a\", upper(Name)) + \"!\"");
            Assert.Equal("value: x=n[a|N]!\n", rendered);
        }

        /// <summary>A collided name whose merged winner is a params-expanded export: the built-in <c>ceil</c>
        /// overloads take no string, so only the export's expanded tier binds — the merged binder emits the same
        /// explicit array spelling the pure-export path does.</summary>
        [Fact]
        public void ACollisionWinnerOnTheParamsExpandedTierPrecompilesAndMatches()
        {
            var rendered = AssertMatches("params/collision-expanded.heddle", "ceil(Name, \"x\", \"y\")");
            Assert.Equal("value: ceiled(n|x,y)\n", rendered);
        }

        [Fact]
        public void ACollisionWinnerWithZeroExpandedArgumentsPrecompilesAndMatches()
        {
            var rendered = AssertMatches("params/collision-empty.heddle", "ceil(Name)");
            Assert.Equal("value: ceiled(n|)\n", rendered);
        }
    }
}
