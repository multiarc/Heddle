define(function (require, exports, module) {
    "use strict";

    var oop = require("../lib/oop");
    var TextHighlightRules = require("./text_highlight_rules").TextHighlightRules;
    var HtmlHighlightRules = require("./html_highlight_rules").HtmlHighlightRules;
    var CSharpHighlightRules = require("./csharp_highlight_rules").CSharpHighlightRules;
    
    var CustomizableHtmlHighlightRules = function() {
        HtmlHighlightRules.call(this);
    }

    oop.inherits(CustomizableHtmlHighlightRules, HtmlHighlightRules);

    var TtlLangHighlightRules = function (ttlMode) {
        TextHighlightRules.call(this);
        var startPrefix;
        
        switch (ttlMode) {
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
                stack.unshift(next);
                this.next = next;
                this.merge = false;
                return token;
            }
        }

        function getNextMode(token, next) {
            return function (val, state, stack) {
                if (stack.length && stack[0] != next) {
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
                    this.next = (stack.length ? stack[0] : (startPrefix + "start")) || (startPrefix + "start");
                    this.merge = false;
                    return token;
                }
            }
            
            var rules = {};
            rules[startPrefix + "start"] = [
                createPushRule(
                    /@\*/,
                    "ttl-sub.comment.start",
                    startPrefix + "ttl-comment"
                ),
                createNextRule(
                    /@\\\\\s*/,
                    "ttl-sub.comment.block",
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
                        return "ttl-sub.keyword.paren.lparen";
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
                        return "ttl-sub.keyword";
                    }
                },
                createPushRule(
                    /@%/,
                    "ttl-sub.keyword.operator.paren.lparen",
                    startPrefix + "ttl-def"
                ),
                createPushRule(
                    /@<</,
                    "ttl-sub.keyword.operator",
                    startPrefix + "ttl-import"
                ),
                createPushRule(
                    /@/,
                    "ttl-sub.support.function",
                    startPrefix + "ttl-out"
                ),
                createPopRule(
                    /}}/,
                    "ttl-sub.keyword.operator.paren.rparen"
                )
            ];

            rules[startPrefix + "ttl-out"] = [
                createPushRule(
                    /@\*/,
                    "ttl-out.comment.start",
                    startPrefix + "ttl-comment"
                ),
                createNextRule(
                    /[a-zA-Zа-яА-Я_][a-zA-Zа-яА-Я0-9_]+/,
                    "ttl-out.identifier",
                    startPrefix + "ttl-out"
                ),
                {
                    regex: /\(/,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.unshift(startPrefix + "ttl-call-returned");
                        stack.unshift(startPrefix + "ttl-call");
                        this.next = startPrefix + "ttl-call";
                        this.merge = false;
                        return "ttl-out.keyword.paren.lparen";
                    },
                },
                createNextRule(
                    /:/,
                    "ttl-out.punctuation.operator",
                    startPrefix + "ttl-out"
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
                        return "ttl-out.keyword.paren.lparen";
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
                        return "ttl-out.keyword";
                    }
                },
                {
                    regex: /@%/,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.unshift(startPrefix + "ttl-def");
                        this.next = startPrefix + "ttl-def";
                        this.merge = false;
                        return "ttl-out.keyword.operator.paren.lparen";
                    }
                },
                {
                    regex: /{{/,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.unshift(startPrefix + "start");
                        this.next = startPrefix + "start";
                        this.merge = false;
                        return "ttl-out.keyword.operator.paren.lparen";
                    }
                },
                {
                    regex: /}}/,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.shift();
                        this.next = stack.length ? stack[0] : (startPrefix + "start");
                        this.merge = false;
                        return "ttl-out.keyword.operator.paren.rparen";
                    }
                },
                {
                    regex: /@<</,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.unshift(startPrefix + "ttl-import");
                        this.next = startPrefix + "ttl-import";
                        this.merge = false;
                        return "ttl-out.keyword.operator";
                    }
                },
                createPopRule(
                    /@\\\\\s*/,
                    "ttl-out.comment.block"
                ),
                createNextRule(
                    /\s+/,
                    "ttl-out.constant.other",
                    startPrefix + "ttl-out"
                ),
                createPopRule(/()?/, "ttl-out.empty")
            ];

            rules[startPrefix + "ttl-call-returned"] = [
                createPushRule(
                    /@\*/,
                    "ttl-call-returned.comment.start",
                    startPrefix + "ttl-comment"
                ),
                createNextRule(
                    /\s*:/,
                    "ttl-call-returned.punctuation.operator",
                    startPrefix + "ttl-out"
                ),
                createNextRule(
                    /@/,
                    "ttl-call-returned.keyword.operator",
                    startPrefix + "ttl-out"
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
                        return "ttl-call-returned.keyword.paren.lparen";
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
                        return "ttl-call-returned.keyword";
                    }
                },
                {
                    regex: /@%/,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.unshift(startPrefix + "ttl-def");
                        this.next = startPrefix + "ttl-def";
                        this.merge = false;
                        return "ttl-call-returned.keyword.operator.paren.lparen";
                    }
                },
                {
                    regex: /{{/,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.unshift(startPrefix + "start");
                        this.next = startPrefix + "start";
                        this.merge = false;
                        return "ttl-call-returned.keyword.operator.paren.lparen";
                    }
                },
                {
                    regex: /}}/,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.shift();
                        this.next = stack.length ? stack[0] : (startPrefix + "start");
                        this.merge = false;
                        return "ttl-call-returned.keyword.operator.paren.rparen";
                    }
                },
                {
                    regex: /@<</,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.unshift(startPrefix + "ttl-import");
                        this.next = startPrefix + "ttl-import";
                        this.merge = false;
                        return "ttl-call-returned.keyword.operator";
                    }
                },
                createNextRule(
                    /@\\\\\s*/,
                    "ttl-call-returned.comment.block",
                    startPrefix + "ttl-call-returned"
                ),
                createNextRule(
                    /\s+/,
                    "ttl-call-returned.text",
                    startPrefix + "ttl-call-returned"
                ),
                createPopRule(/()?/, "ttl-call-returned.empty")
            ];

            rules[startPrefix + "ttl-call"] = [
                createPushRule(
                    /@\*/,
                    "ttl-call.comment.start",
                    startPrefix + "ttl-comment"
                ),
                {
                    regex: /@/,
                    onMatch: function (val, state, stack) {
                        stack.shift();
                        stack.unshift("cs-start");
                        this.next = "cs-start";
                        this.merge = false;
                        return "ttl-call.keyword";
                    },
                },
                createPushRule(
                    /\(/,
                    "ttl-call.keyword.paren.lparen",
                    startPrefix + "ttl-call"
                ),
                createPopRule(
                    /\)/,
                    "ttl-call.keyword.paren.rparen"
                ),
                createNextRule(
                    /::/,
                    "ttl-call.punctuation.operator",
                    startPrefix + "ttl-call"
                ),
                createNextRule(
                    /:/,
                    "ttl-call.punctuation.operator",
                    startPrefix + "ttl-call"
                ),
                createNextRule(
                    /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я0-9_]*/,
                    "ttl-call.variable.language",
                    startPrefix + "ttl-call"
                ),
                createNextRule(
                    /\./,
                    "ttl-call.punctuation.operator",
                    startPrefix + "ttl-call"
                ),
                createNextRule(
                    /\s+/,
                    "ttl-call.constant.other",
                    startPrefix + "ttl-call"
                )
            ];

            rules[startPrefix + "ttl-import"] = [
                createPushRule(
                    /@\*/,
                    "ttl-import.comment.start",
                    startPrefix + "ttl-comment"
                ),
                createNextRule(
                    /{{/,
                    "ttl-import.keyword.operator.paren.lparen",
                    startPrefix + "ttl-import"
                ),
                createNextRule(
                    /[^{}]+/,
                    "ttl-import.constant.other",
                    startPrefix + "ttl-import"
                ),
                createPopRule(
                    /}}/,
                    "ttl-import.keyword.operator.paren.rparen"
                ),
                createNextRule(
                    /./,
                    "ttl-import.constant.other",
                    startPrefix + "ttl-import"
                )
            ];

            rules[startPrefix + "ttl-def"] = [
                createPushRule(
                    /@\*/,
                    "ttl-def.comment.start",
                    startPrefix + "ttl-comment"
                ),
                createNextRule(
                    /</,
                    "ttl-def.punctuation.operator.paren.lparen",
                    startPrefix + "ttl-def-name"
                ),
                createPushRule(
                    /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*\s*</,
                    "ttl-def.storage.type",
                    startPrefix + "ttl-generic-type"
                ),
                createNextRule(
                    /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*\s*(\[])*/,
                    "ttl-def.storage.type",
                    startPrefix + "ttl-def"
                ),
                createPushRule(
                    /{{/,
                    "ttl-def.keyword.operator.paren.lparen",
                    startPrefix + "start"
                ),
                createPushRule(
                    /->/,
                    "ttl-def.keyword.operator",
                    startPrefix + "ttl-out"
                ),
                createNextRule(
                    /::/,
                    "ttl-def.keyword.operator",
                    startPrefix + "ttl-def"
                ),
                createNextRule(
                    /\s+/,
                    "ttl-def.whitespace",
                    startPrefix + "ttl-def"
                ),
                createPopRule(
                    /%@/,
                    "ttl-def.keyword.operator.paren.rparen"
                )
            ];

            rules[startPrefix + "ttl-def-name"] = [
                createNextRule(
                    /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*/,
                    "ttl-def-name.identifier",
                    startPrefix + "ttl-def-name"
                ),
                createNextRule(
                    /:/,
                    "ttl-def-name.keyword.operator",
                    startPrefix + "ttl-def-name"
                ),
                createNextRule(
                    />/,
                    "ttl-def-name.punctuation.operator.paren.rparen",
                    startPrefix + "ttl-def"
                )
            ];

            rules[startPrefix + "ttl-generic-type"] = [
                createNextRule(
                    /\s*[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.,]*\s*(\[])*/,
                    "ttl-generic-type.storage.type",
                    startPrefix + "ttl-generic-type"
                ),
                createNextRule(
                    /(\[])+/,
                    "ttl-generic-type.storage.type",
                    startPrefix + "ttl-generic-type"
                ),
                createPushRule(
                    /</,
                    "ttl-generic-type.storage.type",
                    startPrefix + "ttl-generic-type"
                ),
                createPopRule(
                    />/,
                    "ttl-generic-type.storage.type"
                )
            ];

            rules[startPrefix + "ttl-comment"] = [
                createPopRule(
                    /\*@/,
                    "ttl-comment.comment.end"
                ),
                createNextRule(
                    /([^*]|\*[^@])+/,
                    "ttl-comment.comment.block",
                    startPrefix + "ttl-comment"
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
                    this.next = stack.length ? stack[0] : (startPrefix + "start");
                    this.merge = false;
                    return "cs-start.paren.rparen";
                },
            }
        ]);
    };

    oop.inherits(TtlLangHighlightRules, TextHighlightRules);

    var TtlHighlightRules = function() {
        HtmlHighlightRules.call(this);
        
        var stackBackup = {
            startLines: {},
            endLines: {},
            lastStack: null,
            lastStartRow: null
        };

        var ttlRules = new TtlLangHighlightRules("html").getRules();
        var jsTtlRules = new TtlLangHighlightRules("js").generateRules();
        var cssTtlRules = new TtlLangHighlightRules("css").generateRules();

        this.$embeds = [];

        for(var key in this.$rules) {
            if (key.indexOf("js-") === 0) {
                this.$rules[key].unshift.apply(this.$rules[key], jsTtlRules["js-start"]);
            } else if (key.indexOf("css-") === 0) {
                this.$rules[key].unshift.apply(this.$rules[key], cssTtlRules["css-start"]);
            } else {
                this.$rules[key].unshift.apply(this.$rules[key], ttlRules["start"]);
            }
        }

        for(var key in ttlRules) {
            if (!this.$rules[key]) {
                this.$rules[key] = ttlRules[key];
            }
        }

        for(var key in jsTtlRules) {
            if (!this.$rules[key]) {
                this.$rules[key] = jsTtlRules[key];
            }
        }

        for(var key in cssTtlRules) {
            if (!this.$rules[key]) {
                this.$rules[key] = cssTtlRules[key];
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
                                next:  prefix + "js-start",
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

                    //this part could run from any inner state (ttl-, css-ttl-, js-ttl-, html-css-, html-js-, start)
                    var nextDefaultState = "start";
                    if (state) {
                        var cssState = state.indexOf("css-");
                        var jsState = state.indexOf("js-");
                        var ttlState = state.indexOf("ttl-");

                        if (ttlState > 0) {
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
                    return "ttl-raw.keyword.paren.rparen";
                },
            }
        ]);

        this.embedRules(CustomizableHtmlHighlightRules, "html-ln-", [
            {
                regex: /$|^/,
                onMatch: function (val, state, stack) {
                    stack.shift();

                    //this part could run from any inner state (ttl-, css-ttl-, js-ttl-, html-css-, html-js-, start)
                    var nextDefaultState = "start";
                    if (state) {
                        var cssState = state.indexOf("css-");
                        var jsState = state.indexOf("js-");
                        var ttlState = state.indexOf("ttl-");

                        if (ttlState > 0) {
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
                    return "ttl-call-returned.empty";
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
            {include : "attributes"},
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
            {include : "html-attributes"},
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
            {include : "html-ln-attributes"},
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
            {include : "attributes"},
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
            {include : "html-attributes"},
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
            {include : "html-ln-attributes"},
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

    oop.inherits(TtlHighlightRules, HtmlHighlightRules);

    exports.TtlHighlightRules = TtlHighlightRules;
    exports.TtlLangHighlightRules = TtlLangHighlightRules;
});