using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Templates.Collections;
using Templates.Data;
using Templates.Exceptions;
using Templates.Language;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templates.Runtime {
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
            if (document == null)
                throw new ArgumentNullException("document");
            AntlrInputStream stream = new AntlrInputStream(new StringReader(document));
            TtlLexer lexer = new TtlLexer(stream);
            CommonTokenStream tokens = new CommonTokenStream(lexer);
            TtlParser parser = new TtlParser(tokens);
            var tree = parser.ttl();
            ParseTreeWalker walker = new ParseTreeWalker();
            TtlListener listener = new TtlListener();
            walker.Walk(listener, tree);
            return listener.CurrentParseContext;
        }
    }
}