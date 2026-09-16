using System;
using System.IO;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>The stored compiled-form fixture (P4-R3): <c>TestTemplate/compiled-form-v4.bin</c> is real
    /// <c>heddle compile</c> output for <see cref="FixtureText"/> (key <c>fixture.heddle</c>, model
    /// <c>Heddle.Data.TemplateOptions</c>, profile Text, mode Native, directive lines trimmed). It registers
    /// through the marker path a deployed host uses and renders byte-identically to the text compile of the
    /// same fixture on every TFM — a reader, loader or resolver change that cannot serve this file is a red
    /// build, not a silent fallback. The byte round-trip facts over the same file stay in
    /// <c>Heddle.Tool.Tests</c>. Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormFixtureTests : IDisposable
    {
        internal const string FixtureText =
            "Hello @(upper(TemplateName))!\n@if(TrimDirectiveLines){{Shown: @(MaxRecursionCount).}}\n";

        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormFixtureTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        internal static byte[] FixtureBytes()
        {
            var path = Path.Combine(TestCorpusIndex.CorpusDir, "compiled-form-v4.bin");
            Assert.True(File.Exists(path), "Missing TestTemplate/compiled-form-v4.bin beside the corpus.");
            return File.ReadAllBytes(path);
        }

        private static TemplateOptions FixtureOptions() => new TemplateOptions("fixture")
        {
            OutputProfile = OutputProfile.Text,
            ExpressionMode = ExpressionMode.Native,
            TrimDirectiveLines = true,
            EnableFileChangeCheck = false,
            PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict
        };

        [Fact]
        public void StoredFixtureRegistersAndRendersByteIdenticallyToTheTextCompile()
        {
            using (var guard = FallbackGuard.Install())
            {
                CompiledFormHarness.RegisterImage(FixtureBytes(), "HeddleTestAsm_FixtureV4");
                var options = FixtureOptions();
                var report = PrecompiledTemplates.ValidateAll(options);
                Assert.True(report.Failures.Count == 0,
                    "The stored fixture failed the gauntlet: " + CompiledFormHarness.GateDetail(report) + ".");
                PrecompiledTemplateInfo entry;
                Assert.True(PrecompiledTemplates.TryResolve("fixture.heddle", options, out entry) && entry != null,
                    "TryResolve refused fixture.heddle.");
                Assert.Same(typeof(TemplateOptions), entry.ModelType);
                var strategy = entry.GetStrategy(options);
                if (strategy == null)
                {
                    PrecompiledFallbackReason reason;
                    string detail;
                    string fault = entry.TryGetRequestFault(options, out reason, out detail)
                        ? reason + ": " + detail : "<no fault recorded>";
                    Assert.Fail("The stored fixture did not materialize: " + fault + ".");
                }

                var model = new TemplateOptions("fixture-model") { TrimDirectiveLines = true, MaxRecursionCount = 7 };
                string precompiled = CompiledFormHarness.RenderStrategy(strategy, model);

                var text = new HeddleTemplate(FixtureText,
                    new CompileContext(FixtureOptions(), new ExType(typeof(TemplateOptions))));
                Assert.True(text.CompileResult.Success, text.CompileResult.ToString());
                string dynamic = text.Generate(model);
                Assert.Equal(dynamic, precompiled);
                Assert.Contains("Hello FIXTURE-MODEL!", precompiled);
                Assert.Contains("Shown: 7.", precompiled);
                guard.Verify();
            }
        }
    }
}
