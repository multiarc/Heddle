using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The differential corpus for index expressions. The engine evaluates the receiver once and yields
    /// <c>default(TResult)</c> on a null receiver instead of throwing; array indices convert to <c>int</c> whatever
    /// integral type they carry; a non-array target binds an indexer by reflection, first match wins. The generator
    /// emits the shapes it can prove — any integral array index, exactly one matching indexer candidate — and
    /// degrades where the engine's answer depends on reflection order, and forwards the engine's HED1010 as a
    /// build error where no candidate exists at a known target type.
    /// </summary>
    public class IndexerDifferentialTests
    {
        private const string HostType = "Heddle.Generator.IntegrationTests.Fixtures.IndexHost";

        private static string Template(string expression) =>
            "@model(){{" + HostType + "}}@\\\nvalue: @(" + expression + ")\n";

        private static IndexHost Host() => new IndexHost
        {
            Ints = new[] { 10, 20, 30 },
            Tags = new[] { "a", "b" },
            Grid = new[,] { { 1, 2, 3 }, { 4, 5, 6 } },
            Jagged = new[] { new[] { 1, 2 }, new[] { 3 } },
            Map = new System.Collections.Generic.Dictionary<string, int> { ["key"] = 42 },
            Names = new System.Collections.Generic.List<string> { "x", "y" },
            Count = 3,
            MaybeIndex = 2,
            WideIndex = 1,
            CharIndex = (char) 2,
            Name = "hi",
        };

        private static void AssertMatches(string key, string expression, IndexHost model = null)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, Template(expression), typeof(IndexHost),
                model ?? Host());
            Assert.Equal(dyn, precompiled);
        }

        private static void AssertDegrades(string key, string expression)
        {
            var content = Template(expression);
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.Empty(gen.TemplateSources);
        }

        /// <summary>Asserts both tiers reject the expression under the same id AND the same sentence: the
        /// engine's positioned compile error, and the generator's forwarded build error carrying the engine's
        /// exact message (the template still degrades, so HED7031's notice may ride along).</summary>
        private static void AssertBothTiersReject(string key, string expression, string diagnosticId)
        {
            var content = Template(expression);

            var template = new HeddleTemplate(content,
                new CompileContext(new TemplateOptions(), typeof(IndexHost)));
            Assert.False(template.CompileResult.Success);
            var engineMessages = template.CompileResult.ErrorList
                .Where(e => e.DiagnosticId == diagnosticId).Select(e => e.Error).ToList();
            Assert.NotEmpty(engineMessages);

            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var forwarded = Assert.Single(gen.Diagnostics.Where(d => d.Id == diagnosticId));
            Assert.Equal(DiagnosticSeverity.Error, forwarded.Severity);
            Assert.Contains(forwarded.GetMessage(), engineMessages);
            Assert.Empty(gen.Diagnostics.Where(d => d.Id != diagnosticId && d.Id != "HED7031"));
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.Empty(gen.TemplateSources);
        }

        [Fact]
        public void ArrayIndexing_PrecompilesAndMatches()
        {
            foreach (var (key, expression) in new[]
            {
                ("indexer/array-literal.heddle", "Ints[0]"),
                ("indexer/array-computed.heddle", "Ints[Count - 1]"),
                ("indexer/array-string-element.heddle", "Tags[1]"),
                ("indexer/array-multidim.heddle", "Grid[1, 2]"),
                ("indexer/array-jagged.heddle", "Jagged[0][1]"),
            })
                AssertMatches(key, expression);
        }

        /// <summary>The engine converts every non-<c>int</c> integral index with a truncating
        /// <c>Expression.Convert</c>; the emitted <c>(int)</c> cast is the same conversion, nullable included.</summary>
        [Fact]
        public void ConvertedIndexTypes_PrecompileAndMatch()
        {
            foreach (var (key, expression) in new[]
            {
                ("indexer/index-char.heddle", "Ints[CharIndex]"),
                ("indexer/index-long.heddle", "Ints[WideIndex]"),
                ("indexer/index-nullable.heddle", "Ints[MaybeIndex]"),
            })
                AssertMatches(key, expression);
        }

        [Fact]
        public void SingleCandidateIndexers_PrecompileAndMatch()
        {
            foreach (var (key, expression) in new[]
            {
                ("indexer/dictionary.heddle", "Map[\"key\"]"),
                ("indexer/list.heddle", "Names[1]"),
                ("indexer/string-chars.heddle", "Name[0]"),
                // Two indexers exist, but a string argument converts to only one of them — no order to depend on.
                ("indexer/two-single-match.heddle", "Two[\"s\"]"),
            })
                AssertMatches(key, expression);
        }

        /// <summary>A null receiver yields <c>default(TResult)</c> on the engine — never a
        /// NullReferenceException — and never evaluates the index expression at all.</summary>
        [Fact]
        public void NullReceiver_YieldsDefaultOnBothTiers()
        {
            AssertMatches("indexer/null-array.heddle", "NullInts[0]");

            var noMap = Host();
            noMap.Map = null;
            AssertMatches("indexer/null-dictionary.heddle", "Map[\"key\"]", noMap);

            var noNames = Host();
            noNames.Names = null;
            AssertMatches("indexer/null-list.heddle", "Names[1]", noNames);
        }

        [Fact]
        public void IndexInsideALargerExpression_PrecompilesAndMatches()
        {
            foreach (var (key, expression) in new[]
            {
                ("indexer/in-arith.heddle", "Ints[0] + Count"),
                ("indexer/in-concat.heddle", "\"n=\" + Map[\"key\"]"),
                ("indexer/in-ternary.heddle", "Ints[0] > 0 ? Tags[0] : Name"),
                ("indexer/in-call.heddle", "len(Tags[1])"),
            })
                AssertMatches(key, expression);
        }

        /// <summary>Both tiers charge the model exactly one read of the receiver per render.</summary>
        [Fact]
        public void TheIndexReceiverIsReadOnce()
        {
            IndexHost.Reads = 0;
            AssertMatches("indexer/receiver-once.heddle", "Counted[0]");
            Assert.Equal(2, IndexHost.Reads);
        }

        /// <summary>An <c>int</c> argument converts to both of <see cref="TwoIndexers"/>' indexers, so the engine's
        /// pick is whichever reflection enumerates first — the generator degrades, the engine still renders.</summary>
        [Fact]
        public void MultipleMatchingIndexers_DegradeButTheEngineRenders()
        {
            AssertDegrades("indexer/two-ambiguous.heddle", "Two[Count]");

            var template = new HeddleTemplate(Template("Two[Count]"),
                new CompileContext(new TemplateOptions(), typeof(IndexHost)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            var rendered = template.Generate(Host());
            Assert.True(rendered == "value: int:3\n" || rendered == "value: obj:3\n", rendered);
        }

        /// <summary>A known target type with no matching indexer is the engine's HED1010 on every input, so the
        /// generator forwards that id as a build error with the engine's own sentence.</summary>
        [Fact]
        public void KnownTypeWithNoMatchingIndexer_IsRefusedByBothTiers()
        {
            AssertBothTiersReject("indexer/no-indexer.heddle", "None[0]", HeddleDiagnosticIds.IndexerNotFound);
            AssertBothTiersReject("indexer/array-bad-index.heddle", "Ints[Name]",
                HeddleDiagnosticIds.IndexerNotFound);
        }
    }
}
