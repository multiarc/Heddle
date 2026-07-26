using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The guard proves itself. A guardrail that has never been observed to fire is untested infrastructure, so this
    /// meta-suite <b>seeds</b> a manifest mismatch into otherwise-real generator output and pins what each mode does:
    /// <list type="bullet">
    /// <item><see cref="PrecompiledMismatchPolicy.Strict"/> throws <see cref="PrecompiledMismatchException"/> carrying
    /// the right <see cref="PrecompiledFallbackReason"/>;</item>
    /// <item>sentinel-only (default <c>Fallback</c> policy + <see cref="FallbackGuard"/>) fails at <c>Verify</c>;</item>
    /// <item>the negative control — the same seeded mismatch under an <b>unguarded</b> render silently succeeds, byte-identical
    /// to the precompiled output. This assertion is the executable statement of the threat model: it is the pre-detection world,
    /// and it is exactly how similar drifts shipped unnoticed.</item>
    /// </list>
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class SeededMismatchMetaTests : PrecompiledRegistryTestBase
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";
        private const string Key = "phase0-seeded.heddle";

        private const string Template =
            "@model(){{" + CartType + "}}@\\\n" +
            "Cart @(Name): count @(Count).\n" +
            "@if(IsFeatured){{ FEATURED }}\n";

        private static readonly IReadOnlyList<(string key, string content)> Corpus =
            new[] { (Key, Template) };

        private static Cart Model() => new Cart { Name = "Basket", Count = 3, IsFeatured = true };

        // The seeds. Each asserts it actually changed the manifest, so a generator change that renames the row cannot
        // silently turn this meta-suite into a no-op that "passes".
        private static string SeedWrongContentHash(string manifest) =>
            Replace(manifest, "contentHash: \"", "\"", new string('0', 64), "contentHash");

        private static string SeedWrongExtensionAqn(string manifest) =>
            Replace(manifest, "\"Heddle.Extensions.IfExtension, Heddle\"", null,
                "\"Heddle.Extensions.NotTheIfExtension, Heddle\"", "if-extension AQN");

        private static string SeedWrongOptionsFingerprint(string manifest) =>
            Replace(manifest, "OutputProfile.Html", null, "OutputProfile.Text", "options fingerprint profile");

        private static string Replace(string manifest, string prefix, string terminator, string replacement,
            string what)
        {
            int at = manifest.IndexOf(prefix, StringComparison.Ordinal);
            Assert.True(at >= 0, "Seed target not found in the manifest: " + what);
            if (terminator == null)
                return manifest.Substring(0, at) + replacement + manifest.Substring(at + prefix.Length);
            int start = at + prefix.Length;
            int end = manifest.IndexOf(terminator, start, StringComparison.Ordinal);
            Assert.True(end > start, "Seed target not terminated in the manifest: " + what);
            var seeded = manifest.Substring(0, start) + replacement + manifest.Substring(end);
            Assert.NotEqual(manifest, seeded);
            return seeded;
        }

        private static DifferentialHarness.GenResult Register(Func<string, string> seed)
        {
            var gen = DifferentialHarness.Generate(Corpus, globalOptions: null, extraReferences: null,
                rewriteManifest: seed);
            Assert.NotNull(gen.Assembly);
            PrecompiledTemplates.Register(gen.Assembly);
            return gen;
        }

        /// <summary>Resolves and renders <see cref="Key"/> through a real <see cref="TemplateResolver"/> rooted at
        /// <paramref name="stageDir"/> under the caller's policy. Mirrors what the guarded harness does, minus the
        /// guards the caller wants to leave off.</summary>
        private static string RenderThroughResolver(string stageDir, PrecompiledMismatchPolicy policy,
            bool fileBacked)
        {
            var options = new TemplateOptions
            {
                RootPath = stageDir + Path.DirectorySeparatorChar,
                FileNamePostfix = ".heddle",
                EnableFileChangeCheck = fileBacked,
                PrecompiledMismatchPolicy = policy,
            };
            var resolver = new TemplateResolver(Path.Combine(stageDir, "root.marker"), fileBacked);
            var template = resolver.GetTemplate(Key, string.Empty, out _,
                new CompileContext(options, typeof(Cart)), TemplatePathType.None);
            Assert.NotNull(template);
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(Model());
        }

        // Seeded content-hash mismatch. Only reachable under EnableFileChangeCheck (file-backed).

        [Fact]
        public void SeededHashMismatch_Strict_ThrowsStaleContent()
        {
            Register(SeedWrongContentHash);
            var stageDir = DifferentialHarness.StageCorpus(Corpus);
            try
            {
                var ex = Assert.Throws<PrecompiledMismatchException>(() =>
                    RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Strict, fileBacked: true));
                Assert.Equal(PrecompiledFallbackReason.StaleContent, ex.Reason);
                Assert.Equal(Key, ex.Key);
                Assert.Contains("hash mismatch", ex.Detail);
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(stageDir);
            }
        }

        [Fact]
        public void SeededHashMismatch_SentinelOnly_FailsViaFallbackGuard()
        {
            Register(SeedWrongContentHash);
            var stageDir = DifferentialHarness.StageCorpus(Corpus);
            try
            {
                using var guard = FallbackGuard.Install();
                // Default Fallback policy: TryResolve returns false and the render quietly degrades to the dynamic
                // tier. Nothing throws — the sentinel is the only thing that notices.
                var rendered = RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback, fileBacked: true);
                Assert.Contains("FEATURED", rendered);
                var ex = Assert.Throws<FallbackGuardException>(() => guard.Verify());
                Assert.Contains("StaleContent", ex.Message);
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(stageDir);
            }
        }

        /// <summary>
        /// The negative control, and the reason this phase exists: with neither guard on, the very same seeded
        /// hash mismatch degrades to the dynamic tier and renders <b>byte-identical</b> output. No exception, no
        /// failed assertion, no test anywhere would have caught it.
        /// </summary>
        [Fact]
        public void SeededHashMismatch_Unguarded_SilentlyRendersIdenticalBytes()
        {
            Register(SeedWrongContentHash);
            var stageDir = DifferentialHarness.StageCorpus(Corpus);
            try
            {
                // Reference: the precompiled tier's own bytes, taken with the staleness check off so the gauntlet
                // passes and the adapter serves the render.
                var precompiled = RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback,
                    fileBacked: false);
                var degraded = RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback, fileBacked: true);
                Assert.Equal(precompiled, degraded);
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(stageDir);
            }
        }

        // Seeded extension-AQN mismatch. Checked at gauntlet step 2, before staleness, so it fires in registry-only mode too.

        [Fact]
        public void SeededExtensionAqnMismatch_Strict_ThrowsExtensionBindingMismatch()
        {
            Register(SeedWrongExtensionAqn);
            var stageDir = DifferentialHarness.NonexistentRoot();
            var ex = Assert.Throws<PrecompiledMismatchException>(() =>
                RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Strict, fileBacked: false));
            Assert.Equal(PrecompiledFallbackReason.ExtensionBindingMismatch, ex.Reason);
            Assert.Contains("NotTheIfExtension", ex.Detail);
        }

        [Fact]
        public void SeededExtensionAqnMismatch_SentinelOnly_FailsViaFallbackGuard()
        {
            Register(SeedWrongExtensionAqn);
            var stageDir = DifferentialHarness.StageCorpus(Corpus);
            try
            {
                using var guard = FallbackGuard.Install();
                RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback, fileBacked: false);
                var ex = Assert.Throws<FallbackGuardException>(() => guard.Verify());
                Assert.Contains("ExtensionBindingMismatch", ex.Message);
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(stageDir);
            }
        }

        [Fact]
        public void SeededExtensionAqnMismatch_Unguarded_SilentlyRendersIdenticalBytes()
        {
            var stageDir = DifferentialHarness.StageCorpus(Corpus);
            try
            {
                // The clean registration's precompiled bytes...
                Register(null);
                var precompiled = RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback,
                    fileBacked: false);

                // ...are indistinguishable from the seeded registration's silently-degraded dynamic bytes.
                PrecompiledTemplates.ResetForTests();
                Register(SeedWrongExtensionAqn);
                var degraded = RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback, fileBacked: false);
                Assert.Equal(precompiled, degraded);
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(stageDir);
            }
        }

        // Seeded options-fingerprint mismatch. Gauntlet step 1, the earliest check, and the third distinct failure class.
        // A literal arity change would not compile the manifest, so what is seeded is a wrong fingerprint *value* — the
        // observable form of the same condition, and the one a real options drift would take.

        [Fact]
        public void SeededOptionsFingerprintMismatch_Strict_ThrowsOptionsMismatch()
        {
            Register(SeedWrongOptionsFingerprint);
            var stageDir = DifferentialHarness.NonexistentRoot();
            var ex = Assert.Throws<PrecompiledMismatchException>(() =>
                RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Strict, fileBacked: false));
            Assert.Equal(PrecompiledFallbackReason.OptionsMismatch, ex.Reason);
            Assert.Contains("OutputProfile", ex.Detail);
        }

        [Fact]
        public void SeededOptionsFingerprintMismatch_SentinelOnly_FailsViaFallbackGuard()
        {
            Register(SeedWrongOptionsFingerprint);
            var stageDir = DifferentialHarness.StageCorpus(Corpus);
            try
            {
                using var guard = FallbackGuard.Install();
                RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback, fileBacked: false);
                var ex = Assert.Throws<FallbackGuardException>(() => guard.Verify());
                Assert.Contains("OptionsMismatch", ex.Message);
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(stageDir);
            }
        }

        [Fact]
        public void SeededOptionsFingerprintMismatch_Unguarded_SilentlyRendersIdenticalBytes()
        {
            var stageDir = DifferentialHarness.StageCorpus(Corpus);
            try
            {
                Register(null);
                var precompiled = RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback,
                    fileBacked: false);

                PrecompiledTemplates.ResetForTests();
                Register(SeedWrongOptionsFingerprint);
                var degraded = RenderThroughResolver(stageDir, PrecompiledMismatchPolicy.Fallback,
                    fileBacked: false);
                Assert.Equal(precompiled, degraded);
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(stageDir);
            }
        }
    }
}
