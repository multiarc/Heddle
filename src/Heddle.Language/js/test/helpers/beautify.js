"use strict";

/*
 * Beautify helper (WS4 verification surface).
 *
 * Exercises `js/src/ext/beautify.js` against a real Ace `EditSession` whose
 * tokenizer is the Heddle highlight tokenizer. The beautifier walks the
 * session's tokens via a `TokenIterator` and rewrites `session.doc`, so we
 * need a genuine session - but we avoid Ace's async mode loading by handing
 * the session a tiny in-process mode object that returns our tokenizer.
 */

const { requireHeddleModule } = require("./ace-loader");
const { makeHeddleSession } = require("./session");

let cachedBeautify = null;

function getBeautifyModule() {
    if (!cachedBeautify) {
        cachedBeautify = requireHeddleModule("ext/beautify");
    }
    return cachedBeautify;
}

/**
 * Beautify `text` once and return the formatted result.
 * @param {string} text
 * @returns {string}
 */
function beautify(text) {
    const beautifyModule = getBeautifyModule();
    const session = makeHeddleSession(text);
    beautifyModule.beautify(session);
    return session.doc.getValue();
}

/**
 * Beautify `text` twice; used to assert idempotency (format(format(x)) === format(x)).
 * @param {string} text
 * @returns {{ once: string, twice: string }}
 */
function beautifyTwice(text) {
    const once = beautify(text);
    const twice = beautify(once);
    return { once, twice };
}

module.exports = {
    getBeautifyModule,
    makeHeddleSession,
    beautify,
    beautifyTwice
};
