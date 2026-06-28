"use strict";
import {NoViableAltException} from "./antlr4/index.web";
import LexerNoViableAltException from "./antlr4/error/LexerNoViableAltException";
export class ParseContext {
    constructor() {
        this.errors = [];
    }

    addError(msg, e, token, line, column) {
        if (!e)
            return;
        if (e && (e instanceof NoViableAltException || e instanceof LexerNoViableAltException)) {
            this.errors.push({
                message: msg || 'No viable alternative',
                exception: e,
                position: {
                    startIndex: e.startIndex,
                    line: line,
                    column: column
                }
            });
        } else if (e && e.startToken) {
            this.errors.push({
                message: msg,
                exception: e,
                position: {
                    startIndex: e.startToken.start,
                    length: e.startToken.stop - e.startToken.start + 1,
                    line: line,
                    column: column
                }
            });
        } else if (token) {
            this.errors.push({
                message: msg,
                exception: e,
                position: {
                    startIndex: token.start,
                    length: token.stop - token.start + 1,
                    line: line,
                    column: column
                }
            });
        }
    }
}