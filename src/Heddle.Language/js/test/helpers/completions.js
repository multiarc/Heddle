"use strict";

/*
 * Completions + snippets helper (WS6 verification surface).
 *
 * Loads `js/src/mode/heddle_completions.js` and `js/src/snippets/heddle.snippets.js`
 * and exposes helpers to (a) read the raw extension lists the completer offers,
 * (b) drive the real token-based `getCompletions` path against a Heddle
 * EditSession, and (c) parse the snippet file into name/expansion pairs.
 */

const { requireHeddleModule } = require("./ace-loader");
const { makeHeddleSession } = require("./session");

let cachedCompletions = null;
let cachedSnippets = null;

function getCompletionsModule() {
    if (!cachedCompletions) {
        cachedCompletions = requireHeddleModule("mode/heddle_completions");
    }
    return cachedCompletions;
}

/**
 * @returns {*} a fresh HeddleCompletions instance
 */
function makeCompleter() {
    const { HeddleCompletions } = getCompletionsModule();
    return new HeddleCompletions();
}

/**
 * The built-in extension completions offered inside `@out(` / call contexts.
 * @returns {{caption: string, snippet: string, meta: string}[]}
 */
function extensionCompletions() {
    return makeCompleter().getExtensionCompletions("start", null, { row: 0, column: 0 }, "");
}

/**
 * The extension completions offered in override (`def`) contexts.
 * @returns {{caption: string, snippet: string, meta: string}[]}
 */
function extensionOverrideCompletions() {
    return makeCompleter().getExtensionOverrideCompletions("start", null, { row: 0, column: 0 }, "");
}

/**
 * Drive the real `getCompletions` entry point at a document position. This
 * exercises the token-driven branch (`session.getTokenAt` -> extension list).
 * @param {string} text
 * @param {{row: number, column: number}} pos
 * @param {string} [prefix=""]
 * @returns {Array} completion entries (may be empty for non-completing tokens)
 */
function completionsAt(text, pos, prefix) {
    const completer = makeCompleter();
    const session = makeHeddleSession(text);
    const state = session.getState(pos.row);
    return completer.getCompletions(state, session, pos, prefix == null ? "" : prefix);
}

/**
 * Raw snippet file contents (a template string exported by the module).
 * @returns {string}
 */
function loadSnippetText() {
    if (cachedSnippets == null) {
        cachedSnippets = requireHeddleModule("snippets/heddle.snippets");
    }
    return cachedSnippets;
}

/**
 * Parse the Ace `.snippets` text into `{ name, body }` entries. Ace's snippet
 * format uses `snippet <name>` followed by tab-indented body lines.
 * @returns {{name: string, body: string}[]}
 */
function parseSnippets() {
    const text = loadSnippetText();
    const snippets = [];
    let current = null;
    for (const rawLine of String(text).split(/\r\n|\r|\n/)) {
        const match = /^snippet\s+(.+)$/.exec(rawLine);
        if (match) {
            if (current) {
                snippets.push(current);
            }
            current = { name: match[1].trim(), body: "" };
        } else if (current && /^\t/.test(rawLine)) {
            current.body += rawLine.replace(/^\t/, "") + "\n";
        }
    }
    if (current) {
        snippets.push(current);
    }
    return snippets;
}

module.exports = {
    getCompletionsModule,
    makeCompleter,
    extensionCompletions,
    extensionOverrideCompletions,
    completionsAt,
    loadSnippetText,
    parseSnippets
};
