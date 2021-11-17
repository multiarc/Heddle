define(function(require, exports, module) {
    "use strict";
    var TtlLexer = require("./TtlLexer").TtlLexer;
    var TtlErrorListener = require("./TtlErrorListener").TtlErrorListener;

    class TtlLexerExtended extends TtlLexer
    {
        constructor(input, context) {
            super(input);
            this.context = context;
            this._listeners = [];
            this.addErrorListener(new TtlErrorListener(context));
        }
    }

    exports.TtlLexerExtended = TtlLexerExtended;
});