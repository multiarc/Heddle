/*
 * Lexer Rules
 */

lexer grammar TtlLexer;

tokens { TEXT, OUT_ID }

fragment ID_START: 
	LETTER
	| '_'
	;

fragment LETTER: 
	'a'..'z'
	| 'A'..'Z'
	;

fragment ID_PART: 
	ID_START
	| '.'
	| '0'..'9'
	| '+'
	;

fragment SUB_ST: '[';
fragment SUB_CL: ']';
fragment PARA_ST: '(';
fragment PARA_CL: ')';
fragment DEF_ST: '<%';
fragment DEF_CL: '%>';
fragment OUT_ST: '@';

COMMENT: 
	'@*' .*? '*@' -> channel(COMMENT);

RAW: 
	'@:' .*? ':@';

DEF_START: 
	DEF_ST -> pushMode(DEF);

OUT_START: 
	OUT_ST -> pushMode(OUT);

TEXT: .+?;

mode DEF;


DEF_COMMENT: 
	COMMENT -> channel(COMMENT);

DEF_WS: 
	[ \r\n\t\f]+ -> channel(HIDDEN);

DEF_CLOSE: 
	DEF_CL -> popMode;

SUB_START: 
	SUB_ST -> pushMode(SUB);

DEF_STARTNAME: '<';
DEF_ENDNAME: '>';
DEF_TYPE: '::';
DELIM: ':';
ID: ID_START ID_PART*;



mode SUB;



SUB_COMMENT: 
	COMMENT -> channel(COMMENT);

SUB_CLOSE: 
	SUB_CL -> popMode;

SUB_RAW: 
	RAW -> type(RAW);

SUB_DEFSTART: 
	DEF_START -> type(DEF_START), pushMode(DEF);

SUB_OUTSTART: 
	OUT_START -> type(OUT_START), pushMode(OUT_SUB);

SUB_TEXT: 
	TEXT -> type(TEXT);



mode OUT;


OUT_COMMENT: 
	COMMENT -> channel(COMMENT);

OUT_WS: 
	DEF_WS -> channel(HIDDEN);

OUT_PARAMSTART: 
	PARA_ST -> pushMode(CALL);

OUT_DELIM: 
	DELIM -> type(DELIM);

OUT_SUBSTART: 
	SUB_START -> type(SUB_START), popMode, pushMode(SUB);

OUT_ID: ID;

OUT_RAW: RAW -> type(RAW), popMode;
OUT_DEF_START: DEF_START -> type(DEF_START), popMode, pushMode(DEF);
OUT_OUT_START: OUT_START -> type(OUT_START), popMode, pushMode(OUT);
OUT_SUB_CLOSE: SUB_CLOSE -> type(TEXT), popMode;
OUT_OTHER: . -> type(TEXT), popMode;



mode OUT_SUB;


OUT_SUB_COMMENT: 
	COMMENT -> channel(COMMENT);

OUT_SUB_WS: 
	DEF_WS -> channel(HIDDEN);

OUT_SUB_PARAMSTART: 
	OUT_PARAMSTART -> type(OUT_PARAMSTART), pushMode(CALL);

OUT_SUB_DELIM: 
	DELIM -> type(DELIM);

OUT_SUB_SUBSTART: 
	SUB_START -> type(SUB_START), popMode, pushMode(SUB);

OUT_SUB_OUT_ID: OUT_ID -> type(OUT_ID);

OUT_SUB_RAW: RAW -> type(RAW), popMode;
OUT_SUB_DEF_START: DEF_START -> type(DEF_START), popMode, pushMode(DEF);
OUT_SUB_OUT_START: OUT_START -> type(OUT_START), popMode, pushMode(OUT);
OUT_SUB_SUB_CLOSE: SUB_CLOSE -> type(SUB_CLOSE), popMode, popMode;
OUT_SUB_OTHER: . -> type(TEXT), popMode;


mode CALL;


CALL_OUT_WS: 
	DEF_WS -> channel(HIDDEN);

OUT_PARAMEND: 
	PARA_CL -> popMode;

CALL_OUT_PARAMSTART: 
	OUT_PARAMSTART -> type(OUT_PARAMSTART), pushMode(CALL);

CALL_OUT_DELIM: 
	DELIM -> type(DELIM);

CALL_OUT_ID: OUT_ID -> type(OUT_ID);