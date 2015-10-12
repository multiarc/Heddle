define(function(require, exports, module) {
"use strict";

var TokenIterator = require("../token_iterator").TokenIterator;

var defaultExtensions = [
    "date",
    "time",
    "html",
    "guid",
    "int",
    "list",
    "model",
    "money",
    "out",
    "param",
    "partial",
    "string",
    "if",
    "else",
    "import",
    "using",
    "for"
];

function is(token, type) {
    return token.type == type;
}

var TtlCompletions = function() {

};

(function() {

    this.getCompletions = function(state, session, pos, prefix) {
        var token = session.getTokenAt(pos.row, pos.column);

        if (!token)
            return [];

        if (is(token, "out-start"))
            return this.getExtensionCompletions(state, session, pos, prefix);

        if (is(token, "def_override"))
            return this.getExtensionOverrideCompletions(state, session, pos, prefix);

        return [];
    };

    this.getExtensionCompletions = function (state, session, pos, prefix) {
        return defaultExtensions.map(function (name) {
            return {
                caption: name,
                snippet: name + '($0)',
                meta: "variable",
                score: Number.MAX_VALUE
            };
        });
    };
    this.getExtensionOverrideCompletions = function (state, session, pos, prefix) {
        return defaultExtensions.map(function (name) {
            return {
                caption: name,
                snippet: name,
                meta: "variable",
                score: Number.MAX_VALUE
            };
        });
    };

}).call(TtlCompletions.prototype);

exports.TtlCompletions = TtlCompletions;
});
