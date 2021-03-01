define(function(require, exports, module) {
    "use strict";

    var oop = require("../lib/oop");
    var HtmlModel = require("./html").Mode;
    var TtlFoldMode = require("./folding/ttl").FoldMode;
    var TtlHighlightRules = require("./ttl_highlight_rules").TtlHighlightRules;
    var TTLMatchingBraceOutdent = require("./ttl_brace_outdent").TTLMatchingBraceOutdent;
    var TTLstyleBehaviour = require("./behaviour/ttl").TTLstyleBehaviour;
    var TtlCompletions = require("./ttl_completions").TtlCompletions;
    var WorkerClient = require("../worker/worker_client").WorkerClient;

    var Mode = function() {
        this.HighlightRules = TtlHighlightRules;
        this.$completer = new TtlCompletions();
        this.$outdent = new TTLMatchingBraceOutdent();
        this.$behaviour = new TTLstyleBehaviour();
        this.foldingRules = new TtlFoldMode();
    };
    oop.inherits(Mode, HtmlModel);

    (function() {
        this.blockComment = { start: "@*", end: "*@" };
        this.getNextLineIndent = function(state, line, tab) {
            return this.$getIndent(line);
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

        this.createWorker = function(session) {
            var worker = new WorkerClient(["ace"], "ace/mode/ttl_worker", "TtlWorker");
            worker.attachToDocument(session.getDocument());

            worker.on("annotate", function(results) {
                session.setAnnotations(results.data);
            });

            worker.on("terminate", function() {
                session.clearAnnotations();
            });

            return worker;
        };

        this.$id = "ace/mode/ttl";
    }).call(Mode.prototype);

    exports.Mode = Mode;
});