"use strict";

import HeddleLexer from "./HeddleLexer";
import {HeddleErrorListener} from "./HeddleErrorListener";

export class HeddleLexerExtended extends HeddleLexer {
    constructor(input, context) {
        super(input);
        this.context = context;
        this._underflowTokenStart = -1;
        this._listeners = [];
        this.removeErrorListeners();
        this.addErrorListener(new HeddleErrorListener(context));
    }

    // Phase 2 (post-2.0) — JS-target translation of the grammar's comment-adjacency
    // guard on AT_ESCAPE / SUB_AT_ESCAPE ('@@' {InputStream.LA(1) != '*'}? -> type(RAW)).
    // The predicate action in HeddleLexer.g4 is written for the C# target
    // (`InputStream` property, char literal); the ANTLR JavaScript target copies it
    // verbatim, where `InputStream` is an unresolved identifier and LA(1) returns a
    // code point, so the generated methods throw at runtime. The generated files must
    // never be hand-edited (propagate_js.js contract), so the working translation
    // lives here: these subclass methods shadow the broken prototype-assigned ones.
    // Semantics match the C# build exactly: the escape fires unless the character
    // after '@@' is '*' (then the second '@' starts a comment '@*…*@'); at EOF
    // LA(1) === Token.EOF (-1) and the escape fires, same as C#.
    // Robustness (JSACE-A-OBS) — malformed input must never crash the worker.
    // Grammar rules such as OUT_SUB_CL / CALL_RETURN_SUB_CL run a double
    // `popMode, popMode`, assuming OUT/CALL mode was entered from inside a
    // pushed SUB_BLOCK. When a directive opens right after an *unconnected*
    // '{{' at document scope (UNCONNECTED_SUB_ST emits SUB_START without a
    // pushMode) — e.g. mid-typing input like `{{ @if }}` — the second pop
    // underflows and the ANTLR JS runtime's Lexer.popMode() throws the string
    // "Empty Stack", which would propagate uncaught out of the worker. Degrade
    // instead: fall back to document scope (DEFAULT_MODE) and keep lexing —
    // AND record a positioned HED0003-class error for the underflow itself,
    // because the same event makes the C# compile fail (its runtime throws
    // "Empty Stack" here, caught at the Compile boundary and surfaced as an
    // error). Without the recorded error a sub-class of malformed shapes
    // (e.g. `{{@if}}`, `{{ @if(X) }} }}`) would otherwise degrade to a fully
    // CLEAN parse in the editor while the engine rejects the document. The
    // error is recorded at the offending token's position, at most once per
    // token (a rule may run `popMode, popMode`, underflowing twice on the same
    // token); later genuine parser diagnostics still surface normally. The
    // underflow is unreachable for balanced input, so valid documents are
    // untouched. (Residual-throw backstop lives in heddle_worker.js.)
    popMode() {
        if (this._modeStack.length === 0) {
            if (this.context && this._underflowTokenStart !== this._tokenStartCharIndex) {
                this._underflowTokenStart = this._tokenStartCharIndex;
                this.context.addError(
                    "unexpected block terminator",
                    null,
                    {
                        start: this._tokenStartCharIndex,
                        stop: Math.max(this._tokenStartCharIndex, this._input.index - 1)
                    },
                    this._tokenStartLine,
                    this._tokenStartColumn
                );
            }
            this.mode(HeddleLexer.DEFAULT_MODE);
            return this._mode;
        }
        return super.popMode();
    }

    AT_ESCAPE_sempred(localctx, predIndex) {
        switch (predIndex) {
            case 0:
                return this._input.LA(1) !== 0x2a /* '*' */;
            default:
                throw "No predicate with index:" + predIndex;
        }
    }

    SUB_AT_ESCAPE_sempred(localctx, predIndex) {
        switch (predIndex) {
            case 1:
                return this._input.LA(1) !== 0x2a /* '*' */;
            default:
                throw "No predicate with index:" + predIndex;
        }
    }
}