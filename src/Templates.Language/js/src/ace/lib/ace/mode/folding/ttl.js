define(function (require, exports, module) {
    "use strict";

    var oop = require("../../lib/oop");
    var Range = require("../../range").Range;
    var BaseFoldMode = require("./fold_mode").FoldMode;
    var TokenIterator = require("../../token_iterator").TokenIterator;

    var FoldMode = exports.FoldMode = function() {
        BaseFoldMode.call(this);
    };
    oop.inherits(FoldMode, BaseFoldMode);
    
    var tokenTable = {
        "(": ")",
        "@%": "%@",
        "{{": "}}",
        "<": ">",
        "@{": "}@"
    };

    var Block = function() {
        this.token = "";
        this.closing = false;
        this.start = {row: 0, column: 0};
        this.end = {row: 0, column: 0};
    };

    function is(token, type) {
        return token.type.lastIndexOf(type) > -1;
    }

    (function () {

        this.getFoldWidget = function(session, foldStyle, row) {
            var block = this._findStartBlock(session, row);

            if (!block)
                return this.getCommentFoldWidget(session, row);

            if (!block.token)
                return "";

            if (this._findEndBlock(session, row, block.token, block.end.column))
                return "";

            return "start";
        };

        this.getCommentFoldWidget = function(session, row) {
            if (/@*/.test(session.getState(row)) && !/\*@/.test(session.getLine(row)))
                return "start";
            return "";
        };

        this._findStartBlock = function(session, row) {
            var tokens = session.getTokens(row);
            var block = new Block();

            for (var i = 0; i < tokens.length; i++) {
                var token = tokens[i];
                if (is(token, "paren.lparen")) {
                    block.end.column = block.start.column + token.value.length;
                    block.token = token.value;
                    for (i++; i < tokens.length; i++) {
                        token = tokens[i];
                        block.end.column += token.value.length;
                        if (is(token, "paren.rparen")) {
                            break;
                        }
                    }
                    return block;
                } else if (is(token, "paren.rparen")) {
                    return block;
                }
                block.start.column += token.value.length;
            }

            return null;
        };

        this._findEndBlock = function(session, row, startToken, startColumn) {
            var tokens = session.getTokens(row);
            var expectedEndToken = tokenTable[startToken];
            var column = 0;
            for (var i = 0; i < tokens.length; i++) {
                var token = tokens[i];
                column += token.value.length;
                if (column < startColumn)
                    continue;
                if (is(token, "paren.rparen")) {
                    token = tokens[i + 1];
                    if (token && token.value === expectedEndToken)
                        return true;
                }
            }
            return false;
        };

        /*
         * reads a full tag and places the iterator after the tag
         */
        this._readBlockForward = function(iterator) {
            var token = iterator.getCurrentToken();
            if (!token)
                return null;

            var tag = new Block();
            do {
                tag.token = token;
                
                if (is(token, "paren.")) {
                    tag.closing = is(token, "paren.rparen");
                    tag.start.row = iterator.getCurrentTokenRow();
                    tag.start.column = iterator.getCurrentTokenColumn();
                } else if (is(token, "paren.rparen")) {
                    tag.end.row = iterator.getCurrentTokenRow();
                    tag.end.column = iterator.getCurrentTokenColumn() + token.value.length;
                    iterator.stepForward();
                    return tag;
                }
            } while(token = iterator.stepForward());

            return null;
        };

        this._readBlockBackward = function(iterator) {
            var token = iterator.getCurrentToken();
            if (!token)
                return null;

            var tag = new Block();
            do {
                tag.token = token.value;
                
                if (is(token, "paren.")) {
                    tag.closing = is(token, "paren.rparen");
                    tag.start.row = iterator.getCurrentTokenRow();
                    tag.start.column = iterator.getCurrentTokenColumn();
                    iterator.stepBackward();
                    return tag;
                } else if (is(token, "paren.rparen")) {
                    tag.end.row = iterator.getCurrentTokenRow();
                    tag.end.column = iterator.getCurrentTokenColumn() + token.value.length;
                }
            } while(token = iterator.stepBackward());

            return null;
        };

        this._pop = function(stack, block) {
            if (stack.length) {
                var top = stack[stack.length-1];
                if (!block || top.token === block.token) {
                    return stack.pop();
                } else {
                    return null;
                }
            }
            return null;
        };

        this.getFoldWidgetRange = function(session, foldStyle, row) {
            var startBlock = this._findStartBlock(session, row);

            if (!startBlock) {
                return this.getCommentFoldWidget(session, row)
                    && session.getCommentFoldRange(row, session.getLine(row).length);
            }

            var isBackward = startBlock.closing;
            var stack = [];
            var block;

            if (!isBackward) {
                var iterator = new TokenIterator(session, row, startBlock.start.column);
                var start = {
                    row: row,
                    column: startBlock.start.column + startBlock.tagName.length + 2
                };
                if (startBlock.start.row === startBlock.end.row)
                    start.column = startBlock.end.column;
                while (block = this._readBlockForward(iterator)) {
                    if (block.closing) {
                        this._pop(stack, block);
                        if (stack.length === 0)
                            return Range.fromPoints(start, block.start);
                    }
                    else {
                        stack.push(block);
                    }
                }
            }
            else {
                var iterator = new TokenIterator(session, row, startBlock.end.column);
                var end = {
                    row: row,
                    column: startBlock.start.column
                };

                while (block = this._readBlockBackward(iterator)) {
                    if (!block.closing) {
                        this._pop(stack, block);
                        if (stack.length === 0) {
                            block.start.column += block.tagName.length + 2;
                            if (block.start.row === block.end.row && block.start.column < block.end.column)
                                block.start.column = block.end.column;
                            return Range.fromPoints(block.start, end);
                        }
                    }
                    else {
                        stack.push(block);
                    }
                }
            }

        };
        
    }).call(FoldMode.prototype);

});