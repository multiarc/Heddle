using System;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// Parses documents into a reusable template cache representation.
    /// </summary>
    /// <remarks>
    /// ParserSettings-based methods are the shared front-end core compiled into both Heddle and Heddle.Generator.
    /// Runtime CompileContext adapters are in DocumentParser.Runtime.cs, which the generator does not compile.
    /// </remarks>
    public static partial class DocumentParser
    {
        /// <summary>
        /// Parses document against ParserSettings; front-end diagnostics accumulate on the returned <see cref="ParseContext"/>.
        /// </summary>
        public static ParseContext Parse(string document, ParserSettings settings, out string cleanDocument)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            var context = new ParseContext(provideLanguageFeatures: settings.ProvideLanguageFeatures);
            cleanDocument = Parse(document, context, settings);
            return context;
        }

        public static string Parse(string document, ParseContext context, ParserSettings settings)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var errorFrom = context.Errors.Count;
            var stream = new AntlrInputStream(new System.IO.StringReader(document));
            var lexer = new HeddleLexer(stream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new HeddleParser(tokens);
            var syntaxErrorListener = new HeddleSyntaxErrorListener(context);
            parser.Interpreter.PredictionMode = PredictionMode.SLL;
            HeddleParser.HeddleContext tree;
            parser.RemoveErrorListeners();
            parser.AddErrorListener(syntaxErrorListener);
            if (!settings.ProvideLanguageFeatures)
            {
                bool needRetryIfFailed = false;
                try
                {
                    tree = parser.heddle();
                    needRetryIfFailed = true;
                }
                catch (ParseCanceledException e)
                {
                    tree = ParseDiagnosticMode(stream, parser, syntaxErrorListener);
                    syntaxErrorListener.Context.Warnings.Add(new HeddleCompileWarning
                    {
                        Error = e.Message,
                        Exception = e,
                        Fix = "SLL Mode failed, fix template or investigate why SLL is failing",
                        Position = new BlockPosition(0, 0)
                    });
                }

                if (needRetryIfFailed && context.Errors.Count > errorFrom)
                {
                    tree = ParseDiagnosticMode(stream, parser, syntaxErrorListener);
                }
            }
            else
            {
                parser.Interpreter.PredictionMode = PredictionMode.LL_EXACT_AMBIG_DETECTION;
                tree = parser.heddle();
            }

            if (context.Errors.Count > errorFrom)
            {
                return tree.GetText();
            }

            var walker = new ParseTreeWalker();
            var listener = new HeddleMainListener(context, settings);

            listener.CurrentParseContext.SkippedTokens.AddRange(
                tokens.GetTokens()
                    .Where(t => t.Channel == Lexer.Hidden)
                    .Select(t => new BlockPosition(t)));

            walker.Walk(listener, tree);

            return tree.GetText();
        }

        private static HeddleParser.HeddleContext ParseDiagnosticMode(AntlrInputStream stream, HeddleParser parser,
            HeddleSyntaxErrorListener syntaxErrorListener)
        {
            stream.Reset();
            parser.Reset();
            syntaxErrorListener.Clear();
            parser.Interpreter.PredictionMode = PredictionMode.LL_EXACT_AMBIG_DETECTION;
            return parser.heddle();
        }
    }
}
