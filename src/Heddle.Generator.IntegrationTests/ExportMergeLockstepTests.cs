using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The cross-tier gate on export bookkeeping: manifest <c>FunctionBindings</c> row counts must match the live registry exactly.
    /// <c>shout</c> merged from two containers guards the first-container-wins fix for what used to be permanent <c>FunctionBindingMismatch</c>.
    /// </summary>
    public class ExportMergeLockstepTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        private static TemplateOptions OptionsWithExports()
        {
            var options = new TemplateOptions();
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(TemplateFunctions).Assembly);
            options.Functions = registry;
            return options;
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

        [Fact]
        public void MergedNameRegistersOverloadsFromBothContainers()
        {
            var live = LiveCounts("shout");
            Assert.Equal(2, live.Count);
            Assert.Equal(1, live["Heddle.Generator.IntegrationTests.Fixtures.TemplateFunctions, " +
                                 "Heddle.Generator.IntegrationTests"]);
            Assert.Equal(1, live["Heddle.Generator.IntegrationTests.Fixtures.MoreTemplateFunctions, " +
                                 "Heddle.Generator.IntegrationTests"]);
        }

        [Fact]
        public void PropertyAccessorsAreNotFunctionsOnEitherTier()
        {
            Assert.Empty(LiveCounts("version"));
            Assert.Empty(LiveCounts("get_version"));
        }

        [Fact]
        public void MergedExportCallRendersIdenticallyOnBothTiers()
        {
            // Ranker must pick shout(string) from the merged set of two when argument is a typed model member.
            const string key = "views/merged-export.heddle";
            const string content = "@model(){{" + ProductType + "}}@\\\n<span>@(shout(Name))</span>\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Product),
                new Product { Name = "hi" }, runtimeOptions: OptionsWithExports());
            Assert.Equal(dyn, precompiled);
            Assert.Contains("HI!", dyn);
        }

        [Fact]
        public void MergedExportManifestRecordsARowPerContainer()
        {
            const string key = "views/merged-export-rows.heddle";
            const string content = "@model(){{" + ProductType + "}}@\\\n<span>@(shout(Name))</span>\n";

            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var manifest = gen.ManifestSource ?? string.Empty;
            // Manifest must record both containers; gauntlet rejects a live target absent from the set.
            Assert.Contains("Heddle.Generator.IntegrationTests.Fixtures.TemplateFunctions, " +
                            "Heddle.Generator.IntegrationTests", manifest);
            Assert.Contains("Heddle.Generator.IntegrationTests.Fixtures.MoreTemplateFunctions, " +
                            "Heddle.Generator.IntegrationTests", manifest);
        }

        [Fact]
        public void SecondContainersExclusiveFunctionAlsoBinds()
        {
            const string key = "views/second-container-export.heddle";
            const string content = "@model(){{" + ProductType + "}}@\\\n<span>@(twice(Name))</span>\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Product),
                new Product { Name = "ab" }, runtimeOptions: OptionsWithExports());
            Assert.Equal(dyn, precompiled);
            Assert.Contains("abab", dyn);
        }
    }
}
