/*
 * Lexer Rules
 */

lexer grammar TtlLexer;

import CSharp;

tokens { TEXT, TEXT_WS, IMPORT_TOKEN, ID, ROOT_REF, MEMBER_P, OUT, SUB_START, SUB_CLOSE, CSHARP_END, CSHARP_TOKEN, CSHARP_START, DEF_STARTNAME, DEF_ENDNAME, DELIM, DEF_START, DEF_CLOSE, RAW, OUT_PARAMSTART, OUT_PARAMEND, DEF_OUT }

fragment ID_TOKEN: IDENTIFIER;
fragment ID_TYPE: IDENTIFIER ('.' IDENTIFIER)* SINGLE_LINE_WS* ('<' SINGLE_LINE_WS* ID_TYPE SINGLE_LINE_WS* (',' SINGLE_LINE_WS* ID_TYPE)* SINGLE_LINE_WS* '>')?;

fragment WS: WHITESPACE;

fragment IMP: '@<<';
fragment MEMB_P: '.';
fragment SUB_ST: '{{';
fragment SUB_CL: '}}';
fragment PARA_ST: '(';
fragment PARA_CL: ')';
fragment DEF_ST: '<%';
fragment DEF_CL: '%>';
fragment OUT_ST: '@';
fragment DEF_T: '::';
fragment EXT_DELIM: ':';
fragment DEF_STNAME: '<';
fragment DEF_CLNAME: '>';
fragment DEF_MAKEOUT: '->';
fragment COMMENT_BLOCK: '@*' .*? '*@';
fragment RAW_BLOCK : '@{' .*? '}@';
fragment RAW_LINE : '@:' .*? ('\r?\n' | EOF);
fragment EAT_WS: '@\\' WS*;

COMMENT: COMMENT_BLOCK -> channel(HIDDEN);

SKIP_WS: EAT_WS -> channel(HIDDEN);

RAW: RAW_BLOCK;

RW_LINE: RAW_LINE -> type(RAW);

DEF_START: DEF_ST;
DEF_STARTNAME: DEF_STNAME;
DEF_CLOSE: DEF_CL;
DEF_ENDNAME: DEF_CLNAME;
TYPE_ID: ID_TYPE -> type(ID);

START_IMPORT: IMP -> type(IMPORT_TOKEN), pushMode(IMPORT_MODE);

START_OUT: OUT_ST -> type(OUT), pushMode(OUT_MODE);
DEF_OUT: DEF_MAKEOUT -> type(DEF_OUT), pushMode(OUT_MODE);

SUB_START: SUB_ST;

SUB_CLOSE: SUB_CL;

DEF_TYPE: DEF_T;
DELIM: EXT_DELIM;

TEXT_WS: WS+;

TEXT: .;

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

CALL_RETURN_START: OUT_ST -> type(OUT), mode(OUT_MODE);

CALL_RETURN_RAW: RAW_BLOCK -> type(RAW), popMode;
CALL_RETURN_RW_LINE: RAW_LINE -> type(RAW), popMode;
CALL_RETURN_DEF_START: DEF_ST -> type(DEF_START), popMode;
CALL_RETURN_SUB_START: WS* SUB_ST -> type(SUB_START), popMode;
CALL_RETURN_SUB_CL: SUB_CL -> type(SUB_CLOSE), popMode;
CALL_RETURN_START_IMPORT: IMP -> type(IMPORT_TOKEN), popMode, pushMode(IMPORT_MODE);

CALL_SKIP_WS: EAT_WS -> channel(HIDDEN);

CALL_RETURN_OTHER: . -> type(TEXT), popMode;
	
mode OUT_MODE;

OUT_COMMENT: 
	COMMENT_BLOCK -> channel(HIDDEN);

OUT_WS: WS+ -> type(TEXT_WS);

OUT_ID: ID_TOKEN -> type(ID);

OUT_OUTPARAMSTART:
	PARA_ST -> type(OUT_PARAMSTART), mode(CALL_RETURNED), pushMode(CALL);

OUT_DELIM: EXT_DELIM -> type(DELIM);

OUT_OUT_START: OUT_ST -> type(OUT);

OUT_RAW: RAW_BLOCK -> type(RAW), popMode;
OUT_RW_LINE: RAW_LINE -> type(RAW), popMode;
OUT_DEF_START: DEF_ST -> type(DEF_START), popMode;
OUT_SUB_START: SUB_ST -> type(SUB_START), popMode;
OUT_SUB_CL: SUB_CL -> type(SUB_CLOSE), popMode;
OUT_START_IMPORT: IMP -> type(IMPORT_TOKEN), popMode, pushMode(IMPORT_MODE);

OUT_SKIP_WS: EAT_WS -> channel(HIDDEN);

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

CALL_ID: ID_TOKEN -> type(ID);

CALL_MEMB_P: MEMB_P -> type(MEMBER_P);

CALL_WS: WS+ -> channel(HIDDEN);

mode CS;

CS_CSHARP_WS: WS+ -> type(CSHARP_TOKEN);

CS_CSHARP_START:
	PARA_ST -> type(CSHARP_TOKEN), pushMode(CS);

CS_CSHARP_END: PARA_CL -> type(CSHARP_TOKEN), popMode;

CS_CSHARP_TOKEN: TOKEN -> type(CSHARP_TOKEN);