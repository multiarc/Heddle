grammar Ttl;

/*
 * Parser Rules
 */

ttl: definition*
	| outblock*
	| rawoutput*
	| .*?
	;

definition: DefineStart def+ DefineEnd;

def: DefineNameStart name DefineNameEnd subtemplate typedefenition?;

name: ID
	| ID ':' BaseID
	;

typedefenition: '::' ID;

outblock: Out call (':' call)* subtemplate?;

subtemplate: TemplateStart ttl TempalteEnd;

call: ChainName? ParamStart call (':' call)* ParamEnd
	| ChainName? ParamStart ModelName? ParamEnd
	;

rawoutput: RawStart Anything RawEnd;

/*
 * Lexer Rules
 */

WS: [\r\n\t]+ -> channel(HIDDEN);
BaseID: IdWithDot;
ChainName: IdWithDot;
ModelName: IdWithoutDot;
ID: IdWithDot;
fragment IdWithDot : '@'?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\.\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]*;
fragment IdWithoutDot: '@'?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]*;
DefineNameStart: '<';
DefineNameEnd: '>';
ParamStart: '(';
ParamEnd: ')';
TemplateStart: '[';
TempalteEnd: ']';
DefineStart: '<%';
DefineEnd: '%>';
Out: '@';
RawStart: '@:';
RawEnd: ':@';
Anything: .*?;
Comment: '@*' .*? '*@' -> channel(HIDDEN);