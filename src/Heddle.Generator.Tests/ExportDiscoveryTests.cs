using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <c>[ExportFunctions]</c> discovery against the shared rule-core. The three drifts pinned here are the ones the
    /// gauntlet's exact overload-count comparison made load-bearing: which methods count, whether a second container's
    /// overloads merge, and what a container the runtime refuses does to the build.
    /// </summary>
    public class ExportDiscoveryTests
    {
        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
            refs.Add(MetadataReference.CreateFromFile(
                typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly.Location));
            return refs;
        }

        private static FunctionExportResolver Resolve(string source)
        {
            var compilation = CSharpCompilation.Create("ExportProbe",
                new[] { CSharpSyntaxTree.ParseText(source) }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            return FunctionExportResolver.Build(compilation);
        }

        private const string EligibilitySource = @"
[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Helpers))]
namespace Probe
{
    public static class Helpers
    {
        public static string Slug(string s) => s;
        public static string Slug(int n) => n.ToString();
        public static string Version => ""1"";        // property accessors are special-name — skipped
        public static string operator +(Helpers a, Helpers b) => null;  // not applicable; kept out by MethodKind
    }
}";

        [Fact]
        public void OnlyEligibleMethodsAreCounted()
        {
            var resolver = Resolve(EligibilitySource);

            Assert.True(resolver.TryGet("slug", out var slug));
            Assert.Equal(2, slug.Overloads.Count);
            Assert.Single(slug.ManifestRows);
            Assert.Equal(2, slug.ManifestRows[0].OverloadCount);

            Assert.False(resolver.TryGet("version", out _));
            Assert.False(resolver.TryGet("get_version", out _));
            Assert.Empty(resolver.IneligibleContainers);
        }

        private const string IneligibleMethodSource = @"
[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Helpers))]
namespace Probe
{
    public static class Helpers
    {
        public static string Slug(string s) => s;
        public static void Log(string s) { }
        public static T Echo<T>(T v) => v;
        public static string Swap(ref string v) => v;
        public static string TryThing(out string v) { v = null; return v; }
    }
}";

        [Fact]
        public void IneligibleMethodsAreReportedRatherThanQuietlyExcluded()
        {
            var resolver = Resolve(IneligibleMethodSource);

            // An ineligible METHOD is not a silent build-tier over-count. FunctionRegistry.RegisterContainer wraps
            // the ArgumentException and rethrows, so the whole container fails to register and the host throws at
            // startup. The build must error (HED7021) rather than adjusting a count for a container that will never exist.
            var reasons = resolver.IneligibleContainers.Select(c => c.Reason).ToList();
            Assert.Equal(4, reasons.Count);
            Assert.Contains(reasons, r => r.Contains("Helpers.Log") && r.Contains("must return a value"));
            Assert.Contains(reasons, r => r.Contains("Helpers.Echo") && r.Contains("must not be an open generic"));
            Assert.Contains(reasons, r => r.Contains("Helpers.Swap") && r.Contains("ref/out/pointer"));
            Assert.Contains(reasons, r => r.Contains("Helpers.TryThing") && r.Contains("ref/out/pointer"));

            // The eligible sibling still binds — the build errors, but discovery is not abandoned.
            Assert.True(resolver.TryGet("slug", out var slug));
            Assert.Equal(1, slug.ManifestRows[0].OverloadCount);
        }

        [Theory]
        [InlineData("internal static class Helpers { public static string Slug(string s) => s; }")]
        [InlineData("public sealed class Helpers { public static string Slug(string s) => s; }")]
        public void IneligibleContainerIsReported(string declaration)
        {
            var resolver = Resolve(
                "[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Helpers))]\nnamespace Probe { " +
                declaration + " }");

            var container = Assert.Single(resolver.IneligibleContainers);
            Assert.Equal("Probe.Helpers", container.Display);
            Assert.Contains("must be a public static class", container.Reason);
            Assert.False(resolver.TryGet("slug", out _));
        }

        [Fact]
        public void NestedPublicContainerIsEligible()
        {
            // Reflection's container test is IsPublic || IsNestedPublic, so a nested public static class is legal —
            // the symbol side must walk the containing chain rather than read DeclaredAccessibility alone.
            var resolver = Resolve(@"
[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Outer.Helpers))]
namespace Probe
{
    public static class Outer
    {
        public static class Helpers { public static string Slug(string s) => s; }
    }
}");

            Assert.Empty(resolver.IneligibleContainers);
            Assert.True(resolver.TryGet("slug", out var slug));
            // …and its manifest identity is the '+'-spelled AQN, not the display string's dotted one.
            Assert.Equal("Probe.Outer+Helpers, ExportProbe", slug.ManifestRows[0].Aqn);
        }

        private const string MergeSource = @"
[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.First), typeof(Probe.Second))]
namespace Probe
{
    public static class First
    {
        public static string Slug(string s) => s;
    }

    public static class Second
    {
        public static string Slug(int n) => n.ToString();
        public static string Only(string s) => s;
    }
}";

        [Fact]
        public void SecondContainerMergesItsOverloadsRatherThanBeingIgnored()
        {
            var resolver = Resolve(MergeSource);

            Assert.True(resolver.TryGet("slug", out var slug));
            Assert.Equal(2, slug.Overloads.Count);

            // One manifest row PER container, each with its own count — the shape the merged live registry produces.
            // First-container-wins recorded a single row and made every merged name a permanent FunctionBindingMismatch,
            // because the gauntlet sees a live target absent from the recorded set.
            Assert.Equal(2, slug.ManifestRows.Count);
            Assert.Contains(slug.ManifestRows, r => r.Aqn == "Probe.First, ExportProbe" && r.OverloadCount == 1);
            Assert.Contains(slug.ManifestRows, r => r.Aqn == "Probe.Second, ExportProbe" && r.OverloadCount == 1);
        }

        private const string ReplaceSource = @"
[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.First), typeof(Probe.Second))]
namespace Probe
{
    public static class First { public static string Slug(string s) => s; }
    public static class Second { public static string Slug(string s) => s + ""!""; }
}";

        [Fact]
        public void IdenticalSignatureReplacesRatherThanAddingAnOverload()
        {
            var resolver = Resolve(ReplaceSource);

            Assert.True(resolver.TryGet("slug", out var slug));
            Assert.Single(slug.Overloads);
            // AddOrReplace: the later registration wins the slot, so only the surviving container has a row.
            var row = Assert.Single(slug.ManifestRows);
            Assert.Equal("Probe.Second, ExportProbe", row.Aqn);
            Assert.Equal(1, row.OverloadCount);
        }

        [Fact]
        public void IneligibleExportContainerRedsTheBuildWithHed7021()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/x.heddle", "@model(){{System.String}}@\\\nhi\n") },
                new[]
                {
                    "[assembly: Heddle.Attributes.ExportFunctions(typeof(Probe.Helpers))]\n" +
                    "namespace Probe { internal static class Helpers { public static string Slug(string s) => s; } }"
                });

            var hed7021 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7021");
            Assert.NotEqual(default, hed7021);
            Assert.Equal(DiagnosticSeverity.Error, hed7021.Severity);
            Assert.Contains("Probe.Helpers", hed7021.GetMessage());
            Assert.Contains("public static class", hed7021.GetMessage());
        }
    }
}
