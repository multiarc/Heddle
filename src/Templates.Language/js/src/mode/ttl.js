define(function(require, exports, module) {
    "use strict";

    var oop = require("../lib/oop");
    var HtmlMode = require("./html").Mode;
    var TextMode = require("./text").Mode;
    var CSharpMode = require("./csharp").Mode;
    var JavaScriptMode = require("./javascript").Mode;
    var CssMode = require("./css").Mode;
    var TtlFoldMode = require("./folding/ttl").FoldMode;
    var TtlHighlightRules = require("./ttl_highlight_rules").TtlHighlightRules;
    var TtlLangHighlightRules = require("./ttl_highlight_rules").TtlLangHighlightRules;
    var TTLMatchingBraceOutdent = require("./ttl_brace_outdent").TTLMatchingBraceOutdent;
    var TTLstyleBehaviour = require("./behaviour/ttl").TTLstyleBehaviour;
    var TtlCompletions = require("./ttl_completions").TtlCompletions;
    var TtlTokenizer = require("./ttl/TtlTokenizer").TtlTokenizer;
    var WorkerClient = require("../worker/worker_client").WorkerClient;
    
    var TtlMode = function() {
        this.HighlightRules = TtlLangHighlightRules;
        this.$completer = new TtlCompletions();
        this.$outdent = new TTLMatchingBraceOutdent();
        this.$behaviour = new TTLstyleBehaviour();
        this.foldingRules = new TtlFoldMode();
    }

    oop.inherits(TtlMode, TextMode);

    (function() {
        var tokenTable = {
            "@%": "%@",
            "{{": "}}"
        };

        var reverseTokenTable = {
            "%@": "@%",
            "}}": "{{"
        };

        this.getTokenizer = function() {
            if (!this.$tokenizer) {
                this.$highlightRules = this.$highlightRules || new this.HighlightRules(this.$highlightRuleConfig);
                this.$tokenizer = new TtlTokenizer(this.$highlightRules.getRules());
            }
            return this.$tokenizer;
        };

        this.getNextLineIndent = function(state, line, tab) {
            var indent = this.$getIndent(line);
            if (line.match(/^.*({{|@%)\s*$/))
                indent += tab;
            else if (line.match(/^.*(}}|%@)\s*$/))
                indent.substring(0, indent.length - tab.length);
            return indent;
        };

        this.checkOutdent = function(state, line, input) {
            return this.$outdent.checkOutdent(line, input);
        };

        this.autoOutdent = function(state, doc, row) {
            this.$outdent.autoOutdent(doc, row);
        };
        this.getCompletions = function(state, session, pos, prefix) {
            return this.$completer.getCompletions(state, session, pos, prefix);
        };

        this.$id = "ace/mode/ttl_inline";
    }).call(TtlMode.prototype);

    var Mode = function() {
        HtmlMode.call(this);
        this.blockComment = { start: "@*", end: "*@" };
        this.HighlightRules = TtlHighlightRules;
        this.createModeDelegates({
            "js-": JavaScriptMode,
            "css-": CssMode,
            "cs-": CSharpMode,
            "ttl-": TtlMode
        });
        var ttlFoldMode = new TtlFoldMode();
        this.foldingRules.subModes["ttl-"] = ttlFoldMode;
        this.foldingRules.subModes["start"] = ttlFoldMode;
        this.foldingRules.subModes["js-"] = ttlFoldMode;
        this.foldingRules.subModes["css-"] = ttlFoldMode;
    };
    oop.inherits(Mode, HtmlMode);

    (function() {        
        this.getNextLineIndent = TtlMode.prototype.getNextLineIndent;
        
        this.getTokenizer = TtlMode.prototype.getTokenizer;
        
        this.createWorker = function(session) {
            var worker = new WorkerClient(["ace"], "ace/mode/ttl_worker", "TtlWorker");
            worker.attachToDocument(session.getDocument());

            worker.on("annotate", function(results) {
                session.setAnnotations(results.data);
            });

            worker.on("error", function(e) {
                session.setAnnotations(e.data);
            });

            worker.on("terminate", function() {
                session.clearAnnotations();
            });

            return worker;
        };

        this.$id = "ace/mode/ttl";
        this.snippetFileId = "ace/snippets/ttl";
    }).call(Mode.prototype);

    exports.Mode = Mode;
});