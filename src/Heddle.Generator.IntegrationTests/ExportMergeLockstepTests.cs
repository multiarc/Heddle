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
    /// Phase 3 (F2 / OQ2) — the cross-tier gate on export bookkeeping. The gauntlet compares each manifest
    /// <c>FunctionBindings</c> row's overload count against the live registry <b>exactly</b>, failing on both
    /// <c>&gt;</c> and <c>&lt;</c>, so "the generator counted what the runtime registers" is the property that
    /// decides whether a template touching exports ever stays precompiled.
    /// <para><c>shout</c> is exported by two containers here — <c>TemplateFunctions.Shout(string)</c> and
    /// <c>MoreTemplateFunctions.Shout(int)</c> — which is the merge case first-container-wins used to turn into a
    /// permanent <c>FunctionBindingMismatch</c>.</para>
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

        /// <summary>The live registry's (target AQN → overload count) map for one function name.</summary>
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
            // MoreTemplateFunctions.Version is a property; neither tier may count its accessor.
            Assert.Empty(LiveCounts("version"));
            Assert.Empty(LiveCounts("get_version"));
        }

        [Fact]
        public void MergedExportCallRendersIdenticallyOnBothTiers()
        {
            // shout(string) — the overload the shared ranker must pick out of the merged set of two. The argument
            // is a typed model member: with two candidates the ranker governs, and an argument it cannot type
            // degrades (degrade-on-doubt), which is the same posture the built-in path has had since phase 4.
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
            // Both containers appear as recorded targets for the name; the gauntlet rejects a live target absent
            // from that set, which is exactly what first-container-wins produced.
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
