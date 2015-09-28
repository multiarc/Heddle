define(function(require, exports, module) {
"use strict";

var Range = require("../range").Range;

var TTLMatchingBraceOutdent = function () { };

(function() {

    this.checkOutdent = function(line, input) {
        if (! /^\s+$/.test(line))
            return false;

        return /^(\s*\}\})|(\s*\%\>)/.test(input);
    };

    this.autoOutdent = function(doc, row) {
        var line = doc.getLine(row);
        var match = line.match(/^(\s*\}\})|(\s*\%\>)/);

        if (!match) return 0;

        var column = match[1].length;
        var openBracePos = doc.findMatchingBracket({row: row, column: column});

        if (!openBracePos || openBracePos.row == row) return 0;

        var indent = this.$getIndent(doc.getLine(openBracePos.row));
        doc.replace(new Range(row, 0, row, column-1), indent);
    };

    this.$getIndent = function(line) {
        return line.match(/^\s*/)[0];
    };

}).call(TTLMatchingBraceOutdent.prototype);

exports.TTLMatchingBraceOutdent = TTLMatchingBraceOutdent;
});
