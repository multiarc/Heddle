define(function(require, exports, module) {
    "use strict";
    var TtlLexer = require("./TtlLexer").TtlLexer;
    var TtlErrorListener = require("./TtlErrorListener").TtlErrorListener;

    function TtlLexerExtended(input, context) {
        TtlLexer.call(this, input);
        this.context = context;
        this._listeners = [];
        this.addErrorListener(new TtlErrorListener(context));
        return this;
    }

    TtlLexerExtended.prototype = Object.create(TtlLexer.prototype);
    TtlLexerExtended.prototype.constructor = TtlLexerExtended;

    TtlLexerExtended.prototype.emitToken = function(token) {
        var i, c, la;
        switch (token.type) {
            //@ext(...)sometext handling to interpret "sometext" as TEXT
            case TtlLexer.ID:
                if (this._mode === TtlLexer.OUT_SUB || this._mode === TtlLexer.OUT_MODE) {
                    i = -1 - (token.stop - token.start + 1);
                    la = this._input.LA(i);
                    c = String.fromCharCode(la);
                    while (la !== -1 && c !== ":" && c !== "@" && c !== "(" && c !== ")") {
                        i--;
                        la = this._input.LA(i);
                        c = String.fromCharCode(la);
                    }
                    if (c === ")") {
                        if (this._mode === TtlLexer.CALL)
                            this.popMode();
                        this.popMode();

                        token.type = TtlLexer.TEXT;
                    }
                }
                break;
        //@ext(...)(... handling to interpret "(..." as TEXT
        case TtlLexer.OUT_PARAMSTART:
            i = -1 - (token.stop - token.start + 1);
            la = this._input.LA(i);
            c = String.fromCharCode(la);
            while (la !== -1 && c !== ":" && c !== "@" && c !== "(" && c !== ")") {
                i--;
                la = this._input.LA(i);
                c = String.fromCharCode(la);
            }
            if (c === ")") {
                if (this._mode === TtlLexer.CALL)
                    this.popMode();
                this.popMode();

                token.type = TtlLexer.TEXT;
            }
            break;
        //@ext(...)WS*...sometext handling to interpret "\s...sometext" as TEXT
        case TtlLexer.OUT_WS:
            i = -1 - (token.stop - token.start + 1);
            la = this._input.LA(i);

            var prev = String.fromCharCode(la);
            while (la !== -1 && prev !== ":" && prev !== "@" && prev !== "(" && prev !== ")") {
                i--;
                la = this._input.LA(i);
                prev = String.fromCharCode(la);
            }
            i = 1;
            la = this._input.LA(i);
            c = String.fromCharCode(la);
            var nextLa = this._input.LA(i + 1);
            var nextC = String.fromCharCode(nextLa);
            if (la === -1 || c !== ":" && (c !== "{" || nextLa !== -1 && nextC !== "{") && prev === ")") {
                this.popMode();
                token.type = TtlLexer.TEXT;
            } else {
                token = this.nextToken();
            }
            break;
            case TtlLexer.COMMENT:
                if (this._mode === TtlLexer.OUT_SUB) {
                    i = -1 - (token.stop - token.start + 1);
                    la = this._input.LA(i);

                    prev = String.fromCharCode(la);
                    while (la !== -1 && prev !== ":" && prev !== "@" && prev !== '(' && prev !== ")") {
                        i--;
                        la = this._input.LA(i);
                        prev = String.fromCharCode(la);
                    }
                    i = 1;
                    la = this._input.LA(i);
                    c = String.fromCharCode(la);
                    nextLa = this._input.LA(i + 1);
                    nextC = String.fromCharCode(nextLa);
                    if (la === -1 || c !== ":" && (c !== "{" || nextLa === -1 || nextC !== "{") && prev === ")") {
                        this.popMode();
                        token.type = TtlLexer.SUB_COMMENT;
                    } else {
                        token = this.nextToken();
                    }
                }
                break;
        case TtlLexer.CSHARP_END:
            if (this._mode === TtlLexer.CALL) {
                this.popMode();
                token.type = TtlLexer.OUT_PARAMEND;
            } else {
                token.type = TtlLexer.CSHARP_TOKEN;
            }
            break;
        }

        TtlLexer.prototype.emitToken.call(this, token);
    };

    exports.TtlLexerExtended = TtlLexerExtended;
});