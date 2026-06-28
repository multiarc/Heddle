"use strict";

var Range = require("../range").Range;

var TTLMatchingBraceOutdent = function () {
};

(function () {

    this.findMatchingBracket = function (doc, position, chr) {
        if (position.column == 0) return null;

        var charBeforeCursor = chr || doc.getLine(position.row).charAt(position.column - 1);
        if (charBeforeCursor == "") return null;

        var match = charBeforeCursor.match(/({{|@%|\()|(}}|%@|\))/);
        if (!match)
            return null;

        if (match[1])
            return doc.$findClosingBracket(match[1], position);
        else
            return doc.$findOpeningBracket(match[2], position);
    };

    this.checkOutdent = function (line, input) {
        if (!/^\s+$/.test(line))
            return false;

        return /^(\s*}})|(\s*%@)|(\s*\))/.test(input);
    };

    this.autoOutdent = function (doc, row) {
        var line = doc.getLine(row);
        var match = line.match(/^(\s*}}|\s*%@|\s*\))/);

        if (!match) return 0;

        var column = match[1].length;
        var openBracePos = this.findMatchingBracket(doc, {row: row, column: column});

        if (!openBracePos || openBracePos.row == row) return 0;

        var indent = this.$getIndent(doc.getLine(openBracePos.row));
        doc.replace(new Range(row, 0, row, column - 1), indent);
    };

    this.$getIndent = function (line) {
        return line.match(/^\s*/)[0];
    };

}).call(TTLMatchingBraceOutdent.prototype);

exports.TTLMatchingBraceOutdent = TTLMatchingBraceOutdent;
