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
    "ifnot",
    "elif",
    "elseif",
    "else",
    "profile",
    "raw",
    "import",
    "using",
    "for"
];

// Native-expression built-in functions (FunctionRegistry.Default).
//
// MAINTENANCE COUPLING: this list mirrors the default whitelist seeded by the
// C# engine in src/Heddle/Runtime/Expressions/BuiltInFunctions.cs
// (CreateEntries) and documented on FunctionRegistry.Default. Adding or
// removing a built-in in that C# source requires updating this list so the Ace
// completer keeps offering the correct set. Names are the distinct registered
// names (overloads collapsed).
var nativeFunctions = [
    "upper",
    "lower",
    "trim",
    "len",
    "contains",
    "startswith",
    "endswith",
    "replace",
    "substr",
    "format",
    "str",
    "abs",
    "min",
    "max",
    "round",
    "floor",
    "ceil",
    "range"
];

function is(token, type) {
    return token.type == type;
}

var HeddleCompletions = function() {

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
        var extensions = defaultExtensions.map(function (name) {
            return {
                caption: name,
                snippet: name + '($0)',
                meta: "variable",
                score: Number.MAX_VALUE
            };
        });
        var functions = nativeFunctions.map(function (name) {
            return {
                caption: name,
                snippet: name + '($0)',
                meta: "function",
                score: Number.MAX_VALUE
            };
        });
        return extensions.concat(functions);
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

}).call(HeddleCompletions.prototype);

exports.HeddleCompletions = HeddleCompletions;
