"use strict";

/*
 * WS7 folding fixtures: confirm `{{`/`}}` and `@%`/`%@` fold widgets still
 * pair after the v2 tokenizer changes, and that generic-type `<`/`>` never
 * produce fold widgets.
 *
 * Drives the FoldMode (`mode/folding/heddle.js`) over a real Ace EditSession
 * backed by the Heddle tokenizer, exactly as Ace does when computing gutter
 * fold widgets.
 */

const test = require("node:test");
const assert = require("node:assert");

const { makeHeddleSession } = require("./helpers/session");
const { requireHeddleModule } = require("./helpers/ace-loader");

function makeFoldMode() {
    const { FoldMode } = requireHeddleModule("mode/folding/heddle");
    return new FoldMode();
}

test("'{{' ... '}}' output block folds and pairs", () => {
    // `{{` becomes a fold-open only when emitted from a directive body
    // (`@if(x){{ ... }}`); a bare top-level `{{` is plain HTML text.
    const session = makeHeddleSession("@if(x){{\n  hello\n}}");
    const fold = makeFoldMode();

    assert.strictEqual(
        fold.getFoldWidget(session, "markbeginend", 0),
        "start",
        "row 0 ('{{') should offer a fold-start widget"
    );

    const range = fold.getFoldWidgetRange(session, "markbeginend", 0);
    assert.ok(range, "expected a fold range for the '{{' block");
    assert.strictEqual(range.start.row, 0);
    assert.strictEqual(range.end.row, 2, "fold should close on the '}}' row");
});

test("'@%' ... '%@' definition block folds and pairs", () => {
    const session = makeHeddleSession("@%\nname: Type\n%@");
    const fold = makeFoldMode();

    assert.strictEqual(
        fold.getFoldWidget(session, "markbeginend", 0),
        "start",
        "row 0 ('@%') should offer a fold-start widget"
    );

    const range = fold.getFoldWidgetRange(session, "markbeginend", 0);
    assert.ok(range, "expected a fold range for the '@%' block");
    assert.strictEqual(range.start.row, 0);
    assert.strictEqual(range.end.row, 2, "fold should close on the '%@' row");
});

test("a native expression with '[' ] does not break '{{'/'}}' fold pairing", () => {
    const session = makeHeddleSession("@if(x){{\n  @upper(items[0])\n}}");
    const fold = makeFoldMode();

    assert.strictEqual(fold.getFoldWidget(session, "markbeginend", 0), "start");
    const range = fold.getFoldWidgetRange(session, "markbeginend", 0);
    assert.ok(range, "expected a fold range");
    assert.strictEqual(range.end.row, 2);
});

test("generic-type '<'/'>' do not produce fold widgets", () => {
    // A prop-list header with a generic type must not offer a fold widget on
    // its `<`/`>` (they are storage.type, not fold-pair tokens).
    const session = makeHeddleSession("@%c(items: List<int>)%@");
    const fold = makeFoldMode();

    // The single-line `@%...%@` closes on the same row, so no fold-start.
    assert.strictEqual(
        fold.getFoldWidget(session, "markbeginend", 0),
        "",
        "a single-line def with a generic type should not offer a fold widget"
    );
});
