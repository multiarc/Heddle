"use strict";

/*
 * Behaviour-driver helper (WS7 verification surface).
 *
 * The Heddle bracket behaviour (`mode/behaviour/heddle.js`) is written as Ace
 * `Behaviour` insertion/deletion callbacks that expect a live editor+session.
 * A full Ace `Editor` needs a DOM renderer, which is impractical headlessly,
 * so instead we drive the pure callbacks directly:
 *   - a REAL Ace `EditSession` backed by the Heddle tokenizer (via
 *     `makeHeddleSession`) supplies `getTokens`/`getState`/`$findOpeningBracket`
 *     and a mutable `doc`, exactly what the callbacks read;
 *   - a lightweight mock `editor` supplies cursor/selection accessors.
 *
 * This mirrors how Ace's own CstyleBehaviour is exercised - by invoking the
 * registered callback with a state/action/editor/session/text tuple.
 */

const { requireHeddleModule } = require("./ace-loader");
const { makeHeddleSession } = require("./session");

/** @returns {*} a fresh TTLstyleBehaviour instance */
function makeBehaviour() {
    const { TTLstyleBehaviour } = requireHeddleModule("mode/behaviour/heddle");
    return new TTLstyleBehaviour();
}

/**
 * A minimal editor stand-in exposing only what the behaviour callbacks use.
 * `cursor` is mutable via `setCursor` so a test can simulate typing.
 * @param {*} session
 * @param {{row:number, column:number}} cursor
 */
function makeMockEditor(session, cursor) {
    const pos = { row: cursor.row, column: cursor.column };
    return {
        session,
        multiSelect: null,
        inMultiSelectMode: false,
        setCursor(row, column) {
            pos.row = row;
            pos.column = column;
        },
        getCursorPosition() {
            return { row: pos.row, column: pos.column };
        },
        getSelectionRange() {
            return {
                start: { row: pos.row, column: pos.column },
                end: { row: pos.row, column: pos.column }
            };
        },
        getWrapBehavioursEnabled() {
            return true;
        }
    };
}

/**
 * Invoke a named insertion behaviour callback directly.
 * @returns {*} the behaviour result ({text, selection} | undefined)
 */
function invokeInsertion(behaviour, name, editor, session, text) {
    const handler = behaviour.$behaviours[name] && behaviour.$behaviours[name].insertion;
    if (!handler) {
        throw new Error("no insertion behaviour named '" + name + "'");
    }
    const state = session.getState(editor.getCursorPosition().row);
    return handler.call(behaviour, state, "insertion", editor, session, text);
}

/**
 * Invoke a named deletion behaviour callback directly.
 * @param {*} range an Ace Range instance for the selection being deleted
 */
function invokeDeletion(behaviour, name, editor, session, range) {
    const handler = behaviour.$behaviours[name] && behaviour.$behaviours[name].deletion;
    if (!handler) {
        throw new Error("no deletion behaviour named '" + name + "'");
    }
    const state = session.getState(range.start.row);
    return handler.call(behaviour, state, "deletion", editor, session, range);
}

module.exports = {
    makeHeddleSession,
    makeBehaviour,
    makeMockEditor,
    invokeInsertion,
    invokeDeletion
};
