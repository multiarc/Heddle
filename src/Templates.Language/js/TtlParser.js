// Generated from TtlParser.g4 by ANTLR 4.13.1
// jshint ignore: start
import antlr4 from 'antlr4';
import TtlParserListener from './TtlParserListener.js';
const serializedATN = [4,1,27,230,2,0,7,0,2,1,7,1,2,2,7,2,2,3,7,3,2,4,7,
4,2,5,7,5,2,6,7,6,2,7,7,7,2,8,7,8,2,9,7,9,2,10,7,10,2,11,7,11,2,12,7,12,
2,13,7,13,1,0,1,0,1,0,1,0,1,0,5,0,34,8,0,10,0,12,0,37,9,0,1,1,1,1,1,2,1,
2,1,2,4,2,44,8,2,11,2,12,2,45,1,2,1,2,1,3,1,3,5,3,52,8,3,10,3,12,3,55,9,
3,1,3,1,3,5,3,59,8,3,10,3,12,3,62,9,3,1,3,3,3,65,8,3,1,3,1,3,3,3,69,8,3,
1,3,1,3,5,3,73,8,3,10,3,12,3,76,9,3,1,3,1,3,5,3,80,8,3,10,3,12,3,83,9,3,
1,3,1,3,1,3,1,3,5,3,89,8,3,10,3,12,3,92,9,3,1,3,1,3,5,3,96,8,3,10,3,12,3,
99,9,3,1,3,3,3,102,8,3,1,3,1,3,3,3,106,8,3,1,3,3,3,109,8,3,1,4,1,4,5,4,113,
8,4,10,4,12,4,116,9,4,1,4,1,4,5,4,120,8,4,10,4,12,4,123,9,4,1,5,1,5,5,5,
127,8,5,10,5,12,5,130,9,5,1,5,1,5,1,6,1,6,5,6,136,8,6,10,6,12,6,139,9,6,
1,6,1,6,4,6,143,8,6,11,6,12,6,144,1,6,1,6,1,7,1,7,1,7,3,7,152,8,7,1,8,1,
8,1,8,5,8,157,8,8,10,8,12,8,160,9,8,1,8,5,8,163,8,8,10,8,12,8,166,9,8,1,
9,3,9,169,8,9,1,9,1,9,3,9,173,8,9,1,9,3,9,176,8,9,1,9,1,9,3,9,180,8,9,1,
9,1,9,3,9,184,8,9,1,9,1,9,1,9,4,9,189,8,9,11,9,12,9,190,1,9,1,9,3,9,195,
8,9,1,9,1,9,1,9,1,9,1,9,3,9,202,8,9,1,9,1,9,1,9,1,9,1,9,3,9,209,8,9,1,10,
1,10,1,11,4,11,214,8,11,11,11,12,11,215,1,12,5,12,219,8,12,10,12,12,12,222,
9,12,1,12,1,12,1,12,1,12,1,13,1,13,1,13,0,0,14,0,2,4,6,8,10,12,14,16,18,
20,22,24,26,0,1,2,0,7,9,16,17,254,0,35,1,0,0,0,2,38,1,0,0,0,4,40,1,0,0,0,
6,108,1,0,0,0,8,110,1,0,0,0,10,124,1,0,0,0,12,133,1,0,0,0,14,148,1,0,0,0,
16,153,1,0,0,0,18,208,1,0,0,0,20,210,1,0,0,0,22,213,1,0,0,0,24,220,1,0,0,
0,26,227,1,0,0,0,28,34,3,4,2,0,29,34,3,12,6,0,30,34,3,14,7,0,31,34,3,2,1,
0,32,34,3,26,13,0,33,28,1,0,0,0,33,29,1,0,0,0,33,30,1,0,0,0,33,31,1,0,0,
0,33,32,1,0,0,0,34,37,1,0,0,0,35,33,1,0,0,0,35,36,1,0,0,0,36,1,1,0,0,0,37,
35,1,0,0,0,38,39,5,18,0,0,39,3,1,0,0,0,40,43,5,16,0,0,41,44,3,6,3,0,42,44,
5,2,0,0,43,41,1,0,0,0,43,42,1,0,0,0,44,45,1,0,0,0,45,43,1,0,0,0,45,46,1,
0,0,0,46,47,1,0,0,0,47,48,5,17,0,0,48,5,1,0,0,0,49,53,5,13,0,0,50,52,5,2,
0,0,51,50,1,0,0,0,52,55,1,0,0,0,53,51,1,0,0,0,53,54,1,0,0,0,54,56,1,0,0,
0,55,53,1,0,0,0,56,60,5,4,0,0,57,59,5,2,0,0,58,57,1,0,0,0,59,62,1,0,0,0,
60,58,1,0,0,0,60,61,1,0,0,0,61,64,1,0,0,0,62,60,1,0,0,0,63,65,3,8,4,0,64,
63,1,0,0,0,64,65,1,0,0,0,65,66,1,0,0,0,66,68,5,14,0,0,67,69,3,10,5,0,68,
67,1,0,0,0,68,69,1,0,0,0,69,70,1,0,0,0,70,74,3,24,12,0,71,73,5,2,0,0,72,
71,1,0,0,0,73,76,1,0,0,0,74,72,1,0,0,0,74,75,1,0,0,0,75,77,1,0,0,0,76,74,
1,0,0,0,77,81,5,24,0,0,78,80,5,2,0,0,79,78,1,0,0,0,80,83,1,0,0,0,81,79,1,
0,0,0,81,82,1,0,0,0,82,84,1,0,0,0,83,81,1,0,0,0,84,85,5,4,0,0,85,109,1,0,
0,0,86,90,5,13,0,0,87,89,5,2,0,0,88,87,1,0,0,0,89,92,1,0,0,0,90,88,1,0,0,
0,90,91,1,0,0,0,91,93,1,0,0,0,92,90,1,0,0,0,93,97,5,4,0,0,94,96,5,2,0,0,
95,94,1,0,0,0,96,99,1,0,0,0,97,95,1,0,0,0,97,98,1,0,0,0,98,101,1,0,0,0,99,
97,1,0,0,0,100,102,3,8,4,0,101,100,1,0,0,0,101,102,1,0,0,0,102,103,1,0,0,
0,103,105,5,14,0,0,104,106,3,10,5,0,105,104,1,0,0,0,105,106,1,0,0,0,106,
107,1,0,0,0,107,109,3,24,12,0,108,49,1,0,0,0,108,86,1,0,0,0,109,7,1,0,0,
0,110,114,5,15,0,0,111,113,5,2,0,0,112,111,1,0,0,0,113,116,1,0,0,0,114,112,
1,0,0,0,114,115,1,0,0,0,115,117,1,0,0,0,116,114,1,0,0,0,117,121,5,4,0,0,
118,120,5,2,0,0,119,118,1,0,0,0,120,123,1,0,0,0,121,119,1,0,0,0,121,122,
1,0,0,0,122,9,1,0,0,0,123,121,1,0,0,0,124,128,5,21,0,0,125,127,5,2,0,0,126,
125,1,0,0,0,127,130,1,0,0,0,128,126,1,0,0,0,128,129,1,0,0,0,129,131,1,0,
0,0,130,128,1,0,0,0,131,132,3,16,8,0,132,11,1,0,0,0,133,137,5,3,0,0,134,
136,5,2,0,0,135,134,1,0,0,0,136,139,1,0,0,0,137,135,1,0,0,0,137,138,1,0,
0,0,138,140,1,0,0,0,139,137,1,0,0,0,140,142,5,8,0,0,141,143,3,26,13,0,142,
141,1,0,0,0,143,144,1,0,0,0,144,142,1,0,0,0,144,145,1,0,0,0,145,146,1,0,
0,0,146,147,5,9,0,0,147,13,1,0,0,0,148,149,5,7,0,0,149,151,3,16,8,0,150,
152,3,24,12,0,151,150,1,0,0,0,151,152,1,0,0,0,152,15,1,0,0,0,153,164,3,18,
9,0,154,158,5,15,0,0,155,157,5,2,0,0,156,155,1,0,0,0,157,160,1,0,0,0,158,
156,1,0,0,0,158,159,1,0,0,0,159,161,1,0,0,0,160,158,1,0,0,0,161,163,3,18,
9,0,162,154,1,0,0,0,163,166,1,0,0,0,164,162,1,0,0,0,164,165,1,0,0,0,165,
17,1,0,0,0,166,164,1,0,0,0,167,169,3,20,10,0,168,167,1,0,0,0,168,169,1,0,
0,0,169,170,1,0,0,0,170,172,5,19,0,0,171,173,5,5,0,0,172,171,1,0,0,0,172,
173,1,0,0,0,173,175,1,0,0,0,174,176,5,4,0,0,175,174,1,0,0,0,175,176,1,0,
0,0,176,177,1,0,0,0,177,209,5,20,0,0,178,180,3,20,10,0,179,178,1,0,0,0,179,
180,1,0,0,0,180,181,1,0,0,0,181,183,5,19,0,0,182,184,5,5,0,0,183,182,1,0,
0,0,183,184,1,0,0,0,184,185,1,0,0,0,185,188,5,4,0,0,186,187,5,6,0,0,187,
189,5,4,0,0,188,186,1,0,0,0,189,190,1,0,0,0,190,188,1,0,0,0,190,191,1,0,
0,0,191,192,1,0,0,0,192,209,5,20,0,0,193,195,3,20,10,0,194,193,1,0,0,0,194,
195,1,0,0,0,195,196,1,0,0,0,196,197,5,19,0,0,197,198,3,16,8,0,198,199,5,
20,0,0,199,209,1,0,0,0,200,202,3,20,10,0,201,200,1,0,0,0,201,202,1,0,0,0,
202,203,1,0,0,0,203,204,5,19,0,0,204,205,5,12,0,0,205,206,3,22,11,0,206,
207,5,11,0,0,207,209,1,0,0,0,208,168,1,0,0,0,208,179,1,0,0,0,208,194,1,0,
0,0,208,201,1,0,0,0,209,19,1,0,0,0,210,211,5,4,0,0,211,21,1,0,0,0,212,214,
5,11,0,0,213,212,1,0,0,0,214,215,1,0,0,0,215,213,1,0,0,0,215,216,1,0,0,0,
216,23,1,0,0,0,217,219,5,2,0,0,218,217,1,0,0,0,219,222,1,0,0,0,220,218,1,
0,0,0,220,221,1,0,0,0,221,223,1,0,0,0,222,220,1,0,0,0,223,224,5,8,0,0,224,
225,3,0,0,0,225,226,5,9,0,0,226,25,1,0,0,0,227,228,8,0,0,0,228,27,1,0,0,
0,34,33,35,43,45,53,60,64,68,74,81,90,97,101,105,108,114,121,128,137,144,
151,158,164,168,172,175,179,183,190,194,201,208,215,220];


const atn = new antlr4.atn.ATNDeserializer().deserialize(serializedATN);

const decisionsToDFA = atn.decisionToState.map( (ds, index) => new antlr4.dfa.DFA(ds, index) );

const sharedContextCache = new antlr4.atn.PredictionContextCache();

export default class TtlParser extends antlr4.Parser {

    static grammarFileName = "TtlParser.g4";
    static literalNames = [  ];
    static symbolicNames = [ null, "TEXT", "WS", "IMPORT_TOKEN", "ID", "ROOT_REF", 
                             "MEMBER_P", "OUT", "SUB_START", "SUB_CLOSE", 
                             "CSHARP_END", "CSHARP_TOKEN", "CSHARP_START", 
                             "DEF_STARTNAME", "DEF_ENDNAME", "DELIM", "DEF_START", 
                             "DEF_CLOSE", "RAW", "OUT_PARAMSTART", "OUT_PARAMEND", 
                             "DEF_OUT", "COMMENT", "SKIP_WS", "DEF_TYPE", 
                             "CALL_COMMENT", "CALL_WS", "TYPE_COMMENT" ];
    static ruleNames = [ "ttl", "raw", "definition", "def", "def_base", 
                         "default_chain", "import_block", "outblock", "chain", 
                         "call", "extension_id", "csharp_expression", "subtemplate", 
                         "text" ];

    constructor(input) {
        super(input);
        this._interp = new antlr4.atn.ParserATNSimulator(this, atn, decisionsToDFA, sharedContextCache);
        this.ruleNames = TtlParser.ruleNames;
        this.literalNames = TtlParser.literalNames;
        this.symbolicNames = TtlParser.symbolicNames;
    }



	ttl() {
	    let localctx = new TtlContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 0, TtlParser.RULE_ttl);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 35;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while((((_la) & ~0x1f) === 0 && ((1 << _la) & 268303614) !== 0)) {
	            this.state = 33;
	            this._errHandler.sync(this);
	            var la_ = this._interp.adaptivePredict(this._input,0,this._ctx);
	            switch(la_) {
	            case 1:
	                this.state = 28;
	                this.definition();
	                break;

	            case 2:
	                this.state = 29;
	                this.import_block();
	                break;

	            case 3:
	                this.state = 30;
	                this.outblock();
	                break;

	            case 4:
	                this.state = 31;
	                this.raw();
	                break;

	            case 5:
	                this.state = 32;
	                this.text();
	                break;

	            }
	            this.state = 37;
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
	    this.enterRule(localctx, 2, TtlParser.RULE_raw);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 38;
	        this.match(TtlParser.RAW);
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
	    this.enterRule(localctx, 4, TtlParser.RULE_definition);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 40;
	        this.match(TtlParser.DEF_START);
	        this.state = 43; 
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        do {
	            this.state = 43;
	            this._errHandler.sync(this);
	            switch(this._input.LA(1)) {
	            case 13:
	                this.state = 41;
	                this.def();
	                break;
	            case 2:
	                this.state = 42;
	                this.match(TtlParser.WS);
	                break;
	            default:
	                throw new antlr4.error.NoViableAltException(this);
	            }
	            this.state = 45; 
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        } while(_la===2 || _la===13);
	        this.state = 47;
	        this.match(TtlParser.DEF_CLOSE);
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
	    this.enterRule(localctx, 6, TtlParser.RULE_def);
	    var _la = 0;
	    try {
	        this.state = 108;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,14,this._ctx);
	        switch(la_) {
	        case 1:
	            this.enterOuterAlt(localctx, 1);
	            this.state = 49;
	            this.match(TtlParser.DEF_STARTNAME);
	            this.state = 53;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===2) {
	                this.state = 50;
	                this.match(TtlParser.WS);
	                this.state = 55;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 56;
	            this.match(TtlParser.ID);
	            this.state = 60;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===2) {
	                this.state = 57;
	                this.match(TtlParser.WS);
	                this.state = 62;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 64;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===15) {
	                this.state = 63;
	                this.def_base();
	            }

	            this.state = 66;
	            this.match(TtlParser.DEF_ENDNAME);
	            this.state = 68;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===21) {
	                this.state = 67;
	                this.default_chain();
	            }

	            this.state = 70;
	            this.subtemplate();
	            this.state = 74;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===2) {
	                this.state = 71;
	                this.match(TtlParser.WS);
	                this.state = 76;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 77;
	            this.match(TtlParser.DEF_TYPE);
	            this.state = 81;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===2) {
	                this.state = 78;
	                this.match(TtlParser.WS);
	                this.state = 83;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 84;
	            this.match(TtlParser.ID);
	            break;

	        case 2:
	            this.enterOuterAlt(localctx, 2);
	            this.state = 86;
	            this.match(TtlParser.DEF_STARTNAME);
	            this.state = 90;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===2) {
	                this.state = 87;
	                this.match(TtlParser.WS);
	                this.state = 92;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 93;
	            this.match(TtlParser.ID);
	            this.state = 97;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===2) {
	                this.state = 94;
	                this.match(TtlParser.WS);
	                this.state = 99;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 101;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===15) {
	                this.state = 100;
	                this.def_base();
	            }

	            this.state = 103;
	            this.match(TtlParser.DEF_ENDNAME);
	            this.state = 105;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===21) {
	                this.state = 104;
	                this.default_chain();
	            }

	            this.state = 107;
	            this.subtemplate();
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



	def_base() {
	    let localctx = new Def_baseContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 8, TtlParser.RULE_def_base);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 110;
	        this.match(TtlParser.DELIM);
	        this.state = 114;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 111;
	            this.match(TtlParser.WS);
	            this.state = 116;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	        this.state = 117;
	        this.match(TtlParser.ID);
	        this.state = 121;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 118;
	            this.match(TtlParser.WS);
	            this.state = 123;
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



	default_chain() {
	    let localctx = new Default_chainContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 10, TtlParser.RULE_default_chain);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 124;
	        this.match(TtlParser.DEF_OUT);
	        this.state = 128;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 125;
	            this.match(TtlParser.WS);
	            this.state = 130;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	        this.state = 131;
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
	    this.enterRule(localctx, 12, TtlParser.RULE_import_block);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 133;
	        this.match(TtlParser.IMPORT_TOKEN);
	        this.state = 137;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 134;
	            this.match(TtlParser.WS);
	            this.state = 139;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	        this.state = 140;
	        this.match(TtlParser.SUB_START);
	        this.state = 142; 
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        do {
	            this.state = 141;
	            this.text();
	            this.state = 144; 
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        } while((((_la) & ~0x1f) === 0 && ((1 << _la) & 268237950) !== 0));
	        this.state = 146;
	        this.match(TtlParser.SUB_CLOSE);
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
	    this.enterRule(localctx, 14, TtlParser.RULE_outblock);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 148;
	        this.match(TtlParser.OUT);
	        this.state = 149;
	        this.chain();
	        this.state = 151;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,20,this._ctx);
	        if(la_===1) {
	            this.state = 150;
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
	    this.enterRule(localctx, 16, TtlParser.RULE_chain);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 153;
	        this.call();
	        this.state = 164;
	        this._errHandler.sync(this);
	        var _alt = this._interp.adaptivePredict(this._input,22,this._ctx)
	        while(_alt!=2 && _alt!=antlr4.atn.ATN.INVALID_ALT_NUMBER) {
	            if(_alt===1) {
	                this.state = 154;
	                this.match(TtlParser.DELIM);
	                this.state = 158;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	                while(_la===2) {
	                    this.state = 155;
	                    this.match(TtlParser.WS);
	                    this.state = 160;
	                    this._errHandler.sync(this);
	                    _la = this._input.LA(1);
	                }
	                this.state = 161;
	                this.call(); 
	            }
	            this.state = 166;
	            this._errHandler.sync(this);
	            _alt = this._interp.adaptivePredict(this._input,22,this._ctx);
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
	    this.enterRule(localctx, 18, TtlParser.RULE_call);
	    var _la = 0;
	    try {
	        this.state = 208;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,31,this._ctx);
	        switch(la_) {
	        case 1:
	            this.enterOuterAlt(localctx, 1);
	            this.state = 168;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 167;
	                this.extension_id();
	            }

	            this.state = 170;
	            this.match(TtlParser.OUT_PARAMSTART);
	            this.state = 172;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===5) {
	                this.state = 171;
	                this.match(TtlParser.ROOT_REF);
	            }

	            this.state = 175;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 174;
	                this.match(TtlParser.ID);
	            }

	            this.state = 177;
	            this.match(TtlParser.OUT_PARAMEND);
	            break;

	        case 2:
	            this.enterOuterAlt(localctx, 2);
	            this.state = 179;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 178;
	                this.extension_id();
	            }

	            this.state = 181;
	            this.match(TtlParser.OUT_PARAMSTART);
	            this.state = 183;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===5) {
	                this.state = 182;
	                this.match(TtlParser.ROOT_REF);
	            }

	            this.state = 185;
	            this.match(TtlParser.ID);
	            this.state = 188; 
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            do {
	                this.state = 186;
	                this.match(TtlParser.MEMBER_P);
	                this.state = 187;
	                this.match(TtlParser.ID);
	                this.state = 190; 
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            } while(_la===6);
	            this.state = 192;
	            this.match(TtlParser.OUT_PARAMEND);
	            break;

	        case 3:
	            this.enterOuterAlt(localctx, 3);
	            this.state = 194;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 193;
	                this.extension_id();
	            }

	            this.state = 196;
	            this.match(TtlParser.OUT_PARAMSTART);
	            this.state = 197;
	            this.chain();
	            this.state = 198;
	            this.match(TtlParser.OUT_PARAMEND);
	            break;

	        case 4:
	            this.enterOuterAlt(localctx, 4);
	            this.state = 201;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 200;
	                this.extension_id();
	            }

	            this.state = 203;
	            this.match(TtlParser.OUT_PARAMSTART);
	            this.state = 204;
	            this.match(TtlParser.CSHARP_START);
	            this.state = 205;
	            this.csharp_expression();
	            this.state = 206;
	            this.match(TtlParser.CSHARP_TOKEN);
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



	extension_id() {
	    let localctx = new Extension_idContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 20, TtlParser.RULE_extension_id);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 210;
	        this.match(TtlParser.ID);
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
	    this.enterRule(localctx, 22, TtlParser.RULE_csharp_expression);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 213; 
	        this._errHandler.sync(this);
	        var _alt = 1;
	        do {
	        	switch (_alt) {
	        	case 1:
	        		this.state = 212;
	        		this.match(TtlParser.CSHARP_TOKEN);
	        		break;
	        	default:
	        		throw new antlr4.error.NoViableAltException(this);
	        	}
	        	this.state = 215; 
	        	this._errHandler.sync(this);
	        	_alt = this._interp.adaptivePredict(this._input,32, this._ctx);
	        } while ( _alt!=2 && _alt!=antlr4.atn.ATN.INVALID_ALT_NUMBER );
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
	    this.enterRule(localctx, 24, TtlParser.RULE_subtemplate);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 220;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 217;
	            this.match(TtlParser.WS);
	            this.state = 222;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	        this.state = 223;
	        this.match(TtlParser.SUB_START);
	        this.state = 224;
	        this.ttl();
	        this.state = 225;
	        this.match(TtlParser.SUB_CLOSE);
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
	    this.enterRule(localctx, 26, TtlParser.RULE_text);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 227;
	        _la = this._input.LA(1);
	        if(_la<=0 || (((_la) & ~0x1f) === 0 && ((1 << _la) & 197504) !== 0)) {
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

TtlParser.EOF = antlr4.Token.EOF;
TtlParser.TEXT = 1;
TtlParser.WS = 2;
TtlParser.IMPORT_TOKEN = 3;
TtlParser.ID = 4;
TtlParser.ROOT_REF = 5;
TtlParser.MEMBER_P = 6;
TtlParser.OUT = 7;
TtlParser.SUB_START = 8;
TtlParser.SUB_CLOSE = 9;
TtlParser.CSHARP_END = 10;
TtlParser.CSHARP_TOKEN = 11;
TtlParser.CSHARP_START = 12;
TtlParser.DEF_STARTNAME = 13;
TtlParser.DEF_ENDNAME = 14;
TtlParser.DELIM = 15;
TtlParser.DEF_START = 16;
TtlParser.DEF_CLOSE = 17;
TtlParser.RAW = 18;
TtlParser.OUT_PARAMSTART = 19;
TtlParser.OUT_PARAMEND = 20;
TtlParser.DEF_OUT = 21;
TtlParser.COMMENT = 22;
TtlParser.SKIP_WS = 23;
TtlParser.DEF_TYPE = 24;
TtlParser.CALL_COMMENT = 25;
TtlParser.CALL_WS = 26;
TtlParser.TYPE_COMMENT = 27;

TtlParser.RULE_ttl = 0;
TtlParser.RULE_raw = 1;
TtlParser.RULE_definition = 2;
TtlParser.RULE_def = 3;
TtlParser.RULE_def_base = 4;
TtlParser.RULE_default_chain = 5;
TtlParser.RULE_import_block = 6;
TtlParser.RULE_outblock = 7;
TtlParser.RULE_chain = 8;
TtlParser.RULE_call = 9;
TtlParser.RULE_extension_id = 10;
TtlParser.RULE_csharp_expression = 11;
TtlParser.RULE_subtemplate = 12;
TtlParser.RULE_text = 13;

class TtlContext extends antlr4.ParserRuleContext {

    constructor(parser, parent, invokingState) {
        if(parent===undefined) {
            parent = null;
        }
        if(invokingState===undefined || invokingState===null) {
            invokingState = -1;
        }
        super(parent, invokingState);
        this.parser = parser;
        this.ruleIndex = TtlParser.RULE_ttl;
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
	    if(listener instanceof TtlParserListener ) {
	        listener.enterTtl(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.exitTtl(this);
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
        this.ruleIndex = TtlParser.RULE_raw;
    }

	RAW() {
	    return this.getToken(TtlParser.RAW, 0);
	};

	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterRaw(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
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
        this.ruleIndex = TtlParser.RULE_definition;
    }

	DEF_START() {
	    return this.getToken(TtlParser.DEF_START, 0);
	};

	DEF_CLOSE() {
	    return this.getToken(TtlParser.DEF_CLOSE, 0);
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

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.WS);
	    } else {
	        return this.getToken(TtlParser.WS, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterDefinition(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
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
        this.ruleIndex = TtlParser.RULE_def;
    }

	DEF_STARTNAME() {
	    return this.getToken(TtlParser.DEF_STARTNAME, 0);
	};

	ID = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.ID);
	    } else {
	        return this.getToken(TtlParser.ID, i);
	    }
	};


	DEF_ENDNAME() {
	    return this.getToken(TtlParser.DEF_ENDNAME, 0);
	};

	subtemplate() {
	    return this.getTypedRuleContext(SubtemplateContext,0);
	};

	DEF_TYPE() {
	    return this.getToken(TtlParser.DEF_TYPE, 0);
	};

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.WS);
	    } else {
	        return this.getToken(TtlParser.WS, i);
	    }
	};


	def_base() {
	    return this.getTypedRuleContext(Def_baseContext,0);
	};

	default_chain() {
	    return this.getTypedRuleContext(Default_chainContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterDef(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.exitDef(this);
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
        this.ruleIndex = TtlParser.RULE_def_base;
    }

	DELIM() {
	    return this.getToken(TtlParser.DELIM, 0);
	};

	ID() {
	    return this.getToken(TtlParser.ID, 0);
	};

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.WS);
	    } else {
	        return this.getToken(TtlParser.WS, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterDef_base(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.exitDef_base(this);
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
        this.ruleIndex = TtlParser.RULE_default_chain;
    }

	DEF_OUT() {
	    return this.getToken(TtlParser.DEF_OUT, 0);
	};

	chain() {
	    return this.getTypedRuleContext(ChainContext,0);
	};

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.WS);
	    } else {
	        return this.getToken(TtlParser.WS, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterDefault_chain(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
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
        this.ruleIndex = TtlParser.RULE_import_block;
    }

	IMPORT_TOKEN() {
	    return this.getToken(TtlParser.IMPORT_TOKEN, 0);
	};

	SUB_START() {
	    return this.getToken(TtlParser.SUB_START, 0);
	};

	SUB_CLOSE() {
	    return this.getToken(TtlParser.SUB_CLOSE, 0);
	};

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.WS);
	    } else {
	        return this.getToken(TtlParser.WS, i);
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
	    if(listener instanceof TtlParserListener ) {
	        listener.enterImport_block(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
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
        this.ruleIndex = TtlParser.RULE_outblock;
    }

	OUT() {
	    return this.getToken(TtlParser.OUT, 0);
	};

	chain() {
	    return this.getTypedRuleContext(ChainContext,0);
	};

	subtemplate() {
	    return this.getTypedRuleContext(SubtemplateContext,0);
	};

	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterOutblock(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
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
        this.ruleIndex = TtlParser.RULE_chain;
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
	        return this.getTokens(TtlParser.DELIM);
	    } else {
	        return this.getToken(TtlParser.DELIM, i);
	    }
	};


	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.WS);
	    } else {
	        return this.getToken(TtlParser.WS, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterChain(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
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
        this.ruleIndex = TtlParser.RULE_call;
    }

	OUT_PARAMSTART() {
	    return this.getToken(TtlParser.OUT_PARAMSTART, 0);
	};

	OUT_PARAMEND() {
	    return this.getToken(TtlParser.OUT_PARAMEND, 0);
	};

	extension_id() {
	    return this.getTypedRuleContext(Extension_idContext,0);
	};

	ROOT_REF() {
	    return this.getToken(TtlParser.ROOT_REF, 0);
	};

	ID = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.ID);
	    } else {
	        return this.getToken(TtlParser.ID, i);
	    }
	};


	MEMBER_P = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.MEMBER_P);
	    } else {
	        return this.getToken(TtlParser.MEMBER_P, i);
	    }
	};


	chain() {
	    return this.getTypedRuleContext(ChainContext,0);
	};

	CSHARP_START() {
	    return this.getToken(TtlParser.CSHARP_START, 0);
	};

	csharp_expression() {
	    return this.getTypedRuleContext(Csharp_expressionContext,0);
	};

	CSHARP_TOKEN() {
	    return this.getToken(TtlParser.CSHARP_TOKEN, 0);
	};

	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterCall(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.exitCall(this);
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
        this.ruleIndex = TtlParser.RULE_extension_id;
    }

	ID() {
	    return this.getToken(TtlParser.ID, 0);
	};

	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterExtension_id(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
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
        this.ruleIndex = TtlParser.RULE_csharp_expression;
    }

	CSHARP_TOKEN = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.CSHARP_TOKEN);
	    } else {
	        return this.getToken(TtlParser.CSHARP_TOKEN, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterCsharp_expression(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.exitCsharp_expression(this);
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
        this.ruleIndex = TtlParser.RULE_subtemplate;
    }

	SUB_START() {
	    return this.getToken(TtlParser.SUB_START, 0);
	};

	ttl() {
	    return this.getTypedRuleContext(TtlContext,0);
	};

	SUB_CLOSE() {
	    return this.getToken(TtlParser.SUB_CLOSE, 0);
	};

	WS = function(i) {
		if(i===undefined) {
			i = null;
		}
	    if(i===null) {
	        return this.getTokens(TtlParser.WS);
	    } else {
	        return this.getToken(TtlParser.WS, i);
	    }
	};


	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterSubtemplate(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
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
        this.ruleIndex = TtlParser.RULE_text;
    }

	SUB_CLOSE() {
	    return this.getToken(TtlParser.SUB_CLOSE, 0);
	};

	SUB_START() {
	    return this.getToken(TtlParser.SUB_START, 0);
	};

	DEF_START() {
	    return this.getToken(TtlParser.DEF_START, 0);
	};

	DEF_CLOSE() {
	    return this.getToken(TtlParser.DEF_CLOSE, 0);
	};

	OUT() {
	    return this.getToken(TtlParser.OUT, 0);
	};

	enterRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.enterText(this);
		}
	}

	exitRule(listener) {
	    if(listener instanceof TtlParserListener ) {
	        listener.exitText(this);
		}
	}


}




TtlParser.TtlContext = TtlContext; 
TtlParser.RawContext = RawContext; 
TtlParser.DefinitionContext = DefinitionContext; 
TtlParser.DefContext = DefContext; 
TtlParser.Def_baseContext = Def_baseContext; 
TtlParser.Default_chainContext = Default_chainContext; 
TtlParser.Import_blockContext = Import_blockContext; 
TtlParser.OutblockContext = OutblockContext; 
TtlParser.ChainContext = ChainContext; 
TtlParser.CallContext = CallContext; 
TtlParser.Extension_idContext = Extension_idContext; 
TtlParser.Csharp_expressionContext = Csharp_expressionContext; 
TtlParser.SubtemplateContext = SubtemplateContext; 
TtlParser.TextContext = TextContext; 
