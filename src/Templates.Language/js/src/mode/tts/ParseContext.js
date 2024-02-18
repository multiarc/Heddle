"use strict";

var LexerNoViableAltException = require('antlr4/error/Errors').LexerNoViableAltException;

function ParseContext() {
    this.errors = [];
    return this;
}

ParseContext.prototype.addError = function (msg, e, token) {
    if (!e)
        return;
    if (e && e instanceof LexerNoViableAltException) {
        this.errors.push({
            message: msg,
            exception: e,
            position: {
                startIndex: e.startIndex
            }
        });
    } else if (e && e.startToken) {
        this.errors.push({
            message: msg,
            exception: e,
            position: {
                startIndex: e.startToken.start,
                length: e.startToken.stop - e.startToken.start + 1
            }
        });
    } else if (token) {
        this.errors.push({
            message: msg,
            exception: e,
            position: {
                startIndex: token.start,
                length: token.stop - token.start + 1
            }
        });
    }
}

exports.ParseContext = ParseContext;
