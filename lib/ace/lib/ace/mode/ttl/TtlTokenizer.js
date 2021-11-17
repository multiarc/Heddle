define(function(require, exports, module) {
    "use strict";

    var oop = require("../../lib/oop");
    var Tokenizer = require("../../tokenizer").Tokenizer;

    var MAX_TOKEN_COUNT = 2000;
    
    var TtlWorkerTokenizer = function(rules) {
        Tokenizer.call(this, rules);
    }
    oop.inherits(TtlWorkerTokenizer, Tokenizer);

    var TtlTokenizer = function(rules) {
        Tokenizer.call(this, rules);
    }
    
    oop.inherits(TtlTokenizer, Tokenizer);

    (function() {
        /**
         * Returns an object containing two properties: `tokens`, which contains all the tokens; and `state`, the current state.
         * @returns {Object}
         **/
        this.getLineTokens = function(line, startState, row) {
            if (startState && typeof startState != "string") {
                var stack = startState.slice(0);
                startState = stack[0];
                if (startState === "#tmp") {
                    stack.shift();
                    startState = stack.shift();
                }
            } else
                var stack = [];

            var currentState = startState || "start";
            var state = this.states[currentState];
            if (!state) {
                currentState = "start";
                state = this.states[currentState];
            }
            var mapping = this.matchMappings[currentState];
            var re = this.regExps[currentState];
            re.lastIndex = 0;

            var match, tokens = [];
            var lastIndex = 0;
            var matchAttempts = 0;

            var token = {type: null, value: "", state: currentState};

            while (match = re.exec(line)) {
                var type = mapping.defaultToken;
                var rule = null;
                var value = match[0];
                var index = re.lastIndex;
                var tokenState = currentState;

                if (index - value.length > lastIndex) {
                    var skipped = line.substring(lastIndex, index - value.length);
                    if (token.type == type) {
                        token.value += skipped;
                    } else {
                        if (token.type)
                            tokens.push(token);
                        token = {type: type, value: skipped, state: tokenState};
                    }
                }

                for (var i = 0; i < match.length-2; i++) {
                    if (match[i + 1] === undefined)
                        continue;

                    rule = state[mapping[i]];

                    if (rule.onMatch)
                        type = rule.onMatch(value, currentState, stack, line, row);
                    else
                        type = rule.token;

                    if (rule.next) {
                        if (typeof rule.next == "string") {
                            currentState = rule.next;
                        } else {
                            currentState = rule.next(currentState, stack);
                        }

                        state = this.states[currentState];
                        if (!state) {
                            this.reportError("state doesn't exist", currentState);
                            currentState = "start";
                            state = this.states[currentState];
                        }
                        mapping = this.matchMappings[currentState];
                        lastIndex = index;
                        re = this.regExps[currentState];
                        re.lastIndex = index;
                    }
                    if (rule.consumeLineEnd)
                        lastIndex = index;
                    break;
                }

                if (value) {
                    if (typeof type === "string") {
                        if ((!rule || rule.merge !== false) && token.type === type) {
                            token.value += value;
                        } else {
                            if (token.type)
                                tokens.push(token);
                            token = {type: type, value: value, state: tokenState};
                        }
                    } else if (type) {
                        if (token.type)
                            tokens.push(token);
                        token = {type: null, value: "", state: tokenState};
                        for (var i = 0; i < type.length; i++)
                            tokens.push({...type[i], state: tokenState});
                    }
                }

                if (lastIndex == line.length)
                    break;

                lastIndex = index;

                if (matchAttempts++ > MAX_TOKEN_COUNT) {
                    if (matchAttempts > 2 * line.length) {
                        this.reportError("infinite loop with in ace tokenizer", {
                            startState: startState,
                            line: line
                        });
                    }
                    // chrome doens't show contents of text nodes with very long text
                    while (lastIndex < line.length) {
                        if (token.type)
                            tokens.push(token);
                        token = {
                            value: line.substring(lastIndex, lastIndex += 500),
                            type: "overflow",
                            state: tokenState
                        };
                    }
                    currentState = "start";
                    stack = [];
                    break;
                }
            }

            if (token.type)
                tokens.push(token);

            //>= 1 ???
            if (stack.length >= 1) {
                if (stack[0] !== currentState)
                    stack.unshift("#tmp", currentState);
            }
            // if (stack.length === 1 && stack[0] === startState && startState !== currentState) {
            //     stack.unshift(currentState);
            // }
            return {
                tokens: tokens,
                state: stack.length ? stack : currentState
            };
        };
    }).call(TtlWorkerTokenizer.prototype);

    (function() {
        /**
         * Returns an object containing two properties: `tokens`, which contains all the tokens; and `state`, the current state.
         * @returns {Object}
         **/
        this.getLineTokens = function(line, startState, row) {
            if (startState && typeof startState != "string") {
                var stack = startState.slice(0);
                startState = stack[0];
                if (startState === "#tmp") {
                    stack.shift();
                    startState = stack.shift();
                }
            } else
                var stack = [];

            var currentState = startState || "start";
            var state = this.states[currentState];
            if (!state) {
                currentState = "start";
                state = this.states[currentState];
            }
            var mapping = this.matchMappings[currentState];
            var re = this.regExps[currentState];
            re.lastIndex = 0;

            var match, tokens = [];
            var lastIndex = 0;
            var matchAttempts = 0;

            var token = {type: null, value: ""};

            while (match = re.exec(line)) {
                var type = mapping.defaultToken;
                var rule = null;
                var value = match[0];
                var index = re.lastIndex;

                if (index - value.length > lastIndex) {
                    var skipped = line.substring(lastIndex, index - value.length);
                    if (token.type == type) {
                        token.value += skipped;
                    } else {
                        if (token.type)
                            tokens.push(token);
                        token = {type: type, value: skipped};
                    }
                }

                for (var i = 0; i < match.length-2; i++) {
                    if (match[i + 1] === undefined)
                        continue;

                    rule = state[mapping[i]];

                    if (rule.onMatch)
                        type = rule.onMatch(value, currentState, stack, line, row);
                    else
                        type = rule.token;

                    if (rule.next) {
                        if (typeof rule.next == "string") {
                            currentState = rule.next;
                        } else {
                            currentState = rule.next(currentState, stack);
                        }

                        state = this.states[currentState];
                        if (!state) {
                            this.reportError("state doesn't exist", currentState);
                            currentState = "start";
                            state = this.states[currentState];
                        }
                        mapping = this.matchMappings[currentState];
                        lastIndex = index;
                        re = this.regExps[currentState];
                        re.lastIndex = index;
                    }
                    if (rule.consumeLineEnd)
                        lastIndex = index;
                    break;
                }

                if (value) {
                    if (typeof type === "string") {
                        if ((!rule || rule.merge !== false) && token.type === type) {
                            token.value += value;
                        } else {
                            if (token.type)
                                tokens.push(token);
                            token = {type: type, value: value};
                        }
                    } else if (type) {
                        if (token.type)
                            tokens.push(token);
                        token = {type: null, value: ""};
                        for (var i = 0; i < type.length; i++)
                            tokens.push(type[i]);
                    }
                }

                if (lastIndex == line.length)
                    break;

                lastIndex = index;

                if (matchAttempts++ > MAX_TOKEN_COUNT) {
                    if (matchAttempts > 2 * line.length) {
                        this.reportError("infinite loop with in ace tokenizer", {
                            startState: startState,
                            line: line
                        });
                    }
                    // chrome doens't show contents of text nodes with very long text
                    while (lastIndex < line.length) {
                        if (token.type)
                            tokens.push(token);
                        token = {
                            value: line.substring(lastIndex, lastIndex += 500),
                            type: "overflow"
                        };
                    }
                    currentState = "start";
                    stack = [];
                    break;
                }
            }

            if (token.type)
                tokens.push(token);

            if (stack.length >= 1) {
                if (stack[0] !== currentState)
                    stack.unshift("#tmp", currentState);
            }
            return {
                tokens : tokens,
                state : stack.length ? stack : currentState
            };
        };
    }).call(TtlTokenizer.prototype);
    
    exports.TtlWorkerTokenizer = TtlWorkerTokenizer;
    exports.TtlTokenizer = TtlTokenizer;
});