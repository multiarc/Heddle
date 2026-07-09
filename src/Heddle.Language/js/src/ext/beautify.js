// written based on ace v1.32.6, adjust as you upgrade
"use strict";

// do not indent after singleton tags or <html>
exports.singletonTags = ["area", "base", "br", "col", "command", "embed", "hr", "html", "img", "input", "keygen", "link", "meta", "param", "source", "track", "wbr"];

// insert a line break after block level tags
exports.blockTags = ["article", "aside", "blockquote", "body", "div", "dl", "fieldset", "footer", "form", "head", "header", "html", "nav", "ol", "p", "script", "section", "style", "table", "tbody", "tfoot", "thead", "ul"];

/**
 * Heddle-specific formatter options.
 *
 * This object is the single source of truth for the beautifier's tunables and
 * is read *fresh* on every `beautify()` invocation (see `readOptions` below),
 * so callers can mutate it between formats and get deterministic results.
 *
 * @type {{
 *   lineBreaksAfterCommasInCurlyBlock?: boolean,
 *   maxLineLength?: number,
 *   breakLongPropLists?: boolean
 * }}
 */
exports.formatOptions = {
    // When a prop/named-arg list is broken across lines, break points are placed
    // after commas (one item per line). When false, long lists are never broken.
    lineBreaksAfterCommasInCurlyBlock: true,
    // A prop/named-arg list is only broken when its single-line form would push
    // the current line past this width.
    maxLineLength: 100,
    // Master switch for breaking long prop-declaration / named-argument lists.
    breakLongPropLists: true
};

// Default option values, cloned fresh each call so external mutation of
// `exports.formatOptions` is always honored without leaking state between runs.
var DEFAULT_FORMAT_OPTIONS = {
    lineBreaksAfterCommasInCurlyBlock: true,
    maxLineLength: 100,
    breakLongPropLists: true
};

function readOptions() {
    var opts = exports.formatOptions || {};
    return {
        lineBreaksAfterCommasInCurlyBlock: opts.lineBreaksAfterCommasInCurlyBlock !== undefined
            ? opts.lineBreaksAfterCommasInCurlyBlock
            : DEFAULT_FORMAT_OPTIONS.lineBreaksAfterCommasInCurlyBlock,
        maxLineLength: typeof opts.maxLineLength === "number"
            ? opts.maxLineLength
            : DEFAULT_FORMAT_OPTIONS.maxLineLength,
        breakLongPropLists: opts.breakLongPropLists !== undefined
            ? opts.breakLongPropLists
            : DEFAULT_FORMAT_OPTIONS.breakLongPropLists
    };
}

// -- Heddle expression atom model -------------------------------------------
//
// The Ace Heddle tokenizer folds whitespace into token values inconsistently
// (e.g. `"c "`) and also emits standalone whitespace tokens. To format an
// expression / prop-list region idempotently we throw that whitespace away,
// reduce each significant token to a trimmed "atom" tagged with a kind, and
// re-emit spacing purely from the atom kinds. Because the emitted spacing is a
// pure function of the kinds, re-tokenizing the output yields the same atoms
// and therefore the same text (a fixed point => idempotent).

var ATOM = {
    OPEN: "open",       // ( or [
    CLOSE: "close",     // ) or ]
    COMMA: "comma",     // ,
    COLON: "colon",     // : (named-arg / prop / ternary tail)
    DBLCOLON: "dblcolon", // :: (slot type)
    ASSIGN: "assign",   // = (prop default)
    DOT: "dot",         // . (member access)
    PREFIX: "prefix",   // ! ~ (unary prefix)
    MINUS: "minus",     // - (resolved to unary/binary at emit time)
    BINOP: "binop",     // + * / && || == etc.
    OPERAND: "operand"  // identifier, literal, string, type, slot name
};

function classifyAtom(tok) {
    var v = tok.value.trim();
    if (v === "") {
        return null; // pure whitespace - dropped
    }
    var type = tok.type;
    var kind;
    if (v === "(" || v === "[") {
        kind = ATOM.OPEN;
    } else if (v === ")" || v === "]") {
        kind = ATOM.CLOSE;
    } else if (v === ",") {
        kind = ATOM.COMMA;
    } else if (v === ":") {
        kind = ATOM.COLON;
    } else if (v === "::") {
        kind = ATOM.DBLCOLON;
    } else if (v === "=") {
        kind = ATOM.ASSIGN;
    } else if (v === ".") {
        kind = ATOM.DOT;
    } else if (v === "!" || v === "~") {
        kind = ATOM.PREFIX;
    } else if (v === "-") {
        kind = ATOM.MINUS;
    } else if (type.indexOf("keyword.operator") !== -1 && type.indexOf("paren") === -1) {
        kind = ATOM.BINOP;
    } else {
        kind = ATOM.OPERAND;
    }
    return { v: v, kind: kind };
}

// Resolve a MINUS atom to unary vs binary based on the previous atom.
function isUnary(atoms, i) {
    var prev = i > 0 ? atoms[i - 1] : null;
    if (!prev) {
        return true;
    }
    switch (prev.kind) {
        case ATOM.OPEN:
        case ATOM.COMMA:
        case ATOM.COLON:
        case ATOM.DBLCOLON:
        case ATOM.ASSIGN:
        case ATOM.BINOP:
        case ATOM.PREFIX:
            return true;
        case ATOM.MINUS:
            return isUnary(atoms, i - 1); // a leading unary chain stays unary
        default:
            return false;
    }
}

// Decide whether a single space separates atoms[i-1] and atoms[i].
function spaceBefore(atoms, i) {
    if (i === 0) {
        return false;
    }
    var prev = atoms[i - 1];
    var cur = atoms[i];
    var prevUnaryMinus = prev.kind === ATOM.MINUS && isUnary(atoms, i - 1);
    var curUnaryMinus = cur.kind === ATOM.MINUS && isUnary(atoms, i);

    // Current-atom driven rules first.
    if (cur.kind === ATOM.CLOSE) return false;
    if (cur.kind === ATOM.COMMA) return false;
    if (cur.kind === ATOM.COLON) return false;
    if (cur.kind === ATOM.DBLCOLON) return false;
    if (cur.kind === ATOM.DOT) return false;

    if (cur.kind === ATOM.OPEN) {
        if (cur.v === "[") return false; // indexer binds tight
        // '(' : no space when it forms a call/grouping right after an operand,
        // closing bracket, another opener, or a prefix; space after operators.
        if (prev.kind === ATOM.OPERAND || prev.kind === ATOM.CLOSE) return false;
        if (prev.kind === ATOM.OPEN) return false;
        if (prev.kind === ATOM.PREFIX || prevUnaryMinus) return false;
        return true;
    }

    if (cur.kind === ATOM.PREFIX || curUnaryMinus) {
        if (prev.kind === ATOM.OPEN) return false;
        if (prev.kind === ATOM.PREFIX || prevUnaryMinus) return false;
        return true;
    }

    if (cur.kind === ATOM.BINOP || cur.kind === ATOM.ASSIGN) {
        if (prev.kind === ATOM.OPEN) return false;
        return true;
    }

    // OPERAND
    if (prev.kind === ATOM.OPEN) return false;
    if (prev.kind === ATOM.PREFIX || prevUnaryMinus) return false;
    if (prev.kind === ATOM.DOT) return false;
    return true;
}

// Emit a flat atom list to a single normalized line.
function emitAtoms(atoms) {
    var res = "";
    for (var i = 0; i < atoms.length; i++) {
        if (spaceBefore(atoms, i) && res !== "") {
            res += " ";
        }
        res += atoms[i].v;
    }
    return res;
}

function hasTopLevelComma(atoms) {
    var depth = 0;
    for (var i = 0; i < atoms.length; i++) {
        if (atoms[i].kind === ATOM.OPEN) depth++;
        else if (atoms[i].kind === ATOM.CLOSE) depth--;
        else if (atoms[i].kind === ATOM.COMMA && depth === 0) return true;
    }
    return false;
}

// Split interior atoms into top-level comma-separated items.
function splitTopLevel(atoms) {
    var items = [];
    var current = [];
    var depth = 0;
    for (var i = 0; i < atoms.length; i++) {
        var a = atoms[i];
        if (a.kind === ATOM.OPEN) depth++;
        else if (a.kind === ATOM.CLOSE) depth--;
        if (a.kind === ATOM.COMMA && depth === 0) {
            items.push(current);
            current = [];
        } else {
            current.push(a);
        }
    }
    items.push(current);
    return items;
}

/**
 * Format a Heddle document held in an Ace EditSession.
 *
 * The formatter is deliberately conservative: HTML, text, `@*…*@` comments and
 * string/char literals outside directives are reproduced **verbatim** (WS4 task
 * 4 - no text/HTML reflow). Only three things are rewritten, and each is a pure
 * function of the token stream so the whole pass is idempotent:
 *   1. `@elif`/`@elseif`/`@else` get exactly one space after a closing `}}`.
 *   2. Native-expression regions inside `@(…)` / `@out(…)` get normalized
 *      operator / comma / bracket spacing.
 *   3. Prop-declaration and named-argument lists get `name: type = default`
 *      spacing, with optional line breaks for long lists via `formatOptions`.
 *
 * @param {import("../edit_session").EditSession} session
 */
exports.beautify = function(session) {
    var options = readOptions();
    var tabString = session.getTabString();

    // Flatten the tokenized document, tagging each token with its row so we can
    // reproduce blank lines and non-directive content verbatim.
    var toks = [];
    var rows = session.getLength();
    for (var r = 0; r < rows; r++) {
        var rowToks = session.getTokens(r);
        for (var t = 0; t < rowToks.length; t++) {
            toks.push({ type: rowToks[t].type, value: rowToks[t].value, row: r });
        }
    }

    var out = "";
    var lastRow = 0;

    function newlinesTo(row) {
        while (lastRow < row) {
            out += "\n";
            lastRow++;
        }
    }

    function currentLineText() {
        var nl = out.lastIndexOf("\n");
        return nl === -1 ? out : out.slice(nl + 1);
    }

    function currentBaseIndent() {
        var line = currentLineText();
        var m = /^[ \t]*/.exec(line);
        return m ? m[0] : "";
    }

    // Emit an interior atom list wrapped by an already-emitted open paren and a
    // trailing close string, breaking across lines when the list is long.
    function emitParenList(interior, closeStr) {
        var single = emitAtoms(interior);
        var breakable = options.breakLongPropLists
            && options.lineBreaksAfterCommasInCurlyBlock
            && hasTopLevelComma(interior);
        if (breakable && (currentLineText().length + single.length + closeStr.length) > options.maxLineLength) {
            var baseIndent = currentBaseIndent();
            var items = splitTopLevel(interior);
            for (var j = 0; j < items.length; j++) {
                out += "\n" + baseIndent + tabString + emitAtoms(items[j]);
                if (j < items.length - 1) {
                    out += ",";
                }
            }
            out += "\n" + baseIndent + closeStr;
        } else {
            out += single + closeStr;
        }
    }

    function isDefPropOpen(tk) {
        return tk.type.indexOf("heddle-def") === 0
            && /keyword\.operator\.paren$/.test(tk.type)
            && tk.value === "(";
    }

    function isDefPropClose(tk) {
        return tk.type.indexOf("heddle-def") === 0
            && /keyword\.operator\.paren$/.test(tk.type)
            && tk.value === ")";
    }

    function isElifElse(tk) {
        return tk.type.indexOf("heddle-sub") === 0
            && tk.type.indexOf("keyword") !== -1
            && /^@(elif|elseif|else)$/.test(tk.value);
    }

    var i = 0;
    while (i < toks.length) {
        var tk = toks[i];

        // (1) @elif / @elseif / @else spacing after a closing }} on the same line.
        if (isElifElse(tk)) {
            newlinesTo(tk.row);
            // Trim trailing spaces/tabs (never a newline) so we control the gap.
            out = out.replace(/[ \t]+$/, "");
            if (/\}$/.test(out)) {
                out += " ";
            }
            out += tk.value;
            lastRow = tk.row;
            i++;
            continue;
        }

        // (2) Prop-declaration list: `<name(… )>` inside `@% … %@`.
        if (isDefPropOpen(tk)) {
            newlinesTo(tk.row);
            out += "(";
            var depthD = 1;
            var interiorD = [];
            var k = i + 1;
            for (; k < toks.length; k++) {
                var dk = toks[k];
                if (isDefPropOpen(dk)) {
                    depthD++;
                    var a1 = classifyAtom(dk);
                    if (a1) interiorD.push(a1);
                    continue;
                }
                if (isDefPropClose(dk)) {
                    depthD--;
                    if (depthD === 0) break;
                    var a2 = classifyAtom(dk);
                    if (a2) interiorD.push(a2);
                    continue;
                }
                var a3 = classifyAtom(dk);
                if (a3) interiorD.push(a3);
            }
            emitParenList(interiorD, k < toks.length ? ")" : "");
            lastRow = (k < toks.length ? toks[k] : toks[toks.length - 1]).row;
            i = (k < toks.length ? k + 1 : toks.length);
            continue;
        }

        // (3) Native-expression / named-argument region inside @(…) / @out(…).
        // The opening `(` is a heddle-out token already emitted verbatim; the
        // run's trailing `)` is a heddle-call token and closes the region.
        if (tk.type.indexOf("heddle-call.") === 0) {
            var runAtoms = [];
            var j2 = i;
            for (; j2 < toks.length && toks[j2].type.indexOf("heddle-call.") === 0; j2++) {
                var ca = classifyAtom(toks[j2]);
                if (ca) runAtoms.push(ca);
            }
            var runEndRow = toks[j2 - 1].row;
            var closeStr = "";
            if (runAtoms.length && runAtoms[runAtoms.length - 1].kind === ATOM.CLOSE
                && runAtoms[runAtoms.length - 1].v === ")") {
                closeStr = ")";
                runAtoms.pop();
            }
            emitParenList(runAtoms, closeStr);
            lastRow = runEndRow;
            i = j2;
            continue;
        }

        // Everything else: reproduce verbatim (no HTML/text reflow).
        newlinesTo(tk.row);
        out += tk.value;
        i++;
    }

    session.doc.setValue(out);
};

exports.commands = [{
    name: "beautify",
    description: "Format selection (Beautify)",
    exec: function(editor) {
        exports.beautify(editor.session);
    },
    bindKey: "Ctrl-Shift-B"
}];
