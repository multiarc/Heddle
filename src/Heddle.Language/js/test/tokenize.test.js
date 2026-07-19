"use strict";

/*
 * WS9 smoke tests for the highlight tokenizer harness (feeds WS2).
 *
 * These prove the plumbing works: the Heddle Ace highlight rules load, an Ace
 * Tokenizer is constructed from them, and a token stream with defined Ace
 * classes comes back. Exhaustive per-construct fixtures land in WS2.
 */

const test = require("node:test");
const assert = require("node:assert");

const { tokenizeDocument, tokenTypes, getHeddleTokenizer } = require("./helpers/tokenize");

test("tokenizer builds from the Heddle highlight rules", () => {
    const tokenizer = getHeddleTokenizer();
    assert.ok(tokenizer, "expected a tokenizer instance");
    assert.strictEqual(typeof tokenizer.getLineTokens, "function");
});

test("tokenizes @if(x) {{ ... }} into classed tokens", () => {
    const tokens = tokenizeDocument("@if(x) {{ hello }}");

    assert.ok(tokens.length > 0, "expected a non-empty token stream");

    // Every token must carry a defined, string Ace class and a value.
    for (const tok of tokens) {
        assert.strictEqual(typeof tok.type, "string", "token type must be a string");
        assert.ok(tok.type.length > 0, "token type must not be empty");
        assert.strictEqual(typeof tok.value, "string");
    }

    // The @if directive must be recognised as a Heddle construct, not plain
    // text - i.e. at least one token is classified into a heddle-* class.
    const types = tokenTypes("@if(x) {{ hello }}");
    const heddleClassed = [...types].some((t) => t.indexOf("heddle") !== -1);
    assert.ok(
        heddleClassed,
        "expected at least one heddle-* classified token, got: " + [...types].join(", ")
    );
});

test("plain HTML text still tokenizes (embedded HTML mode wired)", () => {
    const tokens = tokenizeDocument("<div>hello</div>");
    assert.ok(tokens.length > 0);
    const joined = tokens.map((t) => t.value).join("");
    assert.strictEqual(joined, "<div>hello</div>", "tokens must reconstruct the line");
});
