grammar CSharp;

/*
* C# Statements
*/

statement:
	labeled_statement
	| declaration_statement
	| embedded_statement
	;

embedded_statement:
	block
	| empty_statement
	| expression_statement
	| selection_statement
	| iteration_statement
	| jump_statement
	| try_statement
	| checked_statement
	| unchecked_statement
	| lock_statement
	| using_statement 
	| yield_statement
	;

checked_statement:
	'checked' block;

unchecked_statement:
	'unchecked' block;

lock_statement:
	'lock' '(' expression ')' embedded_statement;

using_statement:
	'using' '(' resource_acquisition ')' embedded_statement;

resource_acquisition:
	local_variable_declaration
	| expression
	;

yield_statement:
	'yield' 'return' expression ';'
	| 'yield' 'break' ';';

try_statement:
	'try' block catch_clauses
	| 'try' block finally_clause
	| 'try' block catch_clauses finally_clause
	;

catch_clauses:
	specific_catch_clauses general_catch_clause?
	| specific_catch_clauses? general_catch_clause
	;

specific_catch_clauses:
	specific_catch_clause specific_catch_clause*;

specific_catch_clause:
	'catch' '(' class_type identifier? ')' block;

general_catch_clause:
	'catch' block;

finally_clause:
	'finally' block;

jump_statement:
	break_statement
	| continue_statement
	| goto_statement
	| return_statement
	| throw_statement
	;

break_statement:
	'break' ';';

continue_statement:
	'continue' ';';

goto_statement:
	'goto' identifier ';'
	| 'goto' 'case' constant_expression ';'
	| 'goto' 'default' ';'
	;

return_statement:
	'return' expression? ';';

throw_statement:
	'throw' expression? ';';

iteration_statement:
	while_statement
	| do_statement
	| for_statement
	| foreach_statement
	;

foreach_statement:
	'foreach' '(' local_variable_type identifier 'in' expression ')' embedded_statement;

while_statement:
	'while' '(' boolean_expression ')' embedded_statement;

do_statement:
	'do' embedded_statement 'while' '(' boolean_expression ')' ';';

for_statement:
	'for' '(' for_initializer? ';' for_condition? ';' for_iterator? ')' embedded_statement;

for_initializer:
	local_variable_declaration
	| statement_expression_list
	;

for_condition:
	boolean_expression;

for_iterator:
	statement_expression_list;

statement_expression_list:
	statement_expression (',' statement_expression)*;

selection_statement:
	if_statement
	| switch_statement
	;

switch_statement:
	'switch' '(' expression ')' switch_block;

switch_block:
	'{' switch_sections? '}';

switch_sections:
	switch_section switch_section*;

switch_section:
	switch_labels statement_list;

switch_labels:
	'case' constant_expression ':'
	| 'default' ':'
	;

if_statement:
	'if' '(' boolean_expression ')' embedded_statement
	| 'if' '(' boolean_expression ')' embedded_statement 'else' embedded_statement
	;
	

expression_statement:
	statement_expression ';';

statement_expression:
	sub_primary_expression invocation_expression
	| object_creation_expression
	| assignment
	| post_increment_expression
	| post_decrement_expression
	| pre_increment_expression
	| pre_decrement_expression
	;

post_increment_expression:
	primary_expression '++';

post_decrement_expression:
	primary_expression '--';

block:
	'{' statement_list? '}';

statement_list:
	statement statement*;

empty_statement:
	';';

labeled_statement:
	identifier ':' statement;

declaration_statement:
	local_variable_declaration ';'
	| local_constant_declaration ';'
	;

local_constant_declaration:
	'const' type constant_declarators;

constant_declarators:
	constant_declarator (',' constant_declarator)*;

constant_declarator:
	identifier '=' constant_expression;

constant_expression:
	expression;

boolean_expression:
	expression;

local_variable_declaration: 
	local_variable_type local_variable_declarators;

local_variable_type:
	type
	| 'var'
	;

local_variable_declarators:
	local_variable_declarator (',' local_variable_declarator)*;

local_variable_declarator:
	identifier
	| identifier '=' local_variable_initializer
	;

local_variable_initializer:
	expression
	| array_initializer
	;

array_initializer:
	'{'   variable_initializer_list? '}'
	| '{'   variable_initializer_list ',' '}'
	;

variable_initializer_list:
	variable_initializer (',' variable_initializer)*;

variable_initializer:
	expression
	| array_initializer
	;

/*
* c# expressions
*/

assignment:
	unary_expression assignment_operator expression;

assignment_operator:
	'=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '|=' | '^=' | '<<=' | '>>=';

expression: 
	non_assignment_expression
	| assignment
	;

non_assignment_expression:
	conditional_expression
	| lambda_expression
	| query_expression
	;

query_expression:
	from_clause query_body;

from_clause:
	'from' type? identifier 'in' expression;

query_body:
	query_body_clauses? select_or_group_clause query_continuation?;

query_body_clauses:
	query_body_clause query_body_clause*;

query_body_clause:
	| from_clause
	| let_clause
	| where_clause
	| join_clause
	| join_into_clause
	| orderby_clause
	;

let_clause:
	'let' identifier '=' expression;

where_clause:
	'where' boolean_expression;

join_clause:
	'join' type? identifier 'in' expression 'on' expression 'equals' expression;

join_into_clause:
	'join' type? identifier 'in' expression 'on' expression 'equals' expression 'into' identifier;

orderby_clause:
	'orderby' orderings;

orderings:
	ordering (',' ordering)*;

ordering:
	expression ordering_direction?;

ordering_direction:
	'ascending' | 'descending';

select_or_group_clause:
	select_clause
	| group_clause
	;

select_clause:
	'select' expression;

group_clause:
	'group' expression 'by' expression;

query_continuation:
	'into' identifier query_body;

lambda_expression:
	anonymous_function_signature '=>' anonymous_function_body;

anonymous_function_body:
	expression
	| block
	;

anonymous_function_signature:
	explicit_anonymous_function_signature 
	| implicit_anonymous_function_signature
	;

implicit_anonymous_function_signature:
	'('   implicit_anonymous_function_parameter_list?   ')'
	| implicit_anonymous_function_parameter
	;

implicit_anonymous_function_parameter_list:
	implicit_anonymous_function_parameter (',' implicit_anonymous_function_parameter)*;

implicit_anonymous_function_parameter: identifier;

explicit_anonymous_function_signature:
	'(' explicit_anonymous_function_parameter_list? ')';

explicit_anonymous_function_parameter_list:
	explicit_anonymous_function_parameter (',' explicit_anonymous_function_parameter)*;

explicit_anonymous_function_parameter:
	anonymous_function_parameter_modifier? type identifier;

anonymous_function_parameter_modifier:
	'ref' | 'out';

conditional_expression:
	null_coalescing_expression
	| null_coalescing_expression '?' expression ':' expression
	;

null_coalescing_expression:
	conditional_or_expression
	| conditional_or_expression '??' null_coalescing_expression
	;

conditional_or_expression:
	conditional_and_expression ('||' conditional_and_expression)*;
	
conditional_and_expression:
	inclusive_or_expression ('&&' inclusive_or_expression)*;

inclusive_or_expression:
	exclusive_or_expression ('|' exclusive_or_expression)*;

exclusive_or_expression:
	and_expression ('^' and_expression)*;

and_expression:
	equality_expression ('&' equality_expression)*;

equality_expression:
	relational_expression ('==' relational_expression)*
	| relational_expression ('!=' relational_expression)*
	;

relational_expression:
    shift_expression ('<=' shift_expression)*
	| shift_expression ('>=' shift_expression)*
	| shift_expression ('<' shift_expression)*
	| shift_expression ('<' shift_expression)*
	| shift_expression ('>' shift_expression)*
	| shift_expression ('is' type)*
	| shift_expression ('as' type)*
	;

shift_expression:
	additive_expression ('<<' additive_expression)*
	| additive_expression ('>>' additive_expression)*
	;

additive_expression:
	multiplicative_expression ('+' multiplicative_expression)*
	| multiplicative_expression ('-' multiplicative_expression)*
	;

multiplicative_expression:
	unary_expression ('*' unary_expression)
	| unary_expression ('/' unary_expression)
	| unary_expression ('%' unary_expression)
	;

unary_expression:
	primary_expression
	| '++' unary_expression
	| '--' unary_expression
	| '+' unary_expression
	| '-' unary_expression
	| '!' unary_expression
	| '~' unary_expression
	| cast_expression
	;

pre_increment_expression: '++' unary_expression;

pre_decrement_expression: '--' unary_expression;

cast_expression:
	'(' type ')' unary_expression;

primary_expression:
	sub_primary_expression ( 
		simple_member_access
		| invocation_expression
		| post_increment_access
		| post_decrement_access
		)*
	;

sub_primary_expression:
	primary_no_array_create (
		element_access
		)*
	| array_creation_expression
	;

array_creation_expression:
	'new' type '[' expression_list ']' rank_specifiers? array_initializer?
	| 'new' sub_reference_type array_type+  array_initializer
	| 'new' rank_specifier   array_initializer
	;

collection_initializer:
	'{' element_initializer_list '}'
	| '{' element_initializer_list ',' '}'
	;

element_initializer_list:	
	element_initializer (',' element_initializer)*;

element_initializer:
	non_assignment_expression
	| '{' expression_list '}'
	;

expression_list:
	expression (',' expression)*;

primary_no_array_create:
	literal
	| simple_name
	| parenthesized_expression
	| member_access
	| this_access
	| base_access
	| object_creation_expression
	| delegate_creation_expression
	| anonymous_object_creation
	| typeof_expression
	| checked_expression
	| unchecked_expression
	| default_value_expression
	| anonymous_method_expression
	;

anonymous_method_expression:
	'delegate' explicit_anonymous_function_signature? block;

default_value_expression:
	'default' '(' type ')';

checked_expression:
	'checked' '(' expression ')';

unchecked_expression:
	'unchecked' '(' expression ')';

typeof_expression:
	'typeof' '(' type ')'
	| 'typeof' '(' unbound_type_name ')'
	| 'typeof' '(' 'void' ')'
	;

unbound_type_name:
	identifier generic_dimension_specifier? ('.' identifier generic_dimension_specifier?)*
	| identifier '::' identifier generic_dimension_specifier? ('.' identifier generic_dimension_specifier?)*
	;

generic_dimension_specifier:
	'<' commas? '>';

commas:
	',' (',')*;

anonymous_object_creation:
	'new' anonymous_object_initializer;

anonymous_object_initializer:
	'{' member_declarator_list? '}'
	| '{' member_declarator_list ',' '}'
	;

member_declarator_list:
	member_declarator (',' member_declarator)*;

member_declarator:
	simple_name
	| member_access
	| identifier '=' expression
	;

delegate_creation_expression:
	'new' delegate_type '(' expression ')';

object_creation_expression:
	'new' type '(' argument_list? ')' object_or_collection_initializer?
	| 'new' type object_or_collection_initializer
	;

object_or_collection_initializer:
	object_initializer | collection_initializer;

object_initializer:
	'{' member_initilizer_list? '}'
	| '{' member_initilizer_list ',' '}'
	;

member_initilizer_list:
	member_initializer (',' member_initializer)*;

member_initializer:
	identifier '=' initializer_value;

initializer_value:
	expression
	| object_or_collection_initializer
	;

post_increment_access: '++';
	
post_decrement_access: '--';

this_access: 'this';

base_access: 
	'base' '.' identifier
	| 'base' '[' argument_list ']'
	;

element_access:
	'[' argument_list ']';

invocation_expression:
	'(' argument_list? ')';

simple_name:
	identifier type_argument_list?;

parenthesized_expression:
	'(' expression ')';

simple_member_access:
	'.' identifier type_argument_list?;

member_access:
	predefined_type '.' identifier type_argument_list?
	| qualified_alias_member '.' identifier
	;

type_argument_list:
	'<' type (',' type)* '>';

type:
	value_type
	| reference_type
	| identifier
	;

value_type:
	struct_type
	| enum_type
	;

reference_type:
	sub_reference_type array_type*;

sub_reference_type:
	class_type
	| interface_type
	| delegate_type
	;

class_type:
	'object'
	| 'dynamic'
	| 'string'
	| type_name
	;

interface_type:
	type_name;

delegate_type:
	type_name;

array_type: rank_specifiers;

rank_specifiers: rank_specifier+;

rank_specifier:
	'[' dim_separators? ']';

dim_separators:',' (',')*;

struct_type:
	sub_struct_type nullable_type*;

sub_struct_type:
	type_name
	| simple_type
	;

nullable_type: '?';

enum_type: type_name;

type_name: namespace_or_type_name;

namespace_or_type_name:
	identifier type_argument_list ('.' identifier type_argument_list)*
	| qualified_alias_member ('.' identifier type_argument_list)*
	;

simple_type:
	numeric_type
	| 'bool'
	;

numeric_type:
	integral_type
	| floating_point_type
	| 'decimal'
	;

integral_type:
	'sbyte'
	| 'byte'
	| 'short'
	| 'ushort'
	| 'int'
	| 'uint'
	| 'long'
	| 'ulong'
	| 'char'
	;

floating_point_type:
	'float' | 'double';

predefined_type:
	'bool' | 'byte'| 'char' | 'decimal' | 'double' | 'float' | 'int' | 'long'
	| 'object' | 'sbyte' | 'short' | 'string' | 'uint' | 'ulong' | 'ushort'
	;

qualified_alias_member:
	identifier '::' identifier type_argument_list?;

argument_list:
	argument (',' argument)*;

argument:
	argument_name? argument_value;

argument_name:
	identifier ':';

argument_value:
	expression
	| 'ref' variable_reference
	| 'out' variable_reference
	;

variable_reference:
	expression;

/*
* C# Tokens
*/

WS: WHITESPACE+ -> skip;

TOKEN:
	KEYWORD
	| OPERATOR_OR_PUNCTUATOR
	| STRING
	| CHAR
	| INT
	| REAL
	| IDENTIFIER
	;

fragment NEW_LINE:
	[\r\n] | '\u0085' | '\u2028' | '\u2029';

fragment WHITESPACE: NEW_LINE
	| [ \t\f] 
	| '\u000B'
	| '\u00A0' 
	| '\u1680' 
	| '\u2000'..'\u200A' 
	| '\u202F' 
	| '\u205F' 
	| '\u3000'
	;

/*
* C# C# Keywords
*/

fragment KEYWORD:
	'abstract' | 'as' | 'base' | 'bool' | 'break'
	| 'byte' | 'case' | 'catch' | 'char' | 'checked'
	| 'class' | 'const' | 'continue' | 'decimal' | 'default'
	| 'delegate' | 'do' | 'double' | 'else' | 'enum'
	| 'event' | 'explicit' | 'extern' | 'false' | 'finally'
	| 'fixed' | 'float' | 'for' | 'foreach' | 'goto'
	| 'if' | 'implicit' | 'in' | 'int' | 'interface'
	| 'internal' | 'is' | 'lock' | 'long' | 'namespace'
	| 'new' | 'null' | 'object' | 'operator' | 'out'
	| 'override' | 'params' | 'private' | 'protected' | 'public'
	| 'readonly' | 'ref' | 'return' | 'sbyte' | 'sealed'
	| 'short' | 'sizeof' | 'stackalloc' | 'static' | 'string'
	| 'struct' | 'switch' | 'this' | 'throw' | 'true'
	| 'try' | 'typeof' | 'uint' | 'ulong' | 'unchecked'
	| 'unsafe' | 'ushort' | 'using' | 'virtual' | 'void'
	| 'volatile' | 'while'
	;

/*
* C# Identifier
*/

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

/*
* LITERAL
*/

fragment LITERAL:
	BOOL
	| INT
	| REAL
	| CHAR
	| STRING
	| NULL
	;

fragment BOOL: 
	'true' | 'false';

/*
* INT
*/

fragment INT: 
	DEC_INT_LITERAL | HEX_INT_LITERAL;

fragment DEC_INT_LITERAL: 
	DEC_DIGITS INT_SUFFIX?;

fragment DEC_DIGITS: DEC_DIGIT+;

fragment DEC_DIGIT: '0'..'9';

fragment INT_SUFFIX: [Uu] | [Ll] | [UuLl];

fragment HEX_INT_LITERAL: '0x' HEX_DIGITS INT_SUFFIX?;

fragment HEX_DIGITS: HEX_DIGIT+;

fragment HEX_DIGIT:
	'0'..'9'
	| 'a'..'f'
	| 'A'..'F'
	;

/*
* REAL
*/

fragment REAL:
	DEC_DIGITS '.' DEC_DIGITS EXP_PART? REAL_SUFFIX?
	| '.' DEC_DIGITS EXP_PART? REAL_SUFFIX?
	| DEC_DIGITS EXP_PART REAL_SUFFIX?
	| DEC_DIGITS REAL_SUFFIX
	;

fragment EXP_PART: [eE] SIGN? DEC_DIGITS;

fragment SIGN: [+-];

fragment REAL_SUFFIX: [FfDdMm];

/*
* CHAR
*/

fragment CHAR: '\'' CHARACTER '\'';

fragment CHARACTER: 
	SINGLE_CHAR
	| SIMPLE_ESCAPE
	| HEX_ESCAPE
	| UNICODE_ESCAPE
	;

fragment SINGLE_CHAR: ~(['\u0027\u005C] | [\r\n] | '\u0085' | '\u2028' | '\u2029');

fragment SIMPLE_ESCAPE:
	'\\\'' | '\\\"' | '\\\\' | '\\0' | '\\a' | '\\b' | '\\f' | '\\n' | '\\r' | '\\t' | '\\v';

fragment HEX_ESCAPE:
	'\\x' HEX_DIGIT HEX_DIGIT? HEX_DIGIT? HEX_DIGIT?;

/*
* STRING
*/

fragment STRING:
	REGULAR_STRING
	| VARBATIM_STRING
	;

fragment REGULAR_STRING: '"' REGULAR_STRING_LITERALS? '"';

fragment REGULAR_STRING_LITERALS:
	REGULAR_STRING_LITERAL+
	| CHARACTER
	;

fragment REGULAR_STRING_LITERAL:
	SINGLE_REGULAR_STRING_LITERAL
	| SIMPLE_ESCAPE
	| HEX_ESCAPE
	| UNICODE_ESCAPE
	;

fragment SINGLE_REGULAR_STRING_LITERAL: ~(["\u0022\u005C] | [\r\n] | '\u0085' | '\u2028' | '\u2029');

fragment VARBATIM_STRING:
	'@"' VERBATIM_STRING_LITERALS? '"';

fragment VERBATIM_STRING_LITERALS:
	VERBATIM_STRING_LITERAL+
	| CHARACTER
	;

fragment VERBATIM_STRING_LITERAL:
	SINGLE_VERBATIM_STRING_LITERAL
	| QUOTE_ESCAPE
	;

fragment SINGLE_VERBATIM_STRING_LITERAL: ~'"';

fragment QUOTE_ESCAPE: '""';

/*
* NULL
*/

fragment NULL: 'null';

/*
* OPERATORS
*/

fragment UNICODE_ESCAPE:
	'\\u' HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT
	| '\\U' HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT HEX_DIGIT
	;

fragment OPERATOR_OR_PUNCTUATOR:
		'>>='|	'<<='|	'>>'|	'=>'|	'<<'|	'^='|	'|='|	'&='|	'%='
	|	'->' |	'==' |	'!='|	'<='|	'>='|	'+='|	'-='|	'*='|	'/='|	'??'
	|	'::' |	'++' |	'--'|	'&&'|	'||'|	'{'	|	'}'	|	'['	|	']'	|	'('	
	|	')'	 |	'.'	 |	','	|	':'	|	';' |	'+'	|	'-'	|	'*'	|	'/'	|	'%'	
	|	'&'	 |	'|'	 |	'^'	|	'!'	|	'~' |	'='	|	'<'	|	'>'	|	'?'
	;