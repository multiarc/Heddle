"use strict";
import {ErrorListener} from "./antlr4/index.web";

export class HeddleErrorListener extends ErrorListener {
    constructor(context) {
        super();
        this.context = context;
        this.INSTANCE = this;
    }

    syntaxError(recognizer, offendingSymbol, line, column, msg, e) {
        this.context.addError(msg, e, offendingSymbol, line, column);
    }
}