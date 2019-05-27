using System;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
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
            AntlrInputStream stream = new AntlrInputStream(new StringReader(document));
            var lexer = context.ForceRemoveWhitespace ? (ITokenSource) new TtlLexerNoWS(stream) : new TtlLexer(stream);
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            TtlParser parser = new TtlParser(tokens);
            var syntaxErrorListener = new TtlSyntaxErrorListener(context);
            parser.Interpreter.PredictionMode = compileContext.Options.ProvideLanguageFeatures
                ? PredictionMode.LL_EXACT_AMBIG_DETECTION
                : PredictionMode.SLL;
            TtlParser.TtlContext tree;
            parser.RemoveErrorListeners();
            if (!compileContext.Options.ProvideLanguageFeatures)
            {
                try
                {
                    tree = parser.ttl();
                }
                catch (ParseCanceledException)
                {
                    stream.Reset();
                    parser.Reset();
                    parser.Interpreter.PredictionMode = PredictionMode.LL;
                    parser.AddErrorListener(syntaxErrorListener);
                    tree = parser.ttl();
                }
            }
            else
            {
                parser.AddErrorListener(syntaxErrorListener);
                tree = parser.ttl();
            }

            if (context.Errors.Count > 0)
            {
                compileContext.CompileErrors.AddRange(context.Errors);
                return tree.GetText();
            }

            ParseTreeWalker walker = new ParseTreeWalker();
            //context.DefenitionsOnly = loadDefenitionsOnly;
            TtlMainListener listener = new TtlMainListener(context, compileContext);

            listener.CurrentParseContext.CommentTokens.AddRange(
                tokens.GetTokens()
                    .Where(t => t.Channel == Lexer.Hidden)
                    .Select(t => new BlockPosition(t)));
            
            walker.Walk(listener, tree);

            //context.DefenitionsOnly = false;
            return tree.GetText();
        }
    }
}