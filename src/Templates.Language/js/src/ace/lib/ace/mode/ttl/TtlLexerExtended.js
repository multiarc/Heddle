define(function(require, exports, module) {
    "use strict";
    var TtlLexer = require("./TtlLexer").TtlLexer;
    var TtlErrorListener = require("./TtlErrorListener").TtlErrorListener;

    function TtlLexerExtended(input, context) {
        TtlLexer.call(this, input);
        this.context = context;
        this._previousMode = -1;
        this._listeners = [];
        this.addErrorListener(new TtlErrorListener(context));
        return this;
    }

    TtlLexerExtended.prototype = Object.create(TtlLexer.prototype);
    TtlLexerExtended.prototype.constructor = TtlLexerExtended;

    exports.TtlLexerExtended = TtlLexerExtended;
});