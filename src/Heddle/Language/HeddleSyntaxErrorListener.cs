using System.IO;
using Antlr4.Runtime;
using Heddle.Data;

namespace Heddle.Language
{
    internal sealed class HeddleSyntaxErrorListener : BaseErrorListener
    {
        public ParseContext Context { get; }

        public HeddleSyntaxErrorListener(ParseContext context = null)
        {
            Context = context ?? new ParseContext();
        }

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line,
            int charPositionInLine,
            string msg, RecognitionException e)
        {
            var error = Context.CreateError(e, msg, offendingSymbol);
            error.DiagnosticId = HeddleDiagnosticIds.SyntaxError;
            Context.Errors.Add(error);
        }

        public void Clear()
        {
            Context.Errors.Clear();
        }
    }
}
