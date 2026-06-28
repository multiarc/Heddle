"use strict";
import {CharStreams} from "./antlr4/index.web";
import {CommonTokenStream} from "./antlr4/index.web";
import {ParseContext} from "./ParseContext";
import {HeddleLexerExtended} from "./HeddleLexerExtended";
import {HeddleParserExtended} from "./HeddleParserExtended";

export class DocumentParser {
    constructor(inputDocument) {
        const input = new CharStreams.fromString(inputDocument);
        this.context = new ParseContext();
        this.lexer = new HeddleLexerExtended(input, this.context);
        const tokenStream = new CommonTokenStream(this.lexer);
        this.parser = new HeddleParserExtended(tokenStream, this.context);
        this.parser.buildParseTrees = false;
    }
    parseGetErrors() {
        this.parser.heddle();
        return this.parser.context.errors;
    };
}