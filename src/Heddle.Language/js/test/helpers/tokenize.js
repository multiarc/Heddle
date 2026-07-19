"use strict";

/*
 * Highlight tokenizer helper (WS2 verification surface).
 *
 * Loads the Heddle Ace highlight rules, builds a real Ace `Tokenizer` from
 * them exactly the way the Ace runtime does, and exposes helpers to tokenize
 * a line (or a multi-line document, threading the tokenizer state across
 * lines) into a flat `{ type, value }[]` stream. The `type` is the Ace token
 * class that WS2 fixtures assert on.
 */

const { requireHeddleModule, requireAceModule } = require("./ace-loader");

let cachedTokenizer = null;

/**
 * Build (once) an Ace Tokenizer from `HeddleHighlightRules` - the exact rule
 * set the real `heddle` mode installs (`this.HighlightRules = HeddleHighlightRules`).
 * The rules constructor calls `normalizeRules()` itself, so the state map is
 * ready to hand straight to the Tokenizer.
 * @returns {*} an Ace Tokenizer instance
 */
function getHeddleTokenizer() {
    if (cachedTokenizer) {
        return cachedTokenizer;
    }
    const { HeddleHighlightRules } = requireHeddleModule("mode/heddle_highlight_rules");
    const { Tokenizer } = requireAceModule("tokenizer");
    const rules = new HeddleHighlightRules();
    cachedTokenizer = new Tokenizer(rules.getRules());
    return cachedTokenizer;
}

/**
 * Tokenize a single line starting from `startState`.
 * @param {string} line
 * @param {string|any[]} [startState="start"]
 * @returns {{ tokens: {type: string, value: string}[], state: string|any[] }}
 */
function tokenizeLine(line, startState) {
    const tokenizer = getHeddleTokenizer();
    const result = tokenizer.getLineTokens(line, startState == null ? "start" : startState);
    return { tokens: result.tokens, state: result.state };
}

/**
 * Tokenize a whole document, threading tokenizer state line-to-line the same
 * way an EditSession's BackgroundTokenizer does. Returns the flat token
 * stream across all lines (newlines are not emitted as tokens).
 * @param {string} text  the document text (may contain `\n`)
 * @param {string|any[]} [startState="start"]
 * @returns {{type: string, value: string}[]}
 */
function tokenizeDocument(text, startState) {
    const lines = String(text).split(/\r\n|\r|\n/);
    let state = startState == null ? "start" : startState;
    const all = [];
    for (const line of lines) {
        const res = tokenizeLine(line, state);
        for (const tok of res.tokens) {
            all.push(tok);
        }
        state = res.state;
    }
    return all;
}

/**
 * Convenience: the set of distinct Ace token classes produced for a document.
 * @param {string} text
 * @param {string|any[]} [startState]
 * @returns {Set<string>}
 */
function tokenTypes(text, startState) {
    return new Set(tokenizeDocument(text, startState).map((t) => t.type));
}

module.exports = {
    getHeddleTokenizer,
    tokenizeLine,
    tokenizeDocument,
    tokenTypes
};
