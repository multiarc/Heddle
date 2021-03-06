define(function (require, exports, module) {
    "use strict";
    
    var oop = require("../lib/oop");
    var lang = require("../lib/lang");
    var Mirror = require("../worker/mirror").Mirror;
    var TtlMode = require("../mode/ttl").WorkerMode;
    var DocumentParser = require("./ttl/DocumentParser").DocumentParser;
    var SAXParser = require("./html/saxparser").SAXParser;
    var CSSLint = require("./css/csslint").CSSLint;
    var lint = require("./javascript/jshint").JSHINT;

    var htmlErrorTypes = {
        "expected-doctype-but-got-start-tag": "info",
        "expected-doctype-but-got-chars": "info",
        "non-html-root": "info"
    };

    function startRegex(arr) {
        return RegExp("^(" + arr.join("|") + ")");
    }

    var disabledWarningsRe = startRegex([
        "Bad for in variable '(.+)'.",
        'Missing "use strict"'
    ]);
    var errorsRe = startRegex([
        "Unexpected",
        "Expected ",
        "Confusing (plus|minus)",
        "\\{a\\} unterminated regular expression",
        "Unclosed ",
        "Unmatched ",
        "Unbegun comment",
        "Bad invocation",
        "Missing space after",
        "Missing operator at"
    ]);
    var infoRe = startRegex([
        "Expected an assignment",
        "Bad escapement of EOL",
        "Unexpected comma",
        "Unexpected space",
        "Missing radix parameter.",
        "A leading decimal point can",
        "\\['{a}'\\] is better written in dot notation.",
        "'{a}' used out of scope"
    ]);

    var TtlWorker = exports.TtlWorker = function(sender) {
        Mirror.call(this, sender);
        this.setTimeout(500);
        this.ruleset = null;
        this.setDisabledRules("ids|order-alphabetical");
        this.setInfoRules(
            "adjoining-classes|qualified-headings|zero-units|gradients|" +
            "import|outline-none|vendor-prefix"
        );
        this.setOptions();
    };

    oop.inherits(TtlWorker, Mirror);

    (function() {
        function processEmbeddedLanguageLines(lineTokens) {
            var stringArray = [];
            var lastRow = 0;

            for (var i = 0; i < lineTokens.length; i++) {
                for (; lastRow < lineTokens[i].row; lastRow++) {
                    stringArray.push('\n');
                }
                stringArray.push(lineTokens[i].token.value);
            }

            return stringArray.join('');
        }
        
        this.setOptions = function(options) {
            this.options = options || {
                jsOptions: {
                    // undef: true,
                    // unused: true,
                    esnext: true,
                    moz: true,
                    devel: true,
                    browser: true,
                    node: true,
                    laxcomma: true,
                    laxbreak: true,
                    lastsemic: true,
                    onevar: false,
                    passfail: false,
                    maxerr: 100,
                    expr: true,
                    multistr: true,
                    globalstrict: true
                }
            };
            this.doc.getValue() && this.deferredUpdate.schedule(100);
        };

        this.setInfoRules = function(ruleNames) {
            if (typeof ruleNames == "string")
                ruleNames = ruleNames.split("|");
            this.infoRules = lang.arrayToMap(ruleNames);
            this.doc.getValue() && this.deferredUpdate.schedule(100);
        };

        this.setDisabledRules = function(ruleNames) {
            if (!ruleNames) {
                this.ruleset = null;
            } else {
                if (typeof ruleNames == "string")
                    ruleNames = ruleNames.split("|");
                var all = {};

                CSSLint.getRules().forEach(function(x){
                    all[x.id] = true;
                });
                ruleNames.forEach(function(x) {
                    delete all[x];
                });

                this.ruleset = all;
            }
            this.doc.getValue() && this.deferredUpdate.schedule(100);
        };

        this.changeOptions = function(newOptions) {
            oop.mixin(this.options, newOptions);
            this.doc.getValue() && this.deferredUpdate.schedule(100);
        };

        this.isValidJS = function(str) {
            try {
                // evaluated code can only create variables in this function
                eval("throw 0;" + str);
            } catch(e) {
                if (e === 0)
                    return true;
            }
            return false;
        };
        
        this.validateJs = function(value) {
            if (!value)
                return [];
            
            value = value.replace(/^#!.*\n/, "\n");
            if (!value)
                return [];

            var errors = [];
            // jshint reports many false errors
            // report them as error only if code is actually invalid
            var maxErrorLevel = this.isValidJS(value) ? "warning" : "error";

            // var start = new Date();
            lint(value, this.options.jsOptions, this.options.jsOptions.globals);
            var results = lint.errors;

            var errorAdded = false;
            for (var i = 0; i < results.length; i++) {
                var error = results[i];
                if (!error)
                    continue;
                var raw = error.raw;
                var type = "warning";

                if (raw == "Missing semicolon.") {
                    var str = error.evidence.substr(error.character);
                    str = str.charAt(str.search(/\S/));
                    if (maxErrorLevel == "error" && str && /[\w\d{(['"]/.test(str)) {
                        error.reason = 'Missing ";" before statement';
                        type = "error";
                    } else {
                        type = "info";
                    }
                }
                else if (disabledWarningsRe.test(raw)) {
                    continue;
                }
                else if (infoRe.test(raw)) {
                    type = "info";
                }
                else if (errorsRe.test(raw)) {
                    errorAdded  = true;
                    type = maxErrorLevel;
                }
                else if (raw == "'{a}' is not defined.") {
                    type = "warning";
                }
                else if (raw == "'{a}' is defined but never used.") {
                    type = "info";
                }

                errors.push({
                    row: error.line-1,
                    column: error.character-1,
                    text: error.reason,
                    type: type,
                    raw: raw
                });

                if (errorAdded) {
                    // break;
                }
            }
            
            return errors;
        }

        this.onUpdate = function() {
            var value = this.doc.getValue();
            if (!value) {
                return;
            }

            var parser = new DocumentParser(value);
            var results = parser.parseGetErrors();
            var errors = [];
            for (var i = 0; i < results.length; i++) {
                var error = results[i];
                if (!error || error.position === null)
                    continue;
                var position = this.doc.indexToPosition(error.position.startIndex);
                errors.push({
                    row: position.row,
                    column: position.column,
                    text: error.message,
                    type: "error"
                });
            }
            this.sender.emit("annotate", errors);
            
            if (errors.length === 0) {
                var tokenizer = new TtlMode().getTokenizer();

                var lines = this.doc.getAllLines();
                var htmlTokens = [];
                var jsTokens = [];
                var cssTokens = [];
                var csTokens = [];

                var state = ["start"];

                for (var row = 0; row < lines.length; row++) {
                    var lineTokens = tokenizer.getLineTokens(lines[row], state, row);
                    for (var tokenIndex = 0; tokenIndex < lineTokens.tokens.length; tokenIndex++) {
                        var token = lineTokens.tokens[tokenIndex];
                        if (token.state) {
                            if (token.state.indexOf("js-") === 0) {
                                if (token.type.indexOf("ttl-") !== 0) {
                                    if (token.type.indexOf("meta.tag") === 0) {
                                        htmlTokens.push({token: token, row: row});
                                    } else {
                                        jsTokens.push({token: token, row: row});
                                    }
                                }
                            } else if (token.state.indexOf("css-") === 0) {
                                if (token.type.indexOf("ttl-") !== 0) {
                                    if (token.type.indexOf("meta.tag") === 0) {
                                        htmlTokens.push({token: token, row: row});
                                    }
                                    else {
                                        cssTokens.push({token: token, row: row});
                                    }
                                }
                            } else if (token.state.indexOf("cs-") === 0) {
                                csTokens.push({token: token, row: row});
                            } else if (token.state.indexOf("ttl-") !== 0) {
                                if (token.type.indexOf("ttl-") !== 0) {
                                    htmlTokens.push({token: token, row: row});
                                }
                            }
                        } else {
                            if (token.type) {
                                if (token.type.indexOf("ttl-") !== 0) {
                                    htmlTokens.push({token: token, row: row});
                                }
                            } else {
                                htmlTokens.push({token: token, row: row});
                            }
                        }
                    }

                    state = lineTokens.state;
                }
                
                var htmlString = processEmbeddedLanguageLines(htmlTokens);
                
                if (htmlString) {
                    var saxParser = new SAXParser();
                    var noop = function () {
                    };
                    saxParser.contentHandler = {
                        startDocument: noop,
                        endDocument: noop,
                        startElement: noop,
                        endElement: noop,
                        characters: noop
                    };
                    saxParser.errorHandler = {
                        error: function (message, location, code) {
                            errors.push({
                                row: location.line,
                                column: location.column,
                                text: message,
                                type: htmlErrorTypes[code] || "error"
                            });
                        }
                    };
                    saxParser.parse(htmlString);
                }
                var cssString = processEmbeddedLanguageLines(cssTokens);
                
                if (cssString) {
                    var infoRules = this.infoRules;

                    var result = CSSLint.verify(cssString, this.ruleset);

                    result.messages.forEach(function (msg) {
                        errors.push({
                            row: msg.line - 1,
                            column: msg.col - 1,
                            text: msg.message,
                            type: infoRules[msg.rule.id] ? "info" : msg.type,
                            rule: msg.rule.name
                        });
                    });
                }
                
                var jsString = processEmbeddedLanguageLines(jsTokens);
                jsString = jsString.replace(/^#!.*\n/, "\n");

                this.validateJs(jsString).forEach(jsError => errors.push(jsError));
                
                this.sender.emit("annotate", errors);
            }
        };

    }).call(TtlWorker.prototype);
});