define(function(require, exports, module) {
"use strict";

var oop = require("../lib/oop");
var HtmlHighlightRules = require("./html_highlight_rules").HtmlHighlightRules;
var TextHighlightRules = require("./text_highlight_rules").TextHighlightRules;
var CSharpHighlightRules = require("./csharp_highlight_rules").CSharpHighlightRules;
var TtlHighlightRules = function(options) {
    TextHighlightRules.call(this);
    var ttlRules = {
        "start": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: ["keyword.operator", "paren.lparen"],
                regex: /{{/,
                push: "html-start"
            },
            {
                onMatch: function (val, state, stack) {
                    stack.shift();
                    if (stack.length > 0 && stack[0] != "ttl-def") {
                        this.next = "html-start";
                    }
                    return "keyword.operator";
                },
                regex: /}}/
            },
            {
                token: "support.function",
                regex: /[a-zA-Z_]+[a-zA-Z_0-9]*/,
                next: "start"
            },
            {
                token: "punctuation.operator",
                regex: /:/,
                next: "start"
            },
            {
                token: "paren.lparen",
                regex: /\(/,
                push: "ttl-call"
            },
            {
                token: "punctuation.operator",
                regex: /;/,
                next: "html-start"
            },
            {
                token: ["keyword", "paren.lparen"],
                regex: /@{/,
                push: "ttl-raw"
            },
            {
                token: ["keyword.operator", "paren.lparen"],
                regex: /<%/,
                push: "ttl-def"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: /%>/,
                next: "pop"
            },
            {
                token: "keyword.operator",
                regex: /@/,
                next: "start"
            },
            { include : "html-start"}
        ],
        "ttl-call": [
            {
                token: "punctuation.operator",
                regex: /:/,
                next: "ttl-call"
            },
            {
                token: "variable.language",
                regex: /[a-zA-Z_]+[a-zA-Z_0-9]*/,
                next: "ttl-call"
            },
            {
                token: "paren.lparen",
                regex: /\(/,
                push: "ttl-call"
            },
            {
                onMatch: function (val, state, stack)
                {
                    stack.shift();
                    this.next = "html-start";
                    return "paren.rparen";
                },
                regex: /(\)\s*;)|(\)\s*(?!:|{{|\)|@))/
            },
            {
                token: "paren.rparen",
                regex: /\)/,
                next: "pop"
            },
            {
                token: "paren.rparen",
                regex: /@/,
                next: "cs-start"
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
                regex: /([^}]|}[^@])*/,
                next: "ttl-raw"
            }
        ],
        "ttl-def": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: /%>/,
                next: "pop"
            },
            {
                token: ["punctuation.operator", "paren.lparen"],
                regex: /</,
                next: "ttl-def_name"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: /{{/,
                push: "html-start"
            },
            {
                token: "keyword.operator",
                regex: /::/,
                next: "ttl-def_type"
            },
            {
                token: "text",
                regex: /\s+/,
                next: "ttl-def"
            }
        ],
        "ttl-def_type": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: "storage.type",
                regex: /[a-zA-Z_]+[a-zA-Z_0-9\.\+]*/,
                next: "ttl-def"
            }
        ],
        "ttl-def_name": [
            {
                token: "comment.block",
                regex: /@\*/,
                push: "ttl-comment"
            },
            {
                token: "support.function",
                regex: /[a-zA-Z_]+[a-zA-Z_0-9]*/,
                next: "ttl-def_name"
            }, {
                token: ["punctuation.operator", "paren.rparen"],
                regex: />/,
                next: "ttl-def"
            },
            {
                token: "keyword.operator",
                regex: /:/,
                next: "ttl-def_name"
            }
        ],
        "ttl-comment": [
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
            onMatch: function (val, state, stack) {
                stack.shift();
                if (stack.length > 0 && stack[0] == "start") {
                    this.next = "html-start";
                }
                return "paren.rparen";
            },
            regex: /(\)\s*;)|(\)\s*(?!:|{{|\)|@))/
        },
        {
            token: "paren.rparen",
            regex: /\)/,
            next: "pop"
        },
    ]);
    this.embedRules(HtmlHighlightRules, "html-", [
        {
            token: ["keyword.operator", "paren.lparen"],
            regex: /{{/,
            push: "html-start"
        },
        {
            onMatch: function (val, state, stack) {
                stack.shift();
                this.next = "ttl-def";
                return "keyword.operator";
            },
            regex: /}}\s*(?=::)/
        },
        {
            onMatch: function (val, state, stack) {
                stack.shift();
                if (stack.length > 0 && stack[0] != "ttl-def") {
                    this.next = "html-start";
                }
                return "keyword.operator";
            },
            regex: /}}/
        },
        {
            token: ["keyword", "paren.lparen"],
            regex: /@{/,
            push: "ttl-raw"
        },
        {
            token: ["keyword.operator", "paren.lparen"],
            regex: /<%/,
            push: "ttl-def"
        },
        {
            onMatch: function (val, state, stack) {
                stack.shift();
                this.next = "html-start";
                return "keyword.operator";
            },
            regex: /%>/,
        },
        {
            onMatch: function (val, state, stack) {
                if (state == "html-start") {
                    this.next = "start";
                }
                return "keyword.operator";
            },
            regex: /@/
        }
    ]);
    this.addRules(ttlRules);
    this.normalizeRules();
};

oop.inherits(TtlHighlightRules, HtmlHighlightRules);

exports.TtlHighlightRules = TtlHighlightRules;
});
