extern alias generator;
using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly string[] DeclaredMovement =
        {
            // The third-party case the probe exists for: a bodied call to a REFERENCED extension whose InitStart
            // re-types its body against the caller's scope. Nothing in metadata says so, so a non-probing build
            // degrades it under HED7015 and a probing one precompiles it. Its byte parity across the tiers is
            // pinned by ProbedThirdPartyBodiedCallPrecompilesAndRendersIdentically below.
            "Ext_bodied_custom.g.cs"
        };

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

            // The manifest carries every binding row, so a changed render type or bound type shows there even
            // where an entry class did not move. Compared over the corpus MINUS the recovered templates, because a
            // recovered template legitimately gains a whole entry: what has to be identical is everything else.
            var rest = templates.Where(t => !RecoveredKeys.Contains(Path.GetFileName(t.key))).ToList();
            Assert.Equal(
                DifferentialHarness.Generate(rest, globalOptions: null, extraReferences: extra).ManifestSource,
                DifferentialHarness.Generate(rest, globalOptions: ProbingOn(), extraReferences: extra)
                    .ManifestSource);
        }

        /// <summary>The corpus file names behind <see cref="DeclaredMovement"/>.</summary>
        private static readonly HashSet<string> RecoveredKeys =
            new HashSet<string>(new[] { "ext-bodied-custom.heddle" }, StringComparer.Ordinal);

        /// <summary><b>The payoff, end to end.</b> A bodied call to a third-party extension in a REFERENCED
        /// assembly — no attribute, no declaration, no name the build knows — precompiles because the build ran the
        /// extension's own hook and read what it did with the body, and renders the same bytes as the engine.
        /// The tier is asserted, because a fallback is byte-identical by design and a byte comparison alone would
        /// prove nothing.</summary>
        [Fact]
        public void AProbedThirdPartyBodiedCallPrecompilesAndRendersIdentically()
        {
            const string key = "views/bellow.heddle";
            const string template = "<p>@bellow(){{loud}}</p>\n";

            var degraded = DifferentialHarness.Generate(new[] { (key, template) });
            DifferentialHarness.ExpectDegrade(degraded, key);
            Assert.Contains(degraded.Diagnostics, d => d.Id == "HED7015");

            var probed = DifferentialHarness.Generate(new[] { (key, template) }, ProbingOn());
            DifferentialHarness.ExpectPrecompiled(probed, key);
            Assert.DoesNotContain(probed.Diagnostics, d => d.Id == "HED7015");

            var (precompiled, dynamic) = DifferentialHarness.Render(key, template, null, null, ProbingOn());
            Assert.Equal(dynamic, precompiled);
            Assert.Equal("<p>LOUD</p>\n", precompiled);
        }
    }
}
