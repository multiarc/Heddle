/*
 * Lexer Rules
 */

lexer grammar TtlLexer;

import CSharp;

tokens { TEXT, WS, IMPORT_TOKEN, ID, ROOT_REF, MEMBER_P, OUT, SUB_START, SUB_CLOSE, CSHARP_END, CSHARP_TOKEN, CSHARP_START, DEF_STARTNAME, DEF_ENDNAME, DELIM, DEF_START, DEF_CLOSE, RAW, OUT_PARAMSTART, OUT_PARAMEND, DEF_OUT }

fragment ID_TOKEN: IDENTIFIER;
fragment ID_TYPE: IDENTIFIER ('.' IDENTIFIER)* SINGLE_LINE_WS* ('<' SINGLE_LINE_WS* ID_TYPE SINGLE_LINE_WS* (',' SINGLE_LINE_WS* ID_TYPE)* SINGLE_LINE_WS* '>')? '[]'*;

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
fragment EAT_WS: WS_START WHITESPACE*;

COMMENT: COMMENT_BLOCK -> channel(HIDDEN);

SKIP_WS: EAT_WS -> channel(HIDDEN);

RAW: RAW_BLOCK;

RW_LINE: RAW_LINE -> type(RAW);

DEF_START: DEF_ST -> pushMode(DEF);

START_IMPORT: IMP -> type(IMPORT_TOKEN);

START_OUT: OUT_ST WS* -> type(OUT), mode(CALL);

OUT_ID: ID_TOKEN WS* -> type(ID);

SUB_START: SUB_ST -> pushMode(DEFAULT_MODE);

SUB_CLOSE: SUB_CL -> popMode;

DELIM: EXT_DELIM;

WS: WHITESPACE+;

TEXT: (~[@:{}()\\%*]+ | .);

mode DEF;

DEF_COMMENT:
	COMMENT_BLOCK -> channel(HIDDEN);

DEF_STARTNAME: DEF_STNAME;

DEF_ENDNAME: DEF_CLNAME;

DEF_ID: ID_TOKEN -> type(ID);

DEF_DELIM: EXT_DELIM -> type(DELIM);

DEF_SUB_START: SUB_ST -> type(SUB_START), pushMode(DEFAULT_MODE);

DEF_OUT: DEF_MAKEOUT;

DEF_PARAMSTART: 
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

DEF_CLOSE: DEF_CL -> popMode;

DEF_TYPE: DEF_T -> mode(CS_TYPE);

DEF_WS: WHITESPACE+ -> channel(HIDDEN);

DEF_TEXT_RETURN: . -> type(TEXT), popMode; //fallback for invalid input

mode CALL;

CALL_COMMENT:
	COMMENT_BLOCK -> channel(HIDDEN);
	
CALL_SKIP_WS: EAT_WS -> channel(HIDDEN);

CALL_RAW: RAW_BLOCK;

CALL_RW_LINE: RAW_LINE -> type(RAW);

CALL_DEF_START: DEF_ST -> pushMode(DEF);

CALL_START_IMPORT: IMP -> type(IMPORT_TOKEN);

CSHARP_START:
	OUT_ST -> mode(CS);
	
CALL_PARAMSTART: 
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

CALL_PARAMEND: 
	PARA_CL -> type(OUT_PARAMEND), popMode;

CALL_DELIM: 
	EXT_DELIM -> type(DELIM);

CALL_ROOT_REF: DEF_T -> type(ROOT_REF);

CALL_ID: ID_TOKEN -> type(ID);

CALL_MEMB_P: MEMB_P -> type(MEMBER_P);

CALL_SUB_START: SUB_ST -> type(SUB_START), pushMode(DEFAULT_MODE);

CALL_SUB_CLOSE: SUB_CL -> type(SUB_CLOSE), popMode;

CALL_WS: WHITESPACE+ -> type(WS);

CALL_TEXT_RETURN: . -> type(TEXT), popMode; //fallback for invalid input

mode CS_TYPE;

TYPE_COMMENT:
	COMMENT_BLOCK -> channel(HIDDEN);
	
TYPE_WS: WHITESPACE+ -> channel(HIDDEN);

TYPE_ID: ID_TYPE -> type(ID), mode(DEF);

TYPE_TEXT_RETURN: . -> type(TEXT), popMode; //fallback for invalid input

mode CS;

CS_CSHARP_WS: WHITESPACE+ -> type(CSHARP_TOKEN);

CS_CSHARP_START:
	PARA_ST -> type(CSHARP_TOKEN), pushMode(CS);

CS_CSHARP_END: PARA_CL -> type(OUT_PARAMEND), popMode;

CS_CSHARP_TOKEN: TOKEN -> type(CSHARP_TOKEN);

CS_TEXT_RETURN: . -> type(TEXT), popMode; //fallback for invalid input