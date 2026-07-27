using System.Linq;
using System.Threading;
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
        private const int ParseDepthGuardLimit = 300;

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

        /// <summary>
        /// The property the limit exists for, stated directly: on a 1 MB stack — the Windows default, and what the
        /// thread pool hands out — a parser-recursive shape past the bound must report rather than kill the process.
        /// The measured crash depth there is just over 300, so this is the margin the number was chosen for.
        /// </summary>
        [Theory]
        [InlineData("!")]
        [InlineData("~")]
        public void APrefixRunPastTheLimitIsReportedOnASmallStack(string op)
        {
            var document = "@model(){{dynamic}}@(" + new string(op[0], ParseDepthGuardLimit + 100) + "true)";
            ParseContext context = null;

            var thread = new Thread(
                () => context = DocumentParser.Parse(document, new ParserSettings { RootPath = "<none>" }, out _),
                1024 * 1024);
            thread.Start();
            thread.Join();

            Assert.NotNull(context);
            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
        }

        [Fact]
        public void APrefixOperatorRunIsReportedInsteadOfKillingTheProcess()
        {
            AssertReportsDepth("@model(){{dynamic}}@(" + new string('!', PastTheLimit) + "true)");
        }

        [Fact]
        public void ANestedConditionalIsReportedInsteadOfKillingTheProcess()
        {
            AssertReportsDepth("@model(){{dynamic}}@(" +
                               string.Concat(Enumerable.Repeat("true ? ", PastTheLimit)) + "1" +
                               string.Concat(Enumerable.Repeat(" : 2", PastTheLimit)) + ")");
        }

        [Fact]
        public void ACoalesceChainIsReportedInsteadOfKillingTheProcess()
        {
            AssertReportsDepth("@model(){{dynamic}}@(" +
                               string.Join(" ?? ", Enumerable.Repeat("Title", PastTheLimit)) + ")");
        }

        [Fact]
        public void AFlatOperatorRunIsReportedInsteadOfKillingTheProcess()
        {
            AssertReportsDepth("@model(){{dynamic}}@(" +
                               string.Join("+", Enumerable.Repeat("1", PastTheLimit)) + ")");
        }

        [Fact]
        public void ADeepParenthesisNestIsReportedInsteadOfKillingTheProcess()
        {
            AssertReportsDepth("@model(){{dynamic}}@(" + new string('(', PastTheLimit) + "1" +
                               new string(')', PastTheLimit) + ")");
        }

        [Fact]
        public void ADeepIndexerChainIsReportedInsteadOfKillingTheProcess()
        {
            AssertReportsDepth("@model(){{dynamic}}@(Items" +
                               string.Concat(Enumerable.Repeat("[0]", PastTheLimit)) + ")");
        }

        [Fact]
        public void ADeepBlockNestIsReportedInsteadOfKillingTheProcess()
        {
            AssertReportsDepth("@model(){{dynamic}}" +
                               string.Concat(Enumerable.Repeat("@if(true){{", PastTheLimit)) + "x" +
                               string.Concat(Enumerable.Repeat("}}", PastTheLimit)));
        }

        /// <summary>
        /// The bound must sit far above real templates. A layout nesting a few dozen levels, with long member paths
        /// and chains, has to keep compiling — a guard that fired on ordinary work would be a worse defect than the
        /// one it replaced.
        /// </summary>
        [Fact]
        public void OrdinarilyDeepTemplatesAreUnaffected()
        {
            const int realistic = 40;
            var document = "@model(){{dynamic}}" +
                           string.Concat(Enumerable.Repeat("@if(true){{", realistic)) +
                           "@(" + string.Join(".", Enumerable.Repeat("Member", realistic)) + ")" +
                           string.Concat(Enumerable.Repeat("}}", realistic));

            var context = DocumentParser.Parse(document, new ParserSettings { RootPath = "<none>" }, out _);

            Assert.DoesNotContain(context.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
        }

        /// <summary>
        /// The same bound, on a document that <b>also</b> has a syntax error. The error return path calls
        /// <c>GetText()</c>, which recurses over the whole tree, and it used to run before the tree was ever
        /// measured — so one stray <c>@(</c> handed the overflow straight back. An editor's document has a syntax
        /// error most of the time, which is where this mattered most.
        /// </summary>
        [Theory]
        [InlineData("@(")]
        [InlineData("@if(")]
        public void ADeepTemplateThatAlsoHasASyntaxErrorIsStillBounded(string trailer)
        {
            var document = "@model(){{dynamic}}@(" +
                           string.Join("+", Enumerable.Repeat("1", PastTheLimit)) + ")" + trailer;

            var context = DocumentParser.Parse(document, new ParserSettings { RootPath = "<none>" }, out _);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
        }

        /// <summary>
        /// Unbalanced closers underflow the lexer's mode stack, and the exception escaped the compile rather than
        /// becoming a diagnostic. Six characters were enough. Malformed input is the one thing a template engine is
        /// guaranteed to be handed.
        /// </summary>
        [Theory]
        [InlineData("@(1)}}")]
        [InlineData("@model(){{dynamic}}@(1)}}}}")]
        public void UnbalancedClosersAreReportedRatherThanThrown(string document)
        {
            var context = DocumentParser.Parse(document, new ParserSettings { RootPath = "<none>" }, out _);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.SyntaxError);
        }

        private static void AssertReportsDepth(string document)
        {
            var context = DocumentParser.Parse(document, new ParserSettings { RootPath = "<none>" }, out _);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
        }
    }
}
