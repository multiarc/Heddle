"use strict";

/*
 * WS5 parse helper.
 *
 * Runs the real Heddle worker parse path (DocumentParser -> HeddleLexerExtended /
 * HeddleParserExtended -> HeddleErrorListener -> ParseContext) over a template
 * string and returns the Ace annotation list the worker would emit, using the
 * SAME shared mapping the worker ships (mode/heddle/toAnnotations.js).
 *
 * DocumentParser and its dependencies are ES modules that pull in the ANTLR4 JS
 * runtime via "./antlr4/index.web"; helpers/heddle-esm-hooks.mjs re-anchors those
 * imports onto the installed antlr4 package and forces ESM loading. See that file
 * for details.
 */

const path = require("node:path");
const { register } = require("node:module");
const { pathToFileURL } = require("node:url");
const { errorsToAnnotations } = require("../../src/mode/heddle/toAnnotations");

let registered = false;
function ensureHooksRegistered() {
    if (registered) {
        return;
    }
    register("./heddle-esm-hooks.mjs", pathToFileURL(__filename));
    registered = true;
}

const documentParserUrl = pathToFileURL(
    path.resolve(__dirname, "..", "..", "src", "mode", "heddle", "DocumentParser.js")
).href;

let documentParserPromise = null;
function loadDocumentParser() {
    ensureHooksRegistered();
    if (!documentParserPromise) {
        documentParserPromise = import(documentParserUrl).then((m) => m.DocumentParser);
    }
    return documentParserPromise;
}

/**
 * A text-derived stand-in for Ace's Document.indexToPosition: maps an absolute
 * character index to a 0-based {row, column}. Matches Ace for LF-delimited text
 * (all harness fixtures use "\n").
 * @param {string} text
 */
function makeIndexToPosition(text) {
    const lineStarts = [0];
    for (let i = 0; i < text.length; i++) {
        if (text.charCodeAt(i) === 10 /* \n */) {
            lineStarts.push(i + 1);
        }
    }
    return function indexToPosition(index) {
        let row = 0;
        for (let r = 0; r < lineStarts.length; r++) {
            if (lineStarts[r] <= index) {
                row = r;
            } else {
                break;
            }
        }
        return { row, column: index - lineStarts[row] };
    };
}

/**
 * Parse a template and return the worker annotations
 * ({ row, column, text, type }).
 * @param {string} text
 * @returns {Promise<Array<{row:number,column:number,text:string,type:string}>>}
 */
async function parseAnnotations(text) {
    const DocumentParser = await loadDocumentParser();
    const parser = new DocumentParser(text);
    const results = parser.parseGetErrors();
    return errorsToAnnotations(results, makeIndexToPosition(text));
}

module.exports = { parseAnnotations };
