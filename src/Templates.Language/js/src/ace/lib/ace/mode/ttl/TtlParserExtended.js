define(function(require, exports, module) {
    "use strict";
    var TtlParser = require("./TtlParser").TtlParser;
    var TtlErrorListener = require("./TtlErrorListener").TtlErrorListener;

    function TtlParserExtended(input, context) {
        TtlParser.call(this, input);
        this.context = context;
        this._listeners = [];
        this.addErrorListener(new TtlErrorListener(context));
        return this;
    }

    TtlParserExtended.prototype = Object.create(TtlParser.prototype);
    TtlParserExtended.prototype.constructor = TtlParserExtended;

    exports.TtlParserExtended = TtlParserExtended;
});