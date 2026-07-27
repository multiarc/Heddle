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

        /// <summary>Mirrors the engine's own limit; a copy rather than a link, because a test that reads the value it
        /// is checking against cannot notice the value changing.</summary>
        private const int ParseDepthGuardLimit = 1000;

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

        private static void AssertReportsDepth(string document)
        {
            var context = DocumentParser.Parse(document, new ParserSettings { RootPath = "<none>" }, out _);

            Assert.Contains(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
        }
    }
}
