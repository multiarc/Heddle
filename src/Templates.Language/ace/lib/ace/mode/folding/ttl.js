define(function (require, exports, module) {
    "use strict";

    var oop = require("../../lib/oop");
    var Range = require("../../range").Range;
    var BaseFoldMode = require("./fold_mode").FoldMode;

    var FoldMode = exports.FoldMode = function() {
        BaseFoldMode.call(this);
    };
    oop.inherits(FoldMode, BaseFoldMode);

    (function () {

        this.foldingStartMarker = /(\{\{|\<\%)((?:[^\}]|\}[^\}])|(?:[^\%]|\%[^\>]))*$/;
        this.foldingStopMarker = /^((?:[^\{]|\{[^\{])|(?:[^\<]|\<[^\%]))*/;
        this._getFoldWidgetBase = this.getFoldWidget;
        this.getFoldWidget = function (session, foldStyle, row) {
            var line = session.getLine(row);
            var match = line.match(this.foldingStartMarker);
            if (match) {
                if (match[1])
                    "start";
            }
            match = line.match(this.foldingStopMarker);
            if (match) {
                if (match[1])
                    "end";
            }
            return "";
        };

        this.getFoldWidgetRange = function (session, foldStyle, row, forceMultiline) {
            var line = session.getLine(row);

            var match = line.match(this.foldingStartMarker);
            if (match) {
                var i = match.index;

                if (match[1])
                    return this.openingBracketBlock(session, match[1], row, i);
                return "";
            }
            var match = line.match(this.foldingStopMarker);
            if (match) {
                var i = match.index + match[0].length;

                if (match[1])
                    return this.closingBracketBlock(session, match[1], row, i);

                return "";
            }
        };

        this.getSectionRange = function (session, row) {
            var line = session.getLine(row);
            var startIndent = line.search(/\S/);
            var startRow = row;
            var startColumn = line.length;
            row = row + 1;
            var endRow = row;
            var maxRow = session.getLength();
            while (++row < maxRow) {
                line = session.getLine(row);
                var indent = line.search(/\S/);
                if (indent === -1)
                    continue;
                if (startIndent > indent)
                    break;
                var subRange = this.getFoldWidgetRange(session, "all", row);

                if (subRange) {
                    if (subRange.start.row <= startRow) {
                        break;
                    } else if (subRange.isMultiLine()) {
                        row = subRange.end.row;
                    } else if (startIndent == indent) {
                        break;
                    }
                }
                endRow = row;
            }

            return new Range(startRow, startColumn, endRow, session.getLine(endRow).length);
        };
    }).call(FoldMode.prototype);

});