define(function(require, exports, module) {
    "use strict";

    var oop = require("../lib/oop");
    var TextMode = require("./text").Mode;
    var JavaScriptMode = require("./javascript").Mode;
    var CssMode = require("./css").Mode;
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
        this.createModeDelegates({
            "js-": JavaScriptMode,
            "css-": CssMode
        });
    };
    oop.inherits(Mode, TextMode);

    (function() {
        this.blockComment = { start: "@*", end: "*@" };
        this.getNextLineIndent = function(state, line, tab) {
            var indent = this.$getIndent(line);

            var tokenizedLine = this.getTokenizer().getLineTokens(line, state);
            var tokens = tokenizedLine.tokens;
            var endState = tokenizedLine.state;
            if (tokens.length && tokens[tokens.length - 1].type == "comment") {
                return indent;
            }

            var match = line.match(/^.*<%|{{|\(\s*$/);
            if (match) {
                indent += tab;
            }
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