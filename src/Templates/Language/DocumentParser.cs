using System;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Templates.Data;
using Templates.Runtime;
using Templates.Strings.Core;

namespace Templates.Language
{
    /// <summary>
    /// Parses document and creates template cache that can be used multiple times as source template representation, also used to replace templates with data multiple times (template source preserved)
    /// </summary>
    public static class DocumentParser
    {
        /// <summary>
        /// Performs parse of document
        /// </summary>
        /// <returns>Full template context tree found in source template</returns>
        public static ParseContext Parse(string document, CompileContext compileContext, out string cleanDocument)
        {
            var context = new ParseContext(provideLanguageFeatures: compileContext.Options.ProvideLanguageFeatures,
                forceRemoveWhitespace: compileContext.Options.ForceRemoveWhitespace);
            cleanDocument = Parse(document, context, compileContext);
            return context;
        }

        public static string Parse(string document, ParseContext context, CompileContext compileContext /*, bool loadDefenitionsOnly = false*/)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            var stream = new AntlrInputStream(new StringReader(document));
            var lexer = context.ForceRemoveWhitespace ? (ITokenSource) new TtlLexerNoWS(stream) : new TtlLexer(stream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new TtlParser(tokens);
            var syntaxErrorListener = new TtlSyntaxErrorListener(context);
            parser.Interpreter.PredictionMode = PredictionMode.SLL;
            TtlParser.TtlContext tree;
            parser.RemoveErrorListeners();
            parser.AddErrorListener(syntaxErrorListener);
            if (!compileContext.Options.ProvideLanguageFeatures)
            {
                bool needRetryIfFailed = false;
                try
                {
                    tree = parser.ttl();
                    needRetryIfFailed = true;
                }
                catch (ParseCanceledException e)
                {
                    tree = ParseDiagnosticMode(stream, parser, syntaxErrorListener);
                    syntaxErrorListener.Context.Warnings.Add(new TtlCompileWarning
                    {
                        Error = e.Message,
                        Exception = e,
                        Fix = "SLL Mode failed, fix template or investigate why SLL is failing",
                        Position = new BlockPosition(0, 0)
                    });
                }

                if (needRetryIfFailed && context.Errors.Count > 0)
                {
                    tree = ParseDiagnosticMode(stream, parser, syntaxErrorListener);
                }
            }
            else
            {
                parser.Interpreter.PredictionMode = PredictionMode.LL_EXACT_AMBIG_DETECTION;
                tree = parser.ttl();
            }

            if (context.Errors.Count > 0)
            {
                compileContext.CompileErrors.AddRange(context.Errors);
                return tree.GetText();
            }

            var walker = new ParseTreeWalker();
            //context.DefenitionsOnly = loadDefenitionsOnly;
            var listener = new TtlMainListener(context, compileContext);

            listener.CurrentParseContext.SkippedTokens.AddRange(
                tokens.GetTokens()
                    .Where(t => t.Channel == Lexer.Hidden)
                    .Select(t => new BlockPosition(t)));
            
            walker.Walk(listener, tree);

            //context.DefenitionsOnly = false;
            return tree.GetText();
        }

        private static TtlParser.TtlContext ParseDiagnosticMode(AntlrInputStream stream, TtlParser parser,
            TtlSyntaxErrorListener syntaxErrorListener)
        {
            stream.Reset();
            parser.Reset();
            syntaxErrorListener.Clear();
            parser.Interpreter.PredictionMode = PredictionMode.LL_EXACT_AMBIG_DETECTION;
            return parser.ttl();
        }
    }
}