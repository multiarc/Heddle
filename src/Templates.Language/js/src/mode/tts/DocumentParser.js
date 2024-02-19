"use strict";
import {CharStream} from "./antlr4/index.web";
import {CommonTokenStream} from "./antlr4/index.web";
import {ParseContext} from "./ParseContext";
import {TtlLexerExtended} from "./TtlLexerExtended";
import {TtlParserExtended} from "./TtlParserExtended";

export class DocumentParser {
    constructor(inputDocument) {
        const input = new CharStream(inputDocument);
        this.context = new ParseContext();
        this.lexer = new TtlLexerExtended(input, this.context);
        const tokenStream = new CommonTokenStream(this.lexer);
        this.parser = new TtlParserExtended(tokenStream, this.context);
        this.parser.buildParseTrees = false;
    }
    parseGetErrors() {
        try {
            this.parser.ttl();
        } catch {
            //skip direct throws
        }
        return this.parser.context.errors;
    };
}