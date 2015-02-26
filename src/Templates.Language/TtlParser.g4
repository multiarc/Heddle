parser grammar TtlParser;

options { tokenVocab=TtlLexer; }

/*
 * Parser Rules
 */

ttl: definition*
	| outblock*
	| RAW*
	| TEXT*
	;

definition: DEF_START def+ DEF_CLOSE;

def: DEF_STARTNAME DEF_ID DEF_DELIM DEF_ID DEF_ENDNAME subtemplate DEF_TYPE DEF_ID
	| DEF_STARTNAME DEF_ID DEF_ENDNAME subtemplate DEF_TYPE DEF_ID
	| DEF_STARTNAME DEF_ID DEF_DELIM DEF_ID DEF_ENDNAME subtemplate
	| DEF_STARTNAME DEF_ID DEF_ENDNAME subtemplate
	;

subtemplate: DEF_SUBSTART ttl SUB_CLOSE;

outblock: OUT_START call (OUT_DELIM call)* outtemplate?;

call: OUT_ID? OUT_PARAMSTART call (OUT_DELIM call)* OUT_PARAMEND
	| OUT_ID? OUT_PARAMSTART OUT_ID? OUT_PARAMEND
	;

outtemplate: OUT_SUBSTART ttl SUB_CLOSE;