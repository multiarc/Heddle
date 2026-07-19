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

// ---------------------------------------------------------------------------
// Phase 7 (post-2.0): region-override completions (LSP RegionOverride parity).
// ---------------------------------------------------------------------------

const REGION_DOC = "@%<:header>{{H}}%@\n@%<:item :: Article>{{B}}%@\n@card(A){{@%<";

test("public regions complete at the '<' override position with name:name inserts", () => {
    // Caret right after the '<' that opens the override inside the call body.
    const list = completionsAt(REGION_DOC, { row: 2, column: 13 });
    assert.strictEqual(list.length, 2);
    const header = list.find((i) => i.caption === "header");
    assert.ok(header, "expected the 'header' region completion");
    assert.strictEqual(header.snippet, "header:header");
    assert.strictEqual(header.meta, "region");
    const item = list.find((i) => i.caption === "item");
    assert.ok(item, "expected the 'item' region completion");
    assert.strictEqual(item.snippet, "item:item");
    assert.strictEqual(item.meta, "region :: Article");
});

test("region completions also offered on a partial name after '<'", () => {
    const doc = REGION_DOC + "he";
    const list = completionsAt(doc, { row: 2, column: 15 });
    assert.ok(list.some((i) => i.caption === "header" && i.snippet === "header:header"));
});

test("private definitions are not offered as region completions", () => {
    const doc = "@%<secret>{{x}}%@\n@%<:pub>{{y}}%@\n@card(A){{@%<";
    const list = completionsAt(doc, { row: 2, column: 13 });
    assert.deepStrictEqual(list.map((i) => i.caption), ["pub"]);
});

test("duplicate region declarations collapse to the first (HED5020 shape)", () => {
    const { makeCompleter } = require("./helpers/completions");
    const completer = makeCompleter();
    const fakeSession = { getValue: () => "@%<:a :: T>{{x}}%@ @%<:a>{{y}}%@ @%<:b>{{z}}%@" };
    const list = completer.getRegionOverrideCompletions("start", fakeSession, { row: 0, column: 0 }, "");
    assert.deepStrictEqual(list.map((i) => i.caption), ["a", "b"]);
    assert.strictEqual(list[0].meta, "region :: T");
});

test("region declarations inside comments and string literals are not offered", () => {
    // `<:ghost>` lives only in a `@* ... *@` comment; `<:ghosty>` / `<:ghostz>`
    // only inside native-expression string literals. None is a real
    // declaration, so only the genuine `<:footer>` region may be offered.
    const doc = "@* <:ghost> *@\n"
        + "@%<:footer>{{F}}%@\n"
        + "@%<card>{{@out(upper(\"<:ghosty>\"))@out(lower('<:ghostz>'))}}%@\n"
        + "@card(A){{@%<";
    const list = completionsAt(doc, { row: 3, column: 13 });
    assert.deepStrictEqual(list.map((i) => i.caption), ["footer"]);
    assert.strictEqual(list[0].snippet, "footer:footer");
});

test("no region declarations -> empty region-override completion list", () => {
    const list = completionsAt("@card(A){{@%<", { row: 0, column: 13 });
    assert.deepStrictEqual(list, []);
});

test("phase 7 snippets exist: region and regiontype", () => {
    const snippets = parseSnippets();
    const byName = new Map(snippets.map((s) => [s.name, s.body]));
    assert.ok(byName.has("region"), "expected snippet: region");
    assert.ok(byName.has("regiontype"), "expected snippet: regiontype");
    assert.ok(/<:\$\{1:name\}>\s*\{\{/.test(byName.get("region")), "region snippet should emit <:name> {{");
    assert.ok(/<:\$\{1:name\} :: \$\{2:Type\}>\s*\{\{/.test(byName.get("regiontype")), "regiontype snippet should emit <:name :: Type> {{");
});
