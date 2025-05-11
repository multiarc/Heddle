parser grammar TtlParser;
options { tokenVocab=TtlLexer; }

/*
 * Parser Rules
 */

ttl: (definition | import_block | outblock | raw | text)*;

raw: RAW;

definition: DEF_START (def | WS)+ DEF_CLOSE;

def: 
    DEF_STARTNAME WS* ID WS* def_base? DEF_ENDNAME default_chain? subtemplate WS* DEF_TYPE WS* ID
    | DEF_STARTNAME WS* ID WS* def_base? DEF_ENDNAME default_chain? subtemplate
	;
	
def_base:
    DELIM WS* ID WS*
    ;

default_chain: DEF_OUT WS* chain;

import_block: IMPORT_TOKEN WS* SUB_START text+ SUB_CLOSE;

outblock: OUT chain subtemplate?;

chain: call (DELIM WS* call)*;

call: 
    extension_id? OUT_PARAMSTART ROOT_REF? ID? OUT_PARAMEND
    | extension_id? OUT_PARAMSTART ROOT_REF? ID (MEMBER_P ID )+ OUT_PARAMEND
    | extension_id? OUT_PARAMSTART chain OUT_PARAMEND
    | extension_id? OUT_PARAMSTART CSHARP_START csharp_expression CSHARP_TOKEN
    ;
    
extension_id:
    ID
    ;

csharp_expression: CSHARP_TOKEN+;

subtemplate: 
    WS* SUB_START ttl SUB_CLOSE;

text: ~(SUB_CLOSE | SUB_START | DEF_START | DEF_CLOSE | OUT);