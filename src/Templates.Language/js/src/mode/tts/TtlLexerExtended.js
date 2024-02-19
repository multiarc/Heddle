"use strict";

import TtlLexer from "./TtlLexer";
import {TtlErrorListener} from "./TtlErrorListener";

export class TtlLexerExtended extends TtlLexer {
    constructor(input, context) {
        super(input);
        this.context = context;
        this._listeners = [];
        this.removeErrorListeners();
        this.addErrorListener(new TtlErrorListener(context));
    }
}