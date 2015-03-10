lexer grammar CSharp;

/*
* C# Tokens
*/

fragment TOKEN:
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

fragment IDENTIFIER: IDENTIFIER_START IDENTIFIER_PART*;

fragment IDENTIFIER_START: 
	{IsLetter(InputStream.La(1))}? .
	| UNICODE_ESCAPE
	| '_'
	;

fragment IDENTIFIER_PART:
	{IsIdPart(InputStream.La(1))}? .
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