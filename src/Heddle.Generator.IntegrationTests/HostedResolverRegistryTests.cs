using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The <c>View</c>/<c>PartialView</c>/<c>Master</c> arms consult the precompiled registry, which previously only
    /// the <see cref="TemplatePathType.None"/> arm did — hosted MVC-style lookups
    /// bypassed precompilation entirely no matter how well the keys agreed. The ladder is three tiers now:
    /// registry, then cache, then disk, each in location order.
    /// <para>Every hit is asserted to be the precompiled <i>adapter</i>, not a dynamically-compiled twin: the two
    /// tiers render byte-identically by contract, so output equality alone is not evidence about the tier.</para>
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class HostedResolverRegistryTests : PrecompiledRegistryTestBase
    {
        private const string Content = "@model(){{System.String}}@\\\nhosted @(this)\n";

        private static readonly Dictionary<string, string> FullCSharpBuild = new Dictionary<string, string>
        {
            ["build_property.HeddleExpressionMode"] = "FullCSharp"
        };

        /// <summary>Builds under FullCSharp; Native-built manifests are refused by the fingerprint step.</summary>
        private static void Register(string key, Dictionary<string, string> buildOptions = null)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, Content) }, buildOptions ?? FullCSharpBuild);
            DifferentialHarness.ExpectPrecompiled(gen, key);
            PrecompiledTemplates.Register(gen.Assembly);
        }

        private static TemplateResolver ResolverOver(string rootDirectory) =>
            new TemplateResolver(Path.Combine(rootDirectory, "root.marker"), false);

        private static string NonexistentRoot() =>
            Path.Combine(Path.GetTempPath(), "heddle-hosted-" + Guid.NewGuid().ToString("N"));

        private static void AssertPrecompiledAdapter(HeddleTemplate template, string key)
        {
            Assert.NotNull(template);
            var field = typeof(HeddleTemplate).GetField("_precompiled",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            Assert.True((bool)field.GetValue(template),
                "Resolver served a dynamically-compiled template for '" + key + "' — the registry was not consulted.");
        }

        private static string DynamicReference() =>
            new HeddleTemplate(Content,
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp },
                    typeof(string))).Generate("hi");

        [Fact]
        public void ViewArmServesTheRegisteredEntryThroughTheSearchLadder()
        {
            const string key = "views/home/index.heddle";
            Register(key);

            using var guard = FallbackGuard.Install();
            var resolver = ResolverOver(NonexistentRoot());
            var template = resolver.GetTemplate("index", "home", out _, null, TemplatePathType.View);

            AssertPrecompiledAdapter(template, key);
            Assert.Equal(DynamicReference(), template.Generate("hi"));
            guard.Verify();
        }

        [Fact]
        public void PartialViewArmServesTheRegisteredEntryThroughTheSearchLadder()
        {
            const string key = "views/partial/home/box.heddle";
            Register(key);

            using var guard = FallbackGuard.Install();
            var resolver = ResolverOver(NonexistentRoot());
            var template = resolver.GetTemplate("box", "home", out _, null, TemplatePathType.PartialView);

            AssertPrecompiledAdapter(template, key);
            Assert.Equal(DynamicReference(), template.Generate("hi"));
            guard.Verify();
        }

        /// <summary>Master is reachable only through the public <c>Search</c>, which hands the hit back through its
        /// <c>out cached</c> parameter — no public signature changes.</summary>
        [Fact]
        public void MasterArmReturnsTheRegisteredEntryThroughSearchOutCached()
        {
            const string key = "views/base/home/layout.heddle";
            Register(key);

            using var guard = FallbackGuard.Install();
            var resolver = ResolverOver(NonexistentRoot());
            resolver.Search("layout", "home", TemplatePathType.Master, out var searched, out var cached);

            AssertPrecompiledAdapter(cached, key);
            Assert.Null(searched);
            Assert.Equal(DynamicReference(), cached.Generate("hi"));
            guard.Verify();
        }

        /// <summary>A registry miss is never a failure: the ladder falls through to the unchanged cache/disk tiers
        /// and reports the searched locations exactly as before.</summary>
        [Fact]
        public void RegistryMissFallsThroughToTheUnchangedDiskLadder()
        {
            Register("views/home/index.heddle");

            using var guard = FallbackGuard.Install();
            var resolver = ResolverOver(NonexistentRoot());
            var path = resolver.Search("absent", "home", TemplatePathType.View, out var searched, out var cached);

            Assert.Null(cached);
            Assert.Null(path);
            Assert.NotNull(searched);
            guard.Verify();
        }

        /// <summary>Tier order beats location order; registry hits win over earlier-location disk files.</summary>
        [Fact]
        public void RegistryHitAtALaterLocationBeatsAnEarlierLocationOnDisk()
        {
            const string key = "views/index.heddle";   // `\views\{0}` — the View arm's second location
            Register(key);

            var dir = Path.Combine(Path.GetTempPath(), "heddle-hosted-disk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "views", "home"));
            // `\views\{controller}\{view}` — the first location — exists on disk with different content.
            File.WriteAllText(Path.Combine(dir, "views", "home", "index.heddle"), "from disk\n");
            try
            {
                using var guard = FallbackGuard.Install();
                var resolver = ResolverOver(dir);
                var template = resolver.GetTemplate("index", "home", out _, null, TemplatePathType.View);

                AssertPrecompiledAdapter(template, key);
                Assert.Equal(DynamicReference(), template.Generate("hi"));
                guard.Verify();
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(dir);
            }
        }

        /// <summary>Native-built manifests fail the fingerprint step against FullCSharp requests, triggering fallback.</summary>
        [Fact]
        public void NativeBuiltManifestIsRefusedForTheArmsFullCSharpRequest()
        {
            const string key = "views/home/native.heddle";
            Register(key, new Dictionary<string, string>());   // default build: ExpressionMode.Native

            using var guard = FallbackGuard.Install().Expect(key, PrecompiledFallbackReason.OptionsMismatch);
            var resolver = ResolverOver(NonexistentRoot());
            var path = resolver.Search("native", "home", TemplatePathType.View, out var searched, out var cached);

            Assert.Null(cached);
            Assert.Null(path);
            Assert.NotNull(searched);
            guard.Verify();
        }
    }
}
