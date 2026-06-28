"use strict";

var oop = require("../lib/oop");
var HtmlMode = require("./html").Mode;
var CSharpMode = require("./csharp").Mode;
var JavaScriptMode = require("./javascript").Mode;
var CssMode = require("./css").Mode;
var HeddleFoldMode = require("./folding/heddle").FoldMode;
var HeddleHighlightRules = require("./heddle_highlight_rules").HeddleHighlightRules;
var HeddleTokenizer = require("./heddle/HeddleTokenizer").HeddleTokenizer;
var HeddleWorkerTokenizer = require("./heddle/HeddleTokenizer").HeddleWorkerTokenizer;
var WorkerClient = require("../worker/worker_client").WorkerClient;

var Mode = function () {
    HtmlMode.call(this);
    this.blockComment = {start: "@*", end: "*@"};
    this.HighlightRules = HeddleHighlightRules;
    this.createModeDelegates({
        "js-": JavaScriptMode,
        "css-": CssMode,
        "cs-": CSharpMode,
        "html-js-": JavaScriptMode,
        "html-css-": CssMode
    });
    var heddleFoldMode = new HeddleFoldMode();
    this.foldingRules.subModes["start"] = heddleFoldMode;
    this.foldingRules.subModes["js-"] = heddleFoldMode;
    this.foldingRules.subModes["css-"] = heddleFoldMode;
    this.foldingRules.subModes["html-js-"] = heddleFoldMode;
    this.foldingRules.subModes["html-css-"] = heddleFoldMode;
};
oop.inherits(Mode, HtmlMode);

(function () {
    this.getTokenizer = function () {
        if (!this.$tokenizer) {
            this.$highlightRules = this.$highlightRules || new this.HighlightRules(this.$highlightRuleConfig);
            this.$tokenizer = new HeddleTokenizer(this.$highlightRules.getRules());
        }
        return this.$tokenizer;
    };

    this.getNextLineIndent = function (state, line, tab) {
        var indent = this.$getIndent(line);
        if (line.match(/^.*({{|@%)\s*$/))
            indent += tab;
        else if (line.match(/^.*(}}|%@)\s*$/))
            indent.substring(0, indent.length - tab.length);
        return indent;
    };

    this.createWorker = function (session) {
        var worker = new WorkerClient(["ace"], "ace/mode/heddle_worker", "HeddleWorker");
        worker.attachToDocument(session.getDocument());

        worker.on("annotate", function (results) {
            session.setAnnotations(results.data);
        });

        worker.on("error", function (e) {
            session.setAnnotations(e.data);
        });

        worker.on("terminate", function () {
            session.clearAnnotations();
        });

        return worker;
    };

    this.$id = "ace/mode/heddle";
    this.snippetFileId = "ace/snippets/heddle";
}).call(Mode.prototype);

var WorkerMode = function () {
    HtmlMode.call(this);
    this.HighlightRules = HeddleHighlightRules;
};
oop.inherits(WorkerMode, HtmlMode);

(function () {
    this.getTokenizer = function () {
        if (!this.$tokenizer) {
            this.$highlightRules = this.$highlightRules || new this.HighlightRules(this.$highlightRuleConfig);
            this.$tokenizer = new HeddleWorkerTokenizer(this.$highlightRules.getRules());
        }
        return this.$tokenizer;
    };

    this.$id = "ace/mode/heddle_worker";
}).call(WorkerMode.prototype);

exports.Mode = Mode;
exports.WorkerMode = WorkerMode;