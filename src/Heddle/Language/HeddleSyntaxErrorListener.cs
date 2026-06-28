using System.IO;
using Antlr4.Runtime;

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
            Context.Errors.Add(Context.CreateError(e, msg, offendingSymbol));
        }

        public void Clear()
        {
            Context.Errors.Clear();
        }
    }
}