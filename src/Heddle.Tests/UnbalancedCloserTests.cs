using Heddle.Data;
using Heddle.Language;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// A closer with nothing open underflows the lexer's mode stack, and the <see cref="System.InvalidOperationException"/>
    /// that came out of it escaped the parse instead of landing in the error list: a caller who handed the engine a
    /// malformed template got a throw from an API whose whole contract is that it reports. Malformed input is the one
    /// thing a template engine is guaranteed to be handed, and an editor's document is malformed most of the time.
    /// </summary>
    public class UnbalancedCloserTests
    {
        [Fact]
        public void UnbalancedClosersAreReportedRatherThanThrown()
        {
            var context = DocumentParser.Parse("@(1)}}", new ParserSettings { RootPath = "<none>" }, out var clean);

            var reported = Assert.Single(context.Errors);
            Assert.Equal(HeddleDiagnosticIds.SyntaxError, reported.DiagnosticId);

            // The message, not just the id: every ordinary syntax error carries the same id, so an assertion on the
            // id alone would still pass if this document started failing somewhere else entirely.
            Assert.Contains("could not be tokenized", reported.Error);
            Assert.Equal(string.Empty, clean);
        }
    }
}
