"use strict";

/*
 * Shared Ace EditSession factory for the harness.
 *
 * Builds a real Ace `EditSession` wired to the Heddle highlight tokenizer,
 * used by both the beautify (WS4) and completions (WS6) helpers. Handing
 * `setMode` a mode object that already exposes `getTokenizer()` keeps mode
 * installation fully synchronous (no `config.loadModule`).
 */

const { requireAceModule } = require("./ace-loader");
const { getHeddleTokenizer } = require("./tokenize");

/**
 * @param {string} text
 * @returns {*} an Ace EditSession backed by the Heddle tokenizer
 */
function makeHeddleSession(text) {
    const { EditSession } = requireAceModule("edit_session");
    const tokenizer = getHeddleTokenizer();
    const mode = {
        $id: "ace/mode/heddle",
        getTokenizer() {
            return tokenizer;
        },
        foldingRules: null,
        tokenRe: null,
        nonTokenRe: null,
        // Sessions default to $useWorker=true; supplying a no-op createWorker
        // keeps $startWorker from logging a "Could not load worker" warning.
        createWorker() {
            return null;
        }
    };
    const session = new EditSession(text, mode);
    // Stop the background timer scheduled by setMode/start - getTokens() forces
    // synchronous tokenization on demand, and a dangling timer would keep the
    // node process alive after the tests finish.
    if (session.bgTokenizer && session.bgTokenizer.stop) {
        session.bgTokenizer.stop();
    }
    return session;
}

module.exports = { makeHeddleSession };
