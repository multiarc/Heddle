/*
 * Lexer Rules
 */

lexer grammar TtlLexer;

import CSharp;

tokens { TEXT, IMPORT_TOKEN, ID, ROOT_REF, MEMBER_P, OUT, SUB_START, SUB_CLOSE, CSHARP_END, CSHARP_TOKEN, CSHARP_START, DEF_STARTNAME, DEF_ENDNAME, DEF_TYPE, DELIM, DEF_START, DEF_CLOSE, COMMENT, RAW, OUT_PARAMSTART, OUT_PARAMEND, LINE_TERMINATE, DEF_OUTPUTONEND }

channels { COMMENT_CHANNEL }

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
fragment LINE_TERM: ';';
fragment COMMENT_BLOCK: '@*' .*? '*@';
fragment RAW_BLOCK : '@{' .*? '}@';

NON_SPEC_TEXT: ~[@*{<%]+ -> type(TEXT);

START_COMMENT:
	COMMENT_BLOCK -> channel(COMMENT_CHANNEL);

START_RAW:
	RAW_BLOCK -> type(RAW);

START_DEF_START:
	DEF_ST -> type(DEF_START), pushMode(DEF);

START_IMPORT:
	IMP -> type(IMPORT_TOKEN), pushMode(IMPORT_MODE);

START_OUT:
	OUT_ST -> type(OUT), pushMode(OUT_MODE);

OTHER_TEXT: . -> type(TEXT);

mode DEF;


DEF_COMMENT: 
	COMMENT_BLOCK -> skip;

DEF_WS: WS+ -> skip;

DEF_DEFCLOSE:
	DEF_CL -> type(DEF_CLOSE), popMode;

DEF_DEFSTARTNAME: DEF_STNAME -> type(DEF_STARTNAME);
DEF_DEFENDNAME: DEF_CLNAME -> type(DEF_ENDNAME);

DEF_DEFOUTPUTONEND:
	DEF_MAKEOUT -> type(DEF_OUTPUTONEND), pushMode(DEF_OUT);

DEF_SUBSTART:
	SUB_ST -> type(SUB_START), pushMode(SUB);

DEF_DEFTYPE: DEF_T -> type(DEF_TYPE);
DEF_DELIM: EXT_DELIM -> type(DELIM);
DEF_ID: ID_TOKEN -> type(ID);
DEF_TYPE_ID: ID_TYPE -> type(ID);

mode DEF_OUT;


DEF_OUT_COMMENT: 
	COMMENT_BLOCK -> skip;

DEF_OUT_WS: WS+ -> skip;

DEF_OUT_OUTPARAMSTART:
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

DEF_OUT_DELIM: 
	EXT_DELIM -> type(DELIM);

DEF_OUT_SUBSTART: 
	SUB_ST -> type(SUB_START), popMode, pushMode(SUB);

DEF_OUT_ID: ID_TOKEN -> type(ID);

mode SUB;

SUB_TEXT: ~[@*{<%}]+ -> type(TEXT);

SUB_COMMENT:
	COMMENT_BLOCK -> type(COMMENT);

SUB_LINE_TERMINATE: 
	SUB_CL LINE_TERM WS* -> type(SUB_CLOSE), popMode;

SUB_CLOSE: 
	SUB_CL -> popMode;

SUB_RAW: 
	RAW_BLOCK -> type(RAW);

SUB_DEFSTART: 
	DEF_ST -> type(DEF_START), pushMode(DEF);

SUB_START_IMPORT:
	IMP -> type(IMPORT_TOKEN), pushMode(IMPORT_MODE);

SUB_OUTSTART: 
	OUT_ST -> type(OUT), pushMode(OUT_SUB);

SUB_OTHER_TEXT: . -> type(TEXT);

mode IMPORT_MODE;

IMPORT_COMMENT: 
	COMMENT_BLOCK -> type(COMMENT);

IMPORT_SUBSTART:
	SUB_ST -> type(SUB_START);

IMPORT_PATH:
	~[{}]+ -> type(TEXT);

IMPORT_SUBEND:
	SUB_CL -> type(SUB_CLOSE), popMode;

IMPORT_WS: WS+;

IMPORT_PATH_REST:
	. -> type(TEXT);

mode OUT_MODE;


OUT_COMMENT: 
	COMMENT_BLOCK -> type(COMMENT);

OUT_OUTPARAMSTART:
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

OUT_DELIM: 
	EXT_DELIM -> type(DELIM);

OUT_SUBSTART: 
	SUB_ST -> type(SUB_START), popMode, pushMode(SUB);

OUT_ID: ID_TOKEN -> type(ID);

OUT_LINE_TERMINATE:
	LINE_TERM WS* -> type(LINE_TERMINATE), popMode;

OUT_WS: WS+;

OUT_RAW: RAW_BLOCK -> type(RAW), popMode;
OUT_DEF_START: DEF_ST -> type(DEF_START), popMode, pushMode(DEF);
OUT_OUT_START: OUT_ST -> type(OUT), popMode, pushMode(OUT_MODE);
OUT_SUBCLOSE_TERMINATED: SUB_CL LINE_TERM WS* -> type(TEXT), popMode;
OUT_SUBCLOSE: SUB_CL -> type(TEXT), popMode;
OUT_OTHER: . -> type(TEXT), popMode;


mode OUT_SUB;


OUT_SUBCOMMENT: COMMENT_BLOCK -> type(COMMENT);

OUT_SUBPARAMSTART:
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

OUT_SUBDELIM:
	EXT_DELIM -> type(DELIM);

OUT_SUBSUBSTART:
	SUB_ST -> type(SUB_START), popMode, pushMode(SUB);

OUT_SUBOUTID: OUT_ID -> type(ID);

OUT_SUBLINE_TERMINATE:
	LINE_TERM WS* -> type(LINE_TERMINATE), popMode;

OUT_SUBWS: WS+ -> type(OUT_WS);

OUT_SUBRAW: RAW_BLOCK -> type(RAW), popMode;
OUT_SUBDEFSTART: DEF_ST -> type(DEF_START), popMode, pushMode(DEF);
OUT_SUBOUTSTART: OUT_ST -> type(OUT), popMode, pushMode(OUT_SUB);
OUT_SUBSUBCLOSE_TERMINATED: SUB_CL LINE_TERM WS* -> type(SUB_CLOSE), popMode, popMode;
OUT_SUBSUBCLOSE: SUB_CL -> type(SUB_CLOSE), popMode, popMode;
OUT_SUBOTHER: . -> type(TEXT), popMode;


mode CALL;

CALL_COMMENT:
	COMMENT_BLOCK -> skip;

CSHARP_START:
	OUT_ST -> pushMode(CS);

CALL_OUT_PARAMEND: 
	PARA_CL -> type(OUT_PARAMEND), popMode;

CALL_OUT_PARAMSTART: 
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

CALL_OUT_DELIM: 
	EXT_DELIM -> type(DELIM);

CALL_ROOT_REF: DEF_T -> type(ROOT_REF);

CALL_OUT_ID: ID_TOKEN -> type(ID);

CALL_MEMB_P: MEMB_P -> type(MEMBER_P);

CALL_LINE_TERMINATE: 
	LINE_TERM WS* -> type(LINE_TERMINATE), popMode, popMode;

CALL_OUT_WS: WS+ -> skip;

mode CS;

CS_CSHARP_WS: WS+ -> type(CSHARP_TOKEN);

CS_CSHARP_START:
	PARA_ST -> type(CSHARP_TOKEN), pushMode(CS);

CS_CSHARP_END: PARA_CL -> type(CSHARP_END), popMode;

CS_CSHARP_TOKEN: TOKEN -> type(CSHARP_TOKEN);