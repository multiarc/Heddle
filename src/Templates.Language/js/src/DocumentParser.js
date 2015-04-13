define(function(require, exports, module) {
    "use strict";
    var InputStream = require("antlr4/InputStream").InputStream;
    var CommonTokenStream = require("antlr4/CommonTokenStream").CommonTokenStream;
    var TtlLexerExtended = require("./TtlLexerExtended").TtlLexerExtended;
    var TtlParserExtended = require("./TtlParserExtended").TtlParserExtended;
    var ParseContext = require("./ParseContext").ParseContext;

    function DocumentParser(inputDocument) {
        var input = new InputStream(inputDocument);
        this.context = new ParseContext();
        this.lexer = new TtlLexerExtended(input, this.context);
        var tokenStream = new CommonTokenStream(this.lexer);
        this.parser = new TtlParserExtended(tokenStream, this.context);
        this.parser.buildParseTrees = false;
        return this;
    }

    DocumentParser.prototype.parseGetErrors = function() {
        this.parser.ttl();
        return this.parser.context.errors;
    };

    exports.DocumentParser = DocumentParser;
});