/*
 * Lexer Rules
 */

lexer grammar TtlLexer;

tokens { TEXT, OUT_ID }

channels { COMMENT_CHANNEL }

fragment NEW_LINE:
	[\r\n] | '\u0085' | '\u2028' | '\u2029';

fragment IDENTIFIER: '@'? IDENTIFIER_START IDENTIFIER_PART*;

fragment IDENTIFIER_START: 
	{IsLetter(InputStream.La(2))}? .
	| UNICODE_ESCAPE
	| '_'
	;

fragment IDENTIFIER_PART:
	{IsIdPart(InputStream.La(2))}? .
	| UNICODE_ESCAPE
	| '_'
	;

fragment UNICODE_ESCAPE:
	'\\u' HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT
	| '\\U' HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT
	;

fragment HEX_DIGIT:
	'0'..'9'
	| 'a'..'f'
	| 'A'..'F'
	;

fragment ID: IDENTIFIER;

fragment SUB_ST: '[';
fragment SUB_CL: ']';
fragment PARA_ST: '(';
fragment PARA_CL: ')';
fragment DEF_ST: '<%';
fragment DEF_CL: '%>';
fragment OUT_ST: '@';
fragment LINE_TERM: ';';
fragment WS: NEW_LINE
	| [ \t\f] 
	| '\u000B'
	| '\u00A0' 
	| '\u1680' 
	| '\u2000'..'\u200A' 
	| '\u202F' 
	| '\u205F' 
	| '\u3000'
	;

COMMENT: 
	'@*' .*? '*@' -> channel(COMMENT_CHANNEL);

RAW: 
	'@:' .*? ':@';

DEF_START: 
	DEF_ST -> pushMode(DEF);

OUT_START: 
	OUT_ST -> pushMode(OUT);

TEXT: .+?;

mode DEF;


DEF_COMMENT: 
	COMMENT -> channel(COMMENT_CHANNEL);

DEF_CLOSE: 
	DEF_CL -> popMode;

SUB_START: 
	SUB_ST -> pushMode(SUB);

DEF_STARTNAME: '<';
DEF_ENDNAME: '>';
DEF_TYPE: '::';
DELIM: ':';
DEF_ID: ID;

DEF_WS: WS+ -> skip;

//DEF_WS: WS+ -> type(WHITESPACE);


mode SUB;



SUB_COMMENT: 
	COMMENT -> channel(COMMENT_CHANNEL);

SUB_LINE_TERMINATE: 
	SUB_CL WS* LINE_TERM WS* -> type(SUB_CLOSE), popMode;

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
	COMMENT -> channel(COMMENT_CHANNEL);

OUT_PARAMSTART: 
	PARA_ST -> pushMode(CALL);

OUT_DELIM: 
	DELIM -> type(DELIM);

OUT_SUBSTART: 
	SUB_START -> type(SUB_START), popMode, pushMode(SUB);

OUT_ID: ID;

LINE_TERMINATE: 
	LINE_TERM WS* -> popMode;

OUT_WS: WS+ -> skip;

//OUT_WS: WS+ -> type(WHITESPACE);

OUT_RAW: RAW -> type(RAW), popMode;
OUT_DEF_START: DEF_START -> type(DEF_START), popMode, pushMode(DEF);
OUT_OUT_START: OUT_START -> type(OUT_START), popMode, pushMode(OUT);
OUT_SUB_CLOSE2: SUB_CLOSE WS* LINE_TERM WS* -> type(TEXT), popMode;
OUT_SUB_CLOSE: SUB_CLOSE -> type(TEXT), popMode;
OUT_OTHER: . -> type(TEXT), popMode;



mode OUT_SUB;


OUT_SUB_COMMENT: 
	COMMENT -> channel(COMMENT_CHANNEL);

OUT_SUB_PARAMSTART: 
	OUT_PARAMSTART -> type(OUT_PARAMSTART), pushMode(CALL);

OUT_SUB_DELIM: 
	DELIM -> type(DELIM);

OUT_SUB_SUBSTART: 
	SUB_START -> type(SUB_START), popMode, pushMode(SUB);

OUT_SUB_OUT_ID: OUT_ID -> type(OUT_ID);

OUT_SUB_LINE_TERMINATE: 
	LINE_TERM WS* -> type(LINE_TERMINATE), popMode;

OUT_SUB_WS: WS+ -> type(OUT_WS), skip;

OUT_SUB_RAW: RAW -> type(RAW), popMode;
OUT_SUB_DEF_START: DEF_START -> type(DEF_START), popMode, pushMode(DEF);
OUT_SUB_OUT_START: OUT_START -> type(OUT_START), popMode, pushMode(OUT);
OUT_SUB_SUB_CLOSE2: SUB_CLOSE WS* LINE_TERM WS* -> type(SUB_CLOSE), popMode, popMode;
OUT_SUB_SUB_CLOSE: SUB_CLOSE -> type(SUB_CLOSE), popMode, popMode;
OUT_SUB_OTHER: . -> type(TEXT), popMode;


mode CALL;

OUT_PARAMEND: 
	PARA_CL -> popMode;

CALL_OUT_PARAMSTART: 
	OUT_PARAMSTART -> type(OUT_PARAMSTART), pushMode(CALL);

CALL_OUT_DELIM: 
	DELIM -> type(DELIM);

CALL_OUT_ID: OUT_ID -> type(OUT_ID);

CALL_LINE_TERMINATE: 
	LINE_TERM WS* -> type(LINE_TERMINATE), popMode, popMode;

CALL_OUT_WS: WS+ -> skip;

//CALL_OUT_WS: WS+ -> type(WHITESPACE);

mode CS;

SCHARP_WS: WS+ -> skip;