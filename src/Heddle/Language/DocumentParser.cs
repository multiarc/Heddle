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
    /// Parses document and creates template cache that can be used multiple times as source template representation, also used to replace templates with data multiple times (template source preserved).
    /// </summary>
    /// <remarks>
    /// The <see cref="ParserSettings"/>-based methods here are the shared front-end core (phase 7 D4): they carry no
    /// dependency on the runtime <c>CompileContext</c>, so this file compiles into the <c>Heddle.Generator</c>
    /// analyzer as a linked shared source. The runtime <c>CompileContext</c> adapters live in the sibling partial
    /// <c>DocumentParser.Runtime.cs</c>, which the generator does not compile.
    /// </remarks>
    public static partial class DocumentParser
    {
        /// <summary>
        /// Performs parse of document against a <see cref="ParserSettings"/> seam (the build-time / import-neutral
        /// entry). Front-end diagnostics accumulate on the returned <see cref="ParseContext"/>; no
        /// <c>CompileContext</c> is involved.
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
