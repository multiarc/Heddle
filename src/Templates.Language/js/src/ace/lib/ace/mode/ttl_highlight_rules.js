define(function(require, exports, module) {
"use strict";

var oop = require("../lib/oop");
var HtmlHighlightRules = require("./html_highlight_rules").HtmlHighlightRules;
var CSharpHighlightRules = require("./csharp_highlight_rules").CSharpHighlightRules;
var TtlHighlightRules = function(options) {
    HtmlHighlightRules.call(this);
    var ttlRules = {
        "start": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: "comment.block",
                regex: /@\\\\\s*/,
                next: "start"
            },
            {
                token: ["keyword", "paren.lparen"],
                regex: /@{/,
                push: "ttl-raw"
            },
            {
                token: "keyword",
                regex: /@:/,
                push: "ttl-raw-ln"
            },
            {
                token: ["keyword.operator", "paren.lparen"],
                regex: /@%/,
                push: "ttl-def"
            },
            {
                token: "keyword.operator",
                regex: /@<</,
                push: "ttl-import"
            },
            {
                token: "support.function",
                regex: /@/,
                next: "ttl-out"
            },
            //HTML
        ],
        "ttl-out": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: "identifier",
                regex: /[a-zA-Zа-яА-Я_][a-zA-Zа-яА-Я0-9_]+/,
                next: "ttl-out"
            },
            {
                regex: /\(/,
                onMatch: function (val, state, stack) {
                    stack.pop();
                    stack.push("ttl-call-returned");
                    this.next = "ttl-call";
                    return [{
                        type: "keyword",
                        value: val
                    }, {
                        type: "paren.lparen",
                        value: val
                    }];
                },
            },
            {
                token: "punctuation.operator",
                regex: /:/,
                next: "ttl-out",
            },
            {
                regex: /@{/,
                onMatch: function (val, state, stack)
                {
                    stack.pop();
                    this.next = "ttl-raw";
                    return [{
                        type: "keyword",
                        value: val
                    }, {
                        type: "paren.lparen",
                        value: val
                    }];
                }
            },
            {
                regex: /@:/,
                onMatch: function (val, state, stack)
                {
                    stack.pop();
                    this.next = "ttl-raw-ln";
                    return "keyword";
                }
            },
            {
                regex: /@%/,
                onMatch: function (val, state, stack)
                {
                    stack.pop();
                    this.next = "ttl-def";
                    return [{
                        type: "keyword.operator",
                        value: val
                    }, {
                        type: "paren.lparen",
                        value: val
                    }];
                }
            },
            {
                regex: /@<</,
                onMatch: function (val, state, stack)
                {
                    stack.pop();
                    this.next = "ttl-import";
                    return "keyword.operator";
                }
            },
            {
                token: "comment.block",
                regex: /@\\\\\s*/,
                next: "pop"
            },
            {
                token: "constant.other",
                regex: /\s+/,
                next: "ttl-out"
            },
            {
                token: "constant.other",
                regex: "",
                next: "pop"
            }
        ],
        "ttl-call-returned": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: "punctuation.operator",
                regex: /:/,
                next: "ttl-out",
            },
            {
                token: "keyword.operator",
                regex: /@/,
                next: "ttl-out",
            },
            {
                regex: /@{/,
                onMatch: function (val, state, stack) {
                    stack.pop();
                    this.next = "ttl-raw";
                    return [{
                        type: "keyword",
                        value: val
                    }, {
                        type: "paren.lparen",
                        value: val
                    }];
                }
            },
            {
                regex: /@:/,
                onMatch: function (val, state, stack) {
                    stack.pop();
                    this.next = "ttl-raw-ln";
                    return "keyword";
                }
            },
            {
                regex: /@%/,
                onMatch: function (val, state, stack) {
                    stack.pop();
                    this.next = "ttl-def";
                    return [{
                        type: "keyword.operator",
                        value: val
                    }, {
                        type: "paren.lparen",
                        value: val
                    }];
                }
            },
            {
                regex: /{{/,
                onMatch: function (val, state, stack) {
                    stack.pop();
                    this.next = "ttl-sub";
                    return [{
                        type: "keyword.operator",
                        value: val
                    }, {
                        type: "paren.lparen",
                        value: val
                    }];
                }
            },
            {
                regex: /}}/,
                onMatch: function (val, state, stack) {
                    stack.pop();
                    this.next = stack.pop();
                    return [{
                        type: "keyword.operator",
                        value: val
                    }, {
                        type: "paren.rparen",
                        value: val
                    }];
                }
            },
            {
                regex: /@<</,
                onMatch: function (val, state, stack) {
                    stack.pop();
                    this.next = "ttl-import";
                    return "keyword.operator";
                }
            },
            {
                token: "comment.block",
                regex: /@\\\\\s*/,
                next: "ttl-call-returned"
            },
            {
                token: "constant.other",
                regex: "",
                next: "pop"
            }
        ],
        "ttl-call": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                regex: /@/,
                onMatch: function (val, state, stack)
                {
                    stack.pop();
                    this.next = "cs-start";
                    return "keyword";
                },
            },
            {
                token: ["keyword", "paren.lparen"],
                regex: /\(/,
                push: "ttl-call"
            },
            {
                token: ["keyword", "paren.rparen"],
                regex: /\)/,
                next: "pop"
            },
            {
                token: "punctuation.operator",
                regex: /:/,
                next: "ttl-call",
            },
            {
                token: "punctuation.operator",
                regex: /::/,
                next: "ttl-call",
            },
            {
                token: "variable.language",
                regex: /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я0-9_]*/,
                next: "ttl-call"
            },
            {
                token: "punctuation.operator",
                regex: /\./,
                next: "ttl-call",
            }
        ],
        "ttl-raw": [
            {
                token: ["keyword", "paren.rparen"],
                regex: /}@/,
                next: "pop"
            },
            {
                token: "constant.other",
                regex: /([^}]|}[^@])+/,
                next: "ttl-raw"
            }
        ],
        "ttl-raw-ln": [
            {
                token: "constant.other",
                regex: /.*/,
                next: "pop"
            }
        ],
        "ttl-import": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: ["keyword.operator", "paren.lparen"],
                regex: /{{/,
                next: "ttl-import"
            },
            {
                token: "constant.other",
                regex: /[^{}]+/,
                next: "ttl-import"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: /}}/,
                next: "pop"
            },
            {
                token: "constant.other",
                regex: /./,
                next: "ttl-import"
            }
        ],
        "ttl-def": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: ["punctuation.operator", "paren.lparen"],
                regex: /</,
                next: "ttl-def-name"
            },
            {
                token: "storage.type",
                regex: /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*</,
                push: "ttl-generic-type"
            },
            {
                token: "storage.type",
                regex: /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*\s*(\[])*/,
                next: "ttl-def"
            },
            {
                token: ["keyword.operator", "paren.lparen"],
                regex: /{{/,
                push: "ttl-sub"
            },
            {
                token: "keyword.operator",
                regex: /->/,
                push: "ttl-out"
            },
            {
                token: "keyword.operator",
                regex: /::/,
                next: "ttl-def"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: /%@/,
                next: "pop"
            }
        ],
        "ttl-def-name": [
            {
                token: "identifier",
                regex: /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.]*/,
                next: "ttl-def-name"
            },
            {
                token: "keyword.operator",
                regex: /:/,
                next: "ttl-def-name"
            },
            {
                token: ["punctuation.operator", "paren.rparen"],
                regex: />/,
                next: "ttl-def"
            },
        ],
        "ttl-generic-type": [
            {
                token: "storage.type",
                regex: /[a-zA-Zа-яА-Я_]+[a-zA-Zа-яА-Я_0-9.,]*(\[])*/,
                next: "ttl-def-generic-type"
            },
            {
                token: "storage.type",
                regex: /(\[])*/,
                next: "ttl-def-generic-type"
            },
            {
                token: "storage.type",
                regex: /</,
                push: "ttl-def-generic-type"
            },
            {
                token: "storage.type",
                regex: />/,
                next: "pop"
            },
        ],
        "ttl-sub": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: "comment.block",
                regex: /@\\\\\s*/,
                next: "ttl-sub"
            },
            {
                token: ["keyword", "paren.lparen"],
                regex: /@{/,
                push: "ttl-raw"
            },
            {
                token: "keyword",
                regex: /@:/,
                push: "ttl-raw-ln"
            },
            {
                token: ["keyword.operator", "paren.lparen"],
                regex: /@%/,
                push: "ttl-def"
            },
            {
                token: "keyword.operator",
                regex: /@<</,
                push: "ttl-import"
            },
            {
                token: "support.function",
                regex: /@/,
                next: "ttl-out"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: /}}/,
                next: "pop"
            },
            //HTML
        ],
        "ttl-comment": [
            {
                token: "comment.block",
                regex: /\*@/,
                next: "pop"
            },
            {
                token: "comment.block",
                regex: /([^*]|\*[^@])+/,
                next: "ttl-comment"
            }
        ],
        "ttl-template-type": [
            {
                token: "comment.block",
                regex: /\*@/,
                next: "pop"
            },
            {
                token: "comment.block",
                regex: /([^\*]|\*[^@])*/,
                next: "ttl-comment"
            }
        ]
    };
    this.embedRules(CSharpHighlightRules, "cs-", [
        {
            token: "paren.lparen",
            regex: /\(/,
            push: "cs-start"
        },
        {
            token: "paren.rparen",
            regex: /\)/,
            next: "pop"
        },
    ]);
    this.addRules(ttlRules);
    this.normalizeRules();
};

oop.inherits(TtlHighlightRules, HtmlHighlightRules);

exports.TtlHighlightRules = TtlHighlightRules;
});
