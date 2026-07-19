"use strict";

/*
 * WS7 behaviour fixtures: bracket auto-close / skip-over.
 *
 * Drives the Heddle bracket behaviour callbacks (`mode/behaviour/heddle.js`)
 * directly against a real Ace EditSession + a mock editor (see
 * helpers/behaviour.js). Asserts the v2 `[` `]` pair auto-closes and skips
 * inside native expressions, that existing pairs still work, and that `<`/`>`
 * in generic types are never auto-paired.
 */

const test = require("node:test");
const assert = require("node:assert");

const {
    makeHeddleSession,
    makeBehaviour,
    makeMockEditor,
    invokeInsertion,
    invokeDeletion
} = require("./helpers/behaviour");
const { requireAceModule, requireHeddleModule } = require("./helpers/ace-loader");
const { Range } = requireAceModule("range");
const { tokenizeDocument } = require("./helpers/tokenize");

// A native-expression argument list spread over lines; the caret sits at the
// end of the `arr` line, where the following token is null/whitespace so
// isSaneInsertion passes - exactly the mid-typing situation a user hits.
const NATIVE_EXPR = "@upper(\narr\n)";

test("typing '[' inside a native expression auto-closes to '[]'", () => {
    const session = makeHeddleSession(NATIVE_EXPR);
    const behaviour = makeBehaviour();
    const editor = makeMockEditor(session, { row: 1, column: 3 }); // end of 'arr'

    const result = invokeInsertion(behaviour, "square", editor, session, "[");
    assert.ok(result, "expected the '[' insertion behaviour to fire");
    assert.strictEqual(result.text, "[]", "'[' should auto-close to '[]'");
    // selection [row, col] => caret lands between the brackets.
    assert.deepStrictEqual(result.selection, [1, 1]);
});

test("typing ']' over an auto-inserted ']' skips instead of inserting", () => {
    const session = makeHeddleSession(NATIVE_EXPR);
    const behaviour = makeBehaviour();
    const editor = makeMockEditor(session, { row: 1, column: 3 });

    // 1) Auto-close '[' -> records the auto-inserted ']' in the shared context.
    const open = invokeInsertion(behaviour, "square", editor, session, "[");
    assert.strictEqual(open.text, "[]");

    // 2) Simulate the editor applying the insert: row 1 becomes `arr[]` and the
    //    caret moves between the brackets.
    session.insert({ row: 1, column: 3 }, "[]");
    editor.setCursor(1, 4); // between '[' and ']'

    // 3) Typing ']' now should skip over the existing ']' (empty text result).
    const skip = invokeInsertion(behaviour, "square", editor, session, "]");
    assert.ok(skip, "expected the ']' skip behaviour to fire");
    assert.strictEqual(skip.text, "", "']' should skip over the existing bracket");
});

test("deleting the '[' of an empty '[]' also removes the matching ']'", () => {
    const session = makeHeddleSession("@upper(arr[])");
    const behaviour = makeBehaviour();
    const editor = makeMockEditor(session, { row: 0, column: 10 });

    // Range covering just the '[' at column 10.
    const range = new Range(0, 10, 0, 11);
    const result = invokeDeletion(behaviour, "square", editor, session, range);
    assert.ok(result, "expected the '[' deletion behaviour to fire");
    assert.strictEqual(result.end.column, 12, "deletion range should extend over the ']'");
});

test("existing '(' pair still auto-closes (no regression)", () => {
    const session = makeHeddleSession(NATIVE_EXPR);
    const behaviour = makeBehaviour();
    const editor = makeMockEditor(session, { row: 1, column: 3 });

    const result = invokeInsertion(behaviour, "parens", editor, session, "(");
    assert.ok(result, "expected the '(' insertion behaviour to fire");
    assert.strictEqual(result.text, "()");
});

test("'<' / '>' in generic types are never auto-paired", () => {
    const session = makeHeddleSession("@upper(arr)");
    const behaviour = makeBehaviour();
    const editor = makeMockEditor(session, { row: 0, column: 10 });

    // No behaviour is registered for angle brackets, so every registered pair
    // must decline to auto-close them.
    for (const name of ["braces", "parens", "square", "brackets"]) {
        for (const ch of ["<", ">"]) {
            const result = invokeInsertion(behaviour, name, editor, session, ch);
            assert.ok(
                !result,
                `behaviour '${name}' must not auto-pair '${ch}'`
            );
        }
    }
});

test("'<'/'>' in a generic type tokenize as storage.type, never as brackets", () => {
    // Prop-list type `List<int>` exercises the generic-type sub-state.
    const tokens = tokenizeDocument("@%c(items: List<int>)%@");
    const angleTokens = tokens.filter((t) => t.value.includes("<") || t.value.includes(">"));
    assert.ok(angleTokens.length > 0, "expected the generic type to produce '<'/'>' tokens");
    for (const t of angleTokens) {
        assert.ok(
            t.type.includes("storage.type"),
            `generic '<'/'>' token should be storage.type, got '${t.type}' for '${t.value}'`
        );
        assert.ok(
            !/\bparen\b/.test(t.type),
            `generic '<'/'>' token must not be a paren/bracket class, got '${t.type}'`
        );
    }
});

test("outdent module recognizes ']' alongside '}}' / '%@' / ')'", () => {
    const { TTLMatchingBraceOutdent } = requireHeddleModule("mode/heddle_brace_outdent");
    const outdent = new TTLMatchingBraceOutdent();

    // A whitespace-only line + a closing bracket input triggers auto-outdent.
    for (const close of ["]", ")", "}}", "%@"]) {
        assert.ok(
            outdent.checkOutdent("    ", close),
            `checkOutdent should fire for a closing '${close}'`
        );
    }
    // Non-closing input must not trigger outdent.
    assert.ok(!outdent.checkOutdent("    ", "x"), "checkOutdent should ignore non-closers");
});
