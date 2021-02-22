define(function(require, exports, module) {
    "use strict";
    var ErrorListener = require('antlr4/error/ErrorListener').ErrorListener;
    var ParseContext = require("./ParseContext").ParseContext;

    function TtlErrorListener(context) {
        ErrorListener.call(this);
        this.context = context;
        return this;
    }

    TtlErrorListener.prototype = Object.create(ErrorListener.prototype);
    TtlErrorListener.prototype.constructor = TtlErrorListener;

    TtlErrorListener.INSTANCE = new TtlErrorListener(new ParseContext());

    TtlErrorListener.prototype.syntaxError = function(recognizer, offendingSymbol, line, column, msg, e) {
        this.context.addError(msg, e, offendingSymbol);
    }

    exports.TtlErrorListener = TtlErrorListener;
});