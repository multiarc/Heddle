using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// A character the lexer cannot tokenize went to ANTLR's console listener instead of the error list, and the
    /// skip-recovery then compiled the template as if the character were never typed: a props default of '4 +'
    /// compiled as '4' with zero diagnostics. The diagnostic-mode retry re-parses buffered tokens, so the lexer
    /// never re-fires and the recorded error must survive the retry's clear.
    /// </summary>
    public class LexerErrorReportingTests
    {
        [Fact]
        public void AnUntokenizableCharacterIsReportedNotSilentlyDropped()
        {
            var context = DocumentParser.Parse("@% <pad(width: int = 4 +)>{{x}} %@",
                new ParserSettings { RootPath = "<none>" }, out _);

            var reported = Assert.Single(context.Errors);
            Assert.Equal(HeddleDiagnosticIds.SyntaxError, reported.DiagnosticId);
            Assert.Contains("token recognition error", reported.Error);
            Assert.Equal(23, reported.Position.StartIndex);
        }

        [Fact]
        public void TheEditorParseReportsItToo()
        {
            var context = DocumentParser.Parse("@% <pad(width: int = 4 +)>{{x}} %@",
                new ParserSettings { RootPath = "<none>", ProvideLanguageFeatures = true }, out _);

            Assert.Contains(context.Errors, e =>
                e.DiagnosticId == HeddleDiagnosticIds.SyntaxError && e.Error.Contains("token recognition error"));
        }

        [Fact]
        public void APropsDefaultWithAnUnsupportedOperatorStillFailsWithItsParserError()
        {
            var context = DocumentParser.Parse("@% <card(style: string = 1 + 2)>{{x}} %@",
                new ParserSettings { RootPath = "<none>" }, out _);

            Assert.Contains(context.Errors, e => e.Error.Contains("token recognition error"));
            Assert.True(context.Errors.Count(e => e.DiagnosticId == HeddleDiagnosticIds.SyntaxError) >= 1);
        }
    }
}
