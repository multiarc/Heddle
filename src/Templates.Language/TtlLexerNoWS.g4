/*
 * Lexer Rules
 */

lexer grammar TtlLexerNoWS;

import CSharp;

tokens { TEXT, TEXT_WS, IMPORT_TOKEN, ID, ROOT_REF, MEMBER_P, OUT, SUB_START, SUB_CLOSE, CSHARP_END, CSHARP_TOKEN, CSHARP_START, DEF_STARTNAME, DEF_ENDNAME, DELIM, DEF_START, DEF_CLOSE, COMMENT, RAW, OUT_PARAMSTART, OUT_PARAMEND, LINE_TERMINATE, DEF_OUT }

channels { COMMENT_CHANNEL }

fragment ID_TOKEN: IDENTIFIER;
fragment ID_TYPE: IDENTIFIER ('.' IDENTIFIER)* SINGLE_LINE_WS* ('<' SINGLE_LINE_WS* ID_TYPE SINGLE_LINE_WS* (',' SINGLE_LINE_WS* ID_TYPE)* SINGLE_LINE_WS* '>')?;

fragment WS: WHITESPACE;

fragment IMP: '@<<';
fragment MEMB_P: '.';
fragment SUB_ST: WS* '{{';
fragment SUB_CL: '}}' WS*;
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
fragment LINE_TERM: ';';
fragment COMMENT_BLOCK: '@*' .*? '*@';
fragment RAW_BLOCK : '@{' .*? '}@';

COMMENT: COMMENT_BLOCK -> channel(COMMENT_CHANNEL);

RAW: RAW_BLOCK;

DEF_START: DEF_ST;
DEF_STARTNAME: DEF_STNAME;
DEF_CLOSE: DEF_CL;
DEF_ENDNAME: DEF_CLNAME;
ID: ID_TOKEN;
TYPE_ID: ID_TYPE -> type(ID);

START_IMPORT: IMP -> type(IMPORT_TOKEN), pushMode(IMPORT_MODE);

START_OUT: OUT_ST -> type(OUT), pushMode(PRE_OUT_MODE);
DEF_OUT: DEF_MAKEOUT -> type(DEF_OUT), pushMode(PRE_OUT_MODE);

SUB_START: SUB_ST;
SUB_CLOSE: SUB_CL LINE_TERM? WS*;

DEF_TYPE: DEF_T;
DELIM: EXT_DELIM;

TEXT_WS: WS+;

TEXT: .;

mode PRE_OUT_MODE;

PRE_OUT_WS: WS+ -> skip;

PRE_OUT_ID: ID_TOKEN -> type(ID), pushMode(OUT_MODE);

PRE_OUT_OUTPARAMSTART:
	PARA_ST -> type(OUT_PARAMSTART), pushMode(OUT_MODE), pushMode(CALL);
	
PRE_OUT_OTHER: . -> channel(HIDDEN), popMode;

mode IMPORT_MODE;

IMPORT_COMMENT: 
	COMMENT_BLOCK -> type(COMMENT);

IMPORT_SUBSTART:
	SUB_ST -> type(SUB_START);

IMPORT_PATH:
	~[{}]+ -> channel(HIDDEN);

IMPORT_SUBEND:
	SUB_CL -> type(SUB_CLOSE), popMode;

IMPORT_WS: WS+ -> skip;

IMPORT_PATH_REST:
	. -> channel(HIDDEN);
	
mode OUT_MODE;

OUT_COMMENT: 
	COMMENT_BLOCK -> type(COMMENT);

OUT_OUTPARAMSTART:
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

OUT_LINE_TERMINATE:
	LINE_TERM WS* -> type(LINE_TERMINATE), popMode, popMode;

OUT_DELIM: WS* EXT_DELIM WS* -> type(DELIM), popMode;
OUT_RAW: WS* RAW_BLOCK -> type(RAW), popMode, popMode;
OUT_DEF_START: WS* DEF_ST -> type(DEF_START), popMode, popMode;
OUT_OUT_START: WS* OUT_ST -> type(OUT), popMode;
OUT_SUB_START: WS* SUB_ST -> type(SUB_START), popMode, popMode;
OUT_OTHER: . -> channel(HIDDEN), popMode, popMode;

mode CALL;

CALL_COMMENT:
	COMMENT_BLOCK -> skip;

CSHARP_START:
	OUT_ST -> pushMode(CS);

CALL_PARAMEND: 
	PARA_CL -> type(OUT_PARAMEND), popMode;

CALL_PARAMSTART: 
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

CALL_DELIM: 
	EXT_DELIM -> type(DELIM);

CALL_ROOT_REF: DEF_T -> type(ROOT_REF);

CALL_ID: ID_TOKEN -> type(ID);

CALL_MEMB_P: MEMB_P -> type(MEMBER_P);

CALL_LINE_TERMINATE: 
	LINE_TERM WS* -> type(LINE_TERMINATE), popMode, popMode;

CALL_WS: WS+ -> skip;

mode CS;

CS_CSHARP_WS: WS+ -> type(CSHARP_TOKEN);

CS_CSHARP_START:
	PARA_ST -> type(CSHARP_TOKEN), pushMode(CS);

CS_CSHARP_END: PARA_CL -> type(CSHARP_END), popMode;

CS_CSHARP_TOKEN: TOKEN -> type(CSHARP_TOKEN);