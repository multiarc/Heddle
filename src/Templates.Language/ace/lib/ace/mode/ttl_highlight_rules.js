define(function(require, exports, module) {
"use strict";

var oop = require("../lib/oop");
var HtmlHighlightRules = require("./html_highlight_rules").HtmlHighlightRules;
var TtlHighlightRules = function(options) {
    HtmlHighlightRules.call(this);
    var ttlRules = {
        "ttl-out": [
            {
                token: "comment.block",
                regex: "\\@\\*",
                push: "ttl-comment"
            },
            {
                token: ["keyword.operator", "paren.lparen"],
                regex: "\\{\\{",
                push: "start"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: "\\}\\}",
                next: "pop"
            },
            {
                token: "variable.language",
                regex: "[a-zA-Z_]+[a-zA-Z_0-9]*",
                next: "ttl-out"
            },
            {
                token: "punctuation.operator",
                regex: ":",
                next: "ttl-out"
            },
            {
                token: "paren.lparen",
                regex: "\\(",
                push: "ttl-out"
            },
            {
                token: "paren.rparen",
                regex: "\\)",
                next: "pop"
            },
            {
                token: "punctuation.operator",
                regex: ";",
                next: "start"
            },
            {
                token: ["keyword", "paren.lparen"],
                regex: "\\@\\{",
                push: "ttl-raw"
            },
            {
                token: ["keyword.operator", "paren.lparen"],
                regex: "\\<\\%",
                push: "ttl-def"
            },
            {
                token: "keyword",
                regex: "\\@",
                next: "ttl-out"
            },
            {include: "start"}
        ],
        "ttl-raw": [
            {
                token: ["keyword", "paren.rparen"],
                regex: "\\}\\@",
                next: "pop"
            },
            {
                token: "constant.other",
                regex: "([^\\}]|\\}[^\\@])*",
                next: "ttl-raw"
            }
        ],
        "ttl-def": [
            {
                token: "comment.block",
                regex: "\\@\\*",
                push: "ttl-comment"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: "\\%\\>",
                next: "pop"
            },
            {
                token: ["punctuation.operator", "paren.lparen"],
                regex: "\\<",
                next: "ttl-def_name"
            },
            {
                token: ["keyword.operator", "paren.rparen"],
                regex: "\\{\\{",
                push: "start"
            },
            {
                token: "keyword.operator",
                regex: "::",
                next: "ttl-def_type"
            },
            {
                token: "text",
                regex: "\\s+",
                next: "ttl-def"
            }
        ],
        "ttl-def_type": [
            {
                token: "comment.block",
                regex: "\\@\\*",
                push: "ttl-comment"
            },
            {
                token: "storage.type",
                regex: "[a-zA-Z_]+[a-zA-Z_0-9\\.\\+]*",
                next: "ttl-def"
            }
        ],
        "ttl-def_name": [
            {
                token: "comment.block",
                regex: "\\@\\*",
                push: "ttl-comment"
            },
            {
                token: "support.function",
                regex: "[a-zA-Z_]+[a-zA-Z_0-9]*",
                next: "ttl-def_name"
            }, {
                token: ["punctuation.operator", "paren.rparen"],
                regex: "\\>",
                next: "ttl-def"
            },
            {
                token: "keyword.operator",
                regex: ":",
                next: "ttl-def_name"
            }
        ],
        "ttl-comment": [
            {
                token: "comment.block",
                regex: ".*?\\*\\@",
                next: "pop"
            },
            {
                token: "comment.block",
                regex: "([^\\*]|\\*[^\\@])*",
                next: "ttl-comment"
            }
        ]
    };
    this.$rules.start.unshift(
        {
            token: ["keyword.operator", "paren.rparen"],
            regex: "\\%\\>",
            next: "pop"
        },
        {
            token: "comment.block",
            regex: "\\@\\*",
            push: "ttl-comment"
        },
        {
            token: ["keyword.operator", "paren.lparen"],
            regex: "\\<\\%",
            push: "ttl-def"
        },
        {
            token: ["keyword.operator", "paren.lparen"],
            regex: "\\{\\{",
            push: "start"
        },
        {
            token: ["keyword.operator", "paren.rparen"],
            regex: "\\}\\}",
            next: "pop"
        },
        {
            token: ["keyword", "paren.lparen"],
            regex: "\\@\\{",
            push: "ttl-raw"
        },
        {
            token: "keyword",
            regex: "\\@",
            next: "ttl-out"
        });
    this.addRules(ttlRules);
    this.normalizeRules();
};

oop.inherits(TtlHighlightRules, HtmlHighlightRules);

exports.TtlHighlightRules = TtlHighlightRules;
});
