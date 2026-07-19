/*
 * Lexer Rules
 */

lexer grammar HeddleLexer;

import CSharp;

tokens {
    TEXT, WS, IMPORT_TOKEN, ID, ROOT_REF, MEMBER_P, OUT, SUB_START, SUB_CLOSE, CSHARP_END, CSHARP_TOKEN, CSHARP_START, DEF_STARTNAME, DEF_ENDNAME, DELIM, DEF_START, DEF_CLOSE, RAW, OUT_PARAMSTART, OUT_PARAMEND, DEF_OUT,
    // NEW (phase 1 — native expressions):
    TRUE, FALSE, NULL, INT_LIT, REAL_LIT, STRING_LIT, CHAR_LIT,
    OP_QQ, OP_QUESTION, OP_AND, OP_OR, OP_EQ, OP_NEQ,
    OP_LSHIFT, OP_RSHIFT, OP_LE, OP_GE, OP_LT, OP_GT,
    OP_PLUS, OP_MINUS, OP_STAR, OP_SLASH, OP_PERCENT,
    OP_AMP, OP_PIPE, OP_CARET, OP_NOT, OP_TILDE,
    LBRACKET, RBRACKET, COMMA,
    // NEW (phase 5 — props & slots):
    THIS, ASSIGN
}

fragment ID_TOKEN: IDENTIFIER;
fragment ID_TYPE: IDENTIFIER ('.' IDENTIFIER)* SINGLE_LINE_WS* ('<' SINGLE_LINE_WS* ID_TYPE SINGLE_LINE_WS* (',' SINGLE_LINE_WS* ID_TYPE)* SINGLE_LINE_WS* '>')? '[]'*;

fragment WS: WHITESPACE;

fragment IMP: '@<<';
fragment MEMB_P: '.';
fragment SUB_ST: '{{';
fragment SUB_CL: '}}';
fragment PARA_ST: '(';
fragment PARA_CL: ')';
fragment DEF_ST: '@%';
fragment DEF_CL: '%@';
fragment OUT_ST: '@';
fragment DEF_T: '::';
fragment EXT_DELIM: ':';
fragment DEF_STNAME: '<';
fragment DEF_CLNAME: '>';
fragment DEF_MAKEOUT: '->';
fragment WS_START: '@\\';
fragment COMMENT_BLOCK: '@*' ('@' | '*'* ~[*@])* '*'+ '@';
fragment RAW_BLOCK : '@{' ('@' | '}'* ~[}@])* '}'+ '@';
fragment RAW_START_LN: '@:';
fragment RAW_LINE : RAW_START_LN ~[\r\n]*;
fragment EAT_WS: WS_START WS*;

COMMENT: COMMENT_BLOCK -> channel(HIDDEN);

SKIP_WS: EAT_WS -> channel(HIDDEN);

RAW: RAW_BLOCK;

RW_LINE: RAW_LINE -> type(RAW);

DEF_START: DEF_ST -> pushMode(DEF);

START_IMPORT: IMP -> type(IMPORT_TOKEN), pushMode(IMPORT_MODE);

// Phase 2 (post-2.0) — the literal-@ escape: '@@' re-types to RAW and collapses to a single '@' via the
// raw-substitution path (ParseContext.CreateRawOutputItem special-cases the two-char "@@"). The trailing
// semantic predicate is the comment-adjacency guard: when the character after the pair is '*', the second
// '@' begins a comment ('@*…*@'), so the escape must not fire and the lexer falls back to the one-char
// START_OUT (maximal munch otherwise prefers this two-char rule regardless of order).
AT_ESCAPE: '@@' {InputStream.LA(1) != '*'}? -> type(RAW);

START_OUT: OUT_ST WS* -> type(OUT), pushMode(OUT_MODE);

UNCONNECTED_SUB_ST: SUB_ST -> type(SUB_START);

UNCONNECTED_SUB_CL: SUB_CL -> type(SUB_CLOSE);

TEXT: (~[@]+ | .);

mode SUB_BLOCK;

SUB_COMMENT: COMMENT_BLOCK -> channel(HIDDEN);

SUB_SKIP_WS: EAT_WS -> channel(HIDDEN);

SUB_RAW: RAW_BLOCK -> type(RAW);

SUB_RW_LINE: RAW_LINE -> type(RAW);

SUB_DEF_START: DEF_ST -> type(DEF_START), pushMode(DEF);

SUB_START_IMPORT: IMP -> type(IMPORT_TOKEN), pushMode(IMPORT_MODE);

// Phase 2 (post-2.0) — the SUB_BLOCK mirror of AT_ESCAPE (same comment-adjacency guard).
SUB_AT_ESCAPE: '@@' {InputStream.LA(1) != '*'}? -> type(RAW);

SUB_START_OUT: OUT_ST WS* -> type(OUT), pushMode(OUT_MODE);

SUB_SUB_CLOSE: SUB_CL -> type(SUB_CLOSE), popMode;

SUB_TEXT: (~[@}]+ | .) -> type(TEXT);

mode DEF;

DEF_COMMENT: COMMENT_BLOCK -> channel(HIDDEN);
DEF_STARTNAME: DEF_STNAME;
TYPE_ID: ID_TYPE -> type(ID);
DEF_ENDNAME: DEF_CLNAME;
// NEW (phase 5) — a '(' after the definition name opens the prop list. Reuses the OUT_PARAMSTART
// token type; pushes the dedicated DEF_PROPS mode (it does NOT push CALL — the declaration surface
// has its own vocabulary). '(' is a token-recognition error in DEF mode today, so no existing
// template's token stream can change.
DEF_PROPSTART: PARA_ST -> type(OUT_PARAMSTART), pushMode(DEF_PROPS);
SUB_START: SUB_ST -> pushMode(SUB_BLOCK);
DEF_OUT: DEF_MAKEOUT WS* -> type(DEF_OUT), pushMode(OUT_MODE);
DEF_TYPE: DEF_T;
DELIM: EXT_DELIM;
DEF_CLOSE: DEF_CL -> popMode;
DEF_WS: WS+ -> channel(HIDDEN);

// NEW (phase 5) — the prop-declaration surface. Reachable solely through DEF_PROPSTART, whose trigger
// character '(' is a token error in DEF mode today; therefore no existing template's token stream can
// change (ANTLR only matches rules of the current mode) and the ten existing DEF rules are byte-identical.
mode DEF_PROPS;

DEFP_COMMENT: COMMENT_BLOCK -> channel(HIDDEN);

DEFP_PROPSEND: PARA_CL -> type(OUT_PARAMEND), popMode;

// '::' before ':' for readability; maximal munch already prefers the longer match.
DEFP_SLOT_TYPE: DEF_T -> type(DEF_TYPE);
DEFP_DELIM:    EXT_DELIM -> type(DELIM);
DEFP_ASSIGN:   '=' -> type(ASSIGN);
DEFP_COMMA:    ',' -> type(COMMA);
DEFP_MINUS:    '-' -> type(OP_MINUS);

// Keyword literals for defaults. They carry ID_TYPE's exact trailing pattern (SINGLE_LINE_WS*) so that
// for input "true " both this rule and DEFP_TYPE_ID match five characters — the lengths tie and rule
// order selects the keyword.
DEFP_TRUE:  'true'  SINGLE_LINE_WS* -> type(TRUE);
DEFP_FALSE: 'false' SINGLE_LINE_WS* -> type(FALSE);
DEFP_NULL:  'null'  SINGLE_LINE_WS* -> type(NULL);

// Literals for defaults (fragments from CSharp.g4). REAL before INT is cosmetic — maximal munch already
// prefers '1.5' over '1'. The string rule deliberately mirrors phase 1's CALL_STRING (no UTF8_SUFFIX).
DEFP_REAL:   REAL -> type(REAL_LIT);
DEFP_INT:    INT  -> type(INT_LIT);
DEFP_STRING: '"' REGULAR_STRING_LITERALS? '"' -> type(STRING_LIT);
DEFP_CHAR:   CHAR -> type(CHAR_LIT);

// Prop names AND type names: one rule. ID_TYPE covers plain identifiers, dotted names, generics
// (List<string> is a single token — its '<'/'>' never meet DEF_ENDNAME), and arrays (string[]).
DEFP_TYPE_ID: ID_TYPE -> type(ID);

DEFP_WS: WS+ -> channel(HIDDEN);

mode IMPORT_MODE;

IMPORT_COMMENT: 
	COMMENT_BLOCK -> channel(HIDDEN);
	
IMPORT_SUBSTART:
	SUB_ST -> type(SUB_START);

IMPORT_PATH:
	~[{}]+ -> type(TEXT);

IMPORT_SUBEND:
	SUB_CL -> type(SUB_CLOSE), popMode;

IMPORT_PATH_REST:
	. -> type(TEXT);
	
mode CALL_RETURNED;

CALL_RETURN_COMMENT: 
	COMMENT_BLOCK -> channel(HIDDEN);
	
CALL_RETURN_DELIM: WS* EXT_DELIM -> type(DELIM), mode(OUT_MODE);

CALL_RETURN_START: OUT_ST WS* -> type(OUT), mode(OUT_MODE);

CALL_RETURN_RAW: RAW_BLOCK -> type(RAW), popMode;
CALL_RETURN_RW_LINE: RAW_LINE -> type(RAW), popMode;
CALL_RETURN_DEF_START: DEF_ST -> type(DEF_START), popMode, pushMode(DEF);
CALL_RETURN_SUB_START: WS* SUB_ST -> type(SUB_START), popMode, pushMode(SUB_BLOCK);
CALL_RETURN_SUB_CL: SUB_CL -> type(SUB_CLOSE), popMode, popMode;
CALL_RETURN_START_IMPORT: IMP -> type(IMPORT_TOKEN), popMode, pushMode(IMPORT_MODE);

CALL_SKIP_WS: EAT_WS -> channel(HIDDEN), popMode;

CALL_RETURN_OTHER: . -> type(TEXT), popMode;
	
mode OUT_MODE;

OUT_COMMENT: 
	COMMENT_BLOCK -> channel(HIDDEN);

OUT_WS: WS+ -> type(WS);

OUT_ID: ID_TOKEN WS* -> type(ID);

OUT_OUTPARAMSTART:
	PARA_ST -> type(OUT_PARAMSTART), mode(CALL_RETURNED), pushMode(CALL);

OUT_DELIM: EXT_DELIM -> type(DELIM);

OUT_OUT_START: OUT_ST WS* -> type(OUT);

OUT_RAW: RAW_BLOCK -> type(RAW), popMode;
OUT_RW_LINE: RAW_LINE -> type(RAW), popMode;
OUT_DEF_START: DEF_ST -> type(DEF_START), popMode, pushMode(DEF);
OUT_SUB_START: SUB_ST -> type(SUB_START), popMode, pushMode(SUB_BLOCK);
OUT_SUB_CL: SUB_CL -> type(SUB_CLOSE), popMode, popMode;
OUT_START_IMPORT: IMP -> type(IMPORT_TOKEN), popMode, pushMode(IMPORT_MODE);

OUT_SKIP_WS: EAT_WS -> channel(HIDDEN), popMode;

OUT_OTHER: . -> type(TEXT), popMode;

mode CALL;

CALL_COMMENT:
	COMMENT_BLOCK -> channel(HIDDEN);

CSHARP_START:
	OUT_ST -> popMode, pushMode(CS);

CALL_PARAMSTART:
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

CALL_PARAMEND:
	PARA_CL -> type(OUT_PARAMEND), popMode;

CALL_DELIM:
	EXT_DELIM -> type(DELIM);

CALL_ROOT_REF: DEF_T -> type(ROOT_REF);

// NEW — keyword literals. They carry the same trailing WS* as CALL_ID so that maximal
// munch cannot prefer CALL_ID for 'true ' (5 chars incl. the space) over a bare 'true'
// (4 chars); with equal lengths the rule-order tie-break applies and these win.
CALL_TRUE:  'true'  WS* -> type(TRUE);
CALL_FALSE: 'false' WS* -> type(FALSE);
CALL_NULL:  'null'  WS* -> type(NULL);

// NEW (phase 5) — the 'this' keyword. Same trailing-WS* technique as CALL_TRUE et al.: CALL_ID is
// ID_TOKEN WS*, so a bare 'this' rule would lose maximal munch for input "this " (5 chars via CALL_ID
// vs 4). Equal lengths make rule order decide, so this precedes CALL_ID.
CALL_THIS: 'this' WS* -> type(THIS);

// NEW — numeric/string/char literals (fragments from CSharp.g4). REAL before INT is
// cosmetic (maximal munch already prefers the longer match, e.g. '1.5' over '1').
CALL_REAL:   REAL -> type(REAL_LIT);
CALL_INT:    INT  -> type(INT_LIT);
CALL_STRING: '"' REGULAR_STRING_LITERALS? '"' -> type(STRING_LIT);
CALL_CHAR:   CHAR -> type(CHAR_LIT);

CALL_ID: ID_TOKEN WS* -> type(ID);

CALL_MEMB_P: MEMB_P -> type(MEMBER_P);

// NEW — operators. Multi-character rules listed before their single-character prefixes
// for readability; maximal munch guarantees the longer match regardless of order.
CALL_OP_QQ:       '??' -> type(OP_QQ);
CALL_OP_QUESTION: '?'  -> type(OP_QUESTION);
CALL_OP_AND:      '&&' -> type(OP_AND);
CALL_OP_OR:       '||' -> type(OP_OR);
CALL_OP_EQ:       '==' -> type(OP_EQ);
CALL_OP_NEQ:      '!=' -> type(OP_NEQ);
CALL_OP_LSHIFT:   '<<' -> type(OP_LSHIFT);
CALL_OP_RSHIFT:   '>>' -> type(OP_RSHIFT);
CALL_OP_LE:       '<=' -> type(OP_LE);
CALL_OP_GE:       '>=' -> type(OP_GE);
CALL_OP_LT:       '<'  -> type(OP_LT);
CALL_OP_GT:       '>'  -> type(OP_GT);
CALL_OP_PLUS:     '+'  -> type(OP_PLUS);
CALL_OP_MINUS:    '-'  -> type(OP_MINUS);
CALL_OP_STAR:     '*'  -> type(OP_STAR);
CALL_OP_SLASH:    '/'  -> type(OP_SLASH);
CALL_OP_PERCENT:  '%'  -> type(OP_PERCENT);
CALL_OP_AMP:      '&'  -> type(OP_AMP);
CALL_OP_PIPE:     '|'  -> type(OP_PIPE);
CALL_OP_CARET:    '^'  -> type(OP_CARET);
CALL_OP_NOT:      '!'  -> type(OP_NOT);
CALL_OP_TILDE:    '~'  -> type(OP_TILDE);
CALL_LBRACKET:    '['  -> type(LBRACKET);
CALL_RBRACKET:    ']'  -> type(RBRACKET);
CALL_COMMA:       ','  -> type(COMMA);

CALL_WS: WS+ -> channel(HIDDEN);

// NEW — any character not matched above inside a call (e.g. '=' or '$'). Without this an unrecognized
// character is silently skipped by the lexer, which can leave a deceptively valid token stream; typing it
// as its own token forces a positioned parser syntax error (HED0003) instead. Scoped to CALL mode only.
CALL_UNKNOWN: . ;

// Top level of a C# expression. The ONLY ')' that closes the call lives here, so it is the only
// one typed OUT_PARAMEND. A nested '(' opens CS_NESTED, whose ')' is an ordinary CSHARP_TOKEN.
// This keeps the call terminator unambiguous: csharp_expression never contains an OUT_PARAMEND,
// so a trailing token after ')' is no longer needed to end the expression.
mode CS;

CS_CSHARP_WS: WS+ -> type(CSHARP_TOKEN);

CS_CSHARP_START:
	PARA_ST -> type(CSHARP_TOKEN), pushMode(CS_NESTED);

CS_CSHARP_END: PARA_CL -> type(OUT_PARAMEND), popMode;

// Raw string literals ("""..."""), incl. the $-prefixed interpolated forms - see RAW_STRING_BODY in
// CSharp.g4. The non-greedy body must live in a top-level rule (it does not compose when the rule also
// alternates with the other string rules under maximal munch, as inside the TOKEN fragment).
CS_RAW_STRING: RAW_STRING_BODY -> type(CSHARP_TOKEN);

// Interpolated string starts. Verbatim variants ($@" / @$") must be tried before the
// regular ($") and before TOKEN so the '@' isn't mistaken for a verbatim string ('@"').
CS_INTERP_VERBATIM_1: '$@"' -> type(CSHARP_TOKEN), pushMode(INTERP_VERBATIM_STR);
CS_INTERP_VERBATIM_2: '@$"' -> type(CSHARP_TOKEN), pushMode(INTERP_VERBATIM_STR);
CS_INTERP_REGULAR:    '$"'  -> type(CSHARP_TOKEN), pushMode(INTERP_STR);

CS_CSHARP_TOKEN: TOKEN -> type(CSHARP_TOKEN);

// Parenthesised sub-expression inside a C# expression. Identical to CS except the closing ')'
// is a plain CSHARP_TOKEN (it balances an inner '(' rather than terminating the call).
mode CS_NESTED;

CSN_CSHARP_WS: WS+ -> type(CSHARP_TOKEN);

CSN_CSHARP_START:
	PARA_ST -> type(CSHARP_TOKEN), pushMode(CS_NESTED);

CSN_CSHARP_END: PARA_CL -> type(CSHARP_TOKEN), popMode;

CSN_RAW_STRING: RAW_STRING_BODY -> type(CSHARP_TOKEN);

CSN_INTERP_VERBATIM_1: '$@"' -> type(CSHARP_TOKEN), pushMode(INTERP_VERBATIM_STR);
CSN_INTERP_VERBATIM_2: '@$"' -> type(CSHARP_TOKEN), pushMode(INTERP_VERBATIM_STR);
CSN_INTERP_REGULAR:    '$"'  -> type(CSHARP_TOKEN), pushMode(INTERP_STR);

CSN_CSHARP_TOKEN: TOKEN -> type(CSHARP_TOKEN);

// Regular interpolated string body: $"...{ hole }...". Everything is emitted as a CSHARP_TOKEN
// so the expression text round-trips verbatim to Roslyn; the modes only exist to balance the
// '{'/'}' holes (so a '"' or '(' ')' inside a hole can't terminate the string early).
// Character classes mirror the C# spec (§12.8.3): a bare '}' is not a string character - it is only
// legal as the Close_Brace_Escape_Sequence '}}'. A single '}' closing a hole is consumed in
// INTERP_HOLE, so any '}' reaching this mode must be part of '}}'.
mode INTERP_STR;

ISTR_OPEN_BRACE_ESC:  '{{' -> type(CSHARP_TOKEN);   // Open_Brace_Escape_Sequence
ISTR_CLOSE_BRACE_ESC: '}}' -> type(CSHARP_TOKEN);   // Close_Brace_Escape_Sequence
ISTR_HOLE_OPEN:       '{'  -> type(CSHARP_TOKEN), pushMode(INTERP_HOLE);
ISTR_END:             '"'  -> type(CSHARP_TOKEN), popMode;
ISTR_ESCAPE:          '\\' . -> type(CSHARP_TOKEN);
ISTR_CHAR:            ~["\\{}] -> type(CSHARP_TOKEN);   // Interpolated_Regular_String_Character

// Verbatim interpolated string body: $@"..." / @$"...". No backslash escapes; '""' escapes a quote.
mode INTERP_VERBATIM_STR;

IVSTR_OPEN_BRACE_ESC:  '{{' -> type(CSHARP_TOKEN);   // Open_Brace_Escape_Sequence
IVSTR_CLOSE_BRACE_ESC: '}}' -> type(CSHARP_TOKEN);   // Close_Brace_Escape_Sequence
IVSTR_HOLE_OPEN:       '{'  -> type(CSHARP_TOKEN), pushMode(INTERP_HOLE);
IVSTR_QUOTE_ESC:       '""' -> type(CSHARP_TOKEN);   // Quote_Escape_Sequence
IVSTR_END:             '"'  -> type(CSHARP_TOKEN), popMode;
IVSTR_CHAR:            ~["{}] -> type(CSHARP_TOKEN);   // Interpolated_Verbatim_String_Character

// Interpolation hole: the C# expression between '{' and '}'. Braces are balanced here; parens and
// nested (interpolated) strings are consumed wholesale by TOKEN so their contents can't leak out.
mode INTERP_HOLE;

HOLE_WS:                WS+   -> type(CSHARP_TOKEN);
HOLE_OPEN:              '{'   -> type(CSHARP_TOKEN), pushMode(INTERP_HOLE);
HOLE_CLOSE:             '}'   -> type(CSHARP_TOKEN), popMode;
HOLE_RAW_STRING: RAW_STRING_BODY -> type(CSHARP_TOKEN);
HOLE_INTERP_VERBATIM_1: '$@"' -> type(CSHARP_TOKEN), pushMode(INTERP_VERBATIM_STR);
HOLE_INTERP_VERBATIM_2: '@$"' -> type(CSHARP_TOKEN), pushMode(INTERP_VERBATIM_STR);
HOLE_INTERP_REGULAR:    '$"'  -> type(CSHARP_TOKEN), pushMode(INTERP_STR);
HOLE_TOKEN:             TOKEN -> type(CSHARP_TOKEN);