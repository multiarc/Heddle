/*
 * Lexer Rules
 */

lexer grammar TtlLexer;

COMMENT: DEFAULT_COMMENT | SUB_COMMENT;
RAW: DEFAULT_RAW | SUB_RAW;
DEF_START: DEFAULT_DEFSTART | SUB_DEFSTART;
OUT_START: DEFAULT_OUTSTART | SUB_OUTSTART;
TEXT: DEFAULT_TEXT | SUB_TEXT;

fragment DEFAULT_COMMENT: '@*' .*? '*@' -> skip;
fragment DEFAULT_RAW: '@:' .*? ':@';
fragment DEFAULT_DEFSTART: '<%' -> pushMode(DEF);
fragment DEFAULT_OUTSTART: '@' -> pushMode(OUT);
fragment DEFAULT_TEXT: .+?;

fragment NameChar : NameStartChar
	| '.'
	| '0'..'9'
	| '_'
	| '\u00B7'
	| '\u0300'..'\u036F'
	| '\u203F'..'\u2040'
	;
fragment NameStartChar 
	: 'A'..'Z' | 'a'..'z'
	| '\u00C0'..'\u00D6'
	| '\u00D8'..'\u00F6'
	| '\u00F8'..'\u02FF'
	| '\u0370'..'\u037D'
	| '\u037F'..'\u1FFF'
	| '\u200C'..'\u200D'
	| '\u2070'..'\u218F'
	| '\u2C00'..'\u2FEF'
	| '\u3001'..'\uD7FF'
	| '\uF900'..'\uFDCF'
	| '\uFDF0'..'\uFFFD'
	;

mode DEF;

DEF_CLOSE: '%>' ->popMode;
DEF_STARTNAME : '<';
DEF_ENDNAME : '>';
DEF_TYPE: '::';
DEF_DELIM: ':';
DEF_SUBSTART: '[' -> pushMode(SUB);
DEF_ID : NameStartChar NameChar*;
DEF_WS: [ \r\n\t]+ -> skip;

mode SUB;

SUB_COMMENT: '@*' .*? '*@' -> skip;
SUB_RAW: '@:' .*? ':@';
SUB_DEFSTART: '<%' -> pushMode(DEF);
SUB_OUTSTART: '@' -> pushMode(OUT);
SUB_CLOSE: ']' ->popMode;
SUB_TEXT: .+?;

mode OUT;

OUT_PARAMSTART: '(';
OUT_PARAMEND: ')';
OUT_DELIM: ':';
OUT_SUBSTART: '[' -> pushMode(SUB);
OUT_CLOSE: ';' -> popMode;
OUT_ID : NameStartChar NameChar*;
OUT_WS: [ \r\n\t]+ -> skip;