"use strict";

import HeddleLexer from "./HeddleLexer";
import {HeddleErrorListener} from "./HeddleErrorListener";

export class HeddleLexerExtended extends HeddleLexer {
    constructor(input, context) {
        super(input);
        this.context = context;
        this._listeners = [];
        this.removeErrorListeners();
        this.addErrorListener(new HeddleErrorListener(context));
    }
}