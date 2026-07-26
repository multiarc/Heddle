using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The one drain rule: which channels a host drains, how it decides severity, whether it carries the id and
    /// the fix, and how it de-duplicates used to differ across three hosts. The language server had the complete
    /// rule, the generator had a partial one (parse channels only, severity by which collection the entry sat in,
    /// no fix, warning ids dropped), and <see cref="HeddleCompileResult"/> had a third. These pin the rule they
    /// now share.
    /// </summary>
    public class DiagnosticProjectionTests
    {
        private static HeddleCompileError Error(string message, string id = null, int start = 0, int length = 0) =>
            new HeddleCompileError { Error = message, DiagnosticId = id, Position = new BlockPosition(start, length) };

        private static HeddleCompileWarning Warning(string message, string fix = null, string id = null,
            int start = 0, int length = 0) =>
            new HeddleCompileWarning
            {
                Error = message, Fix = fix, DiagnosticId = id, Position = new BlockPosition(start, length)
            };

        private static ParseContext NewParseContext() =>
            new HeddleTemplate("hello", new CompileContext(ExType.Dynamic)).CompileResult.Context;

        [Fact]
        public void AllFourChannelsAreDrainedInTheHostOrder()
        {
            var compile = new CompileContext(ExType.Dynamic);
            var parse = NewParseContext();
            parse.Errors.Clear();
            parse.Warnings.Clear();

            compile.CompileErrors.Add(Error("ce", "HED0001"));
            compile.CompileWarnings.Add(Warning("cw", id: "HED2004"));
            parse.Errors.Add(Error("pe", "HED0003"));
            parse.Warnings.Add(Warning("pw", id: "HED4005"));

            var drained = HeddleDiagnosticProjection.Drain(compile, parse);

            Assert.Equal(new[] { "HED0001", "HED2004", "HED0003", "HED4005" }, drained.Select(e => e.Id));
            Assert.Equal(new[] { false, true, false, true }, drained.Select(e => e.IsWarning));
        }

        /// <summary>Severity comes from the entry's <b>subtype</b>. A warning object sitting in an error
        /// collection is still a warning — the rule the generator got wrong by keying on the collection.</summary>
        [Fact]
        public void SeverityFollowsTheSubtypeNotTheCollection()
        {
            var compile = new CompileContext(ExType.Dynamic);
            compile.CompileErrors.Add(Warning("misfiled", fix: "move it"));

            var entry = Assert.Single(HeddleDiagnosticProjection.Drain(compile, null));
            Assert.True(entry.IsWarning);
            Assert.Equal("move it", entry.Fix);
        }

        [Fact]
        public void IdFixAndSpanAreCarriedThrough()
        {
            var parse = NewParseContext();
            parse.Errors.Clear();
            parse.Warnings.Clear();
            parse.Warnings.Add(Warning("boom", "do this", "HED1016", 7, 3));

            var entry = Assert.Single(HeddleDiagnosticProjection.Drain(parse));
            Assert.Equal("HED1016", entry.Id);
            Assert.Equal("boom", entry.Message);
            Assert.Equal("do this", entry.Fix);
            Assert.Equal((7, 3), (entry.Offset, entry.Length));
        }

        /// <summary>The same entry object is reachable from more than one context in the nested-parse case, so
        /// the drain de-duplicates by reference — not by value, which would merge two genuinely distinct
        /// diagnostics that happen to word the same fault at the same place.</summary>
        [Fact]
        public void EntriesAreDeDuplicatedByReference()
        {
            var compile = new CompileContext(ExType.Dynamic);
            var parse = NewParseContext();
            parse.Errors.Clear();
            parse.Warnings.Clear();

            var shared = Error("same instance", "HED0001");
            compile.CompileErrors.Add(shared);
            parse.Errors.Add(shared);
            parse.Errors.Add(Error("same instance", "HED0001"));

            Assert.Equal(2, HeddleDiagnosticProjection.Drain(compile, parse).Count);
        }

        /// <summary>The predicate is how a host states its own policy without the rule growing a flag: the
        /// generator filters region-fill candidate errors it may still retract.</summary>
        [Fact]
        public void TheIncludePredicateFiltersBeforeProjection()
        {
            var parse = NewParseContext();
            parse.Errors.Clear();
            parse.Warnings.Clear();
            var retracted = Error("tentative");
            parse.Errors.Add(retracted);
            parse.Errors.Add(Error("real", "HED0003"));

            var drained = HeddleDiagnosticProjection.Drain(parse, e => !ReferenceEquals(e, retracted));
            Assert.Equal("HED0003", Assert.Single(drained).Id);
        }

        /// <summary>The third surface agrees: what <see cref="HeddleCompileResult"/> renders for a real failing
        /// template carries the same <c>(Id, Offset, Length)</c> the shared drain reports for the same entries.</summary>
        [Fact]
        public void CompileResultAgreesWithTheDrainOnARealTemplate()
        {
            var template = new HeddleTemplate("@import(){{gone}}\nhello\n", new CompileContext(ExType.Dynamic));
            var result = template.CompileResult;
            Assert.False(result.Success);

            var drained = HeddleDiagnosticProjection.Drain(result.Context)
                .Select(e => (e.Id, e.Offset, e.Length))
                .ToList();
            var rendered = result.ErrorList
                .Select(e => (e.DiagnosticId, e.Position.StartIndex, e.Position.Length))
                .ToList();

            Assert.Contains(HeddleDiagnosticIds.LegacyImportDirective, drained.Select(d => d.Id));
            foreach (var entry in rendered)
                Assert.Contains(entry, drained);
        }
    }
}
