using System.IO;
using Antlr4.Runtime;

namespace Templates.Language
{
    internal sealed class TtlSyntaxErrorListener : BaseErrorListener
    {
        public ParseContext Context { get; }

        public TtlSyntaxErrorListener(ParseContext context = null)
        {
            Context = context ?? new ParseContext();
        }

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
            string msg, RecognitionException e)
        {
            base.SyntaxError(output, recognizer, offendingSymbol, line, charPositionInLine, msg, e);
            Context.Errors.Add(Context.CreateError(e, msg, offendingSymbol));
        }
    }
}