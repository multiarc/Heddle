parser grammar TtlParser;
options { tokenVocab=TtlLexer; }

/*
 * Parser Rules
 */

ttl: (definition | import_block | outblock | raw | text)*;

raw: RAW;

definition: DEF_START def+ DEF_CLOSE;

def: simple_def
	| inherited_def
	;

inherited_def:
	TEXT_WS* DEF_STARTNAME TEXT_WS* ID TEXT_WS* DELIM TEXT_WS* ID TEXT_WS* DEF_ENDNAME TEXT_WS* default_chain? TEXT_WS* subtemplate TEXT_WS* DEF_TYPE TEXT_WS* ID TEXT_WS*
	| TEXT_WS* DEF_STARTNAME TEXT_WS* ID TEXT_WS* DELIM TEXT_WS* ID TEXT_WS* DEF_ENDNAME TEXT_WS* default_chain? TEXT_WS* subtemplate TEXT_WS*
	;

simple_def:
	TEXT_WS* DEF_STARTNAME TEXT_WS* ID TEXT_WS* DEF_ENDNAME TEXT_WS* default_chain? TEXT_WS* subtemplate TEXT_WS* DEF_TYPE TEXT_WS* ID TEXT_WS*
	| TEXT_WS* DEF_STARTNAME TEXT_WS* ID TEXT_WS* DEF_ENDNAME TEXT_WS* default_chain? TEXT_WS* subtemplate TEXT_WS*
	;

default_chain: DEF_OUT chain;

import_block: IMPORT_TOKEN SUB_START TEXT SUB_CLOSE;

outblock: OUT chain subtemplate? LINE_TERMINATE?;

chain: call (DELIM call)*;

call: named_call
	| unnamed_call
	;

named_call: ID OUT_PARAMSTART ROOT_REF? ID? OUT_PARAMEND
	| ID OUT_PARAMSTART ROOT_REF? ID (MEMBER_P ID)+ OUT_PARAMEND
	| ID OUT_PARAMSTART chain OUT_PARAMEND
	| ID OUT_PARAMSTART CSHARP_START csharp_expression CSHARP_TOKEN
	;

unnamed_call: OUT_PARAMSTART ROOT_REF? ID? OUT_PARAMEND
	| OUT_PARAMSTART ROOT_REF? ID (MEMBER_P ID)+ OUT_PARAMEND
	| OUT_PARAMSTART chain OUT_PARAMEND
	| OUT_PARAMSTART CSHARP_START csharp_expression CSHARP_TOKEN
	;

csharp_expression: CSHARP_TOKEN+;

subtemplate: SUB_START ttl SUB_CLOSE
    | SUB_START TEXT_WS+ SUB_CLOSE
    | SUB_START SUB_CLOSE;

text: ~(SUB_CLOSE | SUB_START | DEF_START | DEF_CLOSE);