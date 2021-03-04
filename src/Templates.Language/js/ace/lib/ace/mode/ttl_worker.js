define(function (require, exports, module) {
    "use strict";
    
    var oop = require("../lib/oop");
    var lang = require("../lib/lang");
    var Mirror = require("../worker/mirror").Mirror;
    var TtlMode = require("../mode/ttl").Mode;
    var DocumentParser = require("./ttl/DocumentParser").DocumentParser;
    var SAXParser = require("./html/saxparser").SAXParser;
    var CSSLint = require("./css/csslint").CSSLint;

    var htmlErrorTypes = {
        "expected-doctype-but-got-start-tag": "info",
        "expected-doctype-but-got-chars": "info",
        "non-html-root": "info"
    };

    var TtlWorker = exports.TtlWorker = function(sender) {
        Mirror.call(this, sender);
        this.setTimeout(500);
        this.ruleset = null;
        this.setDisabledRules("ids|order-alphabetical");
        this.setInfoRules(
            "adjoining-classes|qualified-headings|zero-units|gradients|" +
            "import|outline-none|vendor-prefix"
        );
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
            this.options = options || {};
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

                for (var lineIndex = 0; lineIndex < lines.length; lineIndex++) {
                    var lineTokens = tokenizer.getLineTokens(lines[lineIndex], state);
                    for (var tokenIndex = 0; tokenIndex < lineTokens.tokens.length; tokenIndex++) {
                        var token = lineTokens.tokens[tokenIndex];
                        if (token.state) {
                            if (token.state.indexOf("js-") === 0) {
                                if (token.type.indexOf("ttl-") !== 0) {
                                    if (token.type.indexOf("meta.tag") === 0) {
                                        htmlTokens.push({token: token, row: lineIndex});
                                    } else {
                                        jsTokens.push({token: token, row: lineIndex});
                                    }
                                }
                            } else if (token.state.indexOf("css-") === 0) {
                                if (token.type.indexOf("ttl-") !== 0) {
                                    if (token.type.indexOf("meta.tag") === 0) {
                                        htmlTokens.push({token: token, row: lineIndex});
                                    }
                                    else {
                                        cssTokens.push({token: token, row: lineIndex});
                                    }
                                }
                            } else if (token.state.indexOf("cs-") === 0) {
                                csTokens.push({token: token, row: lineIndex});
                            } else if (token.state.indexOf("ttl-") !== 0) {
                                if (token.type.indexOf("ttl-") !== 0) {
                                    htmlTokens.push({token: token, row: lineIndex});
                                }
                            }
                        } else {
                            if (token.type) {
                                if (token.type.indexOf("ttl-") !== 0) {
                                    htmlTokens.push({token: token, row: lineIndex});
                                }
                            } else {
                                htmlTokens.push({token: token, row: lineIndex});
                            }
                        }
                    }

                    state = lineTokens.state;
                }
                
                var htmlString = processEmbeddedLanguageLines(htmlTokens);
                
                if (htmlString) {
                    var saxParser = new SAXParser();
                    var htmlErrors = [];
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
                            htmlErrors.push({
                                row: location.line,
                                column: location.column,
                                text: message,
                                type: htmlErrorTypes[code] || "error"
                            });
                        }
                    };
                    saxParser.parse(htmlString);
                    this.sender.emit("error", htmlErrors);
                }
                
                var cssString = processEmbeddedLanguageLines(cssTokens);
                
                if (cssString) {
                    var infoRules = this.infoRules;

                    var result = CSSLint.verify(cssString, this.ruleset);
                    this.sender.emit("annotate", result.messages.map(function(msg) {
                        return {
                            row: msg.line - 1,
                            column: msg.col - 1,
                            text: msg.message,
                            type: infoRules[msg.rule.id] ? "info" : msg.type,
                            rule: msg.rule.name
                        };
                    }));
                }
            }
        };

    }).call(TtlWorker.prototype);
});