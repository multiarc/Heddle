define(function(require, exports, module) {
    "use strict";
    var TtlParser = require("./TtlParser").TtlParser;
    var TtlErrorListener = require("./TtlErrorListener").TtlErrorListener;
    
    class TtlParserExtended extends TtlParser {
        constructor(input, context) {
            super(input);
            this.context = context;
            this._listeners = [];
            this.addErrorListener(new TtlErrorListener(context));
        }
    }

    exports.TtlParserExtended = TtlParserExtended;
});