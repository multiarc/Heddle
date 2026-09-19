using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>Routes lexer errors into the compile context; ANTLR's default listener writes them to the
    /// console instead, and the skip-recovery then compiles the template as if the character were never typed.</summary>
    internal sealed class HeddleLexerErrorListener : IAntlrErrorListener<int>
    {
        private readonly ParseContext _context;
        private readonly List<HeddleCompileError> _recorded = new List<HeddleCompileError>();

        public HeddleLexerErrorListener(ParseContext context)
        {
            _context = context;
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line,
            int charPositionInLine, string msg, RecognitionException e)
        {
            var error = _context.CreateError(e, msg);
            error.DiagnosticId = HeddleDiagnosticIds.SyntaxError;
            if (recognizer is Lexer lexer && lexer.TokenStartCharIndex >= 0)
            {
                error.Position = new BlockPosition(lexer.TokenStartCharIndex,
                    Math.Max(1, lexer.InputStream.Index - lexer.TokenStartCharIndex));
            }

            _recorded.Add(error);
            _context.Errors.Add(error);
        }

        /// <summary>Re-adds every recorded error after a diagnostic-mode retry clears the context: the retry
        /// re-parses buffered tokens, so the lexer never re-fires for input it already reported.</summary>
        public void Replay()
        {
            foreach (var error in _recorded)
                _context.Errors.Add(error);
        }
    }
}
