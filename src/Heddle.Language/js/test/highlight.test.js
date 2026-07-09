"use strict";

/*
 * WS2 regression fixtures: per-construct Ace token classification.
 *
 * Each test tokenizes a v2 construct and asserts the produced Ace token class
 * (the §4 "Ace token class" column of docs/archive/ace-v2-migration-plan.md).
 * Token types are of the form `<state-bucket>.<ace-class>` (e.g.
 * `heddle-call.constant.language`); `aceClass()` strips the leading bucket so
 * fixtures assert on the meaningful Ace scope.
 */

const test = require("node:test");
const assert = require("node:assert");

const { tokenizeDocument } = require("./helpers/tokenize");

/** Ace class = token type minus its leading state-bucket segment. */
function aceClass(type) {
    const i = type.indexOf(".");
    return i < 0 ? type : type.slice(i + 1);
}

/** Non-whitespace tokens with their trimmed value + ace class. */
function classified(text) {
    return tokenizeDocument(text)
        .filter((t) => t.value.trim() !== "")
        .map((t) => ({ value: t.value.trim(), raw: t.value, aceClass: aceClass(t.type), type: t.type }));
}

/** First classified token whose trimmed value equals `value`. */
function tokenFor(text, value) {
    const found = classified(text).find((t) => t.value === value);
    assert.ok(
        found,
        `expected a token with value "${value}" in: ${JSON.stringify(classified(text))}`
    );
    return found;
}

/** Assert the token with the given value maps to the expected Ace class. */
function assertClass(text, value, expected) {
    const tok = tokenFor(text, value);
    assert.strictEqual(
        tok.aceClass,
        expected,
        `"${value}" in \`${text}\` -> got ${tok.aceClass}, want ${expected}`
    );
}

// ---------------------------------------------------------------------------
// §4 native-expression literals & keywords
// ---------------------------------------------------------------------------

test("boolean/null literals -> constant.language", () => {
    assertClass("@out(true)", "true", "constant.language");
    assertClass("@out(false)", "false", "constant.language");
    assertClass("@out(null)", "null", "constant.language");
});

test("integer literals -> constant.numeric", () => {
    assertClass("@out(42)", "42", "constant.numeric");
    assertClass("@out(0xFF)", "0xFF", "constant.numeric");
});

test("real literals -> constant.numeric (matched before int)", () => {
    assertClass("@out(1.5f)", "1.5f", "constant.numeric");
    assertClass("@out(3.14)", "3.14", "constant.numeric");
    assertClass("@out(2e3)", "2e3", "constant.numeric");
});

test("string literal (with escapes) -> string.quoted.double", () => {
    assertClass('@out("hi")', '"hi"', "string.quoted.double");
    assertClass('@out("a\\"b")', '"a\\"b"', "string.quoted.double");
});

test("char literal -> string.quoted.single", () => {
    assertClass("@out('x')", "'x'", "string.quoted.single");
    assertClass("@out('\\n')", "'\\n'", "string.quoted.single");
});

test("this keyword -> variable.language", () => {
    assertClass("@out(this)", "this", "variable.language");
});

// ---------------------------------------------------------------------------
// §4 operators, brackets, comma
// ---------------------------------------------------------------------------

test("single-char operators -> keyword.operator", () => {
    for (const op of ["+", "-", "*", "/", "%", "<", ">", "&", "|", "^", "~", "!", "?"]) {
        assertClass(`@out(a ${op} b)`, op, "keyword.operator");
    }
});

test("multi-char operators -> keyword.operator (matched before single-char)", () => {
    for (const op of ["&&", "||", "==", "!=", "<=", ">=", "<<", ">>", "??"]) {
        assertClass(`@out(a ${op} b)`, op, "keyword.operator");
    }
});

test("index/grouping brackets -> keyword.operator", () => {
    assertClass("@out(a[0])", "[", "keyword.operator");
    assertClass("@out(a[0])", "]", "keyword.operator");
});

test("argument comma -> punctuation.operator", () => {
    assertClass("@out(range(1, 2))", ",", "punctuation.operator");
});

test("member access dot -> punctuation.operator", () => {
    assertClass("@out(a.b)", ".", "punctuation.operator");
});

// ---------------------------------------------------------------------------
// §4 call names
// ---------------------------------------------------------------------------

test("function call name -> support.function", () => {
    assertClass("@out(range(1, 10))", "range", "support.function");
});

test("method call name -> support.function", () => {
    assertClass("@out(x.Trim())", "Trim", "support.function");
});

// ---------------------------------------------------------------------------
// §4 named arguments vs ternary colon
// ---------------------------------------------------------------------------

test("named argument name -> variable.parameter, colon -> punctuation.operator", () => {
    const toks = classified("@card(title: value)");
    const name = toks.find((t) => t.value === "title");
    assert.ok(name, "expected a `title` token");
    assert.strictEqual(name.aceClass, "variable.parameter");
    const colon = toks.find((t) => t.value === ":");
    assert.ok(colon, "expected a `:` token");
    assert.strictEqual(colon.aceClass, "punctuation.operator");
});

test("ternary colon (after ?) -> keyword.operator", () => {
    const toks = classified("@out(cond ? a : b)");
    const colon = toks.find((t) => t.value === ":");
    assert.ok(colon, "expected a `:` token");
    assert.strictEqual(colon.aceClass, "keyword.operator");
    const q = toks.find((t) => t.value === "?");
    assert.strictEqual(q.aceClass, "keyword.operator");
});

// ---------------------------------------------------------------------------
// §4 prop declaration surface (DEF_PROPS)
// ---------------------------------------------------------------------------

test("prop-list open/close -> keyword.operator.paren", () => {
    const toks = classified("@% <card(title: string)> {{ }} %@");
    const open = toks.find((t) => t.value === "(");
    const close = toks.find((t) => t.value === ")");
    assert.strictEqual(open.aceClass, "keyword.operator.paren");
    assert.strictEqual(close.aceClass, "keyword.operator.paren");
});

test("prop name -> variable.parameter, type -> storage.type, `:` -> punctuation.operator", () => {
    const toks = classified("@% <card(title: string)> {{ }} %@");
    assert.strictEqual(toks.find((t) => t.value === "title").aceClass, "variable.parameter");
    assert.strictEqual(toks.find((t) => t.value === "string").aceClass, "storage.type");
    // the DEF_PROPS colon delimiter (there is only one here)
    assert.strictEqual(toks.find((t) => t.value === ":").aceClass, "punctuation.operator");
});

test("prop default `=` -> keyword.operator, default literal classified", () => {
    const toks = classified('@% <card(title: string = "plain")> {{ }} %@');
    assert.strictEqual(toks.find((t) => t.value === "=").aceClass, "keyword.operator");
    assert.strictEqual(toks.find((t) => t.value === '"plain"').aceClass, "string.quoted.double");
});

test("slot parameter `out::` -> keyword + keyword.operator", () => {
    const toks = classified("@% <card(body:: string)> {{ }} %@");
    const slotName = toks.find((t) => t.value === "body");
    assert.strictEqual(slotName.aceClass, "keyword");
    const typeOp = toks.find((t) => t.value === "::");
    assert.strictEqual(typeOp.aceClass, "keyword.operator");
    assert.strictEqual(toks.find((t) => t.value === "string").aceClass, "storage.type");
});

test("full prop-declaration header: <card(title: string = \"x\", body:: string)>", () => {
    const toks = classified('@% <card(title: string = "x", body:: string)> {{ }} %@');
    assert.strictEqual(toks.find((t) => t.value === "title").aceClass, "variable.parameter");
    assert.strictEqual(toks.filter((t) => t.value === "string")[0].aceClass, "storage.type");
    assert.strictEqual(toks.find((t) => t.value === "=").aceClass, "keyword.operator");
    assert.strictEqual(toks.find((t) => t.value === '"x"').aceClass, "string.quoted.double");
    assert.strictEqual(toks.find((t) => t.value === ",").aceClass, "punctuation.operator");
    assert.strictEqual(toks.find((t) => t.value === "body").aceClass, "keyword");
    assert.strictEqual(toks.find((t) => t.value === "::").aceClass, "keyword.operator");
});

// The `<name : base>` header `:` must NOT collide with the prop-list `:`.
test("definition header `<name : base>` colon stays a header operator", () => {
    const toks = classified("@% <card : base> {{ }} %@");
    const colon = toks.find((t) => t.value === ":");
    assert.strictEqual(colon.aceClass, "keyword.operator");
});

// ---------------------------------------------------------------------------
// §4 directives
// ---------------------------------------------------------------------------

test("@elif / @elseif -> keyword", () => {
    assert.strictEqual(tokenFor("@elif(x) {{ }}", "@elif").aceClass, "keyword");
    assert.strictEqual(tokenFor("@elseif(x) {{ }}", "@elseif").aceClass, "keyword");
});

test("@else -> keyword", () => {
    assert.strictEqual(tokenFor("@else {{ }}", "@else").aceClass, "keyword");
});

test("@raw -> support.function", () => {
    assert.strictEqual(tokenFor("@raw(x)", "@raw").aceClass, "support.function");
});

test("@profile -> support.function", () => {
    assert.strictEqual(tokenFor("@profile(html) {{ }}", "@profile").aceClass, "support.function");
});

// ---------------------------------------------------------------------------
// Nested combinations
// ---------------------------------------------------------------------------

test("expression inside @if(...)", () => {
    const toks = classified("@if(a >= 10 && b != null) {{ }}");
    assert.strictEqual(toks.find((t) => t.value === ">=").aceClass, "keyword.operator");
    assert.strictEqual(toks.find((t) => t.value === "&&").aceClass, "keyword.operator");
    assert.strictEqual(toks.find((t) => t.value === "!=").aceClass, "keyword.operator");
    assert.strictEqual(toks.find((t) => t.value === "null").aceClass, "constant.language");
    assert.strictEqual(toks.find((t) => t.value === "10").aceClass, "constant.numeric");
});

test("@out(this.X)", () => {
    const toks = classified("@out(this.Name)");
    assert.strictEqual(toks.find((t) => t.value === "this").aceClass, "variable.language");
    assert.strictEqual(toks.find((t) => t.value === ".").aceClass, "punctuation.operator");
    assert.strictEqual(toks.find((t) => t.value === "Name").aceClass, "variable.language");
});

test("prop default expression: nested call + literals", () => {
    const toks = classified('@% <card(title: string = upper("x"))> {{ }} %@');
    // `upper` is a call in the default expression position; it is inside the
    // prop list surface, still classified as a function name is not required by
    // §4 here (DEF_PROPS default), but it must not be a default/text class.
    const upper = toks.find((t) => t.value === "upper");
    assert.ok(upper, "expected an `upper` token");
    assert.notStrictEqual(upper.aceClass, "text");
    assert.notStrictEqual(upper.type, "text");
});

// ---------------------------------------------------------------------------
// Coverage assertion (§7 WS2 DoD): no v2 token falls to a default/text class.
// ---------------------------------------------------------------------------

test("coverage: no v2 token falls to a default/empty/text class", () => {
    const samples = [
        '@out(range(1, 10) + a[0] * 2.5f - len("hi") % 3)',
        "@out(cond ? this.Value : null)",
        "@if(a >= 10 && b != null || !c) {{ }}",
        '@card(title: "T", count: 42, flag: true) {{ }}',
        '@% <card(title: string = "x", count: int = 0, body:: string)> {{ }} %@',
        "@elif(x) {{ }}",
        "@else {{ }}",
        "@raw(this.Html)",
        "@profile(html) {{ }}"
    ];

    for (const sample of samples) {
        const toks = tokenizeDocument(sample).filter((t) => t.value.trim() !== "");
        for (const t of toks) {
            const cls = aceClass(t.type);
            assert.ok(
                t.type && t.type !== "text" && cls !== "text" && cls !== "" && cls !== "empty",
                `token "${t.value}" in \`${sample}\` fell to default class "${t.type}"`
            );
        }
    }
});
