lexer grammar CSharp;

/*
* C# Tokens
*/

fragment TOKEN:
	KEYWORD
	| CONTEXTUAL_KEYWORD
	| OPERATOR_OR_PUNCTUATOR
	| STRING
	| CHAR
	| INT
	| REAL
	| VERBATIM_IDENTIFIER
	| IDENTIFIER
	;

fragment NEW_LINE:
	[\r\n] | '\u0085' | '\u2028' | '\u2029';

fragment WHITESPACE: NEW_LINE
	| SINGLE_LINE_WS
	;

 fragment SINGLE_LINE_WS:
	[ \t\f] 
	| '\u000B'
	| '\u00A0' 
	| '\u1680' 
	| '\u2000'..'\u200A' 
	| '\u202F' 
	| '\u205F' 
	| '\u3000'
	;

/*
* C# Keywords
*/

fragment KEYWORD:
	'abstract' | 'as' | 'base' | 'bool' | 'break'
	| 'byte' | 'case' | 'catch' | 'char' | 'checked'
	| 'class' | 'const' | 'continue' | 'decimal' | 'default'
	| 'delegate' | 'do' | 'double' | 'else' | 'enum'
	| 'event' | 'explicit' | 'extern' | 'false' | 'file' | 'finally'
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
* C# Contextual Keywords (§6.4.4)
*
* These are not reserved - they are identifiers in most contexts - so for token round-tripping it is
* immaterial whether they are matched here or as IDENTIFIER. They are listed for completeness against the
* spec; maximal munch still lets longer identifiers (e.g. 'varies', 'records') win over the keyword.
*/
fragment CONTEXTUAL_KEYWORD:
	'add' | 'alias' | 'allows' | 'and' | 'ascending' | 'async'
	| 'await' | 'by' | 'Cdecl' | 'descending' | 'dynamic'
	| 'equals' | 'extension' | 'Fastcall' | 'field' | 'from' | 'get' | 'global'
	| 'group' | 'init' | 'into' | 'join' | 'let'
	| 'managed' | 'nameof' | 'nint' | 'not' | 'notnull'
	| 'nuint' | 'on' | 'or' | 'orderby' | 'partial'
	| 'record' | 'remove' | 'required' | 'scoped' | 'select' | 'set' | 'Stdcall'
	| 'Thiscall' | 'unmanaged' | 'value' | 'var' | 'when'
	| 'where' | 'yield'
	;

/*
* C# Identifier
*/

fragment IDENTIFIER: IDENTIFIER_START IDENTIFIER_PART*;

// Verbatim identifier: '@' prefix lets a keyword be used as an identifier (e.g. @class, @new).
// Defined only for the C# TOKEN set so it does not affect Heddle's own '@'-based syntax.
fragment VERBATIM_IDENTIFIER: '@' IDENTIFIER;

// Identifier characters follow the Unicode categories from the C# spec (§6.4.3): a letter
// (Letter, subcat letter-number) or '_' to start; letters, decimal digits, connecting,
// combining and formatting characters to continue. (The previous rules were ASCII-only and
// wrongly allowed '+' in identifier-part, which mis-lexed e.g. 'a+b' as one identifier.)
fragment IDENTIFIER_START:
	[\p{L}\p{Nl}_]
	| UNICODE_ESCAPE
	;

fragment IDENTIFIER_PART:
	[\p{L}\p{Nl}\p{Nd}\p{Pc}\p{Mn}\p{Mc}\p{Cf}]
	| UNICODE_ESCAPE
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
	DEC_INT_LITERAL | HEX_INT_LITERAL | BIN_INT_LITERAL;

fragment DEC_INT_LITERAL:
	DEC_DIGITS INT_SUFFIX?;

// Digit separators ('_') are allowed between digits (C# 7+).
fragment DEC_DIGITS: DEC_DIGIT DECORATED_DEC_DIGIT*;

fragment DECORATED_DEC_DIGIT: '_'* DEC_DIGIT;

fragment DEC_DIGIT: '0'..'9';

fragment INT_SUFFIX:
	'UL' | 'Ul' | 'uL' | 'ul' | 'LU' | 'Lu' | 'lU' | 'lu'
	| 'U' | 'u' | 'L' | 'l'
	;

fragment HEX_INT_LITERAL: ('0x' | '0X') HEX_DIGITS INT_SUFFIX?;

fragment HEX_DIGITS: HEX_DIGIT DECORATED_HEX_DIGIT*;

fragment DECORATED_HEX_DIGIT: '_'* HEX_DIGIT;

fragment HEX_DIGIT:
	'0'..'9'
	| 'a'..'f'
	| 'A'..'F'
	;

fragment BIN_INT_LITERAL: ('0b' | '0B') BIN_DIGITS INT_SUFFIX?;

fragment BIN_DIGITS: BIN_DIGIT DECORATED_BIN_DIGIT*;

fragment DECORATED_BIN_DIGIT: '_'* BIN_DIGIT;

fragment BIN_DIGIT: '0' | '1';

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

fragment SINGLE_CHAR: ~([\u0027\u005C] | [\r\n] | '\u0085' | '\u2028' | '\u2029');

fragment SIMPLE_ESCAPE:
	'\\\'' | '\\"' | '\\\\' | '\\0' | '\\a' | '\\b' | '\\e' | '\\f' | '\\n' | '\\r' | '\\t' | '\\v';

fragment HEX_ESCAPE:
	'\\x' HEX_DIGIT HEX_DIGIT? HEX_DIGIT? HEX_DIGIT?;

/*
* STRING
*/

fragment STRING:
	REGULAR_STRING
	| VARBATIM_STRING
	;

fragment UTF8_SUFFIX: 'u8' | 'U8';

fragment REGULAR_STRING: '"' REGULAR_STRING_LITERALS? '"' UTF8_SUFFIX?;

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

fragment SINGLE_REGULAR_STRING_LITERAL: ~([\u0022\u005C] | [\r\n] | '\u0085' | '\u2028' | '\u2029');

fragment VARBATIM_STRING:
	'@"' VERBATIM_STRING_LITERALS? '"' UTF8_SUFFIX?;

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
* RAW STRING (C# 11)
*
* A raw string is delimited by 3 or more quotes, with the closing run equal in length to the opening run.
* The recursion below adds one quote to each side per step, so the open/close delimiters are always matched
* for ANY width without enumerating widths. The non-greedy body consumes the literal as ONE token, so its
* exact text round-trips to Roslyn and interior quotes/parens/braces cannot disturb Heddle's token balancing.
* The optional '$' prefix covers the interpolated raw forms ($"""...""", $$"""...""").
*/
fragment RAW_STRING_BODY: '$'* RAW_DELIMITED UTF8_SUFFIX?;

fragment RAW_DELIMITED: '"' RAW_DELIMITED '"' | '"""' .*? '"""';

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
		'>>>='|	'>>>'|	'>>='|	'<<='|	'>>'|	'=>'|	'<<'|	'^='|	'|='|	'&='|	'%='
	|	'->' |	'==' |	'!='|	'<='|	'>='|	'+='|	'-='|	'*='|	'/='|	'??='|	'??'
	|	'::' |	'++' |	'--'|	'&&'|	'||'|	'{'	|	'}'	|	'['	|	']'	|	'('
	|	')'	 |	'..' |	'.'	 |	','	|	':'	|	';' |	'+'	|	'-'	|	'*'	|	'/'	|	'%'
	|	'&'	 |	'|'	 |	'^'	|	'!'	|	'~' |	'='	|	'<'	|	'>'	|	'?'
	;