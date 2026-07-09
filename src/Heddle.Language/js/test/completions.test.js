"use strict";

/*
 * WS9 smoke tests for the completions + snippets harness (feeds WS6).
 *
 * Proves `mode/heddle_completions.js` and `snippets/heddle.snippets.js` load
 * and offer non-empty entries. Mode-specific "offered in the right context"
 * fixtures land in WS6.
 */

const test = require("node:test");
const assert = require("node:assert");

const {
    extensionCompletions,
    extensionOverrideCompletions,
    parseSnippets,
    loadSnippetText,
    completionsAt
} = require("./helpers/completions");

test("extension completions are offered and well-formed", () => {
    const list = extensionCompletions();
    assert.ok(Array.isArray(list) && list.length > 0, "expected a non-empty completion list");
    for (const item of list) {
        assert.strictEqual(typeof item.caption, "string");
        assert.ok(item.caption.length > 0);
        assert.strictEqual(typeof item.snippet, "string");
    }
    // A few well-known built-in extensions must be present.
    const captions = list.map((i) => i.caption);
    for (const expected of ["if", "for", "out"]) {
        assert.ok(captions.includes(expected), "expected extension completion: " + expected);
    }
});

test("v2 extension completions (elif/elseif/else/profile/raw/out) are offered", () => {
    const captions = extensionCompletions().map((i) => i.caption);
    for (const expected of ["elif", "elseif", "else", "profile", "raw", "out"]) {
        assert.ok(captions.includes(expected), "expected v2 extension completion: " + expected);
    }
    // The pre-existing extensions must still be offered.
    for (const expected of ["if", "ifnot", "for", "param", "import", "using", "partial"]) {
        assert.ok(captions.includes(expected), "expected retained extension completion: " + expected);
    }
});

test("native-expression built-in functions are offered as completions", () => {
    // Mirrors FunctionRegistry.Default (BuiltInFunctions.CreateEntries).
    const registryFunctions = [
        "upper", "lower", "trim", "len", "contains", "startswith", "endswith",
        "replace", "substr", "format", "str", "abs", "min", "max", "round",
        "floor", "ceil", "range"
    ];
    const list = extensionCompletions();
    const captions = list.map((i) => i.caption);
    for (const fn of registryFunctions) {
        assert.ok(captions.includes(fn), "expected registry function completion: " + fn);
    }
    // Functions should be tagged distinctly from extensions.
    const rangeEntry = list.find((i) => i.caption === "range");
    assert.strictEqual(rangeEntry.meta, "function");
});

test("override completions are offered", () => {
    const list = extensionOverrideCompletions();
    assert.ok(Array.isArray(list) && list.length > 0, "expected a non-empty override list");
});

test("getCompletions returns an array via the token-driven path", () => {
    // A plain-text position should complete to [] (no throw) - this proves the
    // session/getTokenAt plumbing works end to end.
    const result = completionsAt("plain text", { row: 0, column: 2 });
    assert.ok(Array.isArray(result));
});

test("snippet file parses into named snippets", () => {
    const text = loadSnippetText();
    assert.strictEqual(typeof text, "string");
    assert.ok(text.indexOf("snippet") !== -1, "expected snippet definitions");

    const snippets = parseSnippets();
    assert.ok(snippets.length > 0, "expected at least one snippet");
    const names = snippets.map((s) => s.name);
    for (const expected of ["if", "list"]) {
        assert.ok(names.includes(expected), "expected snippet: " + expected);
    }
});

test("v2 snippets exist with correct trigger names", () => {
    const snippets = parseSnippets();
    const byName = new Map(snippets.map((s) => [s.name, s.body]));
    // Existing snippets keep working.
    for (const expected of ["list", "if", "ifnot"]) {
        assert.ok(byName.has(expected), "expected retained snippet: " + expected);
    }
    // New WS6 snippets.
    for (const expected of ["elif", "else", "for", "param", "prop", "slot"]) {
        assert.ok(byName.has(expected), "expected new snippet: " + expected);
    }
    // Spot-check bodies expand to the right v2 constructs.
    assert.ok(/@elif\(/.test(byName.get("elif")), "elif snippet should emit @elif(");
    assert.ok(/@else\s*\{\{/.test(byName.get("else")), "else snippet should emit @else {{");
    assert.ok(/@for\(/.test(byName.get("for")), "for snippet should emit @for(");
    assert.ok(/@param\(/.test(byName.get("param")), "param snippet should emit @param(");
    assert.ok(/:\s*\$\{3:Type\}/.test(byName.get("prop")), "prop snippet should declare a typed prop");
    assert.ok(/out::/.test(byName.get("slot")), "slot snippet should declare out:: Type");
    assert.ok(/@out\(/.test(byName.get("slot")), "slot snippet should emit @out(");
});
