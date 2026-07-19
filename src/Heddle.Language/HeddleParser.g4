parser grammar HeddleParser;
options { tokenVocab=HeddleLexer; }

/*
 * Parser Rules
 */

heddle: (definition | import_block | outblock | raw | text)*;

raw: RAW;

definition: DEF_START def+ DEF_CLOSE;

def:
    DEF_STARTNAME ID def_props? def_base? DEF_ENDNAME default_chain? subtemplate def_type?
    | DEF_STARTNAME DELIM ID def_region_type? DEF_ENDNAME subtemplate                          // NEW (phase 7): <:name> public region
	;

// NEW (phase 7): the in-header model type of a public region (<:item :: Article>). The type sits
// inside the angle brackets (R7's ratified <:name :: Type> shape), distinct from a definition's
// trailing def_type (}} :: T). A region carries no prop list, no base, and no default output chain.
def_region_type: DEF_TYPE ID;

def_props: OUT_PARAMSTART (def_prop_item (COMMA def_prop_item)*)? OUT_PARAMEND;

def_prop_item: def_prop | def_slot;

// name : Type [= literal]
def_prop: ID DELIM ID def_prop_default?;

// out :: Type   (the identifier must be 'out' — enforced at walk time, HED5016)
def_slot: ID DEF_TYPE ID;

def_prop_default: ASSIGN def_literal;

def_literal:
    OP_MINUS? (INT_LIT | REAL_LIT)
    | STRING_LIT
    | CHAR_LIT
    | TRUE
    | FALSE
    | NULL
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
    | extension_id? OUT_PARAMSTART native_expression OUT_PARAMEND
    | extension_id? OUT_PARAMSTART (expr COMMA)? named_argument (COMMA named_argument)* OUT_PARAMEND
    ;

named_argument: ID DELIM expr;

member_expression:
    ROOT_REF? ID (MEMBER_P ID)*
    ;


extension_id:
    ID
    ;

csharp_expression: CSHARP_TOKEN+;

native_expression: expr;

expr:
      expr MEMBER_P ID arg_list                                    # MethodCallExpr
    | expr MEMBER_P ID                                             # MemberHopExpr
    | expr LBRACKET expr (COMMA expr)* RBRACKET                    # IndexExpr
    | op=(OP_NOT | OP_MINUS | OP_PLUS | OP_TILDE) expr             # UnaryExpr
    | expr op=(OP_STAR | OP_SLASH | OP_PERCENT) expr               # MultiplicativeExpr
    | expr op=(OP_PLUS | OP_MINUS) expr                            # AdditiveExpr
    | expr op=(OP_LSHIFT | OP_RSHIFT) expr                         # ShiftExpr
    | expr op=(OP_LT | OP_GT | OP_LE | OP_GE) expr                 # RelationalExpr
    | expr op=(OP_EQ | OP_NEQ) expr                                # EqualityExpr
    | expr OP_AMP expr                                             # BitAndExpr
    | expr OP_CARET expr                                           # BitXorExpr
    | expr OP_PIPE expr                                            # BitOrExpr
    | expr OP_AND expr                                             # AndAlsoExpr
    | expr OP_OR expr                                              # OrElseExpr
    | <assoc=right> expr OP_QQ expr                                # CoalesceExpr
    | <assoc=right> expr OP_QUESTION expr DELIM expr               # TernaryExpr
    | ID arg_list                                                  # FunctionCallExpr
    | OUT_PARAMSTART expr OUT_PARAMEND                             # GroupExpr
    | literal                                                      # LiteralExpr
    | THIS                                                         # ThisExpr    // NEW (phase 5)
    | ROOT_REF? ID                                                 # PathRootExpr
    ;

arg_list: OUT_PARAMSTART (expr (COMMA expr)*)? OUT_PARAMEND;

literal: INT_LIT | REAL_LIT | STRING_LIT | CHAR_LIT | TRUE | FALSE | NULL;

subtemplate: 
    WS* SUB_START heddle SUB_CLOSE;

text: ~(SUB_CLOSE | SUB_START | DEF_START | DEF_CLOSE | OUT | RAW);