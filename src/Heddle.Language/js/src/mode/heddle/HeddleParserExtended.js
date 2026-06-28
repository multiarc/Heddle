"use strict";

import HeddleParser from "./HeddleParser";
import {HeddleErrorListener} from "./HeddleErrorListener";

export class HeddleParserExtended extends HeddleParser {
    constructor(input, context) {
        super(input);
        this.context = context;
        this._listeners = [];
        this.removeErrorListeners();
        this.addErrorListener(new HeddleErrorListener(context));
    }
}