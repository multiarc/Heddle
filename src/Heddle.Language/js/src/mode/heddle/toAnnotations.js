"use strict";

/*
 * Shared "ParseContext error -> Ace annotation" mapping.
 *
 * SINGLE SOURCE OF TRUTH. Consumed by BOTH:
 *   - the language worker (mode/heddle_worker.js onUpdate), and
 *   - the WS5 test harness (js/test/helpers/parse.js),
 * so the harness validates the exact mapping the worker ships.
 *
 * Message alignment with the C# engine (HED0003 syntax errors)
 * ------------------------------------------------------------
 * The C# parser attaches HeddleSyntaxErrorListener, which stores the *raw ANTLR
 * message* in HeddleCompileError.Error and tags DiagnosticId = "HED0003"
 * (Heddle.Data.HeddleDiagnosticIds.SyntaxError). HeddleCompileError renders the
 * pair as `"{DiagnosticId}: {Error}"` (its private ErrorWithId, used by
 * ToString()). Because the C# and JS ANTLR 4.13.1 runtimes emit identical
 * DefaultErrorStrategy phrasing ("mismatched input ... expecting ...",
 * "no viable alternative at input ...", "extraneous input ... expecting ...",
 * "missing ... at ...", "token recognition error at: ..."), prefixing the raw
 * ANTLR message with "HED0003: " reproduces the engine's displayed syntax
 * diagnostic. See src/Heddle/Language/HeddleSyntaxErrorListener.cs and
 * src/Heddle/Data/HeddleCompileError.cs.
 */

var SYNTAX_ERROR_ID = "HED0003";

/**
 * Map the raw ParseContext.errors list to Ace annotations.
 *
 * @param {Array} results         ParseContext.errors ({ message, position }).
 * @param {function(number):{row:number,column:number}} indexToPosition
 *        Converts an absolute character index into a 0-based {row, column}
 *        (Ace Document.indexToPosition in the worker; a text-derived shim in
 *        the harness).
 * @returns {Array<{row:number,column:number,text:string,type:string}>}
 */
function errorsToAnnotations(results, indexToPosition) {
    var annotations = [];
    if (!results) {
        return annotations;
    }
    for (var i = 0; i < results.length; i++) {
        var error = results[i];
        if (!error || !error.position) {
            continue;
        }
        var row;
        var column;
        if (typeof error.position.startIndex === "number") {
            var position = indexToPosition(error.position.startIndex);
            row = position.row;
            column = position.column;
        } else if (typeof error.position.line === "number") {
            // Fall back to the listener-reported 1-based line / 0-based column
            // (e.g. lexer errors that carry no offending token).
            row = error.position.line - 1;
            column = error.position.column || 0;
        } else {
            continue;
        }
        annotations.push({
            row: row,
            column: column,
            text: SYNTAX_ERROR_ID + ": " + error.message,
            type: "error"
        });
    }
    return annotations;
}

exports.SYNTAX_ERROR_ID = SYNTAX_ERROR_ID;
exports.errorsToAnnotations = errorsToAnnotations;
