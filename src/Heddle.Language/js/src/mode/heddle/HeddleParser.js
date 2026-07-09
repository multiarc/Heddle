"use strict";
// Generated from HeddleParser.g4 by ANTLR 4.13.1
// jshint ignore: start
import antlr4 from "./antlr4/index.web";
import HeddleParserListener from './HeddleParserListener.js';
const serializedATN = [4,1,103,357,2,0,7,0,2,1,7,1,2,2,7,2,2,3,7,3,2,4,7,
4,2,5,7,5,2,6,7,6,2,7,7,7,2,8,7,8,2,9,7,9,2,10,7,10,2,11,7,11,2,12,7,12,
2,13,7,13,2,14,7,14,2,15,7,15,2,16,7,16,2,17,7,17,2,18,7,18,2,19,7,19,2,
20,7,20,2,21,7,21,2,22,7,22,2,23,7,23,2,24,7,24,2,25,7,25,2,26,7,26,1,0,
1,0,1,0,1,0,1,0,5,0,60,8,0,10,0,12,0,63,9,0,1,1,1,1,1,2,1,2,4,2,69,8,2,11,
2,12,2,70,1,2,1,2,1,3,1,3,1,3,3,3,78,8,3,1,3,3,3,81,8,3,1,3,1,3,3,3,85,8,
3,1,3,1,3,3,3,89,8,3,1,4,1,4,1,4,1,4,5,4,95,8,4,10,4,12,4,98,9,4,3,4,100,
8,4,1,4,1,4,1,5,1,5,3,5,106,8,5,1,6,1,6,1,6,1,6,3,6,112,8,6,1,7,1,7,1,7,
1,7,1,8,1,8,1,8,1,9,3,9,122,8,9,1,9,1,9,1,9,1,9,1,9,1,9,3,9,130,8,9,1,10,
1,10,1,10,1,11,1,11,1,11,1,12,1,12,1,12,1,13,1,13,5,13,143,8,13,10,13,12,
13,146,9,13,1,13,1,13,4,13,150,8,13,11,13,12,13,151,1,13,1,13,1,14,1,14,
1,14,3,14,159,8,14,1,15,1,15,1,15,5,15,164,8,15,10,15,12,15,167,9,15,1,16,
3,16,170,8,16,1,16,1,16,1,16,1,16,1,16,1,16,3,16,178,8,16,1,16,1,16,5,16,
182,8,16,10,16,12,16,185,9,16,1,16,3,16,188,8,16,1,16,1,16,3,16,192,8,16,
1,16,1,16,1,16,1,16,1,16,3,16,199,8,16,1,16,1,16,1,16,1,16,1,16,3,16,206,
8,16,1,16,1,16,1,16,1,16,3,16,212,8,16,1,16,1,16,1,16,5,16,217,8,16,10,16,
12,16,220,9,16,1,16,1,16,3,16,224,8,16,1,17,1,17,1,17,1,17,1,18,3,18,231,
8,18,1,18,1,18,1,18,5,18,236,8,18,10,18,12,18,239,9,18,1,19,1,19,1,20,4,
20,244,8,20,11,20,12,20,245,1,21,1,21,1,22,1,22,1,22,1,22,1,22,1,22,1,22,
1,22,1,22,1,22,1,22,1,22,3,22,262,8,22,1,22,3,22,265,8,22,1,22,1,22,1,22,
1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,
22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,
1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,22,1,
22,1,22,1,22,1,22,1,22,5,22,318,8,22,10,22,12,22,321,9,22,1,22,1,22,5,22,
325,8,22,10,22,12,22,328,9,22,1,23,1,23,1,23,1,23,5,23,334,8,23,10,23,12,
23,337,9,23,3,23,339,8,23,1,23,1,23,1,24,1,24,1,25,5,25,346,8,25,10,25,12,
25,349,9,25,1,25,1,25,1,25,1,25,1,26,1,26,1,26,0,1,44,27,0,2,4,6,8,10,12,
14,16,18,20,22,24,26,28,30,32,34,36,38,40,42,44,46,48,50,52,0,9,1,0,25,26,
2,0,41,42,49,50,1,0,43,45,1,0,41,42,1,0,35,36,1,0,37,40,1,0,33,34,1,0,22,
28,2,0,7,9,16,18,394,0,61,1,0,0,0,2,64,1,0,0,0,4,66,1,0,0,0,6,74,1,0,0,0,
8,90,1,0,0,0,10,105,1,0,0,0,12,107,1,0,0,0,14,113,1,0,0,0,16,117,1,0,0,0,
18,129,1,0,0,0,20,131,1,0,0,0,22,134,1,0,0,0,24,137,1,0,0,0,26,140,1,0,0,
0,28,155,1,0,0,0,30,160,1,0,0,0,32,223,1,0,0,0,34,225,1,0,0,0,36,230,1,0,
0,0,38,240,1,0,0,0,40,243,1,0,0,0,42,247,1,0,0,0,44,264,1,0,0,0,46,329,1,
0,0,0,48,342,1,0,0,0,50,347,1,0,0,0,52,354,1,0,0,0,54,60,3,4,2,0,55,60,3,
26,13,0,56,60,3,28,14,0,57,60,3,2,1,0,58,60,3,52,26,0,59,54,1,0,0,0,59,55,
1,0,0,0,59,56,1,0,0,0,59,57,1,0,0,0,59,58,1,0,0,0,60,63,1,0,0,0,61,59,1,
0,0,0,61,62,1,0,0,0,62,1,1,0,0,0,63,61,1,0,0,0,64,65,5,18,0,0,65,3,1,0,0,
0,66,68,5,16,0,0,67,69,3,6,3,0,68,67,1,0,0,0,69,70,1,0,0,0,70,68,1,0,0,0,
70,71,1,0,0,0,71,72,1,0,0,0,72,73,5,17,0,0,73,5,1,0,0,0,74,75,5,13,0,0,75,
77,5,4,0,0,76,78,3,8,4,0,77,76,1,0,0,0,77,78,1,0,0,0,78,80,1,0,0,0,79,81,
3,20,10,0,80,79,1,0,0,0,80,81,1,0,0,0,81,82,1,0,0,0,82,84,5,14,0,0,83,85,
3,24,12,0,84,83,1,0,0,0,84,85,1,0,0,0,85,86,1,0,0,0,86,88,3,50,25,0,87,89,
3,22,11,0,88,87,1,0,0,0,88,89,1,0,0,0,89,7,1,0,0,0,90,99,5,19,0,0,91,96,
3,10,5,0,92,93,5,53,0,0,93,95,3,10,5,0,94,92,1,0,0,0,95,98,1,0,0,0,96,94,
1,0,0,0,96,97,1,0,0,0,97,100,1,0,0,0,98,96,1,0,0,0,99,91,1,0,0,0,99,100,
1,0,0,0,100,101,1,0,0,0,101,102,5,20,0,0,102,9,1,0,0,0,103,106,3,12,6,0,
104,106,3,14,7,0,105,103,1,0,0,0,105,104,1,0,0,0,106,11,1,0,0,0,107,108,
5,4,0,0,108,109,5,15,0,0,109,111,5,4,0,0,110,112,3,16,8,0,111,110,1,0,0,
0,111,112,1,0,0,0,112,13,1,0,0,0,113,114,5,4,0,0,114,115,5,61,0,0,115,116,
5,4,0,0,116,15,1,0,0,0,117,118,5,55,0,0,118,119,3,18,9,0,119,17,1,0,0,0,
120,122,5,42,0,0,121,120,1,0,0,0,121,122,1,0,0,0,122,123,1,0,0,0,123,130,
7,0,0,0,124,130,5,27,0,0,125,130,5,28,0,0,126,130,5,22,0,0,127,130,5,23,
0,0,128,130,5,24,0,0,129,121,1,0,0,0,129,124,1,0,0,0,129,125,1,0,0,0,129,
126,1,0,0,0,129,127,1,0,0,0,129,128,1,0,0,0,130,19,1,0,0,0,131,132,5,15,
0,0,132,133,5,4,0,0,133,21,1,0,0,0,134,135,5,61,0,0,135,136,5,4,0,0,136,
23,1,0,0,0,137,138,5,21,0,0,138,139,3,30,15,0,139,25,1,0,0,0,140,144,5,3,
0,0,141,143,5,2,0,0,142,141,1,0,0,0,143,146,1,0,0,0,144,142,1,0,0,0,144,
145,1,0,0,0,145,147,1,0,0,0,146,144,1,0,0,0,147,149,5,8,0,0,148,150,3,52,
26,0,149,148,1,0,0,0,150,151,1,0,0,0,151,149,1,0,0,0,151,152,1,0,0,0,152,
153,1,0,0,0,153,154,5,9,0,0,154,27,1,0,0,0,155,156,5,7,0,0,156,158,3,30,
15,0,157,159,3,50,25,0,158,157,1,0,0,0,158,159,1,0,0,0,159,29,1,0,0,0,160,
165,3,32,16,0,161,162,5,15,0,0,162,164,3,32,16,0,163,161,1,0,0,0,164,167,
1,0,0,0,165,163,1,0,0,0,165,166,1,0,0,0,166,31,1,0,0,0,167,165,1,0,0,0,168,
170,3,38,19,0,169,168,1,0,0,0,169,170,1,0,0,0,170,171,1,0,0,0,171,172,5,
19,0,0,172,173,5,12,0,0,173,174,3,40,20,0,174,175,5,20,0,0,175,224,1,0,0,
0,176,178,3,38,19,0,177,176,1,0,0,0,177,178,1,0,0,0,178,179,1,0,0,0,179,
183,5,19,0,0,180,182,5,2,0,0,181,180,1,0,0,0,182,185,1,0,0,0,183,181,1,0,
0,0,183,184,1,0,0,0,184,187,1,0,0,0,185,183,1,0,0,0,186,188,3,36,18,0,187,
186,1,0,0,0,187,188,1,0,0,0,188,189,1,0,0,0,189,224,5,20,0,0,190,192,3,38,
19,0,191,190,1,0,0,0,191,192,1,0,0,0,192,193,1,0,0,0,193,194,5,19,0,0,194,
195,3,30,15,0,195,196,5,20,0,0,196,224,1,0,0,0,197,199,3,38,19,0,198,197,
1,0,0,0,198,199,1,0,0,0,199,200,1,0,0,0,200,201,5,19,0,0,201,202,3,42,21,
0,202,203,5,20,0,0,203,224,1,0,0,0,204,206,3,38,19,0,205,204,1,0,0,0,205,
206,1,0,0,0,206,207,1,0,0,0,207,211,5,19,0,0,208,209,3,44,22,0,209,210,5,
53,0,0,210,212,1,0,0,0,211,208,1,0,0,0,211,212,1,0,0,0,212,213,1,0,0,0,213,
218,3,34,17,0,214,215,5,53,0,0,215,217,3,34,17,0,216,214,1,0,0,0,217,220,
1,0,0,0,218,216,1,0,0,0,218,219,1,0,0,0,219,221,1,0,0,0,220,218,1,0,0,0,
221,222,5,20,0,0,222,224,1,0,0,0,223,169,1,0,0,0,223,177,1,0,0,0,223,191,
1,0,0,0,223,198,1,0,0,0,223,205,1,0,0,0,224,33,1,0,0,0,225,226,5,4,0,0,226,
227,5,15,0,0,227,228,3,44,22,0,228,35,1,0,0,0,229,231,5,5,0,0,230,229,1,
0,0,0,230,231,1,0,0,0,231,232,1,0,0,0,232,237,5,4,0,0,233,234,5,6,0,0,234,
236,5,4,0,0,235,233,1,0,0,0,236,239,1,0,0,0,237,235,1,0,0,0,237,238,1,0,
0,0,238,37,1,0,0,0,239,237,1,0,0,0,240,241,5,4,0,0,241,39,1,0,0,0,242,244,
5,11,0,0,243,242,1,0,0,0,244,245,1,0,0,0,245,243,1,0,0,0,245,246,1,0,0,0,
246,41,1,0,0,0,247,248,3,44,22,0,248,43,1,0,0,0,249,250,6,22,-1,0,250,251,
7,1,0,0,251,265,3,44,22,18,252,253,5,4,0,0,253,265,3,46,23,0,254,255,5,19,
0,0,255,256,3,44,22,0,256,257,5,20,0,0,257,265,1,0,0,0,258,265,3,48,24,0,
259,265,5,54,0,0,260,262,5,5,0,0,261,260,1,0,0,0,261,262,1,0,0,0,262,263,
1,0,0,0,263,265,5,4,0,0,264,249,1,0,0,0,264,252,1,0,0,0,264,254,1,0,0,0,
264,258,1,0,0,0,264,259,1,0,0,0,264,261,1,0,0,0,265,326,1,0,0,0,266,267,
10,17,0,0,267,268,7,2,0,0,268,325,3,44,22,18,269,270,10,16,0,0,270,271,7,
3,0,0,271,325,3,44,22,17,272,273,10,15,0,0,273,274,7,4,0,0,274,325,3,44,
22,16,275,276,10,14,0,0,276,277,7,5,0,0,277,325,3,44,22,15,278,279,10,13,
0,0,279,280,7,6,0,0,280,325,3,44,22,14,281,282,10,12,0,0,282,283,5,46,0,
0,283,325,3,44,22,13,284,285,10,11,0,0,285,286,5,48,0,0,286,325,3,44,22,
12,287,288,10,10,0,0,288,289,5,47,0,0,289,325,3,44,22,11,290,291,10,9,0,
0,291,292,5,31,0,0,292,325,3,44,22,10,293,294,10,8,0,0,294,295,5,32,0,0,
295,325,3,44,22,9,296,297,10,7,0,0,297,298,5,29,0,0,298,325,3,44,22,7,299,
300,10,6,0,0,300,301,5,30,0,0,301,302,3,44,22,0,302,303,5,15,0,0,303,304,
3,44,22,6,304,325,1,0,0,0,305,306,10,21,0,0,306,307,5,6,0,0,307,308,5,4,
0,0,308,325,3,46,23,0,309,310,10,20,0,0,310,311,5,6,0,0,311,325,5,4,0,0,
312,313,10,19,0,0,313,314,5,51,0,0,314,319,3,44,22,0,315,316,5,53,0,0,316,
318,3,44,22,0,317,315,1,0,0,0,318,321,1,0,0,0,319,317,1,0,0,0,319,320,1,
0,0,0,320,322,1,0,0,0,321,319,1,0,0,0,322,323,5,52,0,0,323,325,1,0,0,0,324,
266,1,0,0,0,324,269,1,0,0,0,324,272,1,0,0,0,324,275,1,0,0,0,324,278,1,0,
0,0,324,281,1,0,0,0,324,284,1,0,0,0,324,287,1,0,0,0,324,290,1,0,0,0,324,
293,1,0,0,0,324,296,1,0,0,0,324,299,1,0,0,0,324,305,1,0,0,0,324,309,1,0,
0,0,324,312,1,0,0,0,325,328,1,0,0,0,326,324,1,0,0,0,326,327,1,0,0,0,327,
45,1,0,0,0,328,326,1,0,0,0,329,338,5,19,0,0,330,335,3,44,22,0,331,332,5,
53,0,0,332,334,3,44,22,0,333,331,1,0,0,0,334,337,1,0,0,0,335,333,1,0,0,0,
335,336,1,0,0,0,336,339,1,0,0,0,337,335,1,0,0,0,338,330,1,0,0,0,338,339,
1,0,0,0,339,340,1,0,0,0,340,341,5,20,0,0,341,47,1,0,0,0,342,343,7,7,0,0,
343,49,1,0,0,0,344,346,5,2,0,0,345,344,1,0,0,0,346,349,1,0,0,0,347,345,1,
0,0,0,347,348,1,0,0,0,348,350,1,0,0,0,349,347,1,0,0,0,350,351,5,8,0,0,351,
352,3,0,0,0,352,353,5,9,0,0,353,51,1,0,0,0,354,355,8,8,0,0,355,53,1,0,0,
0,38,59,61,70,77,80,84,88,96,99,105,111,121,129,144,151,158,165,169,177,
183,187,191,198,205,211,218,223,230,237,245,261,264,319,324,326,335,338,
347];


const atn = new antlr4.atn.ATNDeserializer().deserialize(serializedATN);

const decisionsToDFA = atn.decisionToState.map( (ds, index) => new antlr4.dfa.DFA(ds, index) );

const sharedContextCache = new antlr4.atn.PredictionContextCache();

export default class HeddleParser extends antlr4.Parser {

    static grammarFileName = "HeddleParser.g4";
    static literalNames = [ null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, "'='", null, null, "'??'", "'?'", "'&&'", 
                            "'||'", "'=='", "'!='", "'<<'", "'>>'", "'<='", 
                            "'>='", "'<'", "'>'", "'+'", "'*'", "'/'", "'%'", 
                            "'&'", "'|'", "'^'", "'!'", "'~'", "'['", "']'", 
                            null, null, null, "'\"\"'", "'}'" ];
    static symbolicNames = [ null, "TEXT", "WS", "IMPORT_TOKEN", "ID", "ROOT_REF", 
                             "MEMBER_P", "OUT", "SUB_START", "SUB_CLOSE", 
                             "CSHARP_END", "CSHARP_TOKEN", "CSHARP_START", 
                             "DEF_STARTNAME", "DEF_ENDNAME", "DELIM", "DEF_START", 
                             "DEF_CLOSE", "RAW", "OUT_PARAMSTART", "OUT_PARAMEND", 
                             "DEF_OUT", "TRUE", "FALSE", "NULL", "INT_LIT", 
                             "REAL_LIT", "STRING_LIT", "CHAR_LIT", "OP_QQ", 
                             "OP_QUESTION", "OP_AND", "OP_OR", "OP_EQ", 
                             "OP_NEQ", "OP_LSHIFT", "OP_RSHIFT", "OP_LE", 
                             "OP_GE", "OP_LT", "OP_GT", "OP_PLUS", "OP_MINUS", 
                             "OP_STAR", "OP_SLASH", "OP_PERCENT", "OP_AMP", 
                             "OP_PIPE", "OP_CARET", "OP_NOT", "OP_TILDE", 
                             "LBRACKET", "RBRACKET", "COMMA", "THIS", "ASSIGN", 
                             "COMMENT", "SKIP_WS", "SUB_COMMENT", "SUB_SKIP_WS", 
                             "DEF_COMMENT", "DEF_TYPE", "DEF_WS", "DEFP_COMMENT", 
                             "DEFP_WS", "IMPORT_COMMENT", "CALL_RETURN_COMMENT", 
                             "CALL_SKIP_WS", "OUT_COMMENT", "OUT_SKIP_WS", 
                             "CALL_COMMENT", "CALL_WS", "CALL_UNKNOWN", 
                             "DEFP_ASSIGN", "DEFP_COMMA", "DEFP_MINUS", 
                             "CALL_OP_QQ", "CALL_OP_QUESTION", "CALL_OP_AND", 
                             "CALL_OP_OR", "CALL_OP_EQ", "CALL_OP_NEQ", 
                             "CALL_OP_LSHIFT", "CALL_OP_RSHIFT", "CALL_OP_LE", 
                             "CALL_OP_GE", "CALL_OP_LT", "CALL_OP_GT", "CALL_OP_PLUS", 
                             "CALL_OP_STAR", "CALL_OP_SLASH", "CALL_OP_PERCENT", 
                             "CALL_OP_AMP", "CALL_OP_PIPE", "CALL_OP_CARET", 
                             "CALL_OP_NOT", "CALL_OP_TILDE", "CALL_LBRACKET", 
                             "CALL_RBRACKET", "ISTR_OPEN_BRACE_ESC", "ISTR_CLOSE_BRACE_ESC", 
                             "ISTR_END", "IVSTR_QUOTE_ESC", "HOLE_CLOSE" ];
    static ruleNames = [ "heddle", "raw", "definition", "def", "def_props", 
                         "def_prop_item", "def_prop", "def_slot", "def_prop_default", 
                         "def_literal", "def_base", "def_type", "default_chain", 
                         "import_block", "outblock", "chain", "call", "named_argument", 
                         "member_expression", "extension_id", "csharp_expression", 
                         "native_expression", "expr", "arg_list", "literal", 
                         "subtemplate", "text" ];

    constructor(input) {
        super(input);
        this._interp = new antlr4.atn.ParserATNSimulator(this, atn, decisionsToDFA, sharedContextCache);
        this.ruleNames = HeddleParser.ruleNames;
        this.literalNames = HeddleParser.literalNames;
        this.symbolicNames = HeddleParser.symbolicNames;
    }

    sempred(localctx, ruleIndex, predIndex) {
    	switch(ruleIndex) {
    	case 22:
    	    		return this.expr_sempred(localctx, predIndex);
        default:
            throw "No predicate with index:" + ruleIndex;
       }
    }

    expr_sempred(localctx, predIndex) {
    	switch(predIndex) {
    		case 0:
    			return this.precpred(this._ctx, 17);
    		case 1:
    			return this.precpred(this._ctx, 16);
    		case 2:
    			return this.precpred(this._ctx, 15);
    		case 3:
    			return this.precpred(this._ctx, 14);
    		case 4:
    			return this.precpred(this._ctx, 13);
    		case 5:
    			return this.precpred(this._ctx, 12);
    		case 6:
    			return this.precpred(this._ctx, 11);
    		case 7:
    			return this.precpred(this._ctx, 10);
    		case 8:
    			return this.precpred(this._ctx, 9);
    		case 9:
    			return this.precpred(this._ctx, 8);
    		case 10:
    			return this.precpred(this._ctx, 7);
    		case 11:
    			return this.precpred(this._ctx, 6);
    		case 12:
    			return this.precpred(this._ctx, 21);
    		case 13:
    			return this.precpred(this._ctx, 20);
    		case 14:
    			return this.precpred(this._ctx, 19);
    		default:
    			throw "No predicate with index:" + predIndex;
    	}
    };




	heddle() {
	    let localctx = new HeddleContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 0, HeddleParser.RULE_heddle);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 61;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while((((_la) & ~0x1f) === 0 && ((1 << _la) & 4294835454) !== 0) || ((((_la - 32)) & ~0x1f) === 0 && ((1 << (_la - 32)) & 4294967295) !== 0) || ((((_la - 64)) & ~0x1f) === 0 && ((1 << (_la - 64)) & 4294967295) !== 0) || ((((_la - 96)) & ~0x1f) === 0 && ((1 << (_la - 96)) & 255) !== 0)) {
	            this.state = 59;
	            this._errHandler.sync(this);
	            var la_ = this._interp.adaptivePredict(this._input,0,this._ctx);
	            switch(la_) {
	            case 1:
	                this.state = 54;
	                this.definition();
	                break;

	            case 2:
	                this.state = 55;
	                this.import_block();
	                break;

	            case 3:
	                this.state = 56;
	                this.outblock();
	                break;

	            case 4:
	                this.state = 57;
	                this.raw();
	                break;

	            case 5:
	                this.state = 58;
	                this.text();
	                break;

	            }
	            this.state = 63;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	raw() {
	    let localctx = new RawContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 2, HeddleParser.RULE_raw);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 64;
	        this.match(HeddleParser.RAW);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	definition() {
	    let localctx = new DefinitionContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 4, HeddleParser.RULE_definition);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 66;
	        this.match(HeddleParser.DEF_START);
	        this.state = 68; 
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        do {
	            this.state = 67;
	            this.def();
	            this.state = 70; 
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        } while(_la===13);
	        this.state = 72;
	        this.match(HeddleParser.DEF_CLOSE);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def() {
	    let localctx = new DefContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 6, HeddleParser.RULE_def);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 74;
	        this.match(HeddleParser.DEF_STARTNAME);
	        this.state = 75;
	        this.match(HeddleParser.ID);
	        this.state = 77;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===19) {
	            this.state = 76;
	            this.def_props();
	        }

	        this.state = 80;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===15) {
	            this.state = 79;
	            this.def_base();
	        }

	        this.state = 82;
	        this.match(HeddleParser.DEF_ENDNAME);
	        this.state = 84;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===21) {
	            this.state = 83;
	            this.default_chain();
	        }

	        this.state = 86;
	        this.subtemplate();
	        this.state = 88;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===61) {
	            this.state = 87;
	            this.def_type();
	        }

	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def_props() {
	    let localctx = new Def_propsContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 8, HeddleParser.RULE_def_props);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 90;
	        this.match(HeddleParser.OUT_PARAMSTART);
	        this.state = 99;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===4) {
	            this.state = 91;
	            this.def_prop_item();
	            this.state = 96;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===53) {
	                this.state = 92;
	                this.match(HeddleParser.COMMA);
	                this.state = 93;
	                this.def_prop_item();
	                this.state = 98;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	        }

	        this.state = 101;
	        this.match(HeddleParser.OUT_PARAMEND);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def_prop_item() {
	    let localctx = new Def_prop_itemContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 10, HeddleParser.RULE_def_prop_item);
	    try {
	        this.state = 105;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,9,this._ctx);
	        switch(la_) {
	        case 1:
	            this.enterOuterAlt(localctx, 1);
	            this.state = 103;
	            this.def_prop();
	            break;

	        case 2:
	            this.enterOuterAlt(localctx, 2);
	            this.state = 104;
	            this.def_slot();
	            break;

	        }
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def_prop() {
	    let localctx = new Def_propContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 12, HeddleParser.RULE_def_prop);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 107;
	        this.match(HeddleParser.ID);
	        this.state = 108;
	        this.match(HeddleParser.DELIM);
	        this.state = 109;
	        this.match(HeddleParser.ID);
	        this.state = 111;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===55) {
	            this.state = 110;
	            this.def_prop_default();
	        }

	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def_slot() {
	    let localctx = new Def_slotContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 14, HeddleParser.RULE_def_slot);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 113;
	        this.match(HeddleParser.ID);
	        this.state = 114;
	        this.match(HeddleParser.DEF_TYPE);
	        this.state = 115;
	        this.match(HeddleParser.ID);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def_prop_default() {
	    let localctx = new Def_prop_defaultContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 16, HeddleParser.RULE_def_prop_default);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 117;
	        this.match(HeddleParser.ASSIGN);
	        this.state = 118;
	        this.def_literal();
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def_literal() {
	    let localctx = new Def_literalContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 18, HeddleParser.RULE_def_literal);
	    var _la = 0;
	    try {
	        this.state = 129;
	        this._errHandler.sync(this);
	        switch(this._input.LA(1)) {
	        case 25:
	        case 26:
	        case 42:
	            this.enterOuterAlt(localctx, 1);
	            this.state = 121;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===42) {
	                this.state = 120;
	                this.match(HeddleParser.OP_MINUS);
	            }

	            this.state = 123;
	            _la = this._input.LA(1);
	            if(!(_la===25 || _la===26)) {
	            this._errHandler.recoverInline(this);
	            }
	            else {
	            	this._errHandler.reportMatch(this);
	                this.consume();
	            }
	            break;
	        case 27:
	            this.enterOuterAlt(localctx, 2);
	            this.state = 124;
	            this.match(HeddleParser.STRING_LIT);
	            break;
	        case 28:
	            this.enterOuterAlt(localctx, 3);
	            this.state = 125;
	            this.match(HeddleParser.CHAR_LIT);
	            break;
	        case 22:
	            this.enterOuterAlt(localctx, 4);
	            this.state = 126;
	            this.match(HeddleParser.TRUE);
	            break;
	        case 23:
	            this.enterOuterAlt(localctx, 5);
	            this.state = 127;
	            this.match(HeddleParser.FALSE);
	            break;
	        case 24:
	            this.enterOuterAlt(localctx, 6);
	            this.state = 128;
	            this.match(HeddleParser.NULL);
	            break;
	        default:
	            throw new antlr4.error.NoViableAltException(this);
	        }
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def_base() {
	    let localctx = new Def_baseContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 20, HeddleParser.RULE_def_base);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 131;
	        this.match(HeddleParser.DELIM);
	        this.state = 132;
	        this.match(HeddleParser.ID);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	def_type() {
	    let localctx = new Def_typeContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 22, HeddleParser.RULE_def_type);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 134;
	        this.match(HeddleParser.DEF_TYPE);
	        this.state = 135;
	        this.match(HeddleParser.ID);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	default_chain() {
	    let localctx = new Default_chainContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 24, HeddleParser.RULE_default_chain);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 137;
	        this.match(HeddleParser.DEF_OUT);
	        this.state = 138;
	        this.chain();
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	import_block() {
	    let localctx = new Import_blockContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 26, HeddleParser.RULE_import_block);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 140;
	        this.match(HeddleParser.IMPORT_TOKEN);
	        this.state = 144;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 141;
	            this.match(HeddleParser.WS);
	            this.state = 146;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	        this.state = 147;
	        this.match(HeddleParser.SUB_START);
	        this.state = 149; 
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        do {
	            this.state = 148;
	            this.text();
	            this.state = 151; 
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        } while((((_la) & ~0x1f) === 0 && ((1 << _la) & 4294507646) !== 0) || ((((_la - 32)) & ~0x1f) === 0 && ((1 << (_la - 32)) & 4294967295) !== 0) || ((((_la - 64)) & ~0x1f) === 0 && ((1 << (_la - 64)) & 4294967295) !== 0) || ((((_la - 96)) & ~0x1f) === 0 && ((1 << (_la - 96)) & 255) !== 0));
	        this.state = 153;
	        this.match(HeddleParser.SUB_CLOSE);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	outblock() {
	    let localctx = new OutblockContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 28, HeddleParser.RULE_outblock);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 155;
	        this.match(HeddleParser.OUT);
	        this.state = 156;
	        this.chain();
	        this.state = 158;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,15,this._ctx);
	        if(la_===1) {
	            this.state = 157;
	            this.subtemplate();

	        }
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	chain() {
	    let localctx = new ChainContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 30, HeddleParser.RULE_chain);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 160;
	        this.call();
	        this.state = 165;
	        this._errHandler.sync(this);
	        var _alt = this._interp.adaptivePredict(this._input,16,this._ctx)
	        while(_alt!=2 && _alt!=antlr4.atn.ATN.INVALID_ALT_NUMBER) {
	            if(_alt===1) {
	                this.state = 161;
	                this.match(HeddleParser.DELIM);
	                this.state = 162;
	                this.call(); 
	            }
	            this.state = 167;
	            this._errHandler.sync(this);
	            _alt = this._interp.adaptivePredict(this._input,16,this._ctx);
	        }

	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	call() {
	    let localctx = new CallContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 32, HeddleParser.RULE_call);
	    var _la = 0;
	    try {
	        this.state = 223;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,26,this._ctx);
	        switch(la_) {
	        case 1:
	            this.enterOuterAlt(localctx, 1);
	            this.state = 169;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 168;
	                this.extension_id();
	            }

	            this.state = 171;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 172;
	            this.match(HeddleParser.CSHARP_START);
	            this.state = 173;
	            this.csharp_expression();
	            this.state = 174;
	            this.match(HeddleParser.OUT_PARAMEND);
	            break;

	        case 2:
	            this.enterOuterAlt(localctx, 2);
	            this.state = 177;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 176;
	                this.extension_id();
	            }

	            this.state = 179;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 183;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===2) {
	                this.state = 180;
	                this.match(HeddleParser.WS);
	                this.state = 185;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 187;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4 || _la===5) {
	                this.state = 186;
	                this.member_expression();
	            }

	            this.state = 189;
	            this.match(HeddleParser.OUT_PARAMEND);
	            break;

	        case 3:
	            this.enterOuterAlt(localctx, 3);
	            this.state = 191;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 190;
	                this.extension_id();
	            }

	            this.state = 193;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 194;
	            this.chain();
	            this.state = 195;
	            this.match(HeddleParser.OUT_PARAMEND);
	            break;

	        case 4:
	            this.enterOuterAlt(localctx, 4);
	            this.state = 198;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 197;
	                this.extension_id();
	            }

	            this.state = 200;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 201;
	            this.native_expression();
	            this.state = 202;
	            this.match(HeddleParser.OUT_PARAMEND);
	            break;

	        case 5:
	            this.enterOuterAlt(localctx, 5);
	            this.state = 205;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 204;
	                this.extension_id();
	            }

	            this.state = 207;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 211;
	            this._errHandler.sync(this);
	            var la_ = this._interp.adaptivePredict(this._input,24,this._ctx);
	            if(la_===1) {
	                this.state = 208;
	                this.expr(0);
	                this.state = 209;
	                this.match(HeddleParser.COMMA);

	            }
	            this.state = 213;
	            this.named_argument();
	            this.state = 218;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===53) {
	                this.state = 214;
	                this.match(HeddleParser.COMMA);
	                this.state = 215;
	                this.named_argument();
	                this.state = 220;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 221;
	            this.match(HeddleParser.OUT_PARAMEND);
	            break;

	        }
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	named_argument() {
	    let localctx = new Named_argumentContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 34, HeddleParser.RULE_named_argument);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 225;
	        this.match(HeddleParser.ID);
	        this.state = 226;
	        this.match(HeddleParser.DELIM);
	        this.state = 227;
	        this.expr(0);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	member_expression() {
	    let localctx = new Member_expressionContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 36, HeddleParser.RULE_member_expression);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 230;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===5) {
	            this.state = 229;
	            this.match(HeddleParser.ROOT_REF);
	        }

	        this.state = 232;
	        this.match(HeddleParser.ID);
	        this.state = 237;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===6) {
	            this.state = 233;
	            this.match(HeddleParser.MEMBER_P);
	            this.state = 234;
	            this.match(HeddleParser.ID);
	            this.state = 239;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	extension_id() {
	    let localctx = new Extension_idContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 38, HeddleParser.RULE_extension_id);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 240;
	        this.match(HeddleParser.ID);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	csharp_expression() {
	    let localctx = new Csharp_expressionContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 40, HeddleParser.RULE_csharp_expression);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 243; 
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        do {
	            this.state = 242;
	            this.match(HeddleParser.CSHARP_TOKEN);
	            this.state = 245; 
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        } while(_la===11);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	native_expression() {
	    let localctx = new Native_expressionContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 42, HeddleParser.RULE_native_expression);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 247;
	        this.expr(0);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}


	expr(_p) {
		if(_p===undefined) {
		    _p = 0;
		}
	    const _parentctx = this._ctx;
	    const _parentState = this.state;
	    let localctx = new ExprContext(this, this._ctx, _parentState);
	    let _prevctx = localctx;
	    const _startState = 44;
	    this.enterRecursionRule(localctx, 44, HeddleParser.RULE_expr, _p);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 264;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,31,this._ctx);
	        switch(la_) {
	        case 1:
	            localctx = new UnaryExprContext(this, localctx);
	            this._ctx = localctx;
	            _prevctx = localctx;

	            this.state = 250;
	            localctx.op = this._input.LT(1);
	            _la = this._input.LA(1);
	            if(!(((((_la - 41)) & ~0x1f) === 0 && ((1 << (_la - 41)) & 771) !== 0))) {
	                localctx.op = this._errHandler.recoverInline(this);
	            }
	            else {
	            	this._errHandler.reportMatch(this);
	                this.consume();
	            }
	            this.state = 251;
	            this.expr(18);
	            break;

	        case 2:
	            localctx = new FunctionCallExprContext(this, localctx);
	            this._ctx = localctx;
	            _prevctx = localctx;
	            this.state = 252;
	            this.match(HeddleParser.ID);
	            this.state = 253;
	            this.arg_list();
	            break;

	        case 3:
	            localctx = new GroupExprContext(this, localctx);
	            this._ctx = localctx;
	            _prevctx = localctx;
	            this.state = 254;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 255;
	            this.expr(0);
	            this.state = 256;
	            this.match(HeddleParser.OUT_PARAMEND);
	            break;

	        case 4:
	            localctx = new LiteralExprContext(this, localctx);
	            this._ctx = localctx;
	            _prevctx = localctx;
	            this.state = 258;
	            this.literal();
	            break;

	        case 5:
	            localctx = new ThisExprContext(this, localctx);
	            this._ctx = localctx;
	            _prevctx = localctx;
	            this.state = 259;
	            this.match(HeddleParser.THIS);
	            break;

	        case 6:
	            localctx = new PathRootExprContext(this, localctx);
	            this._ctx = localctx;
	            _prevctx = localctx;
	            this.state = 261;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===5) {
	                this.state = 260;
	                this.match(HeddleParser.ROOT_REF);
	            }

	            this.state = 263;
	            this.match(HeddleParser.ID);
	            break;

	        }
	        this._ctx.stop = this._input.LT(-1);
	        this.state = 326;
	        this._errHandler.sync(this);
	        var _alt = this._interp.adaptivePredict(this._input,34,this._ctx)
	        while(_alt!=2 && _alt!=antlr4.atn.ATN.INVALID_ALT_NUMBER) {
	            if(_alt===1) {
	                if(this._parseListeners!==null) {
	                    this.triggerExitRuleEvent();
	                }
	                _prevctx = localctx;
	                this.state = 324;
	                this._errHandler.sync(this);
	                var la_ = this._interp.adaptivePredict(this._input,33,this._ctx);
	                switch(la_) {
	                case 1:
	                    localctx = new MultiplicativeExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 266;
	                    if (!( this.precpred(this._ctx, 17))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 17)");
	                    }
	                    this.state = 267;
	                    localctx.op = this._input.LT(1);
	                    _la = this._input.LA(1);
	                    if(!(((((_la - 43)) & ~0x1f) === 0 && ((1 << (_la - 43)) & 7) !== 0))) {
	                        localctx.op = this._errHandler.recoverInline(this);
	                    }
	                    else {
	                    	this._errHandler.reportMatch(this);
	                        this.consume();
	                    }
	                    this.state = 268;
	                    this.expr(18);
	                    break;

	                case 2:
	                    localctx = new AdditiveExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 269;
	                    if (!( this.precpred(this._ctx, 16))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 16)");
	                    }
	                    this.state = 270;
	                    localctx.op = this._input.LT(1);
	                    _la = this._input.LA(1);
	                    if(!(_la===41 || _la===42)) {
	                        localctx.op = this._errHandler.recoverInline(this);
	                    }
	                    else {
	                    	this._errHandler.reportMatch(this);
	                        this.consume();
	                    }
	                    this.state = 271;
	                    this.expr(17);
	                    break;

	                case 3:
	                    localctx = new ShiftExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 272;
	                    if (!( this.precpred(this._ctx, 15))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 15)");
	                    }
	                    this.state = 273;
	                    localctx.op = this._input.LT(1);
	                    _la = this._input.LA(1);
	                    if(!(_la===35 || _la===36)) {
	                        localctx.op = this._errHandler.recoverInline(this);
	                    }
	                    else {
	                    	this._errHandler.reportMatch(this);
	                        this.consume();
	                    }
	                    this.state = 274;
	                    this.expr(16);
	                    break;

	                case 4:
	                    localctx = new RelationalExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 275;
	                    if (!( this.precpred(this._ctx, 14))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 14)");
	                    }
	                    this.state = 276;
	                    localctx.op = this._input.LT(1);
	                    _la = this._input.LA(1);
	                    if(!(((((_la - 37)) & ~0x1f) === 0 && ((1 << (_la - 37)) & 15) !== 0))) {
	                        localctx.op = this._errHandler.recoverInline(this);
	                    }
	                    else {
	                    	this._errHandler.reportMatch(this);
	                        this.consume();
	                    }
	                    this.state = 277;
	                    this.expr(15);
	                    break;

	                case 5:
	                    localctx = new EqualityExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 278;
	                    if (!( this.precpred(this._ctx, 13))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 13)");
	                    }
	                    this.state = 279;
	                    localctx.op = this._input.LT(1);
	                    _la = this._input.LA(1);
	                    if(!(_la===33 || _la===34)) {
	                        localctx.op = this._errHandler.recoverInline(this);
	                    }
	                    else {
	                    	this._errHandler.reportMatch(this);
	                        this.consume();
	                    }
	                    this.state = 280;
	                    this.expr(14);
	                    break;

	                case 6:
	                    localctx = new BitAndExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 281;
	                    if (!( this.precpred(this._ctx, 12))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 12)");
	                    }
	                    this.state = 282;
	                    this.match(HeddleParser.OP_AMP);
	                    this.state = 283;
	                    this.expr(13);
	                    break;

	                case 7:
	                    localctx = new BitXorExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 284;
	                    if (!( this.precpred(this._ctx, 11))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 11)");
	                    }
	                    this.state = 285;
	                    this.match(HeddleParser.OP_CARET);
	                    this.state = 286;
	                    this.expr(12);
	                    break;

	                case 8:
	                    localctx = new BitOrExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 287;
	                    if (!( this.precpred(this._ctx, 10))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 10)");
	                    }
	                    this.state = 288;
	                    this.match(HeddleParser.OP_PIPE);
	                    this.state = 289;
	                    this.expr(11);
	                    break;

	                case 9:
	                    localctx = new AndAlsoExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 290;
	                    if (!( this.precpred(this._ctx, 9))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 9)");
	                    }
	                    this.state = 291;
	                    this.match(HeddleParser.OP_AND);
	                    this.state = 292;
	                    this.expr(10);
	                    break;

	                case 10:
	                    localctx = new OrElseExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 293;
	                    if (!( this.precpred(this._ctx, 8))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 8)");
	                    }
	                    this.state = 294;
	                    this.match(HeddleParser.OP_OR);
	                    this.state = 295;
	                    this.expr(9);
	                    break;

	                case 11:
	                    localctx = new CoalesceExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 296;
	                    if (!( this.precpred(this._ctx, 7))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 7)");
	                    }
	                    this.state = 297;
	                    this.match(HeddleParser.OP_QQ);
	                    this.state = 298;
	                    this.expr(7);
	                    break;

	                case 12:
	                    localctx = new TernaryExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 299;
	                    if (!( this.precpred(this._ctx, 6))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 6)");
	                    }
	                    this.state = 300;
	                    this.match(HeddleParser.OP_QUESTION);
	                    this.state = 301;
	                    this.expr(0);
	                    this.state = 302;
	                    this.match(HeddleParser.DELIM);
	                    this.state = 303;
	                    this.expr(6);
	                    break;

	                case 13:
	                    localctx = new MethodCallExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 305;
	                    if (!( this.precpred(this._ctx, 21))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 21)");
	                    }
	                    this.state = 306;
	                    this.match(HeddleParser.MEMBER_P);
	                    this.state = 307;
	                    this.match(HeddleParser.ID);
	                    this.state = 308;
	                    this.arg_list();
	                    break;

	                case 14:
	                    localctx = new MemberHopExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 309;
	                    if (!( this.precpred(this._ctx, 20))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 20)");
	                    }
	                    this.state = 310;
	                    this.match(HeddleParser.MEMBER_P);
	                    this.state = 311;
	                    this.match(HeddleParser.ID);
	                    break;

	                case 15:
	                    localctx = new IndexExprContext(this, new ExprContext(this, _parentctx, _parentState));
	                    this.pushNewRecursionContext(localctx, _startState, HeddleParser.RULE_expr);
	                    this.state = 312;
	                    if (!( this.precpred(this._ctx, 19))) {
	                        throw new antlr4.error.FailedPredicateException(this, "this.precpred(this._ctx, 19)");
	                    }
	                    this.state = 313;
	                    this.match(HeddleParser.LBRACKET);
	                    this.state = 314;
	                    this.expr(0);
	                    this.state = 319;
	                    this._errHandler.sync(this);
	                    _la = this._input.LA(1);
	                    while(_la===53) {
	                        this.state = 315;
	                        this.match(HeddleParser.COMMA);
	                        this.state = 316;
	                        this.expr(0);
	                        this.state = 321;
	                        this._errHandler.sync(this);
	                        _la = this._input.LA(1);
	                    }
	                    this.state = 322;
	                    this.match(HeddleParser.RBRACKET);
	                    break;

	                } 
	            }
	            this.state = 328;
	            this._errHandler.sync(this);
	            _alt = this._interp.adaptivePredict(this._input,34,this._ctx);
	        }

	    } catch( error) {
	        if(error instanceof antlr4.error.RecognitionException) {
		        localctx.exception = error;
		        this._errHandler.reportError(this, error);
		        this._errHandler.recover(this, error);
		    } else {
		    	throw error;
		    }
	    } finally {
	        this.unrollRecursionContexts(_parentctx)
	    }
	    return localctx;
	}



	arg_list() {
	    let localctx = new Arg_listContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 46, HeddleParser.RULE_arg_list);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 329;
	        this.match(HeddleParser.OUT_PARAMSTART);
	        this.state = 338;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if((((_la) & ~0x1f) === 0 && ((1 << _la) & 533200944) !== 0) || ((((_la - 41)) & ~0x1f) === 0 && ((1 << (_la - 41)) & 8963) !== 0)) {
	            this.state = 330;
	            this.expr(0);
	            this.state = 335;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===53) {
	                this.state = 331;
	                this.match(HeddleParser.COMMA);
	                this.state = 332;
	                this.expr(0);
	                this.state = 337;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	        }

	        this.state = 340;
	        this.match(HeddleParser.OUT_PARAMEND);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	literal() {
	    let localctx = new LiteralContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 48, HeddleParser.RULE_literal);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 342;
	        _la = this._input.LA(1);
	        if(!((((_la) & ~0x1f) === 0 && ((1 << _la) & 532676608) !== 0))) {
	        this._errHandler.recoverInline(this);
	        }
	        else {
	        	this._errHandler.reportMatch(this);
	            this.consume();
	        }
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	subtemplate() {
	    let localctx = new SubtemplateContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 50, HeddleParser.RULE_subtemplate);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 347;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 344;
	            this.match(HeddleParser.WS);
	            this.state = 349;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	        this.state = 350;
	        this.match(HeddleParser.SUB_START);
	        this.state = 351;
	        this.heddle();
	        this.state = 352;
	        this.match(HeddleParser.SUB_CLOSE);
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}



	text() {
	    let localctx = new TextContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 52, HeddleParser.RULE_text);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 354;
	        _la = this._input.LA(1);
	        if(_la<=0 || (((_la) & ~0x1f) === 0 && ((1 << _la) & 459648) !== 0)) {
	        this._errHandler.recoverInline(this);
	        }
	        else {
	        	this._errHandler.reportMatch(this);
	            this.consume();
	        }
	    } catch (re) {
	    	if(re instanceof antlr4.error.RecognitionException) {
		        localctx.exception = re;
		        this._errHandler.reportError(this, re);
		        this._errHandler.recover(this, re);
		    } else {
		    	throw re;
		    }
	    } finally {
	        this.exitRule();
	    }
	    return localctx;
	}


}

HeddleParser.EOF = antlr4.Token.EOF;
HeddleParser.TEXT = 1;
HeddleParser.WS = 2;
HeddleParser.IMPORT_TOKEN = 3;
HeddleParser.ID = 4;
HeddleParser.ROOT_REF = 5;
HeddleParser.MEMBER_P = 6;
HeddleParser.OUT = 7;
HeddleParser.SUB_START = 8;
HeddleParser.SUB_CLOSE = 9;
HeddleParser.CSHARP_END = 10;
HeddleParser.CSHARP_TOKEN = 11;
HeddleParser.CSHARP_START = 12;
HeddleParser.DEF_STARTNAME = 13;
HeddleParser.DEF_ENDNAME = 14;
HeddleParser.DELIM = 15;
HeddleParser.DEF_START = 16;
HeddleParser.DEF_CLOSE = 17;
HeddleParser.RAW = 18;
HeddleParser.OUT_PARAMSTART = 19;
HeddleParser.OUT_PARAMEND = 20;
HeddleParser.DEF_OUT = 21;
HeddleParser.TRUE = 22;
HeddleParser.FALSE = 23;
HeddleParser.NULL = 24;
HeddleParser.INT_LIT = 25;
HeddleParser.REAL_LIT = 26;
HeddleParser.STRING_LIT = 27;
HeddleParser.CHAR_LIT = 28;
HeddleParser.OP_QQ = 29;
HeddleParser.OP_QUESTION = 30;
HeddleParser.OP_AND = 31;
HeddleParser.OP_OR = 32;
HeddleParser.OP_EQ = 33;
HeddleParser.OP_NEQ = 34;
HeddleParser.OP_LSHIFT = 35;
HeddleParser.OP_RSHIFT = 36;
HeddleParser.OP_LE = 37;
HeddleParser.OP_GE = 38;
HeddleParser.OP_LT = 39;
HeddleParser.OP_GT = 40;
HeddleParser.OP_PLUS = 41;
HeddleParser.OP_MINUS = 42;
HeddleParser.OP_STAR = 43;
HeddleParser.OP_SLASH = 44;
HeddleParser.OP_PERCENT = 45;
HeddleParser.OP_AMP = 46;
HeddleParser.OP_PIPE = 47;
HeddleParser.OP_CARET = 48;
HeddleParser.OP_NOT = 49;
HeddleParser.OP_TILDE = 50;
HeddleParser.LBRACKET = 51;
HeddleParser.RBRACKET = 52;
HeddleParser.COMMA = 53;
HeddleParser.THIS = 54;
HeddleParser.ASSIGN = 55;
HeddleParser.COMMENT = 56;
HeddleParser.SKIP_WS = 57;
HeddleParser.SUB_COMMENT = 58;
HeddleParser.SUB_SKIP_WS = 59;
HeddleParser.DEF_COMMENT = 60;
HeddleParser.DEF_TYPE = 61;
HeddleParser.DEF_WS = 62;
HeddleParser.DEFP_COMMENT = 63;
HeddleParser.DEFP_WS = 64;
HeddleParser.IMPORT_COMMENT = 65;
HeddleParser.CALL_RETURN_COMMENT = 66;
HeddleParser.CALL_SKIP_WS = 67;
HeddleParser.OUT_COMMENT = 68;
HeddleParser.OUT_SKIP_WS = 69;
HeddleParser.CALL_COMMENT = 70;
HeddleParser.CALL_WS = 71;
HeddleParser.CALL_UNKNOWN = 72;
HeddleParser.DEFP_ASSIGN = 73;
HeddleParser.DEFP_COMMA = 74;
HeddleParser.DEFP_MINUS = 75;
HeddleParser.CALL_OP_QQ = 76;
HeddleParser.CALL_OP_QUESTION = 77;
HeddleParser.CALL_OP_AND = 78;
HeddleParser.CALL_OP_OR = 79;
HeddleParser.CALL_OP_EQ = 80;
HeddleParser.CALL_OP_NEQ = 81;
HeddleParser.CALL_OP_LSHIFT = 82;
HeddleParser.CALL_OP_RSHIFT = 83;
HeddleParser.CALL_OP_LE = 84;
HeddleParser.CALL_OP_GE = 85;
HeddleParser.CALL_OP_LT = 86;
HeddleParser.CALL_OP_GT = 87;
HeddleParser.CALL_OP_PLUS = 88;
HeddleParser.CALL_OP_STAR = 89;
HeddleParser.CALL_OP_SLASH = 90;
HeddleParser.CALL_OP_PERCENT = 91;
HeddleParser.CALL_OP_AMP = 92;
HeddleParser.CALL_OP_PIPE = 93;
HeddleParser.CALL_OP_CARET = 94;
HeddleParser.CALL_OP_NOT = 95;
HeddleParser.CALL_OP_TILDE = 96;
HeddleParser.CALL_LBRACKET = 97;
HeddleParser.CALL_RBRACKET = 98;
HeddleParser.ISTR_OPEN_BRACE_ESC = 99;
HeddleParser.ISTR_CLOSE_BRACE_ESC = 100;
HeddleParser.ISTR_END = 101;
HeddleParser.IVSTR_QUOTE_ESC = 102;
HeddleParser.HOLE_CLOSE = 103;

HeddleParser.RULE_heddle = 0;
HeddleParser.RULE_raw = 1;
HeddleParser.RULE_definition = 2;
HeddleParser.RULE_def = 3;
HeddleParser.RULE_def_props = 4;
HeddleParser.RULE_def_prop_item = 5;
HeddleParser.RULE_def_prop = 6;
HeddleParser.RULE_def_slot = 7;
HeddleParser.RULE_def_prop_default = 8;
HeddleParser.RULE_def_literal = 9;
HeddleParser.RULE_def_base = 10;
HeddleParser.RULE_def_type = 11;
HeddleParser.RULE_default_chain = 12;
HeddleParser.RULE_import_block = 13;
HeddleParser.RULE_outblock = 14;
HeddleParser.RULE_chain = 15;
HeddleParser.RULE_call = 16;
HeddleParser.RULE_named_argument = 17;
HeddleParser.RULE_member_expression = 18;
HeddleParser.RULE_extension_id = 19;
HeddleParser.RULE_csharp_expression = 20;
HeddleParser.RULE_native_expression = 21;
HeddleParser.RULE_expr = 22;
HeddleParser.RULE_arg_list = 23;
HeddleParser.RULE_literal = 24;
HeddleParser.RULE_subtemplate = 25;
HeddleParser.RULE_text = 26;

class HeddleContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_heddle;
    }

	definition = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(DefinitionContext);
	    } else {
	        return this.getTypedRuleContext(DefinitionContext,i);
	    }
	};

	import_block = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(Import_blockContext);
	    } else {
	        return this.getTypedRuleContext(Import_blockContext,i);
	    }
	};

	outblock = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(OutblockContext);
	    } else {
	        return this.getTypedRuleContext(OutblockContext,i);
	    }
	};

	raw = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(RawContext);
	    } else {
	        return this.getTypedRuleContext(RawContext,i);
	    }
	};

	text = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(TextContext);
	    } else {
	        return this.getTypedRuleContext(TextContext,i);
	    }
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterHeddle(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitHeddle(this);
		}
	}


}



class RawContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_raw;
    }

	RAW() {
	    return this.getToken(HeddleParser.RAW, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterRaw(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitRaw(this);
		}
	}


}



class DefinitionContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_definition;
    }

	DEF_START() {
	    return this.getToken(HeddleParser.DEF_START, 0);
	};

	DEF_CLOSE() {
	    return this.getToken(HeddleParser.DEF_CLOSE, 0);
	};

	def = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(DefContext);
	    } else {
	        return this.getTypedRuleContext(DefContext,i);
	    }
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDefinition(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDefinition(this);
		}
	}


}



class DefContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def;
    }

	DEF_STARTNAME() {
	    return this.getToken(HeddleParser.DEF_STARTNAME, 0);
	};

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	DEF_ENDNAME() {
	    return this.getToken(HeddleParser.DEF_ENDNAME, 0);
	};

	subtemplate() {
	    return this.getTypedRuleContext(SubtemplateContext,0);
	};

	def_props() {
	    return this.getTypedRuleContext(Def_propsContext,0);
	};

	def_base() {
	    return this.getTypedRuleContext(Def_baseContext,0);
	};

	default_chain() {
	    return this.getTypedRuleContext(Default_chainContext,0);
	};

	def_type() {
	    return this.getTypedRuleContext(Def_typeContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef(this);
		}
	}


}



class Def_propsContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def_props;
    }

	OUT_PARAMSTART() {
	    return this.getToken(HeddleParser.OUT_PARAMSTART, 0);
	};

	OUT_PARAMEND() {
	    return this.getToken(HeddleParser.OUT_PARAMEND, 0);
	};

	def_prop_item = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(Def_prop_itemContext);
	    } else {
	        return this.getTypedRuleContext(Def_prop_itemContext,i);
	    }
	};

	COMMA = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.COMMA);
	    } else {
	        return this.getToken(HeddleParser.COMMA, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef_props(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef_props(this);
		}
	}


}



class Def_prop_itemContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def_prop_item;
    }

	def_prop() {
	    return this.getTypedRuleContext(Def_propContext,0);
	};

	def_slot() {
	    return this.getTypedRuleContext(Def_slotContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef_prop_item(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef_prop_item(this);
		}
	}


}



class Def_propContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def_prop;
    }

	ID = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.ID);
	    } else {
	        return this.getToken(HeddleParser.ID, i);
	    }
	};


	DELIM() {
	    return this.getToken(HeddleParser.DELIM, 0);
	};

	def_prop_default() {
	    return this.getTypedRuleContext(Def_prop_defaultContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef_prop(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef_prop(this);
		}
	}


}



class Def_slotContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def_slot;
    }

	ID = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.ID);
	    } else {
	        return this.getToken(HeddleParser.ID, i);
	    }
	};


	DEF_TYPE() {
	    return this.getToken(HeddleParser.DEF_TYPE, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef_slot(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef_slot(this);
		}
	}


}



class Def_prop_defaultContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def_prop_default;
    }

	ASSIGN() {
	    return this.getToken(HeddleParser.ASSIGN, 0);
	};

	def_literal() {
	    return this.getTypedRuleContext(Def_literalContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef_prop_default(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef_prop_default(this);
		}
	}


}



class Def_literalContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def_literal;
    }

	INT_LIT() {
	    return this.getToken(HeddleParser.INT_LIT, 0);
	};

	REAL_LIT() {
	    return this.getToken(HeddleParser.REAL_LIT, 0);
	};

	OP_MINUS() {
	    return this.getToken(HeddleParser.OP_MINUS, 0);
	};

	STRING_LIT() {
	    return this.getToken(HeddleParser.STRING_LIT, 0);
	};

	CHAR_LIT() {
	    return this.getToken(HeddleParser.CHAR_LIT, 0);
	};

	TRUE() {
	    return this.getToken(HeddleParser.TRUE, 0);
	};

	FALSE() {
	    return this.getToken(HeddleParser.FALSE, 0);
	};

	NULL() {
	    return this.getToken(HeddleParser.NULL, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef_literal(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef_literal(this);
		}
	}


}



class Def_baseContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def_base;
    }

	DELIM() {
	    return this.getToken(HeddleParser.DELIM, 0);
	};

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef_base(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef_base(this);
		}
	}


}



class Def_typeContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_def_type;
    }

	DEF_TYPE() {
	    return this.getToken(HeddleParser.DEF_TYPE, 0);
	};

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDef_type(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDef_type(this);
		}
	}


}



class Default_chainContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_default_chain;
    }

	DEF_OUT() {
	    return this.getToken(HeddleParser.DEF_OUT, 0);
	};

	chain() {
	    return this.getTypedRuleContext(ChainContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterDefault_chain(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitDefault_chain(this);
		}
	}


}



class Import_blockContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_import_block;
    }

	IMPORT_TOKEN() {
	    return this.getToken(HeddleParser.IMPORT_TOKEN, 0);
	};

	SUB_START() {
	    return this.getToken(HeddleParser.SUB_START, 0);
	};

	SUB_CLOSE() {
	    return this.getToken(HeddleParser.SUB_CLOSE, 0);
	};

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.WS);
	    } else {
	        return this.getToken(HeddleParser.WS, i);
	    }
	};


	text = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(TextContext);
	    } else {
	        return this.getTypedRuleContext(TextContext,i);
	    }
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterImport_block(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitImport_block(this);
		}
	}


}



class OutblockContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_outblock;
    }

	OUT() {
	    return this.getToken(HeddleParser.OUT, 0);
	};

	chain() {
	    return this.getTypedRuleContext(ChainContext,0);
	};

	subtemplate() {
	    return this.getTypedRuleContext(SubtemplateContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterOutblock(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitOutblock(this);
		}
	}


}



class ChainContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_chain;
    }

	call = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(CallContext);
	    } else {
	        return this.getTypedRuleContext(CallContext,i);
	    }
	};

	DELIM = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.DELIM);
	    } else {
	        return this.getToken(HeddleParser.DELIM, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterChain(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitChain(this);
		}
	}


}



class CallContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_call;
    }

	OUT_PARAMSTART() {
	    return this.getToken(HeddleParser.OUT_PARAMSTART, 0);
	};

	CSHARP_START() {
	    return this.getToken(HeddleParser.CSHARP_START, 0);
	};

	csharp_expression() {
	    return this.getTypedRuleContext(Csharp_expressionContext,0);
	};

	OUT_PARAMEND() {
	    return this.getToken(HeddleParser.OUT_PARAMEND, 0);
	};

	extension_id() {
	    return this.getTypedRuleContext(Extension_idContext,0);
	};

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.WS);
	    } else {
	        return this.getToken(HeddleParser.WS, i);
	    }
	};


	member_expression() {
	    return this.getTypedRuleContext(Member_expressionContext,0);
	};

	chain() {
	    return this.getTypedRuleContext(ChainContext,0);
	};

	native_expression() {
	    return this.getTypedRuleContext(Native_expressionContext,0);
	};

	named_argument = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(Named_argumentContext);
	    } else {
	        return this.getTypedRuleContext(Named_argumentContext,i);
	    }
	};

	expr() {
	    return this.getTypedRuleContext(ExprContext,0);
	};

	COMMA = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.COMMA);
	    } else {
	        return this.getToken(HeddleParser.COMMA, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterCall(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitCall(this);
		}
	}


}



class Named_argumentContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_named_argument;
    }

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	DELIM() {
	    return this.getToken(HeddleParser.DELIM, 0);
	};

	expr() {
	    return this.getTypedRuleContext(ExprContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterNamed_argument(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitNamed_argument(this);
		}
	}


}



class Member_expressionContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_member_expression;
    }

	ID = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.ID);
	    } else {
	        return this.getToken(HeddleParser.ID, i);
	    }
	};


	ROOT_REF() {
	    return this.getToken(HeddleParser.ROOT_REF, 0);
	};

	MEMBER_P = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.MEMBER_P);
	    } else {
	        return this.getToken(HeddleParser.MEMBER_P, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterMember_expression(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitMember_expression(this);
		}
	}


}



class Extension_idContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_extension_id;
    }

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterExtension_id(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitExtension_id(this);
		}
	}


}



class Csharp_expressionContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_csharp_expression;
    }

	CSHARP_TOKEN = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.CSHARP_TOKEN);
	    } else {
	        return this.getToken(HeddleParser.CSHARP_TOKEN, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterCsharp_expression(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitCsharp_expression(this);
		}
	}


}



class Native_expressionContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_native_expression;
    }

	expr() {
	    return this.getTypedRuleContext(ExprContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterNative_expression(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitNative_expression(this);
		}
	}


}



class ExprContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_expr;
    }


	 
		copyFrom(ctx) {
			super.copyFrom(ctx);
		}

}


class CoalesceExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_QQ() {
	    return this.getToken(HeddleParser.OP_QQ, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterCoalesceExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitCoalesceExpr(this);
		}
	}


}

HeddleParser.CoalesceExprContext = CoalesceExprContext;

class BitAndExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_AMP() {
	    return this.getToken(HeddleParser.OP_AMP, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterBitAndExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitBitAndExpr(this);
		}
	}


}

HeddleParser.BitAndExprContext = BitAndExprContext;

class RelationalExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        this.op = null;;
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_LT() {
	    return this.getToken(HeddleParser.OP_LT, 0);
	};

	OP_GT() {
	    return this.getToken(HeddleParser.OP_GT, 0);
	};

	OP_LE() {
	    return this.getToken(HeddleParser.OP_LE, 0);
	};

	OP_GE() {
	    return this.getToken(HeddleParser.OP_GE, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterRelationalExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitRelationalExpr(this);
		}
	}


}

HeddleParser.RelationalExprContext = RelationalExprContext;

class BitOrExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_PIPE() {
	    return this.getToken(HeddleParser.OP_PIPE, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterBitOrExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitBitOrExpr(this);
		}
	}


}

HeddleParser.BitOrExprContext = BitOrExprContext;

class UnaryExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        this.op = null;;
        super.copyFrom(ctx);
    }

	expr() {
	    return this.getTypedRuleContext(ExprContext,0);
	};

	OP_NOT() {
	    return this.getToken(HeddleParser.OP_NOT, 0);
	};

	OP_MINUS() {
	    return this.getToken(HeddleParser.OP_MINUS, 0);
	};

	OP_PLUS() {
	    return this.getToken(HeddleParser.OP_PLUS, 0);
	};

	OP_TILDE() {
	    return this.getToken(HeddleParser.OP_TILDE, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterUnaryExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitUnaryExpr(this);
		}
	}


}

HeddleParser.UnaryExprContext = UnaryExprContext;

class IndexExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	LBRACKET() {
	    return this.getToken(HeddleParser.LBRACKET, 0);
	};

	RBRACKET() {
	    return this.getToken(HeddleParser.RBRACKET, 0);
	};

	COMMA = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.COMMA);
	    } else {
	        return this.getToken(HeddleParser.COMMA, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterIndexExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitIndexExpr(this);
		}
	}


}

HeddleParser.IndexExprContext = IndexExprContext;

class AndAlsoExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_AND() {
	    return this.getToken(HeddleParser.OP_AND, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterAndAlsoExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitAndAlsoExpr(this);
		}
	}


}

HeddleParser.AndAlsoExprContext = AndAlsoExprContext;

class MultiplicativeExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        this.op = null;;
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_STAR() {
	    return this.getToken(HeddleParser.OP_STAR, 0);
	};

	OP_SLASH() {
	    return this.getToken(HeddleParser.OP_SLASH, 0);
	};

	OP_PERCENT() {
	    return this.getToken(HeddleParser.OP_PERCENT, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterMultiplicativeExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitMultiplicativeExpr(this);
		}
	}


}

HeddleParser.MultiplicativeExprContext = MultiplicativeExprContext;

class GroupExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	OUT_PARAMSTART() {
	    return this.getToken(HeddleParser.OUT_PARAMSTART, 0);
	};

	expr() {
	    return this.getTypedRuleContext(ExprContext,0);
	};

	OUT_PARAMEND() {
	    return this.getToken(HeddleParser.OUT_PARAMEND, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterGroupExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitGroupExpr(this);
		}
	}


}

HeddleParser.GroupExprContext = GroupExprContext;

class OrElseExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_OR() {
	    return this.getToken(HeddleParser.OP_OR, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterOrElseExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitOrElseExpr(this);
		}
	}


}

HeddleParser.OrElseExprContext = OrElseExprContext;

class FunctionCallExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	arg_list() {
	    return this.getTypedRuleContext(Arg_listContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterFunctionCallExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitFunctionCallExpr(this);
		}
	}


}

HeddleParser.FunctionCallExprContext = FunctionCallExprContext;

class EqualityExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        this.op = null;;
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_EQ() {
	    return this.getToken(HeddleParser.OP_EQ, 0);
	};

	OP_NEQ() {
	    return this.getToken(HeddleParser.OP_NEQ, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterEqualityExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitEqualityExpr(this);
		}
	}


}

HeddleParser.EqualityExprContext = EqualityExprContext;

class AdditiveExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        this.op = null;;
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_PLUS() {
	    return this.getToken(HeddleParser.OP_PLUS, 0);
	};

	OP_MINUS() {
	    return this.getToken(HeddleParser.OP_MINUS, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterAdditiveExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitAdditiveExpr(this);
		}
	}


}

HeddleParser.AdditiveExprContext = AdditiveExprContext;

class LiteralExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	literal() {
	    return this.getTypedRuleContext(LiteralContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterLiteralExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitLiteralExpr(this);
		}
	}


}

HeddleParser.LiteralExprContext = LiteralExprContext;

class MemberHopExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr() {
	    return this.getTypedRuleContext(ExprContext,0);
	};

	MEMBER_P() {
	    return this.getToken(HeddleParser.MEMBER_P, 0);
	};

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterMemberHopExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitMemberHopExpr(this);
		}
	}


}

HeddleParser.MemberHopExprContext = MemberHopExprContext;

class PathRootExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	ROOT_REF() {
	    return this.getToken(HeddleParser.ROOT_REF, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterPathRootExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitPathRootExpr(this);
		}
	}


}

HeddleParser.PathRootExprContext = PathRootExprContext;

class ShiftExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        this.op = null;;
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_LSHIFT() {
	    return this.getToken(HeddleParser.OP_LSHIFT, 0);
	};

	OP_RSHIFT() {
	    return this.getToken(HeddleParser.OP_RSHIFT, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterShiftExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitShiftExpr(this);
		}
	}


}

HeddleParser.ShiftExprContext = ShiftExprContext;

class BitXorExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_CARET() {
	    return this.getToken(HeddleParser.OP_CARET, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterBitXorExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitBitXorExpr(this);
		}
	}


}

HeddleParser.BitXorExprContext = BitXorExprContext;

class ThisExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	THIS() {
	    return this.getToken(HeddleParser.THIS, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterThisExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitThisExpr(this);
		}
	}


}

HeddleParser.ThisExprContext = ThisExprContext;

class TernaryExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	OP_QUESTION() {
	    return this.getToken(HeddleParser.OP_QUESTION, 0);
	};

	DELIM() {
	    return this.getToken(HeddleParser.DELIM, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterTernaryExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitTernaryExpr(this);
		}
	}


}

HeddleParser.TernaryExprContext = TernaryExprContext;

class MethodCallExprContext extends ExprContext {

    constructor(parser, ctx) {
        super(parser);
        super.copyFrom(ctx);
    }

	expr() {
	    return this.getTypedRuleContext(ExprContext,0);
	};

	MEMBER_P() {
	    return this.getToken(HeddleParser.MEMBER_P, 0);
	};

	ID() {
	    return this.getToken(HeddleParser.ID, 0);
	};

	arg_list() {
	    return this.getTypedRuleContext(Arg_listContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterMethodCallExpr(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitMethodCallExpr(this);
		}
	}


}

HeddleParser.MethodCallExprContext = MethodCallExprContext;

class Arg_listContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_arg_list;
    }

	OUT_PARAMSTART() {
	    return this.getToken(HeddleParser.OUT_PARAMSTART, 0);
	};

	OUT_PARAMEND() {
	    return this.getToken(HeddleParser.OUT_PARAMEND, 0);
	};

	expr = function(i) {
	    if(i===undefined) {
	        i = null;
	    }
	    if(i===null) {
	        return this.getTypedRuleContexts(ExprContext);
	    } else {
	        return this.getTypedRuleContext(ExprContext,i);
	    }
	};

	COMMA = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.COMMA);
	    } else {
	        return this.getToken(HeddleParser.COMMA, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterArg_list(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitArg_list(this);
		}
	}


}



class LiteralContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_literal;
    }

	INT_LIT() {
	    return this.getToken(HeddleParser.INT_LIT, 0);
	};

	REAL_LIT() {
	    return this.getToken(HeddleParser.REAL_LIT, 0);
	};

	STRING_LIT() {
	    return this.getToken(HeddleParser.STRING_LIT, 0);
	};

	CHAR_LIT() {
	    return this.getToken(HeddleParser.CHAR_LIT, 0);
	};

	TRUE() {
	    return this.getToken(HeddleParser.TRUE, 0);
	};

	FALSE() {
	    return this.getToken(HeddleParser.FALSE, 0);
	};

	NULL() {
	    return this.getToken(HeddleParser.NULL, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterLiteral(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitLiteral(this);
		}
	}


}



class SubtemplateContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_subtemplate;
    }

	SUB_START() {
	    return this.getToken(HeddleParser.SUB_START, 0);
	};

	heddle() {
	    return this.getTypedRuleContext(HeddleContext,0);
	};

	SUB_CLOSE() {
	    return this.getToken(HeddleParser.SUB_CLOSE, 0);
	};

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(HeddleParser.WS);
	    } else {
	        return this.getToken(HeddleParser.WS, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterSubtemplate(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitSubtemplate(this);
		}
	}


}



class TextContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = HeddleParser.RULE_text;
    }

	SUB_CLOSE() {
	    return this.getToken(HeddleParser.SUB_CLOSE, 0);
	};

	SUB_START() {
	    return this.getToken(HeddleParser.SUB_START, 0);
	};

	DEF_START() {
	    return this.getToken(HeddleParser.DEF_START, 0);
	};

	DEF_CLOSE() {
	    return this.getToken(HeddleParser.DEF_CLOSE, 0);
	};

	OUT() {
	    return this.getToken(HeddleParser.OUT, 0);
	};

	RAW() {
	    return this.getToken(HeddleParser.RAW, 0);
	};

	enterRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.enterText(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof HeddleParserListener ) {
	        listener.exitText(this);
		}
	}


}




HeddleParser.HeddleContext = HeddleContext; 
HeddleParser.RawContext = RawContext; 
HeddleParser.DefinitionContext = DefinitionContext; 
HeddleParser.DefContext = DefContext; 
HeddleParser.Def_propsContext = Def_propsContext; 
HeddleParser.Def_prop_itemContext = Def_prop_itemContext; 
HeddleParser.Def_propContext = Def_propContext; 
HeddleParser.Def_slotContext = Def_slotContext; 
HeddleParser.Def_prop_defaultContext = Def_prop_defaultContext; 
HeddleParser.Def_literalContext = Def_literalContext; 
HeddleParser.Def_baseContext = Def_baseContext; 
HeddleParser.Def_typeContext = Def_typeContext; 
HeddleParser.Default_chainContext = Default_chainContext; 
HeddleParser.Import_blockContext = Import_blockContext; 
HeddleParser.OutblockContext = OutblockContext; 
HeddleParser.ChainContext = ChainContext; 
HeddleParser.CallContext = CallContext; 
HeddleParser.Named_argumentContext = Named_argumentContext; 
HeddleParser.Member_expressionContext = Member_expressionContext; 
HeddleParser.Extension_idContext = Extension_idContext; 
HeddleParser.Csharp_expressionContext = Csharp_expressionContext; 
HeddleParser.Native_expressionContext = Native_expressionContext; 
HeddleParser.ExprContext = ExprContext; 
HeddleParser.Arg_listContext = Arg_listContext; 
HeddleParser.LiteralContext = LiteralContext; 
HeddleParser.SubtemplateContext = SubtemplateContext; 
HeddleParser.TextContext = TextContext; 
