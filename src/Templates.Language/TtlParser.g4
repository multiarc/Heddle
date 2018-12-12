parser grammar TtlParser;
options { tokenVocab=TtlLexer; }

/*
 * Parser Rules
 */

ttl: (definition | import_block | outblock | raw | comment | text)*;

comment: COMMENT;

raw: RAW;

definition: DEF_START def+ DEF_CLOSE;

def: simple_def
	| inherited_def
	;

inherited_def:
	DEF_STARTNAME ID DELIM ID DEF_ENDNAME default_chain? subtemplate DEF_TYPE ID
	| DEF_STARTNAME ID DELIM ID DEF_ENDNAME default_chain? subtemplate
	;

simple_def:
	DEF_STARTNAME ID DEF_ENDNAME default_chain? subtemplate DEF_TYPE ID
	| DEF_STARTNAME ID DEF_ENDNAME default_chain? subtemplate
	;

default_chain: DEF_OUT chain;

import_block: IMPORT_TOKEN SUB_START TEXT SUB_CLOSE;

outblock: OUT chain subtemplate?
	| OUT chain subtemplate? LINE_TERMINATE
	;

chain: call (DELIM call)*;

call: named_call
	| unnamed_call
	;

named_call: ID OUT_PARAMSTART ROOT_REF? ID? OUT_PARAMEND
	| ID OUT_PARAMSTART ROOT_REF? ID (MEMBER_P ID)+ OUT_PARAMEND
	| ID OUT_PARAMSTART chain OUT_PARAMEND
	| ID OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND
	;

unnamed_call: OUT_PARAMSTART ROOT_REF? ID? OUT_PARAMEND
	| OUT_PARAMSTART ROOT_REF? ID (MEMBER_P ID)+ OUT_PARAMEND
	| OUT_PARAMSTART chain OUT_PARAMEND
	| OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND
	;

csharp_expression: CSHARP_TOKEN+;

subtemplate: SUB_START ttl SUB_CLOSE;

text: .;