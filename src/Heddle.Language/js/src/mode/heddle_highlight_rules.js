"use strict";

var oop = require("../lib/oop");
var TextHighlightRules = require("./text_highlight_rules").TextHighlightRules;
var HtmlHighlightRules = require("./html_highlight_rules").HtmlHighlightRules;
var CSharpHighlightRules = require("./csharp_highlight_rules").CSharpHighlightRules;

var CustomizableHtmlHighlightRules = function () {
    HtmlHighlightRules.call(this);
}

oop.inherits(CustomizableHtmlHighlightRules, HtmlHighlightRules);

var HeddleLangHighlightRules = function (heddleMode) {
    TextHighlightRules.call(this);
    var startPrefix;

    switch (heddleMode) {
        case "css":
            startPrefix = "css-";
            break;
        case "js":
            startPrefix = "js-";
            break;
        case "html":
        default:
            startPrefix = "";
            break;
    }

    function createNextRule(regex, token, next) {
        return {
            regex: regex,
            onMatch: getNextMode(token, next)
        }
    }

    function createPushRule(regex, token, next) {
        return {
            regex: regex,
            onMatch: getPushMode(token, next)
        }
    }

    function getPushMode(token, next) {
        return function (val, state, stack) {
            if (state && stack[0] !== state && state.indexOf(startPrefix + "heddle-") !== 0) {
                stack.unshift("#tmp", state);
            }
            stack.unshift(next);
            this.next = next;
            this.merge = false;
            return token;
        }
    }

    function getNextMode(token, next) {
        return function (val, state, stack) {
            if (stack.length && stack[0] !== next) {
                stack.shift();
                stack.unshift(next);
            }
            this.next = next;
            this.merge = false;
            return token;
        }
    }

    function _generateRules() {

        function createPopRule(regex, token) {
            return {
                regex: regex,
                onMatch: getPopMode(token)
            }
        }

        function getPopMode(token) {
            return function (val, state, stack) {
                stack.shift();
                if (stack[0] === "#tmp") {
                    stack.shift();
                    this.next = stack.shift();
                } else {
                    this.next = (stack.length ? stack[0] : (startPrefix + "start")) || (startPrefix + "start");
                }
                this.merge = false;
                return token;
            }
        }

        var rules = {};
        rules[startPrefix + "start"] = [
            createPushRule(
                /@\*/,
                "heddle-sub.comment.start",
                startPrefix + "heddle-comment"
            ),
            createNextRule(
                /@\\\\\s*/,
                "heddle-sub.comment.block",
                startPrefix + "start"
            ),
            {
                regex: /@{/,
                onMatch: function (val, state, stack) {
                    var next;
                    if (state) {
                        next = "html-" + state;
                    } else {
                        next = "html-" + startPrefix + "start"
                    }
                    this.next = next;
                    stack.unshift(next);
                    this.merge = false;
                    return "heddle-sub.keyword.paren.lparen";
                }
            },
            {
                regex: /@:/,
                onMatch: function (val, state, stack) {
                    var next;
                    if (state) {
                        next = "html-ln-" + state;
                    } else {
                        next = "html-ln-" + startPrefix + "start"
                    }
                    this.next = next;
                    stack.unshift(next);
                    this.merge = false;
                    return "heddle-sub.keyword";
                }
            },
            createPushRule(
                /@%/,
                "heddle-sub.keyword.operator.paren.lparen",
                startPrefix + "heddle-def"
            ),
            createPushRule(
                /@<</,
                "heddle-sub.keyword.operator",
                startPrefix + "heddle-import"
            ),
            createPushRule(
                /@/,
                "heddle-sub.support.function",
                startPrefix + "heddle-out"
            ),
            createPopRule(
                /}}/,
                "heddle-sub.keyword.operator.paren.rparen"
            )
        ];

        rules[startPrefix + "heddle-out"] = [
            createPushRule(
                /@\*/,
                "heddle-out.comment.start",
                startPrefix + "heddle-comment"
            ),
            createNextRule(
                /[a-zA-Zа-яА-Я_][a-zA-Zа-яА-Я0-9_]+/,
                "heddle-out.identifier",
                startPrefix + "heddle-out"
            ),
            {
                regex: /\(/,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.unshift(startPrefix + "heddle-call-returned");
                    stack.unshift(startPrefix + "heddle-call");
                    this.next = startPrefix + "heddle-call";
                    this.merge = false;
                    return "heddle-out.keyword.paren.lparen";
                },
            },
            createNextRule(
                /:/,
                "heddle-out.punctuation.operator",
                startPrefix + "heddle-out"
            ),
            {
                regex: /@{/,
                onMatch: function (val, state, stack) {
                    var next;
                    if (state) {
                        next = "html-" + state;
                    } else {
                        next = "html-" + startPrefix + "start"
                    }
                    this.next = next;
                    stack.shift();
                    stack.unshift(next);
                    this.merge = false;
                    return "heddle-out.keyword.paren.lparen";
                }
            },
            {
                regex: /@:/,
                onMatch: function (val, state, stack) {
                    var next;
                    if (state) {
                        next = "html-ln-" + state;
                    } else {
                        next = "html-ln-" + startPrefix + "start"
                    }
                    this.next = next;
                    stack.shift();
                    stack.unshift(next);
                    this.merge = false;
                    return "heddle-out.keyword";
                }
            },
            {
                regex: /@%/,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.unshift(startPrefix + "heddle-def");
                    this.next = startPrefix + "heddle-def";
                    this.merge = false;
                    return "heddle-out.keyword.operator.paren.lparen";
                }
            },
            {
                regex: /{{/,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.unshift(startPrefix + "start");
                    this.next = startPrefix + "start";
                    this.merge = false;
                    return "heddle-out.keyword.operator.paren.lparen";
                }
            },
            {
                regex: /}}/,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.shift();
                    if (stack[0] === "#tmp") {
                        stack.shift();
                        this.next = stack.shift();
                    } else {
                        this.next = stack.length ? stack[0] : (startPrefix + "start");
                    }
                    this.merge = false;
                    return "heddle-out.keyword.operator.paren.rparen";
                }
            },
            {
                regex: /@<</,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.unshift(startPrefix + "heddle-import");
                    this.next = startPrefix + "heddle-import";
                    this.merge = false;
                    return "heddle-out.keyword.operator";
                }
            },
            createPopRule(
                /@\\\\\s*/,
                "heddle-out.comment.block"
            ),
            createNextRule(
                /\s+/,
                "heddle-out.constant.other",
                startPrefix + "heddle-out"
            ),
            createPopRule(/()?/, "heddle-out.empty")
        ];

        rules[startPrefix + "heddle-call-returned"] = [
            createPushRule(
                /@\*/,
                "heddle-call-returned.comment.start",
                startPrefix + "heddle-comment"
            ),
            createNextRule(
                /\s*:/,
                "heddle-call-returned.punctuation.operator",
                startPrefix + "heddle-out"
            ),
            createNextRule(
                /@/,
                "heddle-call-returned.keyword.operator",
                startPrefix + "heddle-out"
            ),
            {
                regex: /@{/,
                onMatch: function (val, state, stack) {
                    var next;
                    if (state) {
                        next = "html-" + state;
                    } else {
                        next = "html-" + startPrefix + "start"
                    }
                    this.next = next;
                    stack.shift();
                    stack.unshift(next);
                    this.merge = false;
                    return "heddle-call-returned.keyword.paren.lparen";
                }
            },
            {
                regex: /@:/,
                onMatch: function (val, state, stack) {
                    var next;
                    if (state) {
                        next = "html-ln-" + state;
                    } else {
                        next = "html-ln-" + startPrefix + "start"
                    }
                    this.next = next;
                    stack.shift();
                    stack.unshift(next);
                    this.merge = false;
                    return "heddle-call-returned.keyword";
                }
            },
            {
                regex: /@%/,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.unshift(startPrefix + "heddle-def");
                    this.next = startPrefix + "heddle-def";
                    this.merge = false;
                    return "heddle-call-returned.keyword.operator.paren.lparen";
                }
            },
            {
                regex: /{{/,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.unshift(startPrefix + "start");
                    this.next = startPrefix + "start";
                    this.merge = false;
                    return "heddle-call-returned.keyword.operator.paren.lparen";
                }
            },
            {
                regex: /}}/,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.shift();
                    if (stack[0] === "#tmp") {
                        stack.shift();
                        this.next = stack.shift();
                    } else {
                        this.next = stack.length ? stack[0] : (startPrefix + "start");
                    }
                    this.merge = false;
                    return "heddle-call-returned.keyword.operator.paren.rparen";
                }
            },
            {
                regex: /@<</,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.unshift(startPrefix + "heddle-import");
                    this.next = startPrefix + "heddle-import";
                    this.merge = false;
                    return "heddle-call-returned.keyword.operator";
                }
            },
            createNextRule(
                /@\\\\\s*/,
                "heddle-call-returned.comment.block",
                startPrefix + "heddle-call-returned"
            ),
            createNextRule(
                /\s+/,
                "heddle-call-returned.text",
                startPrefix + "heddle-call-returned"
            ),
            createPopRule(/()?/, "heddle-call-returned.empty")
        ];

        rules[startPrefix + "heddle-call"] = [
            createPushRule(
                /@\*/,
                "heddle-call.comment.start",
                startPrefix + "heddle-comment"
            ),
            {
                regex: /@/,
                onMatch: function (val, state, stack) {
                    stack.shift();
                    stack.unshift("cs-start");
                    this.next = "cs-start";
                    this.merge = false;
                    return "heddle-call.keyword";
                },
            },
            createPushRule(
                /\(/,
                "heddle-call.keyword.paren.lparen",
                startPrefix + "heddle-call"
            ),
            createPopRule(
                /\)/,
                "heddle-call.keyword.paren.rparen"
            ),
            createNextRule(
                /::/,
                "heddle-call.punctuation.operator",
                startPrefix + "heddle-call"
            ),
            createNextRule(
                /:/,
                "heddle-call.punctuation.operator",
                startPrefix + "heddle-call"
            ),
            createNextRule(
                /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я0-9_]*/,
                "heddle-call.variable.language",
                startPrefix + "heddle-call"
            ),
            createNextRule(
                /\./,
                "heddle-call.punctuation.operator",
                startPrefix + "heddle-call"
            ),
            createNextRule(
                /\s+/,
                "heddle-call.constant.other",
                startPrefix + "heddle-call"
            )
        ];

        rules[startPrefix + "heddle-import"] = [
            createPushRule(
                /@\*/,
                "heddle-import.comment.start",
                startPrefix + "heddle-comment"
            ),
            createNextRule(
                /{{/,
                "heddle-import.keyword.operator.paren.lparen",
                startPrefix + "heddle-import"
            ),
            createNextRule(
                /[^{}]+/,
                "heddle-import.constant.other",
                startPrefix + "heddle-import"
            ),
            createPopRule(
                /}}/,
                "heddle-import.keyword.operator.paren.rparen"
            ),
            createNextRule(
                /./,
                "heddle-import.constant.other",
                startPrefix + "heddle-import"
            )
        ];

        rules[startPrefix + "heddle-def"] = [
            createPushRule(
                /@\*/,
                "heddle-def.comment.start",
                startPrefix + "heddle-comment"
            ),
            createNextRule(
                /</,
                "heddle-def.punctuation.operator.paren.lparen",
                startPrefix + "heddle-def-name"
            ),
            createPushRule(
                /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*\s*</,
                "heddle-def.storage.type",
                startPrefix + "heddle-generic-type"
            ),
            createNextRule(
                /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*\s*(\[])*/,
                "heddle-def.storage.type",
                startPrefix + "heddle-def"
            ),
            createPushRule(
                /{{/,
                "heddle-def.keyword.operator.paren.lparen",
                startPrefix + "start"
            ),
            createPushRule(
                /->/,
                "heddle-def.keyword.operator",
                startPrefix + "heddle-out"
            ),
            createNextRule(
                /::/,
                "heddle-def.keyword.operator",
                startPrefix + "heddle-def"
            ),
            createNextRule(
                /\s+/,
                "heddle-def.whitespace",
                startPrefix + "heddle-def"
            ),
            createPopRule(
                /%@/,
                "heddle-def.keyword.operator.paren.rparen"
            )
        ];

        rules[startPrefix + "heddle-def-name"] = [
            createNextRule(
                /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*/,
                "heddle-def-name.identifier",
                startPrefix + "heddle-def-name"
            ),
            createNextRule(
                /:/,
                "heddle-def-name.keyword.operator",
                startPrefix + "heddle-def-name"
            ),
            createNextRule(
                />/,
                "heddle-def-name.punctuation.operator.paren.rparen",
                startPrefix + "heddle-def"
            )
        ];

        rules[startPrefix + "heddle-generic-type"] = [
            createNextRule(
                /\s*[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.,]*\s*(\[])*/,
                "heddle-generic-type.storage.type",
                startPrefix + "heddle-generic-type"
            ),
            createNextRule(
                /(\[])+/,
                "heddle-generic-type.storage.type",
                startPrefix + "heddle-generic-type"
            ),
            createPushRule(
                /</,
                "heddle-generic-type.storage.type",
                startPrefix + "heddle-generic-type"
            ),
            createPopRule(
                />/,
                "heddle-generic-type.storage.type"
            )
        ];

        rules[startPrefix + "heddle-comment"] = [
            createPopRule(
                /\*@/,
                "heddle-comment.comment.end"
            ),
            createNextRule(
                /([^*]|\*[^@])+/,
                "heddle-comment.comment.block",
                startPrefix + "heddle-comment"
            )
        ];

        return rules;
    }

    this.generateRules = _generateRules;

    this.addRules(
        _generateRules("")
    );

    this.embedRules(CSharpHighlightRules, "cs-", [
        createPushRule(
            /\(/,
            "cs-start.paren.lparen",
            "cs-start"
        ),
        {
            regex: /\)/,
            onMatch: function (val, state, stack) {
                stack.shift();
                if (stack[0] === "#tmp") {
                    stack.shift();
                    this.next = stack.shift();
                } else {
                    this.next = stack.length ? stack[0] : (startPrefix + "start");
                }
                this.merge = false;
                return "cs-start.paren.rparen";
            },
        }
    ]);
};

oop.inherits(HeddleLangHighlightRules, TextHighlightRules);

var HeddleHighlightRules = function () {
    HtmlHighlightRules.call(this);

    var stackBackup = {
        startLines: {},
        endLines: {},
        lastStack: null,
        lastStartRow: null
    };

    var heddleRules = new HeddleLangHighlightRules("html").getRules();
    var jsHeddleRules = new HeddleLangHighlightRules("js").generateRules();
    var cssHeddleRules = new HeddleLangHighlightRules("css").generateRules();

    this.$embeds = [];

    for (var key in this.$rules) {
        if (key.indexOf("js-") === 0) {
            this.$rules[key].unshift.apply(this.$rules[key], jsHeddleRules["js-start"]);
        } else if (key.indexOf("css-") === 0) {
            this.$rules[key].unshift.apply(this.$rules[key], cssHeddleRules["css-start"]);
        } else {
            this.$rules[key].unshift.apply(this.$rules[key], heddleRules["start"]);
        }
    }

    for (var key in heddleRules) {
        if (!this.$rules[key]) {
            this.$rules[key] = heddleRules[key];
        }
    }

    for (var key in jsHeddleRules) {
        if (!this.$rules[key]) {
            this.$rules[key] = jsHeddleRules[key];
        }
    }

    for (var key in cssHeddleRules) {
        if (!this.$rules[key]) {
            this.$rules[key] = cssHeddleRules[key];
        }
    }

    function onMatchEmbeddedStart(stack, row) {
        stackBackup.lastStartRow = row;

        if (stack.length) {
            stackBackup.lastStack = stack.splice(0);
            stackBackup.startLines[row] = stackBackup.lastStack;
        }
    }

    function onMatchEmbeddedEnd(stack, row) {
        stack.splice(0);

        var startRows = Object.keys(stackBackup.startLines).map(x => parseInt(x)).sort((a, b) => {
            if (a < b)
                return -1;
            if (a > b)
                return 1;
            return 0;
        });
        var blockStart = -1;

        if (stackBackup.lastStack) {
            stackBackup.endLines[row] = stackBackup.lastStack;
            blockStart = stackBackup.lastStartRow;
            stackBackup.lastStack = null;
            stackBackup.lastStartRow = null;
        } else if (stackBackup.lastStartRow) {
            stackBackup.endLines[row] = stackBackup.startLines[stackBackup.lastStartRow];
            blockStart = stackBackup.lastStartRow;
            stackBackup.lastStartRow = null;
        } else if (!stackBackup.endLines[row]) {
            for (i = 0; i < startRows.length; i++) {
                if (startRows[i] <= row) {
                    blockStart = startRows[i];
                }
                if (startRows[i] > row) {
                    break;
                }
            }
            if (blockStart !== -1) {
                stackBackup.endLines[row] = stackBackup.startLines[blockStart];
            }
        }

        var endRows = Object.keys(stackBackup.endLines).map(x => parseInt(x)).sort((a, b) => {
            if (a < b)
                return -1;
            if (a > b)
                return 1;
            return 0;
        });

        //clean up
        if (blockStart !== -1) {
            for (var i = 0; i < startRows.length; i++) {
                if (startRows[i] > blockStart && startRows[i] < row) {
                    delete stackBackup.startLines[startRows[i]];
                }
            }
            for (var i = 0; i < endRows.length; i++) {
                if (endRows[i] > blockStart && endRows[i] < row) {
                    delete stackBackup.endLines[endRows[i]];
                }
            }
        }

        var backup = stackBackup.endLines[row];

        if (backup) {
            backup.forEach((item) => {
                stack.push(item);
            });
        }
    }

    function applyTagState(tag, prefix) {
        prefix = prefix ? prefix : '';
        if (tag.next && Array.isArray(tag.next)) {
            var newOps = [];

            tag.next.forEach((tagOp, idx) => {
                if (tagOp.token === "meta.tag.punctuation.tag-close.xml" && tagOp.next === "js-start") {
                    newOps.push({
                        index: idx,
                        operation: {
                            token: "meta.tag.punctuation.tag-close.xml",
                            regex: "/?>",
                            next: prefix + "js-start",
                            onMatch: function (value, currentState, stack, line, row) {
                                onMatchEmbeddedStart(stack, row);
                                this.merge = false;
                                return this.token;
                            }
                        }
                    });
                }
                if (tagOp.token === "meta.tag.punctuation.tag-close.xml" && tagOp.next === "css-start") {
                    newOps.push({
                        index: idx,
                        operation: {
                            token: "meta.tag.punctuation.tag-close.xml",
                            regex: "/?>",
                            next: prefix + "css-start",
                            onMatch: function (value, currentState, stack, line, row) {
                                onMatchEmbeddedStart(stack, row);
                                this.merge = false;
                                return this.token;
                            }
                        }
                    });
                }
            });

            newOps.forEach(newOp => {
                tag.next[newOp.index] = newOp.operation;
            });
        }
    }

    function applyIncludePrefixing(stateItem, prefix) {
        let newOps;
        prefix = prefix ? prefix : '';

        if (stateItem.include) {
            stateItem.include = prefix + stateItem.include;
        }

        if (stateItem.next && Array.isArray(stateItem.next)) {
            newOps = [];

            stateItem.next.forEach((tagOp, idx) => {
                if (tagOp.include && typeof tagOp.include === 'string') {
                    newOps.push({
                        index: idx,
                        operation: {
                            include: prefix + tagOp.include
                        }
                    });
                }
            });

            newOps.forEach(newOp => {
                stateItem.next[newOp.index] = newOp.operation;
            });
        }

        if (stateItem.push && stateItem.push.include) {
            stateItem.push.include = prefix + stateItem.push.include;
        }

        if (stateItem.push && Array.isArray(stateItem.push)) {
            newOps = [];

            stateItem.push.forEach((tagOp, idx) => {
                if (tagOp.include && typeof tagOp.include === 'string') {
                    newOps.push({
                        index: idx,
                        operation: {
                            include: prefix + tagOp.include
                        }
                    });
                }
            });

            newOps.forEach(newOp => {
                stateItem.push[newOp.index] = newOp.operation;
            });
        }
    }

    this.embedRules(CustomizableHtmlHighlightRules, "html-", [
        {
            regex: /}@/,
            onMatch: function (val, state, stack) {
                stack.shift();

                //this part could run from any inner state (heddle-, css-heddle-, js-heddle-, html-css-, html-js-, start)
                var nextDefaultState = "start";
                if (state) {
                    var cssState = state.indexOf("css-");
                    var jsState = state.indexOf("js-");
                    var heddleState = state.indexOf("heddle-");

                    if (heddleState > 0) {
                        if (cssState >= 0) {
                            nextDefaultState = "css-start";
                        } else if (jsState >= 0) {
                            nextDefaultState = "js-start";
                        }
                    } else {
                        if (cssState === 0) {
                            nextDefaultState = cssState;
                        } else if (cssState > 0) {
                            nextDefaultState = state.substring(cssState);
                        }
                        if (jsState === 0) {
                            nextDefaultState = cssState;
                        } else if (jsState > 0) {
                            nextDefaultState = state.substring(jsState);
                        }
                    }
                }
                this.next = stack.length ? stack[0] : nextDefaultState;

                this.merge = false;
                return "heddle-raw.keyword.paren.rparen";
            },
        }
    ]);

    this.embedRules(CustomizableHtmlHighlightRules, "html-ln-", [
        {
            regex: /$|^/,
            onMatch: function (val, state, stack) {
                stack.shift();

                //this part could run from any inner state (heddle-, css-heddle-, js-heddle-, html-css-, html-js-, start)
                var nextDefaultState = "start";
                if (state) {
                    var cssState = state.indexOf("css-");
                    var jsState = state.indexOf("js-");
                    var heddleState = state.indexOf("heddle-");

                    if (heddleState > 0) {
                        if (cssState >= 0) {
                            nextDefaultState = "css-start";
                        } else if (jsState >= 0) {
                            nextDefaultState = "js-start";
                        }
                    } else {
                        if (cssState === 0) {
                            nextDefaultState = cssState;
                        } else if (cssState > 0) {
                            nextDefaultState = state.substring(cssState);
                        }
                        if (jsState === 0) {
                            nextDefaultState = cssState;
                        } else if (jsState > 0) {
                            nextDefaultState = state.substring(jsState);
                        }
                    }
                }
                this.next = stack.length ? stack[0] : nextDefaultState;

                this.merge = false;
                return "heddle-call-returned.empty";
            },
        }
    ]);

    //replace tag sub-processors start operations to save stack backup and clear initial stack state
    this.$rules["tag"].forEach(tag => {
        applyTagState(tag);
    });

    this.$rules["html-tag"].forEach(tag => {
        applyTagState(tag, "html-");
    });

    this.$rules["html-ln-tag"].forEach(tag => {
        applyTagState(tag, "html-ln-");
    });

    Object.keys(this.$rules).forEach(rule => {
        if (rule.indexOf("html-ln-") === 0) {
            this.$rules[rule].forEach(stateItem => applyIncludePrefixing(stateItem, "html-ln-"));
        }
        if (rule.indexOf("html-ln-") !== 0 && rule.indexOf("html-") === 0) {
            this.$rules[rule].forEach(stateItem => applyIncludePrefixing(stateItem, "html-"));
        }
    });

    //restore stack from backup upon return
    this.$rules["script-end"] = [
        {include: "attributes"},
        {
            token: "meta.tag.punctuation.tag-close.xml",
            regex: "/?>",
            onMatch: function (value, currentState, stack, line, row) {
                onMatchEmbeddedEnd(stack, row);
                this.next = stack.length ? stack[0] : "start";
                return this.token;
            }
        }
    ];

    this.$rules["html-script-end"] = [
        {include: "html-attributes"},
        {
            token: "meta.tag.punctuation.tag-close.xml",
            regex: "/?>",
            onMatch: function (value, currentState, stack, line, row) {
                onMatchEmbeddedEnd(stack, row);
                this.next = stack.length ? stack[0] : "html-start";
                return this.token;
            }
        }
    ];

    this.$rules["html-ln-script-end"] = [
        {include: "html-ln-attributes"},
        {
            token: "meta.tag.punctuation.tag-close.xml",
            regex: "/?>",
            onMatch: function (value, currentState, stack, line, row) {
                onMatchEmbeddedEnd(stack, row);
                this.next = stack.length ? stack[0] : "html-ln-start";
                return this.token;
            }
        }
    ];

    //restore stack from backup upon return
    this.$rules["style-end"] = [
        {include: "attributes"},
        {
            token: "meta.tag.punctuation.tag-close.xml",
            regex: "/?>",
            onMatch: function (value, currentState, stack, line, row) {
                onMatchEmbeddedEnd(stack, row);
                this.next = stack.length ? stack[0] : "start";
                return this.token;
            }
        }
    ];

    this.$rules["html-style-end"] = [
        {include: "html-attributes"},
        {
            token: "meta.tag.punctuation.tag-close.xml",
            regex: "/?>",
            onMatch: function (value, currentState, stack, line, row) {
                onMatchEmbeddedEnd(stack, row);
                this.next = stack.length ? stack[0] : "html-start";
                return this.token;
            }
        }
    ];

    this.$rules["html-ln-style-end"] = [
        {include: "html-ln-attributes"},
        {
            token: "meta.tag.punctuation.tag-close.xml",
            regex: "/?>",
            onMatch: function (value, currentState, stack, line, row) {
                onMatchEmbeddedEnd(stack, row);
                this.next = stack.length ? stack[0] : "html-ln-start";
                return this.token;
            }
        }
    ];

    this.$embeds = [];

    this.normalizeRules();
};

// (function() {
// }).call(HtmlHighlightRules.prototype);

oop.inherits(HeddleHighlightRules, HtmlHighlightRules);

exports.HeddleHighlightRules = HeddleHighlightRules;
exports.HeddleLangHighlightRules = HeddleLangHighlightRules;
