parser grammar TtlParser;
options { tokenVocab=TtlLexer; }

/*
 * Parser Rules
 */

ttl: (definition | outblock | raw | TEXT)*;

raw: RAW;

definition: DEF_START def+ DEF_CLOSE;

def: simple_def
	| inherited_def
	;

inherited_def:
	DEF_STARTNAME DEF_ID DELIM DEF_ID DEF_ENDNAME subtemplate DEF_TYPE DEF_ID
	| DEF_STARTNAME DEF_ID DELIM DEF_ID DEF_ENDNAME subtemplate
	;

simple_def:
	DEF_STARTNAME DEF_ID DEF_ENDNAME subtemplate DEF_TYPE DEF_ID
	| DEF_STARTNAME DEF_ID DEF_ENDNAME subtemplate
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
	| OUT_ID OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND
	;

unnamed_call: OUT_PARAMSTART OUT_ID? OUT_PARAMEND
	| OUT_PARAMSTART chain OUT_PARAMEND
	| OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND
	;

csharp_expression: CSHARP_TOKEN*;

subtemplate: SUB_START ttl SUB_CLOSE;