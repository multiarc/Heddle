"use strict";
import {NoViableAltException} from "./antlr4/index.web";
import LexerNoViableAltException from "./antlr4/error/LexerNoViableAltException";
export class ParseContext {
    constructor() {
        this.errors = [];
    }

    addError(msg, e, token, line, column) {
        // Always record a positioned error (the engine's HeddleSyntaxErrorListener
        // never swallows a syntaxError callback and never throws). ANTLR reports
        // extraneous-input / missing-token recoveries with a null exception `e`
        // but a non-null offending `token`; those must still surface as errors.
        let startIndex = null;
        let length = null;
        if (e && (e instanceof NoViableAltException || e instanceof LexerNoViableAltException)
            && typeof e.startIndex === "number") {
            startIndex = e.startIndex;
        } else if (e && e.startToken && typeof e.startToken.start === "number") {
            startIndex = e.startToken.start;
            length = e.startToken.stop - e.startToken.start + 1;
        } else if (e && e.offendingToken && typeof e.offendingToken.start === "number") {
            startIndex = e.offendingToken.start;
            length = e.offendingToken.stop - e.offendingToken.start + 1;
        } else if (token && typeof token.start === "number") {
            startIndex = token.start;
            length = token.stop - token.start + 1;
        }
        this.errors.push({
            message: msg || 'syntax error',
            exception: e,
            position: {
                startIndex: startIndex,
                length: length,
                line: line,
                column: column
            }
        });
    }
}