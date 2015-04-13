using System;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Templates.Exceptions;
using Templates.Strings.Core;

namespace Templates.Language {
    /// <summary>
    /// Parses document and creates template cache that can be used multiple times as source template representation, also used to replace templates with data multiple times (template source preserved)
    /// </summary>
    public static class DocumentParser {
        /// <summary>
        /// Performs parse of document
        /// </summary>
        /// <returns>Full template context tree found in source template</returns>
        public static ParseContext Parse(string document)
        {
            var context = new ParseContext();
            Parse(document, context);
            return context;
        }

        public static void Parse(string document, ParseContext context, bool loadDefenitionsOnly = false)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            AntlrInputStream stream = new AntlrInputStream(new StringReader(document));
            TtlLexer lexer = new TtlLexer(stream);
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            TtlParser parser = new TtlParser(tokens);
            var syntaxErrorListener = new TtlSyntaxErrorListener(context);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(syntaxErrorListener);
            var tree = parser.ttl();
            if (context.Errors.Length > 0)
                throw new TemplateParseException(context.Errors);
            ParseTreeWalker walker = new ParseTreeWalker();
            context.DefenitionsOnly = loadDefenitionsOnly;
            TtlMainListener listener = new TtlMainListener(context);
            walker.Walk(listener, tree);
            context.DefenitionsOnly = false;
            listener.CurrentParseContext.CommentTokens.AddRange(
                tokens.GetTokens()
                    .Where(t => t.Channel == TtlLexer.COMMENT_CHANNEL)
                    .Select(t => new BlockPosition(t.StartIndex, t.StopIndex - t.StartIndex + 1)));
        }
    }
}