"use strict";
import {CharStreams} from "./antlr4/index.web";
import {CommonTokenStream} from "./antlr4/index.web";
import {ParseContext} from "./ParseContext";
import {TtlLexerExtended} from "./TtlLexerExtended";
import {TtlParserExtended} from "./TtlParserExtended";

export class DocumentParser {
    constructor(inputDocument) {
        const input = new CharStreams.fromString(inputDocument);
        this.context = new ParseContext();
        this.lexer = new TtlLexerExtended(input, this.context);
        const tokenStream = new CommonTokenStream(this.lexer);
        this.parser = new TtlParserExtended(tokenStream, this.context);
        this.parser.buildParseTrees = false;
    }
    parseGetErrors() {
        this.parser.ttl();
        return this.parser.context.errors;
    };
}