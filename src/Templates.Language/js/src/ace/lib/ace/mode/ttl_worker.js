define(function (require, exports, module) {
    "use strict";
    
    var oop = require("../lib/oop");
    var Mirror = require("../worker/mirror").Mirror;

    var TtlWorker = exports.TtlWorker = function(sender) {
        Mirror.call(this, sender);
        this.setTimeout(500);
        this.setOptions();
    };

    oop.inherits(TtlWorker, Mirror);

    // load nodejs compatible require
    // var ace_require = require;
    // window.require = undefined; // prevent error: "Honey: 'require' already defined in global scope"
    // var Honey = { 'requirePath': ['..'] }; // walk up to js folder, see Honey docs
    // importScripts(window.location.protocol + "://" + window.location.host + "/lib/require.js");
    // var antlr4_require = window.require;
    // window.require = require = ace_require;

    var DocumentParser;
    try {
        //window.require = antlr4_require;
        window.antlr4 = require("./ttl/antlr4/index");
        DocumentParser = require("./ttl/DocumentParser").DocumentParser;
    } finally {
        //window.require = ace_require;
    }

    (function() {
        this.setOptions = function(options) {
            this.options = options || {};
            this.doc.getValue() && this.deferredUpdate.schedule(100);
        };

        this.changeOptions = function(newOptions) {
            oop.mixin(this.options, newOptions);
            this.doc.getValue() && this.deferredUpdate.schedule(100);
        };

        this.onUpdate = function() {
            var value = this.doc.getValue();
            var parser = new DocumentParser(value);
            var results = parser.parseGetErrors();
            var errors = [];
            for (var i = 0; i < results.length; i++) {
                var error = results[i];
                if (!error || error.position === null)
                    continue;
                var position = this.doc.indexToPosition(error.position.startIndex);
                errors.push({
                    row: position.row,
                    column: position.column,
                    text: error.message,
                    type: "error"
                });
            }
            this.sender.emit("annotate", errors);
        };

    }).call(TtlWorker.prototype);
});