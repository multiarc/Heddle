"use strict";
var ErrorListener = require('antlr4/error/ErrorListener').ErrorListener;
var ParseContext = require("./ParseContext").ParseContext;

class TtlErrorListener extends ErrorListener {
    constructor(context) {
        super(new ParseContext());
        this.context = context;
        this.INSTANCE = this;
    }

    syntaxError(recognizer, offendingSymbol, line, column, msg, e) {
        this.context.addError(msg, e, offendingSymbol);
    }
}

exports.TtlErrorListener = TtlErrorListener;
