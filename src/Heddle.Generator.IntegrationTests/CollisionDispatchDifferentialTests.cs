using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime.Expressions;
using Microsoft.CodeAnalysis;
using Xunit;

[assembly: Heddle.Attributes.ExportFunctions(
    typeof(Heddle.Generator.IntegrationTests.Fixtures.CollisionExports))]

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>Exports colliding with shipped built-in names: <c>Lower(string)</c> is the exact signature of the
    /// built-in <c>lower</c>, so the merged registry replaces the built-in; <c>Floor(string)</c> merely joins the
    /// <c>floor</c> overload set beside its four numeric built-ins. Outputs are distinctive so byte parity proves
    /// which target a call resolved to.</summary>
    public static class CollisionExports
    {
        public static string Lower(string value) => "lowered(" + (value ?? string.Empty) + ")";

        public static string Floor(string value) => "floored(" + (value ?? string.Empty) + ")";
    }
}

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The differential corpus for a name that is both a built-in and an export. The generator replays the
    /// registry's merge — exact-signature exports replace their built-in counterparts, everything else joins one
    /// overload set — and ranks over the merged candidates with the shared core, so the call pre-compiles to
    /// whichever target the engine's own ranker reaches instead of refusing the whole expression.
    /// </summary>
    public class CollisionDispatchDifferentialTests
    {
        private const string OrderType = "Heddle.Generator.IntegrationTests.Fixtures.Order";
        private const string ShimTarget = "Heddle.Runtime.Expressions.BuiltInFunctions, Heddle";
        private const string ContainerAqn =
            "Heddle.Generator.IntegrationTests.Fixtures.CollisionExports, Heddle.Generator.IntegrationTests";

        private static string Template(string expression) =>
            "@model(){{" + OrderType + "}}@\\\nvalue: @(" + expression + ")\n";

        private static TemplateOptions OptionsWithExports()
        {
            var options = new TemplateOptions();
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(CollisionExports).Assembly);
            options.Functions = registry;
            return options;
        }

        private static Order Host() => new Order { Name = "AbC", Count = 3 };

        private static string AssertMatches(string key, string expression)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(Order),
                Host(), runtimeOptions: OptionsWithExports());
            Assert.Equal(dyn, precompiled);
            return dyn;
        }

        [Fact]
        public void AnExactSignatureExportReplacesTheBuiltInOnBothTiers()
        {
            var rendered = AssertMatches("collision/replaced.heddle", "lower(Name)");
            Assert.Equal("value: lowered(AbC)\n", rendered);
        }

        [Fact]
        public void AJoinedNameBindsTheBuiltInForANumericArgument()
        {
            var rendered = AssertMatches("collision/join-builtin.heddle", "floor(Count)");
            Assert.Equal("value: 3\n", rendered);
        }

        [Fact]
        public void AJoinedNameBindsTheExportForAStringArgument()
        {
            var rendered = AssertMatches("collision/join-export.heddle", "floor(Name)");
            Assert.Equal("value: floored(AbC)\n", rendered);
        }

        [Fact]
        public void ACollidedCallInsideALargerExpression_PrecompilesAndMatches()
        {
            var rendered = AssertMatches("collision/in-expression.heddle",
                "\"n=\" + lower(Name) + \"/\" + floor(Count)");
            Assert.Equal("value: n=lowered(AbC)/3\n", rendered);
        }

        /// <summary>The manifest carries one row per live target of the collided name: the shim row with only the
        /// built-in overloads the exports left in place, and the container row — while a name whose sole built-in
        /// overload was replaced records no shim row at all, matching the merged registry the gauntlet compares
        /// each row against exactly.</summary>
        [Fact]
        public void TheManifestRecordsARowPerLiveTargetOfACollidedName()
        {
            const string key = "collision/manifest-rows.heddle";
            const string content = "@model(){{" + OrderType + "}}@\\\n" +
                                   "@(lower(Name)) @(floor(Count)) @(floor(Name))\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var manifest = gen.ManifestSource ?? string.Empty;
            Assert.Contains("PrecompiledFunctionBinding(\"floor\", \"" + ShimTarget + "\", 4)", manifest);
            Assert.Contains("PrecompiledFunctionBinding(\"floor\", \"" + ContainerAqn + "\", 1)", manifest);
            Assert.Contains("PrecompiledFunctionBinding(\"lower\", \"" + ContainerAqn + "\", 1)", manifest);
            Assert.DoesNotContain("PrecompiledFunctionBinding(\"lower\", \"" + ShimTarget + "\"", manifest);
        }

        /// <summary>The recorded rows are the merged registry's exact shape — the comparison the gauntlet runs
        /// per row: <c>lower</c> holds only the replacement, <c>floor</c> its four built-ins plus the join.</summary>
        [Fact]
        public void TheLiveMergedRegistryMatchesTheRecordedRows()
        {
            Assert.Equal(new Dictionary<string, int> { [ContainerAqn] = 1 }, LiveCounts("lower"));
            Assert.Equal(new Dictionary<string, int> { [ShimTarget] = 4, [ContainerAqn] = 1 },
                LiveCounts("floor"));
        }

        /// <summary>An argument no merged candidate takes is the same proof the single-world binders report: the
        /// engine, ranking its merged registry, refuses with the twin runtime id.</summary>
        [Fact]
        public void ACollidedCallNoMergedCandidateTakesIsABuildError()
        {
            const string key = "collision/inapplicable.heddle";
            var content = Template("floor(Approved)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var single = Assert.Single(gen.Diagnostics.Where(
                d => d.Id == HeddleDiagnosticIds.BuildFunctionCallNotBindable));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            var message = single.GetMessage();
            Assert.Contains("'floor'", message);
            Assert.Contains("floor(int)", message);
            Assert.Contains("floor(string)", message);
            Assert.Contains(HeddleDiagnosticIds.NoFunctionOverload, message);
            DifferentialHarness.ExpectDegrade(gen, key);

            var compiled = new HeddleTemplate(content,
                new Runtime.CompileContext(OptionsWithExports(), typeof(Order)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains(compiled.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.NoFunctionOverload);
        }

        /// <summary>Live registry overload counts by target AQN for one function name.</summary>
        private static Dictionary<string, int> LiveCounts(string functionName)
        {
            var registry = OptionsWithExports().Functions;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in registry.GetOverloads(functionName))
            {
                var aqn = Heddle.Precompiled.PrecompiledGauntlet.AqnSansVersion(entry.Method.DeclaringType);
                counts[aqn] = counts.TryGetValue(aqn, out var c) ? c + 1 : 1;
            }

            return counts;
        }
    }
}
