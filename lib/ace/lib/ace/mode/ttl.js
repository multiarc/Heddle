define(function(require, exports, module) {
    "use strict";

    var oop = require("../lib/oop");
    var HtmlMode = require("./html").Mode;
    var CSharpMode = require("./csharp").Mode;
    var JavaScriptMode = require("./javascript").Mode;
    var CssMode = require("./css").Mode;
    var TtlFoldMode = require("./folding/ttl").FoldMode;
    var TtlHighlightRules = require("./ttl_highlight_rules").TtlHighlightRules;
    var TtlTokenizer = require("./ttl/TtlTokenizer").TtlTokenizer;
    var TtlWorkerTokenizer = require("./ttl/TtlTokenizer").TtlWorkerTokenizer;
    var WorkerClient = require("../worker/worker_client").WorkerClient;

    var Mode = function() {
        HtmlMode.call(this);
        this.blockComment = { start: "@*", end: "*@" };
        this.HighlightRules = TtlHighlightRules;
        this.createModeDelegates({
            "js-": JavaScriptMode,
            "css-": CssMode,
            "cs-": CSharpMode,
            "html-js-": JavaScriptMode,
            "html-css-": CssMode
        });
        var ttlFoldMode = new TtlFoldMode();
        this.foldingRules.subModes["start"] = ttlFoldMode;
        this.foldingRules.subModes["js-"] = ttlFoldMode;
        this.foldingRules.subModes["css-"] = ttlFoldMode;
        this.foldingRules.subModes["html-js-"] = ttlFoldMode;
        this.foldingRules.subModes["html-css-"] = ttlFoldMode;
    };
    oop.inherits(Mode, HtmlMode);

    (function() {
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

    var WorkerMode = function() {
        HtmlMode.call(this);
        this.HighlightRules = TtlHighlightRules;
    };
    oop.inherits(WorkerMode, HtmlMode);

    (function() {
        this.getTokenizer = function() {
            if (!this.$tokenizer) {
                this.$highlightRules = this.$highlightRules || new this.HighlightRules(this.$highlightRuleConfig);
                this.$tokenizer = new TtlWorkerTokenizer(this.$highlightRules.getRules());
            }
            return this.$tokenizer;
        };

        this.$id = "ace/mode/ttl_worker";
    }).call(WorkerMode.prototype);

    exports.Mode = Mode;
    exports.WorkerMode = WorkerMode;
});