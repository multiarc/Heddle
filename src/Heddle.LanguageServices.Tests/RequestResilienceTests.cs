using System;
using Heddle.LanguageServer;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>A request is asked on every keystroke of a document that is broken most of the time. It answers
    /// with what it can — nothing, at worst — and never with an exception: one that throws keeps throwing for as
    /// long as the document stays as it is, and the editor has no completion at all until then.</summary>
    public class RequestResilienceTests
    {
        private const string Path = "doc.heddle";

        /// <summary>A closing <c>}}</c> with nothing open empties the lexer's mode stack, and the next pop throws.
        /// Completion tokenizes the buffer to find the bodies still open, and that throw came straight out of it.</summary>
        [Theory]
        [InlineData("@(1)}}")]
        [InlineData("@%<card>{{ <b>@(Title)</b> }}%@\n<p>@card() @(Title)}}</p>\n@(Ti")]
        public void CompletionSurvivesACloserWithNothingOpen(string text)
        {
            using var service = CorpusFixture.NewTypedService();
            service.Analyze(Path, text, 1);
            for (int offset = 0; offset <= text.Length; offset++)
                Assert.NotNull(service.GetCompletions(Path, offset));
            Assert.Null(Record.Exception(() => service.GetHover(Path, 2)));
            Assert.Null(Record.Exception(() => service.GetDefinition(Path, 2)));
        }

        /// <summary>Whatever a request handler throws, the editor is answered with nothing rather than an error;
        /// only cancellation, which is the editor's own, passes through.</summary>
        [Fact]
        public void ARequestThatThrowsIsAnsweredWithNothing()
        {
            var server = new LspServer();
            Assert.Equal(7, server.Answer<int>("probe", 7, () => throw new InvalidOperationException("stack empty")));
            Assert.Equal(7, server.Answer<int>("probe", 7, () => throw new NullReferenceException()));
            Assert.Equal(3, server.Answer("probe", 7, () => 3));
            Assert.Throws<OperationCanceledException>(() =>
                server.Answer<int>("probe", 7, () => throw new OperationCanceledException()));
        }
    }
}
