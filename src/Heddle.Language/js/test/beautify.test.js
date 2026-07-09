"use strict";

/*
 * WS4 beautify goldens + WS9 smoke tests.
 *
 * Proves `ext/beautify.js` loads, runs against a Heddle-tokenized EditSession,
 * and is idempotent (format twice == format once). The golden fixtures below
 * assert the Heddle-specific formatting from WS4 §5:
 *   - `@if/@elif/@else` chain spacing after `}}`
 *   - native-expression operator / bracket / comma spacing inside `@(…)`
 *   - prop-declaration header `name: type = default` layout
 *   - named-argument `name: value` layout
 *   - strings + `@*…*@` comments preserved verbatim
 *   - `formatOptions` long-list breaking (read fresh each call)
 * Every golden also asserts idempotency: `beautify(beautify(x)) === beautify(x)`.
 */

const test = require("node:test");
const assert = require("node:assert");

const { beautify, beautifyTwice, getBeautifyModule, makeHeddleSession } = require("./helpers/beautify");

// The break indent is the session's tab string (spaces or a tab, per Ace config).
const TAB = makeHeddleSession("").getTabString();

test("beautify module exposes beautify() and formatOptions", () => {
    const mod = getBeautifyModule();
    assert.strictEqual(typeof mod.beautify, "function");
    assert.ok(mod.formatOptions && typeof mod.formatOptions === "object");
});

test("formatOptions declares the Heddle option schema", () => {
    const mod = getBeautifyModule();
    // WS4 task 3: the Heddle-specific keys must be present on the exported object.
    assert.strictEqual(typeof mod.formatOptions.lineBreaksAfterCommasInCurlyBlock, "boolean");
    assert.strictEqual(typeof mod.formatOptions.maxLineLength, "number");
    assert.strictEqual(typeof mod.formatOptions.breakLongPropLists, "boolean");
});

test("beautify returns a string and preserves content", () => {
    const out = beautify("<div>hi</div>");
    assert.strictEqual(typeof out, "string");
    assert.ok(out.indexOf("hi") !== -1, "expected content to survive formatting");
});

test("beautify is idempotent (format twice == format once)", () => {
    const inputs = [
        "<div>hi</div>",
        "@if(x) {{ <span>ok</span> }}"
    ];
    for (const input of inputs) {
        const { once, twice } = beautifyTwice(input);
        assert.strictEqual(twice, once, "second format changed output for: " + JSON.stringify(input));
    }
});

// ---------------------------------------------------------------------------
// WS4 golden fixtures (before -> after) + idempotency
// ---------------------------------------------------------------------------

/**
 * Assert `beautify(before) === after` and that formatting is idempotent
 * (`beautify(after) === after`). The idempotency check is the WS4 DoD gate.
 */
function golden(before, after) {
    const once = beautify(before);
    assert.strictEqual(once, after, "beautify(before) mismatch");
    const twice = beautify(once);
    assert.strictEqual(twice, once, "beautify is not idempotent for: " + JSON.stringify(after));
}

test("golden: @if/@elif/@else chain gets a single space after }}", () => {
    // No space, then over-spaced: both normalize to exactly one space before @elif/@else.
    golden(
        "@if(x) {{ a }}@elif(y) {{ b }}@else {{ c }}",
        "@if(x) {{ a }} @elif(y) {{ b }} @else {{ c }}"
    );
    golden(
        "@if(x) {{ a }}   @elif(y) {{ b }}   @else {{ c }}",
        "@if(x) {{ a }} @elif(y) {{ b }} @else {{ c }}"
    );
});

test("golden: @elif/@else on their own line keep the line break (no reflow)", () => {
    // A newline before @elif/@else must be preserved (not collapsed to a space).
    golden(
        "@if(x) {{ a }}\n@elif(y) {{ b }}\n@else {{ c }}",
        "@if(x) {{ a }}\n@elif(y) {{ b }}\n@else {{ c }}"
    );
});

test("golden: native expression - operators, unary, brackets, commas", () => {
    golden("@(a+b*c - !d)", "@(a + b * c - !d)");
    golden("@(a&&b||c==d)", "@(a && b || c == d)");
    golden("@(items[0] ,foo, bar)", "@(items[0], foo, bar)");
    golden("@(-x + y)", "@(-x + y)");
});

test("golden: prop-declaration header layout (name: type = default, slot::)", () => {
    golden(
        '@% <card( title :  string="x" ,  count:int = 0 )> {{ }} %@',
        '@% <card(title: string = "x", count: int = 0)> {{ }} %@'
    );
    golden(
        '@%<card(title:string="x",body::string)>{{ }}%@',
        '@%<card(title: string = "x", body:: string)>{{ }}%@'
    );
});

test("golden: named-argument call layout (name: value)", () => {
    golden("@out(a:1,b:2)", "@out(a: 1, b: 2)");
    golden("@out( alpha:1 , beta:2 )", "@out(alpha: 1, beta: 2)");
});

test("golden: strings and @*...*@ comments preserved verbatim", () => {
    // Spacing, commas and operators *inside* a string literal are never rewritten.
    golden('@(  "a string , with ! ops"  )', '@("a string , with ! ops")');
    // A comment's interior (including deliberate double spaces, comma, bang) is untouched.
    golden("@* keep  spacing , ! *@", "@* keep  spacing , ! *@");
    // HTML/text outside directives is not reflowed.
    golden("<div>  hello ,  world  </div>", "<div>  hello ,  world  </div>");
});

// ---------------------------------------------------------------------------
// WS4 formatOptions: long-list breaking, read fresh each invocation
// ---------------------------------------------------------------------------

/** Run `fn` with a temporary patch of `exports.formatOptions`, always restoring. */
function withOptions(patch, fn) {
    const mod = getBeautifyModule();
    const saved = Object.assign({}, mod.formatOptions);
    Object.assign(mod.formatOptions, patch);
    try {
        return fn();
    } finally {
        // Restore exactly (delete keys that were not originally present).
        for (const k of Object.keys(mod.formatOptions)) {
            if (!(k in saved)) delete mod.formatOptions[k];
        }
        Object.assign(mod.formatOptions, saved);
    }
}

test("golden: long named-argument list breaks across lines when over maxLineLength", () => {
    withOptions({ maxLineLength: 30, breakLongPropLists: true, lineBreaksAfterCommasInCurlyBlock: true }, () => {
        const before = "@out(alpha: 1, beta: 2, gamma: 3)";
        const after = "@out(\n" + TAB + "alpha: 1,\n" + TAB + "beta: 2,\n" + TAB + "gamma: 3\n)";
        const once = beautify(before);
        assert.strictEqual(once, after, "long list did not break as expected");
        assert.strictEqual(beautify(once), once, "broken long list is not idempotent");
    });
});

test("golden: long prop-declaration list breaks across lines", () => {
    withOptions({ maxLineLength: 30, breakLongPropLists: true, lineBreaksAfterCommasInCurlyBlock: true }, () => {
        const before = "@%<card(title: string, body: string, footer: string)>{{ }}%@";
        const after = "@%<card(\n" + TAB + "title: string,\n" + TAB + "body: string,\n" + TAB + "footer: string\n)>{{ }}%@";
        const once = beautify(before);
        assert.strictEqual(once, after, "long prop list did not break as expected");
        assert.strictEqual(beautify(once), once, "broken prop list is not idempotent");
    });
});

test("formatOptions read fresh: breakLongPropLists=false disables breaking", () => {
    withOptions({ maxLineLength: 30, breakLongPropLists: false }, () => {
        const before = "@out(alpha: 1, beta: 2, gamma: 3)";
        // Same tight single-line normalization, no line breaks.
        assert.strictEqual(beautify(before), "@out(alpha: 1, beta: 2, gamma: 3)");
    });
    // With defaults restored, the same short input also stays on one line.
    assert.strictEqual(beautify("@out(alpha: 1, beta: 2, gamma: 3)"), "@out(alpha: 1, beta: 2, gamma: 3)");
});

