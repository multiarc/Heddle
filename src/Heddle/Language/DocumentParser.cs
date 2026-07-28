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
            parser.RemoveErrorListeners();
            parser.AddErrorListener(syntaxErrorListener);

            // Bounds nesting for everything below — the parser's own descent, the tree walk, and the AST and chain
            // builders, all of which recurse over a structure this keeps shallow enough to survive.
            // A parse that began inside a host's import reader is its own, not part of the one in flight; without
            // this it inherited the outer import stack and reported a cycle that was not there.
            var outerImportState = ImportParseState.BeginIsolatedParse();
            ImportParseState.Current.BeginTopLevelParse();
            var depthGuard = new ParseDepthGuard();
            parser.AddParseListener(depthGuard);
            try
            {
                return ParseBounded(context, settings, parser, syntaxErrorListener, stream, tokens, errorFrom,
                    depthGuard);
            }
            catch (ParseDepthExceededException)
            {
                context.Errors.Add(new HeddleCompileError
                {
                    Error = "Template is nested too deeply to compile (limit " + ParseDepthGuard.MaxDepth +
                            " levels). Reduce the depth of the expression, chain, or block nesting.",
                    Position = new BlockPosition(0, 0),
                    DiagnosticId = HeddleDiagnosticIds.TemplateNestedTooDeeply
                });
                return string.Empty;
            }
            finally
            {
                ImportParseState.EndIsolatedParse(outerImportState);
            }
        }

        private static string ParseBounded(ParseContext context, ParserSettings settings,
            HeddleParser parser, HeddleSyntaxErrorListener syntaxErrorListener, AntlrInputStream stream,
            CommonTokenStream tokens, int errorFrom, ParseDepthGuard depthGuard)
        {
            HeddleParser.HeddleContext tree;
            try
            {
                tree = RunParse(context, settings, parser, syntaxErrorListener, stream, errorFrom, depthGuard);
            }
            catch (InvalidOperationException)
            {
                // The lexer's mode stack underflows on unbalanced closers — '@(1)}}' is enough — and the exception
                // escaped the compile instead of becoming an error. Malformed input is the one thing a template
                // engine is guaranteed to be handed, and the contract is that it produces a diagnostic.
                context.Errors.Add(new HeddleCompileError
                {
                    Error = "Unbalanced or unexpected token; the template could not be tokenized.",
                    Position = new BlockPosition(0, 0),
                    DiagnosticId = HeddleDiagnosticIds.SyntaxError
                });
                return string.Empty;
            }

            // Before anything recurses over the tree — and GetText() below does, which is why this cannot sit after
            // the error return. A left-associative run is invisible to the parse listener (ANTLR loops it), so this
            // is the only bound that sees it, and skipping it for a document that also had a syntax error handed the
            // overflow straight back. An editor's document has a syntax error most of the time.
            ParseDepthGuard.EnsureTreeWithinLimit(tree);

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

        /// <summary>The parse itself, kept separate so a tokenizer fault is distinguishable from a fault in the
        /// walk that follows it — the walk runs engine code whose exceptions must not be mistaken for bad input.</summary>
        private static HeddleParser.HeddleContext RunParse(ParseContext context, ParserSettings settings,
            HeddleParser parser, HeddleSyntaxErrorListener syntaxErrorListener, AntlrInputStream stream,
            int errorFrom, ParseDepthGuard depthGuard)
        {
            HeddleParser.HeddleContext tree;
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
                    tree = ParseDiagnosticMode(stream, parser, syntaxErrorListener, depthGuard);
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
                    tree = ParseDiagnosticMode(stream, parser, syntaxErrorListener, depthGuard);
                }
            }
            else
            {
                parser.Interpreter.PredictionMode = PredictionMode.LL_EXACT_AMBIG_DETECTION;
                tree = parser.heddle();
            }

            return tree;
        }

        private static HeddleParser.HeddleContext ParseDiagnosticMode(AntlrInputStream stream, HeddleParser parser,
            HeddleSyntaxErrorListener syntaxErrorListener, ParseDepthGuard depthGuard)
        {
            stream.Reset();
            parser.Reset();
            // The second attempt of a two-stage parse shares the guard with the first, whose count was abandoned
            // partway when the attempt failed.
            depthGuard.Reset();
            syntaxErrorListener.Clear();
            parser.Interpreter.PredictionMode = PredictionMode.LL_EXACT_AMBIG_DETECTION;
            return parser.heddle();
        }
    }
}
