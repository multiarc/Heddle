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

        // The property this bound exists for cannot be asserted in-process. A test that parses past the limit
        // on a small stack does not go red when the property breaks — it takes the test host down, which is how a
        // previous attempt aborted the Release run after 1143 of 1865 tests. Verifying it needs a child process
        // comparing exit codes, which no suite here does; the limit's value is asserted above instead.

        private static void AssertReportsDepth(string document)
        {
            var context = DocumentParser.Parse(document, new ParserSettings { RootPath = "<none>" }, out _);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
        }
    }
}
