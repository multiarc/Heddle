parser grammar TtlParser;
options { tokenVocab=TtlLexer; }

/*
 * Parser Rules
 */

ttl: (definition | outblock | RAW | TEXT)*;

definition: DEF_START def+ DEF_CLOSE;

def: DEF_STARTNAME ID DELIM ID DEF_ENDNAME subtemplate DEF_TYPE ID
	| DEF_STARTNAME ID DEF_ENDNAME subtemplate DEF_TYPE ID
	| DEF_STARTNAME ID DELIM ID DEF_ENDNAME subtemplate
	| DEF_STARTNAME ID DEF_ENDNAME subtemplate
	;

outblock: OUT_START chain subtemplate?
	| OUT_START chain subtemplate? LINE_TERMINATE
	;

chain: call (DELIM call)*;

call: named_call
	| unnamed_call
	;

named_call: OUT_ID OUT_PARAMSTART OUT_ID? OUT_PARAMEND
	| OUT_ID OUT_PARAMSTART chain OUT_PARAMEND
	//| OUT_ID OUT_PARAMSTART CALL_CSHARP CSHARP_EXPRESSION OUT_PARAMEND
	;

unnamed_call: OUT_PARAMSTART OUT_ID? OUT_PARAMEND
	| OUT_PARAMSTART chain OUT_PARAMEND
	//| OUT_PARAMSTART CALL_CSHARP CSHARP_EXPRESSION OUT_PARAMEND
	;

subtemplate: SUB_START ttl SUB_CLOSE;