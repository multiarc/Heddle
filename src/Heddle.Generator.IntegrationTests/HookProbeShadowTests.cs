extern alias generator;
using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using ConfigReader = generator::Heddle.Generator.Pipeline.ConfigReader;
using ExtensionBinder = generator::Heddle.Generator.Emit.ExtensionBinder;
using HookOracle = generator::Heddle.Generator.Probe.HookOracle;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// <b>The safety gate on hook probing.</b> Turning the probe on changes where the emitter's body typing comes
    /// from, so before any coverage is claimed from it the question is whether it changes anything it should not.
    /// Two halves, and both have to hold.
    /// <list type="number">
    /// <item><description>The probe agrees with every row <c>BodyModelRules</c> pins — asserted for the build-side
    /// reflection driver in <c>Heddle.Generator.Tests</c>, and for the engine-side driver against every registered
    /// extension in <c>Heddle.Tests</c>.</description></item>
    /// <item><description><b>No template that precompiles today changes a byte</b> when probing is on. That is this
    /// file. A template whose bytes move is either a coverage gain that has to be declared, or a divergence.</description></item>
    /// </list>
    /// <para>The declared-movement list below is the whole point: it is empty until an item deliberately makes it
    /// non-empty, and every name in it names a template the probe recovers. A byte that moves without being
    /// declared here is a defect, not a fix.</para>
    /// </summary>
    public class HookProbeShadowTests
    {
        private static Dictionary<string, string> ProbingOn() => new Dictionary<string, string>
        {
            ["build_property.HeddleProbeExtensionHooks"] = "true"
        };

        /// <summary>Templates whose generated source is allowed to differ between a probing and a non-probing
        /// build, each because the probe recovers a call site the emitter otherwise refuses. Names are the
        /// generated hint names' template keys.</summary>
        private static readonly string[] DeclaredMovement = new string[0];

        /// <summary><b>The gate is not vacuous.</b> Asserted first and separately, because everything below it
        /// would pass trivially against a build where probing never engaged: with the property set, this
        /// compilation's engine reference really does yield a live probe, and that probe really does answer for a
        /// built-in.</summary>
        [Fact]
        public void TheProbeIsLiveForThisCompilation()
        {
            var oracle = OracleFor(new Dictionary<string, string>
            {
                [Heddle.Precompiled.HeddleBuildOptions.BuildPropertyPrefix +
                 Heddle.Precompiled.HeddleBuildOptions.ProbeExtensionHooksProperty] = "true"
            });

            Assert.True(oracle.Enabled, "The hook probe did not engage, so every byte comparison below is vacuous.");
            Assert.True(oracle.TryGet("list", "Heddle.Extensions.ListExtension", "Heddle", out var list));
            Assert.Equal(generator::Heddle.Language.BodyModelSource.ElementOfData, list.Body);
            Assert.Equal(generator::Heddle.Language.ChainedModelSource.Int32Index, list.Chained);
        }

        /// <summary>And off by default: an unset property engages nothing at all, which is what makes every other
        /// suite in this repository a non-probing build.</summary>
        [Fact]
        public void TheProbeIsOffUnlessTheBuildAsksForIt()
        {
            Assert.False(OracleFor(new Dictionary<string, string>()).Enabled);
        }

        private static HookOracle OracleFor(Dictionary<string, string> globalOptions)
        {
            var compilation = CSharpCompilation.Create("HeddleProbeShadow",
                Array.Empty<SyntaxTree>(), DifferentialHarness.BaseReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var config = ConfigReader.Read(new HarnessOptions(globalOptions),
                new List<ConfigReader.OptionError>());
            return HookOracle.For(compilation, config, ExtensionBinder.Build(compilation).ExtensionAssemblies);
        }

        private sealed class HarnessOptions : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _values;
            public HarnessOptions(Dictionary<string, string> values) => _values = values;
            public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
        }

        /// <summary>The gate. Both builds see the same templates, the same references and the same options but for
        /// the one property; every generated source is compared byte for byte.</summary>
        [Fact]
        public void ProbingChangesNoByteOfATemplateItDoesNotRecover()
        {
            var templates = TestCorpusIndex.Load();
            var extra = DifferentialHarness.EngineTestModelReferences();

            var off = DifferentialHarness.Generate(templates, globalOptions: null, extraReferences: extra);
            var on = DifferentialHarness.Generate(templates, globalOptions: ProbingOn(), extraReferences: extra);

            var moved = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var name in off.TemplateSources.Keys.Union(on.TemplateSources.Keys))
            {
                off.TemplateSources.TryGetValue(name, out var a);
                on.TemplateSources.TryGetValue(name, out var b);
                if (!string.Equals(a, b, StringComparison.Ordinal))
                    moved.Add(name);
            }

            Assert.True(moved.SetEquals(DeclaredMovement),
                "Hook probing moved bytes in templates that are not declared as recovered by it: " +
                string.Join(", ", moved.Except(DeclaredMovement)) + "; and declared-but-unmoved: " +
                string.Join(", ", DeclaredMovement.Except(moved)));

            // The manifest is the second output and carries every binding row, so a changed render type or a
            // changed bound type would show here even where the entry class did not move.
            if (moved.Count == 0)
                Assert.Equal(off.ManifestSource, on.ManifestSource);
        }
    }
}
