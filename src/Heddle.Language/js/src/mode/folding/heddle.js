"use strict";

var oop = require("../../lib/oop");
var Range = require("../../range").Range;
var BaseFoldMode = require("./fold_mode").FoldMode;
var TokenIterator = require("../../token_iterator").TokenIterator;

var FoldMode = exports.FoldMode = function () {
    BaseFoldMode.call(this);
};
oop.inherits(FoldMode, BaseFoldMode);

var tokenTable = {
    "@%": "%@",
    "{{": "}}"
};

var reverseTokenTable = {
    "%@": "@%",
    "}}": "{{"
};

var Block = function () {
    this.token = "";
    this.closing = false;
    this.start = {row: 0, column: 0};
    this.end = {row: 0, column: 0};
};

(function () {

    this.getFoldWidget = function (session, foldStyle, row) {
        var block = this._findStartBlock(session, row);

        if (!block)
            return this.getCommentFoldWidget(session, row);

        if (!block.token
            || this._findEndBlock(session, row, block.token, block.end.column))
            return "";

        return "start";
    };

    this.getCommentFoldWidget = function (session, row) {
        if (/comment/.test(session.getState(row)) && /@\*/.test(session.getLine(row)))
            return "start";
        return "";
    };

    this._findCommentEndBlock = function (session, row) {
        var tokens = session.getTokens(row);

        var start = {
            row: row,
            column: 0
        };

        for (var i = 0; i < tokens.length; i++) {
            var startToken = tokens[i];
            if (startToken.value === "@*" && startToken.type.indexOf("comment.start") !== -1) {
                start.column += startToken.value.length;
                break;
            }
            start.column += startToken.value.length;
        }

        row++;
        var iterator = new TokenIterator(session, row, 0);
        var token;

        while (token = iterator.getCurrentToken()) {
            if (token.value === "*@" && token.type.indexOf("comment.end") !== -1) {
                return {
                    start: start,
                    end: {
                        row: iterator.getCurrentTokenRow(),
                        column: iterator.getCurrentTokenColumn()
                    }
                }
            }
            iterator.stepForward();
        }

        return null;
    };

    this._findStartBlock = function (session, row) {
        var tokens = session.getTokens(row);
        var block = new Block();
        block.end.row = row;
        block.start.row = row;

        for (var i = 0; i < tokens.length; i++) {
            var token = tokens[i];
            if (tokenTable[token.value] && token.type.indexOf("keyword.operator.paren.lparen") !== -1) {
                block.end.column = block.start.column + token.value.length;
                block.token = token.value;
                for (i++; i < tokens.length; i++) {
                    token = tokens[i];
                    block.end.column += token.value.length;
                    if (reverseTokenTable[token.value] && token.type.indexOf("keyword.operator.paren.rparen") !== -1) {
                        return null;
                    }
                }
                return block;
            }
            block.start.column += token.value.length;
        }

        return null;
    };

    this._findEndBlock = function (session, row, startToken, startColumn) {
        var tokens = session.getTokens(row);
        var column = 0;
        for (var i = 0; i < tokens.length; i++) {
            var token = tokens[i];
            column += token.value.length;
            if (column < startColumn)
                continue;
            if (reverseTokenTable[token.value] === startToken && token.type.indexOf("keyword.operator.paren.rparen") !== -1) {
                return true;
            }
        }
        return false;
    };

    this._readBlockForward = function (iterator, startToken) {
        var endToken = tokenTable[startToken];
        var token = iterator.getCurrentToken();
        if (!token)
            return null;

        var block = new Block();
        do {
            block.token = token.value;

            if (token.value === startToken && token.type.indexOf("keyword.operator.paren.lparen") !== -1) {
                block.start.row = iterator.getCurrentTokenRow();
                block.start.column = iterator.getCurrentTokenColumn();
                block.end.row = iterator.getCurrentTokenRow();
                block.end.column = iterator.getCurrentTokenColumn() + token.value.length;
                iterator.stepForward();
                return block;
            } else if (token.value === endToken && token.type.indexOf("keyword.operator.paren.rparen") !== -1) {
                block.start.row = iterator.getCurrentTokenRow();
                block.start.column = iterator.getCurrentTokenColumn();
                block.end.row = iterator.getCurrentTokenRow();
                block.end.column = iterator.getCurrentTokenColumn() + token.value.length;
                block.closing = true;
                iterator.stepForward();
                return block;
            }
        } while (token = iterator.stepForward());

        return null;
    };

    this._readBlockBackward = function (iterator, endBlock) {
        var token = iterator.getCurrentToken();
        if (!token)
            return null;

        var block = new Block();
        do {
            block.token = token.value;

            if (tokenTable[token.value] && token.type.indexOf("keyword.operator.paren.lparen") !== -1) {
                block.start.row = iterator.getCurrentTokenRow();
                block.start.column = iterator.getCurrentTokenColumn();
                iterator.stepBackward();
                return block;
            } else if (reverseTokenTable[token.value] && token.type.indexOf("keyword.operator.paren.rparen") !== -1) {
                block.end.row = iterator.getCurrentTokenRow();
                block.end.column = iterator.getCurrentTokenColumn() + token.value.length;
            }
        } while (token = iterator.stepBackward());

        return null;
    };

    this._pop = function (stack, block) {
        if (stack.length) {
            var top = stack[stack.length - 1];
            if (!block
                || tokenTable[top.token] === block.token
                || reverseTokenTable[top.token] === block.token) {
                return stack.pop();
            } else {
                return null;
            }
        }
        return null;
    };

    this.getFoldWidgetRange = function (session, foldStyle, row) {
        var startBlock = this._findStartBlock(session, row);

        if (!startBlock) {
            var commentBlock = this._findCommentEndBlock(session, row);
            if (commentBlock) {
                return Range.fromPoints(commentBlock.start, commentBlock.end);
            }

            return "";
        }

        var stack = [];
        var block;

        var iterator = new TokenIterator(session, row, startBlock.end.column);
        var start = {
            row: row,
            column: startBlock.start.column + startBlock.token.length + 1
        };
        if (startBlock.start.row === startBlock.end.row)
            start.column = startBlock.end.column;
        while (block = this._readBlockForward(iterator, startBlock.token)) {
            if (block.closing) {
                this._pop(stack, block);
                if (stack.length === 0)
                    return Range.fromPoints(start, block.start);
            } else {
                stack.push(block);
            }
        }
    };

}).call(FoldMode.prototype);
