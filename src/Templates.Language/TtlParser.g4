parser grammar TtlParser;
options { tokenVocab=TtlLexer; }

/*
 * Parser Rules
 */

ttl: (definition | import_block | outblock | raw | text)*;

raw: RAW;

definition: DEF_START def+ DEF_CLOSE;

def: 
    DEF_STARTNAME ID def_base? DEF_ENDNAME default_chain? subtemplate def_type?
	;
	
def_base:
    DELIM ID
    ;
 
def_type:
    DEF_TYPE ID
    ;

default_chain: DEF_OUT chain;

import_block: IMPORT_TOKEN WS* SUB_START text+ SUB_CLOSE;

outblock: OUT chain subtemplate?;

chain: call (DELIM call)*;

call: 
    extension_id? OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND
    | extension_id? OUT_PARAMSTART WS* member_expression? OUT_PARAMEND
    | extension_id? OUT_PARAMSTART chain OUT_PARAMEND
    ;
    
member_expression:
    ROOT_REF? ID (MEMBER_P ID)*
    ;
    
    
extension_id:
    ID
    ;

csharp_expression: (CSHARP_TOKEN | OUT_PARAMEND)+;

subtemplate: 
    WS* SUB_START ttl SUB_CLOSE;

text: ~(SUB_CLOSE | SUB_START | DEF_START | DEF_CLOSE | OUT | RAW);