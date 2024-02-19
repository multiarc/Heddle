"use strict";

import TtlParser from "./TtlParser";
import {TtlErrorListener} from "./TtlErrorListener";

export class TtlParserExtended extends TtlParser {
    constructor(input, context) {
        super(input);
        this.context = context;
        this._listeners = [];
        this.removeErrorListeners();
        this.addErrorListener(new TtlErrorListener(context));
    }
}