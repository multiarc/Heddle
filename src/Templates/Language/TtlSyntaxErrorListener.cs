using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Sharpen;

namespace Templates.Language
{
    internal sealed class TtlSyntaxErrorListener : BaseErrorListener
    {
        public ParseContext Context { get; }

        public TtlSyntaxErrorListener(ParseContext context = null)
        {
            Context = context ?? new ParseContext();
        }

        public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line,
            int charPositionInLine, string msg,
            RecognitionException e)
        {
            Context.Errors.Add(Context.CreateError(e, msg, offendingSymbol));
        }
    }
}