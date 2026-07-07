using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Heddle.Demo.Models;
using Heddle.Demo.Wasm;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Phase 9 D14 — the cross-host facade contract. One shared fixture set
    /// (<c>src/Heddle.Demo.Wasm/contract-fixtures/*.json</c>) asserted against <see cref="DemoHost"/> on ordinary
    /// CoreCLR; the same fixtures are asserted browser-side by the Playwright S3/S4 scenarios. Facade drift between
    /// the LSP server and the browser host now breaks a test on both sides of the boundary.
    /// </summary>
    public class DemoContractTests
    {
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static string FixtureDir =>
            Path.Combine(AppContext.BaseDirectory, "contract-fixtures");

        public static IEnumerable<object[]> Fixtures()
        {
            foreach (var file in Directory.EnumerateFiles(FixtureDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                yield return new object[] { Path.GetFileName(file) };
        }

        [Theory]
        [MemberData(nameof(Fixtures))]
        public void Fixture_MatchesFacadeProjection(string fixtureFile)
        {
            var fixture = JsonSerializer.Deserialize<Fixture>(
                File.ReadAllText(Path.Combine(FixtureDir, fixtureFile)), JsonOpts);
            Assert.NotNull(fixture);

            var host = new DemoHost();
            const string path = "demo.heddle";
            var text = fixture.UseStarter != null
                ? DemoCatalog.Get(fixture.UseStarter).StarterTemplate
                : fixture.Text;
            Assert.False(string.IsNullOrEmpty(text), $"fixture {fixture.Name}: no text/useStarter");

            switch (fixture.Type)
            {
                case "completion":
                    RunCompletion(host, path, text, fixture);
                    break;
                case "diagnostic":
                    RunDiagnostic(host, path, text, fixture);
                    break;
                case "render":
                    RunRender(host, path, text, fixture);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"fixture {fixture.Name}: unknown type '{fixture.Type}'");
            }
        }

        private static void RunCompletion(DemoHost host, string path, string text, Fixture fixture)
        {
            host.Analyze(path, text, fixture.Version);
            var offset = text.IndexOf(fixture.CaretAfter, StringComparison.Ordinal);
            Assert.True(offset >= 0, $"fixture {fixture.Name}: caretAfter '{fixture.CaretAfter}' not found");
            offset += fixture.CaretAfter.Length;

            var result = host.Complete(path, offset);
            var byKind = result.Items;

            // Property items are matched EXACTLY as a set (label+detail+insertText) — the scope-type members.
            var actualProps = byKind.Where(i => i.Kind == "property")
                .Select(i => (i.Label, i.Detail, i.InsertText)).OrderBy(t => t.Label, StringComparer.Ordinal).ToList();
            var expectedProps = (fixture.ExpectProperties ?? new List<ItemSpec>())
                .Select(p => (p.Label, p.Detail, p.InsertText)).OrderBy(t => t.Label, StringComparer.Ordinal).ToList();
            Assert.Equal(expectedProps, actualProps);

            // Prop (named-argument) items matched exactly, when the fixture pins them.
            if (fixture.ExpectProps != null)
            {
                var actualPropArgs = byKind.Where(i => i.Kind == "prop")
                    .Select(i => (i.Label, i.Detail, i.InsertText)).OrderBy(t => t.Label, StringComparer.Ordinal).ToList();
                var expectedPropArgs = fixture.ExpectProps
                    .Select(p => (p.Label, p.Detail, p.InsertText)).OrderBy(t => t.Label, StringComparer.Ordinal).ToList();
                Assert.Equal(expectedPropArgs, actualPropArgs);
            }

            // Keyword items matched exactly as a set.
            var actualKeywords = byKind.Where(i => i.Kind == "keyword").Select(i => i.Label)
                .OrderBy(l => l, StringComparer.Ordinal).ToList();
            var expectedKeywords = (fixture.ExpectKeywords ?? new List<string>())
                .OrderBy(l => l, StringComparer.Ordinal).ToList();
            Assert.Equal(expectedKeywords, actualKeywords);

            // Function items: the pinned subset must be present (the registry list is asserted exactly by the
            // engine's own DefaultFunctionLockstep suite; here we assert the projection surfaces them).
            var actualFunctions = new HashSet<string>(byKind.Where(i => i.Kind == "function").Select(i => i.Label));
            foreach (var fn in fixture.ExpectFunctions ?? new List<string>())
                Assert.Contains(fn, actualFunctions);
        }

        private static void RunDiagnostic(DemoHost host, string path, string text, Fixture fixture)
        {
            var result = host.Analyze(path, text, fixture.Version);
            Assert.Equal(fixture.ExpectCsharpTierUsed, result.CsharpTierUsed);

            var d = fixture.ExpectDiagnostic;
            var match = result.Diagnostics.FirstOrDefault(x =>
                x.Id == d.Id && x.Offset == d.Offset && x.Length == d.Length);
            Assert.True(match != null,
                $"fixture {fixture.Name}: expected {d.Id} @{d.Offset}:{d.Length}; got " +
                string.Join(", ", result.Diagnostics.Select(x => $"{x.Id}@{x.Offset}:{x.Length}")));
            Assert.Equal(d.Severity, match.Severity);
            if (!string.IsNullOrEmpty(d.MessageContains))
                Assert.Contains(d.MessageContains, match.Message);
        }

        private static void RunRender(DemoHost host, string path, string text, Fixture fixture)
        {
            host.Analyze(path, text, fixture.Version);
            var result = host.Render(path, fixture.ModelId);
            Assert.True(result.Error == null, $"fixture {fixture.Name}: render error: {result.Error}");
            Assert.NotNull(result.Html);
            foreach (var fragment in fixture.ExpectContains ?? new List<string>())
                Assert.Contains(fragment, result.Html);
        }

        private sealed class Fixture
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Text { get; set; }
            public string UseStarter { get; set; }
            public int Version { get; set; }
            public string CaretAfter { get; set; }
            public string ModelId { get; set; }
            public List<ItemSpec> ExpectProperties { get; set; }
            public List<ItemSpec> ExpectProps { get; set; }
            public List<string> ExpectKeywords { get; set; }
            public List<string> ExpectFunctions { get; set; }
            public DiagnosticSpec ExpectDiagnostic { get; set; }
            public bool ExpectCsharpTierUsed { get; set; }
            public List<string> ExpectContains { get; set; }
        }

        private sealed class ItemSpec
        {
            public string Label { get; set; }
            public string Detail { get; set; }
            public string InsertText { get; set; }
        }

        private sealed class DiagnosticSpec
        {
            public string Id { get; set; }
            public string Severity { get; set; }
            public int Offset { get; set; }
            public int Length { get; set; }
            public string MessageContains { get; set; }
        }
    }
}
