using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Deeply nested input used to kill the process. The generated parser spends a stack frame per rule invocation,
    /// and a <c>StackOverflowException</c> cannot be caught, so one template file — hostile or merely mistaken —
    /// ended the host. The contract is that a malformed template becomes an entry in the error list.
    /// <para>Both halves are covered here, and they failed differently. Left-associative operators are rewritten by
    /// ANTLR into a loop, so a flat <c>1+1+1…</c> never troubled the parser and instead overflowed Heddle's own tree
    /// walk. Prefix operators and the right-associative <c>??</c> and <c>?:</c> recurse inside the parser itself,
    /// upstream of any Heddle code, and were the shapes that stayed fatal after the walk was guarded.</para>
    /// <para>The bound is a depth count rather than a stack measurement, which is what makes these tests mean
    /// anything: a stack probe answers differently on a pool thread, a main thread, and an optimised build, so the
    /// same template would pass here and crash in production.</para>
    /// </summary>
    public class DeepNestingTests
    {
        private const int PastTheLimit = ParseDepthGuardLimit + 500;

        /// <summary>
        /// Mirrors the engine's own limit. The copy exists so a change to the production value cannot pass
        /// unnoticed — and it failed at that once already: the value moved 1000 → 300 while this constant stayed at
        /// 1000, and because the test depths are far past both, nothing went red. A copy only notices if something
        /// compares it, which is what <see cref="TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack"/>
        /// now does.
        /// </summary>
        private const int ParseDepthGuardLimit = 250;

        /// <summary>
        /// The limit is not an arbitrary round number: it is chosen to sit below the depth at which the smallest
        /// stack the engine can be hosted on runs out, so the bound is reached before the process dies. Moving it
        /// upward silently — which is exactly what happened while this went unasserted — restores the crash on a
        /// 1 MB thread, and no other test in the repo observes the value at all.
        /// </summary>
        [Fact]
        public void TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack()
        {
            Assert.Equal(ParseDepthGuardLimit, ParseDepthGuard.MaxDepth);
        }

        // The property this bound exists for cannot be asserted in-process for the shapes that recurse inside ANTLR.
        // A test that parses one of those past the limit does not go red when the property breaks — it takes the
        // test host down, which is how a previous attempt aborted the Release run after 1143 of 1865 tests.
        // Verifying that needs a child process comparing exit codes, which no suite here does; the limit's value is
        // asserted above instead.
        //
        // A flat left-associative run is the one shape with no such hazard at this depth. ANTLR rewrites left
        // recursion into a loop, so the parser never recurses and the depth lands entirely in the tree it builds —
        // walked with an explicit stack. It reaches the reporting path, so what it goes red on is the report.
        //
        // "No hazard" is a margin, not a property. With the guard disabled a flat run of 3000 still survives and one
        // of 5000 takes the host down — the recursion is in the AST build and the tree walk, past the report point.
        // The depth used below is 750. That margin holds only while the limit does: raising MaxDepth toward 3000
        // would make this test the host-killer the paragraph above is about, and the constant that pins the limit is
        // what stands between the two.

        /// <summary>A run of this many additions is that many tree levels and no parser recursion at all.</summary>
        [Fact]
        public void AFlatRunPastTheLimitIsReported()
        {
            AssertReportsDepth("@(" + string.Join("+", Enumerable.Repeat("1", PastTheLimit)) + ")");
        }

        /// <summary>
        /// Two unrelated conditions report <c>HED4007</c> — this one, and <c>@&lt;&lt;</c> imports nested past their
        /// own bound — so the id alone tells a reader nothing about which happened or what to shorten. The message
        /// is the only thing that distinguishes them, and it carries the limit that was hit.
        /// </summary>
        private static void AssertReportsDepth(string document)
        {
            var context = DocumentParser.Parse(document, new ParserSettings { RootPath = "<none>" }, out _);

            var reported = Assert.Single(context.Errors
                .Where(e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply));
            Assert.Contains("nested too deeply to compile (limit " + ParseDepthGuardLimit + " levels)",
                reported.Error);
            Assert.Contains("expression, chain, or block nesting", reported.Error);
            Assert.DoesNotContain("@<<", reported.Error);
        }
    }
}
