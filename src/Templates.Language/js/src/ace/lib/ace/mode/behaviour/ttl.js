define(function(require, exports, module) {
"use strict";

var oop = require("../../lib/oop");
var Behaviour = require("../behaviour").Behaviour;
var TokenIterator = require("../../token_iterator").TokenIterator;
var lang = require("../../lib/lang");

var SAFE_INSERT_IN_TOKENS =
    ["text", "paren.rparen", "punctuation.operator"];
var SAFE_INSERT_BEFORE_TOKENS =
    ["text", "paren.rparen", "punctuation.operator", "comment"];

var context;
var contextCache = {};
var initContext = function(editor) {
    var id = -1;
    if (editor.multiSelect) {
        id = editor.selection.index;
        if (contextCache.rangeCount != editor.multiSelect.rangeCount)
            contextCache = {rangeCount: editor.multiSelect.rangeCount};
    }
    if (contextCache[id])
        return context = contextCache[id];
    context = contextCache[id] = {
        autoInsertedBrackets: 0,
        autoInsertedRow: -1,
        autoInsertedLineEnd: "",
        maybeInsertedBrackets: 0,
        maybeInsertedRow: -1,
        maybeInsertedLineStart: "",
        maybeInsertedLineEnd: ""
    };
};

var getWrapped = function(selection, selected, opening, closing) {
    var rowDiff = selection.end.row - selection.start.row;
    return {
        text: opening + selected + closing,
        selection: [
                0,
                selection.start.column + 1,
                rowDiff,
                selection.end.column + (rowDiff ? 0 : 1)
            ]
    };
};

var TTLstyleBehaviour = function() {
    this.add("braces", "insertion", function(state, action, editor, session, text) {
        var cursor = editor.getCursorPosition();
        var line = session.doc.getLine(cursor.row);
        if (text == '{{') {
            initContext(editor);
            var selection = editor.getSelectionRange();
            var selected = session.doc.getTextRange(selection);
            if (selected !== "" && selected !== "{{" && editor.getWrapBehavioursEnabled()) {
                return getWrapped(selection, selected, '{{', '}}');
            } else if (TTLstyleBehaviour.isSaneInsertion(editor, session)) {
                if (/(\%\@|\}\}|\))/.test(line[cursor.column]) || editor.inMultiSelectMode) {
                    TTLstyleBehaviour.recordAutoInsert(editor, session, "}}");
                    return {
                        text: '{{}}',
                        selection: [2, 1]
                    };
                } else {
                    TTLstyleBehaviour.recordMaybeInsert(editor, session, "{{");
                    return {
                        text: '{{',
                        selection: [2, 1]
                    };
                }
            }
        } else if (text == '}}') {
            initContext(editor);
            var rightChar = line.substring(cursor.column, cursor.column + 1);
            if (rightChar == '}}') {
                var matching = session.$findOpeningBracket('}}', {column: cursor.column + 1, row: cursor.row});
                if (matching !== null && TTLstyleBehaviour.isAutoInsertedClosing(cursor, line, text)) {
                    TTLstyleBehaviour.popAutoInsertedClosing();
                    return {
                        text: '',
                        selection: [2, 1]
                    };
                }
            }
        } else if (text == "\n" || text == "\r\n") {
            initContext(editor);
            var closing = "";
            if (TTLstyleBehaviour.isMaybeInsertedClosing(cursor, line)) {
                closing = lang.stringRepeat("}}", context.maybeInsertedBrackets);
                TTLstyleBehaviour.clearMaybeInsertedClosing();
            }
            var rightChar = line.substring(cursor.column, cursor.column + 1);
            if (rightChar === '}}') {
                var openBracePos = session.findMatchingBracket({row: cursor.row, column: cursor.column+1}, '}}');
                if (!openBracePos)
                     return null;
                var next_indent = this.$getIndent(session.getLine(openBracePos.row));
            } else if (closing) {
                var next_indent = this.$getIndent(line);
            } else {
                TTLstyleBehaviour.clearMaybeInsertedClosing();
                return;
            }
            var indent = next_indent + session.getTabString();

            return {
                text: '\n' + indent + '\n' + next_indent + closing,
                selection: [1, indent.length, 1, indent.length]
            };
        } else {
            TTLstyleBehaviour.clearMaybeInsertedClosing();
        }
    });

    this.add("braces", "deletion", function(state, action, editor, session, range) {
        var selected = session.doc.getTextRange(range);
        if (!range.isMultiLine() && selected == '{{') {
            initContext(editor);
            var line = session.doc.getLine(range.start.row);
            var rightChar = line.substring(range.end.column, range.end.column + 1);
            if (rightChar == '}}') {
                range.end.column++;
                return range;
            } else {
                context.maybeInsertedBrackets--;
            }
        }
    });

    this.add("parens", "insertion", function(state, action, editor, session, text) {
        if (text == '(') {
            initContext(editor);
            var selection = editor.getSelectionRange();
            var selected = session.doc.getTextRange(selection);
            if (selected !== "" && editor.getWrapBehavioursEnabled()) {
                return getWrapped(selection, selected, '(', ')');
            } else if (TTLstyleBehaviour.isSaneInsertion(editor, session)) {
                TTLstyleBehaviour.recordAutoInsert(editor, session, ")");
                return {
                    text: '()',
                    selection: [1, 1]
                };
            }
        } else if (text == ')') {
            initContext(editor);
            var cursor = editor.getCursorPosition();
            var line = session.doc.getLine(cursor.row);
            var rightChar = line.substring(cursor.column, cursor.column + 1);
            if (rightChar == ')') {
                var matching = session.$findOpeningBracket(')', {column: cursor.column + 1, row: cursor.row});
                if (matching !== null && TTLstyleBehaviour.isAutoInsertedClosing(cursor, line, text)) {
                    TTLstyleBehaviour.popAutoInsertedClosing();
                    return {
                        text: '',
                        selection: [1, 1]
                    };
                }
            }
        }
    });

    this.add("parens", "deletion", function(state, action, editor, session, range) {
        var selected = session.doc.getTextRange(range);
        if (!range.isMultiLine() && selected == '(') {
            initContext(editor);
            var line = session.doc.getLine(range.start.row);
            var rightChar = line.substring(range.start.column + 1, range.start.column + 2);
            if (rightChar == ')') {
                range.end.column++;
                return range;
            }
        }
    });

    this.add("brackets", "insertion", function(state, action, editor, session, text) {
        if (text == '@%') {
            initContext(editor);
            var selection = editor.getSelectionRange();
            var selected = session.doc.getTextRange(selection);
            if (selected !== "" && editor.getWrapBehavioursEnabled()) {
                return getWrapped(selection, selected, '@%', '%@');
            } else if (TTLstyleBehaviour.isSaneInsertion(editor, session)) {
                TTLstyleBehaviour.recordAutoInsert(editor, session, "%@");
                return {
                    text: '@%%@',
                    selection: [1, 1]
                };
            }
        } else if (text == '%@') {
            initContext(editor);
            var cursor = editor.getCursorPosition();
            var line = session.doc.getLine(cursor.row);
            var rightChar = line.substring(cursor.column, cursor.column + 1);
            if (rightChar == '%@') {
                var matching = session.$findOpeningBracket('%@', {column: cursor.column + 1, row: cursor.row});
                if (matching !== null && TTLstyleBehaviour.isAutoInsertedClosing(cursor, line, text)) {
                    TTLstyleBehaviour.popAutoInsertedClosing();
                    return {
                        text: '',
                        selection: [1, 1]
                    };
                }
            }
        }
    });

    this.add("brackets", "deletion", function(state, action, editor, session, range) {
        var selected = session.doc.getTextRange(range);
        if (!range.isMultiLine() && selected == '@%') {
            initContext(editor);
            var line = session.doc.getLine(range.start.row);
            var rightChar = line.substring(range.start.column + 1, range.start.column + 2);
            if (rightChar == '%@') {
                range.end.column++;
                return range;
            }
        }
    });
};

    
TTLstyleBehaviour.isSaneInsertion = function(editor, session) {
    var cursor = editor.getCursorPosition();
    var iterator = new TokenIterator(session, cursor.row, cursor.column);
    
    // Don't insert in the middle of a keyword/identifier/lexical
    if (!this.$matchTokenType(iterator.getCurrentToken() || "text", SAFE_INSERT_IN_TOKENS)) {
        // Look ahead in case we're at the end of a token
        var iterator2 = new TokenIterator(session, cursor.row, cursor.column + 1);
        if (!this.$matchTokenType(iterator2.getCurrentToken() || "text", SAFE_INSERT_IN_TOKENS))
            return false;
    }
    
    // Only insert in front of whitespace/comments
    iterator.stepForward();
    return iterator.getCurrentTokenRow() !== cursor.row ||
        this.$matchTokenType(iterator.getCurrentToken() || "text", SAFE_INSERT_BEFORE_TOKENS);
};

TTLstyleBehaviour.$matchTokenType = function(token, types) {
    return types.indexOf(token.type || token) > -1;
};

TTLstyleBehaviour.recordAutoInsert = function(editor, session, bracket) {
    var cursor = editor.getCursorPosition();
    var line = session.doc.getLine(cursor.row);
    // Reset previous state if text or context changed too much
    if (!this.isAutoInsertedClosing(cursor, line, context.autoInsertedLineEnd[0]))
        context.autoInsertedBrackets = 0;
    context.autoInsertedRow = cursor.row;
    context.autoInsertedLineEnd = bracket + line.substr(cursor.column);
    context.autoInsertedBrackets++;
};

TTLstyleBehaviour.recordMaybeInsert = function(editor, session, bracket) {
    var cursor = editor.getCursorPosition();
    var line = session.doc.getLine(cursor.row);
    if (!this.isMaybeInsertedClosing(cursor, line))
        context.maybeInsertedBrackets = 0;
    context.maybeInsertedRow = cursor.row;
    context.maybeInsertedLineStart = line.substr(0, cursor.column) + bracket;
    context.maybeInsertedLineEnd = line.substr(cursor.column);
    context.maybeInsertedBrackets++;
};

TTLstyleBehaviour.isAutoInsertedClosing = function(cursor, line, bracket) {
    return context.autoInsertedBrackets > 0 &&
        cursor.row === context.autoInsertedRow &&
        bracket === context.autoInsertedLineEnd[0] &&
        line.substr(cursor.column) === context.autoInsertedLineEnd;
};

TTLstyleBehaviour.isMaybeInsertedClosing = function(cursor, line) {
    return context.maybeInsertedBrackets > 0 &&
        cursor.row === context.maybeInsertedRow &&
        line.substr(cursor.column) === context.maybeInsertedLineEnd &&
        line.substr(0, cursor.column) == context.maybeInsertedLineStart;
};

TTLstyleBehaviour.popAutoInsertedClosing = function() {
    context.autoInsertedLineEnd = context.autoInsertedLineEnd.substr(1);
    context.autoInsertedBrackets--;
};

TTLstyleBehaviour.clearMaybeInsertedClosing = function() {
    if (context) {
        context.maybeInsertedBrackets = 0;
        context.maybeInsertedRow = -1;
    }
};



oop.inherits(TTLstyleBehaviour, Behaviour);

exports.TTLstyleBehaviour = TTLstyleBehaviour;
});
