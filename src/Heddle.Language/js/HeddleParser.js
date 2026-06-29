// Generated from HeddleParser.g4 by ANTLR 4.13.1
// jshint ignore: start
import antlr4 from 'antlr4';
import HeddleParserListener from './HeddleParserListener.js';
const serializedATN = [4,1,39,164,2,0,7,0,2,1,7,1,2,2,7,2,2,3,7,3,2,4,7,
4,2,5,7,5,2,6,7,6,2,7,7,7,2,8,7,8,2,9,7,9,2,10,7,10,2,11,7,11,2,12,7,12,
2,13,7,13,2,14,7,14,2,15,7,15,1,0,1,0,1,0,1,0,1,0,5,0,38,8,0,10,0,12,0,41,
9,0,1,1,1,1,1,2,1,2,4,2,47,8,2,11,2,12,2,48,1,2,1,2,1,3,1,3,1,3,3,3,56,8,
3,1,3,1,3,3,3,60,8,3,1,3,1,3,3,3,64,8,3,1,4,1,4,1,4,1,5,1,5,1,5,1,6,1,6,
1,6,1,7,1,7,5,7,77,8,7,10,7,12,7,80,9,7,1,7,1,7,4,7,84,8,7,11,7,12,7,85,
1,7,1,7,1,8,1,8,1,8,3,8,93,8,8,1,9,1,9,1,9,5,9,98,8,9,10,9,12,9,101,9,9,
1,10,3,10,104,8,10,1,10,1,10,1,10,1,10,1,10,1,10,3,10,112,8,10,1,10,1,10,
5,10,116,8,10,10,10,12,10,119,9,10,1,10,3,10,122,8,10,1,10,1,10,3,10,126,
8,10,1,10,1,10,1,10,1,10,3,10,132,8,10,1,11,3,11,135,8,11,1,11,1,11,1,11,
5,11,140,8,11,10,11,12,11,143,9,11,1,12,1,12,1,13,4,13,148,8,13,11,13,12,
13,149,1,14,5,14,153,8,14,10,14,12,14,156,9,14,1,14,1,14,1,14,1,14,1,15,
1,15,1,15,0,0,16,0,2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,0,1,2,0,7,9,
16,18,171,0,39,1,0,0,0,2,42,1,0,0,0,4,44,1,0,0,0,6,52,1,0,0,0,8,65,1,0,0,
0,10,68,1,0,0,0,12,71,1,0,0,0,14,74,1,0,0,0,16,89,1,0,0,0,18,94,1,0,0,0,
20,131,1,0,0,0,22,134,1,0,0,0,24,144,1,0,0,0,26,147,1,0,0,0,28,154,1,0,0,
0,30,161,1,0,0,0,32,38,3,4,2,0,33,38,3,14,7,0,34,38,3,16,8,0,35,38,3,2,1,
0,36,38,3,30,15,0,37,32,1,0,0,0,37,33,1,0,0,0,37,34,1,0,0,0,37,35,1,0,0,
0,37,36,1,0,0,0,38,41,1,0,0,0,39,37,1,0,0,0,39,40,1,0,0,0,40,1,1,0,0,0,41,
39,1,0,0,0,42,43,5,18,0,0,43,3,1,0,0,0,44,46,5,16,0,0,45,47,3,6,3,0,46,45,
1,0,0,0,47,48,1,0,0,0,48,46,1,0,0,0,48,49,1,0,0,0,49,50,1,0,0,0,50,51,5,
17,0,0,51,5,1,0,0,0,52,53,5,13,0,0,53,55,5,4,0,0,54,56,3,8,4,0,55,54,1,0,
0,0,55,56,1,0,0,0,56,57,1,0,0,0,57,59,5,14,0,0,58,60,3,12,6,0,59,58,1,0,
0,0,59,60,1,0,0,0,60,61,1,0,0,0,61,63,3,28,14,0,62,64,3,10,5,0,63,62,1,0,
0,0,63,64,1,0,0,0,64,7,1,0,0,0,65,66,5,15,0,0,66,67,5,4,0,0,67,9,1,0,0,0,
68,69,5,27,0,0,69,70,5,4,0,0,70,11,1,0,0,0,71,72,5,21,0,0,72,73,3,18,9,0,
73,13,1,0,0,0,74,78,5,3,0,0,75,77,5,2,0,0,76,75,1,0,0,0,77,80,1,0,0,0,78,
76,1,0,0,0,78,79,1,0,0,0,79,81,1,0,0,0,80,78,1,0,0,0,81,83,5,8,0,0,82,84,
3,30,15,0,83,82,1,0,0,0,84,85,1,0,0,0,85,83,1,0,0,0,85,86,1,0,0,0,86,87,
1,0,0,0,87,88,5,9,0,0,88,15,1,0,0,0,89,90,5,7,0,0,90,92,3,18,9,0,91,93,3,
28,14,0,92,91,1,0,0,0,92,93,1,0,0,0,93,17,1,0,0,0,94,99,3,20,10,0,95,96,
5,15,0,0,96,98,3,20,10,0,97,95,1,0,0,0,98,101,1,0,0,0,99,97,1,0,0,0,99,100,
1,0,0,0,100,19,1,0,0,0,101,99,1,0,0,0,102,104,3,24,12,0,103,102,1,0,0,0,
103,104,1,0,0,0,104,105,1,0,0,0,105,106,5,19,0,0,106,107,5,12,0,0,107,108,
3,26,13,0,108,109,5,20,0,0,109,132,1,0,0,0,110,112,3,24,12,0,111,110,1,0,
0,0,111,112,1,0,0,0,112,113,1,0,0,0,113,117,5,19,0,0,114,116,5,2,0,0,115,
114,1,0,0,0,116,119,1,0,0,0,117,115,1,0,0,0,117,118,1,0,0,0,118,121,1,0,
0,0,119,117,1,0,0,0,120,122,3,22,11,0,121,120,1,0,0,0,121,122,1,0,0,0,122,
123,1,0,0,0,123,132,5,20,0,0,124,126,3,24,12,0,125,124,1,0,0,0,125,126,1,
0,0,0,126,127,1,0,0,0,127,128,5,19,0,0,128,129,3,18,9,0,129,130,5,20,0,0,
130,132,1,0,0,0,131,103,1,0,0,0,131,111,1,0,0,0,131,125,1,0,0,0,132,21,1,
0,0,0,133,135,5,5,0,0,134,133,1,0,0,0,134,135,1,0,0,0,135,136,1,0,0,0,136,
141,5,4,0,0,137,138,5,6,0,0,138,140,5,4,0,0,139,137,1,0,0,0,140,143,1,0,
0,0,141,139,1,0,0,0,141,142,1,0,0,0,142,23,1,0,0,0,143,141,1,0,0,0,144,145,
5,4,0,0,145,25,1,0,0,0,146,148,5,11,0,0,147,146,1,0,0,0,148,149,1,0,0,0,
149,147,1,0,0,0,149,150,1,0,0,0,150,27,1,0,0,0,151,153,5,2,0,0,152,151,1,
0,0,0,153,156,1,0,0,0,154,152,1,0,0,0,154,155,1,0,0,0,155,157,1,0,0,0,156,
154,1,0,0,0,157,158,5,8,0,0,158,159,3,0,0,0,159,160,5,9,0,0,160,29,1,0,0,
0,161,162,8,0,0,0,162,31,1,0,0,0,20,37,39,48,55,59,63,78,85,92,99,103,111,
117,121,125,131,134,141,149,154];


const atn = new antlr4.atn.ATNDeserializer().deserialize(serializedATN);

const decisionsToDFA = atn.decisionToState.map( (ds, index) => new antlr4.dfa.DFA(ds, index) );

const sharedContextCache = new antlr4.atn.PredictionContextCache();

export default class HeddleParser extends antlr4.Parser {

    static grammarFileName = "HeddleParser.g4";
    static literalNames = [ null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, null, null, 
                            null, null, null, null, null, null, "'\"\"'", 
                            "'}'" ];
    static symbolicNames = [ null, "TEXT", "WS", "IMPORT_TOKEN", "ID", "ROOT_REF", 
                             "MEMBER_P", "OUT", "SUB_START", "SUB_CLOSE", 
                             "CSHARP_END", "CSHARP_TOKEN", "CSHARP_START", 
                             "DEF_STARTNAME", "DEF_ENDNAME", "DELIM", "DEF_START", 
                             "DEF_CLOSE", "RAW", "OUT_PARAMSTART", "OUT_PARAMEND", 
                             "DEF_OUT", "COMMENT", "SKIP_WS", "SUB_COMMENT", 
                             "SUB_SKIP_WS", "DEF_COMMENT", "DEF_TYPE", "DEF_WS", 
                             "IMPORT_COMMENT", "CALL_RETURN_COMMENT", "CALL_SKIP_WS", 
                             "OUT_COMMENT", "OUT_SKIP_WS", "CALL_COMMENT", 
                             "CALL_WS", "ISTR_DBL_OPEN", "ISTR_END", "IVSTR_QUOTE_ESC", 
                             "HOLE_CLOSE" ];
    static ruleNames = [ "heddle", "raw", "definition", "def", "def_base", 
                         "def_type", "default_chain", "import_block", "outblock", 
                         "chain", "call", "member_expression", "extension_id", 
                         "csharp_expression", "subtemplate", "text" ];

    constructor(input) {
        super(input);
        this._interp = new antlr4.atn.ParserATNSimulator(this, atn, decisionsToDFA, sharedContextCache);
        this.ruleNames = HeddleParser.ruleNames;
        this.literalNames = HeddleParser.literalNames;
        this.symbolicNames = HeddleParser.symbolicNames;
    }



	heddle() {
	    let localctx = new HeddleContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 0, HeddleParser.RULE_heddle);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 39;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while((((_la) & ~0x1f) === 0 && ((1 << _la) & 4294835454) !== 0) || ((((_la - 32)) & ~0x1f) === 0 && ((1 << (_la - 32)) & 255) !== 0)) {
	            this.state = 37;
	            this._errHandler.sync(this);
	            var la_ = this._interp.adaptivePredict(this._input,0,this._ctx);
	            switch(la_) {
	            case 1:
	                this.state = 32;
	                this.definition();
	                break;

	            case 2:
	                this.state = 33;
	                this.import_block();
	                break;

	            case 3:
	                this.state = 34;
	                this.outblock();
	                break;

	            case 4:
	                this.state = 35;
	                this.raw();
	                break;

	            case 5:
	                this.state = 36;
	                this.text();
	                break;

	            }
	            this.state = 41;
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
	        this.state = 42;
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
	        this.state = 44;
	        this.match(HeddleParser.DEF_START);
	        this.state = 46; 
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        do {
	            this.state = 45;
	            this.def();
	            this.state = 48; 
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        } while(_la===13);
	        this.state = 50;
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
	        this.state = 52;
	        this.match(HeddleParser.DEF_STARTNAME);
	        this.state = 53;
	        this.match(HeddleParser.ID);
	        this.state = 55;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===15) {
	            this.state = 54;
	            this.def_base();
	        }

	        this.state = 57;
	        this.match(HeddleParser.DEF_ENDNAME);
	        this.state = 59;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===21) {
	            this.state = 58;
	            this.default_chain();
	        }

	        this.state = 61;
	        this.subtemplate();
	        this.state = 63;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===27) {
	            this.state = 62;
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



	def_base() {
	    let localctx = new Def_baseContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 8, HeddleParser.RULE_def_base);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 65;
	        this.match(HeddleParser.DELIM);
	        this.state = 66;
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
	    this.enterRule(localctx, 10, HeddleParser.RULE_def_type);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 68;
	        this.match(HeddleParser.DEF_TYPE);
	        this.state = 69;
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
	    this.enterRule(localctx, 12, HeddleParser.RULE_default_chain);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 71;
	        this.match(HeddleParser.DEF_OUT);
	        this.state = 72;
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
	    this.enterRule(localctx, 14, HeddleParser.RULE_import_block);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 74;
	        this.match(HeddleParser.IMPORT_TOKEN);
	        this.state = 78;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 75;
	            this.match(HeddleParser.WS);
	            this.state = 80;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	        this.state = 81;
	        this.match(HeddleParser.SUB_START);
	        this.state = 83; 
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        do {
	            this.state = 82;
	            this.text();
	            this.state = 85; 
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        } while((((_la) & ~0x1f) === 0 && ((1 << _la) & 4294507646) !== 0) || ((((_la - 32)) & ~0x1f) === 0 && ((1 << (_la - 32)) & 255) !== 0));
	        this.state = 87;
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
	    this.enterRule(localctx, 16, HeddleParser.RULE_outblock);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 89;
	        this.match(HeddleParser.OUT);
	        this.state = 90;
	        this.chain();
	        this.state = 92;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,8,this._ctx);
	        if(la_===1) {
	            this.state = 91;
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
	    this.enterRule(localctx, 18, HeddleParser.RULE_chain);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 94;
	        this.call();
	        this.state = 99;
	        this._errHandler.sync(this);
	        var _alt = this._interp.adaptivePredict(this._input,9,this._ctx)
	        while(_alt!=2 && _alt!=antlr4.atn.ATN.INVALID_ALT_NUMBER) {
	            if(_alt===1) {
	                this.state = 95;
	                this.match(HeddleParser.DELIM);
	                this.state = 96;
	                this.call(); 
	            }
	            this.state = 101;
	            this._errHandler.sync(this);
	            _alt = this._interp.adaptivePredict(this._input,9,this._ctx);
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
	    this.enterRule(localctx, 20, HeddleParser.RULE_call);
	    var _la = 0;
	    try {
	        this.state = 131;
	        this._errHandler.sync(this);
	        var la_ = this._interp.adaptivePredict(this._input,15,this._ctx);
	        switch(la_) {
	        case 1:
	            this.enterOuterAlt(localctx, 1);
	            this.state = 103;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 102;
	                this.extension_id();
	            }

	            this.state = 105;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 106;
	            this.match(HeddleParser.CSHARP_START);
	            this.state = 107;
	            this.csharp_expression();
	            this.state = 108;
	            this.match(HeddleParser.OUT_PARAMEND);
	            break;

	        case 2:
	            this.enterOuterAlt(localctx, 2);
	            this.state = 111;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 110;
	                this.extension_id();
	            }

	            this.state = 113;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 117;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            while(_la===2) {
	                this.state = 114;
	                this.match(HeddleParser.WS);
	                this.state = 119;
	                this._errHandler.sync(this);
	                _la = this._input.LA(1);
	            }
	            this.state = 121;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4 || _la===5) {
	                this.state = 120;
	                this.member_expression();
	            }

	            this.state = 123;
	            this.match(HeddleParser.OUT_PARAMEND);
	            break;

	        case 3:
	            this.enterOuterAlt(localctx, 3);
	            this.state = 125;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	            if(_la===4) {
	                this.state = 124;
	                this.extension_id();
	            }

	            this.state = 127;
	            this.match(HeddleParser.OUT_PARAMSTART);
	            this.state = 128;
	            this.chain();
	            this.state = 129;
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



	member_expression() {
	    let localctx = new Member_expressionContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 22, HeddleParser.RULE_member_expression);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 134;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        if(_la===5) {
	            this.state = 133;
	            this.match(HeddleParser.ROOT_REF);
	        }

	        this.state = 136;
	        this.match(HeddleParser.ID);
	        this.state = 141;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===6) {
	            this.state = 137;
	            this.match(HeddleParser.MEMBER_P);
	            this.state = 138;
	            this.match(HeddleParser.ID);
	            this.state = 143;
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
	    this.enterRule(localctx, 24, HeddleParser.RULE_extension_id);
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 144;
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
	    this.enterRule(localctx, 26, HeddleParser.RULE_csharp_expression);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 147; 
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        do {
	            this.state = 146;
	            this.match(HeddleParser.CSHARP_TOKEN);
	            this.state = 149; 
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



	subtemplate() {
	    let localctx = new SubtemplateContext(this, this._ctx, this.state);
	    this.enterRule(localctx, 28, HeddleParser.RULE_subtemplate);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 154;
	        this._errHandler.sync(this);
	        _la = this._input.LA(1);
	        while(_la===2) {
	            this.state = 151;
	            this.match(HeddleParser.WS);
	            this.state = 156;
	            this._errHandler.sync(this);
	            _la = this._input.LA(1);
	        }
	        this.state = 157;
	        this.match(HeddleParser.SUB_START);
	        this.state = 158;
	        this.heddle();
	        this.state = 159;
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
	    this.enterRule(localctx, 30, HeddleParser.RULE_text);
	    var _la = 0;
	    try {
	        this.enterOuterAlt(localctx, 1);
	        this.state = 161;
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
HeddleParser.COMMENT = 22;
HeddleParser.SKIP_WS = 23;
HeddleParser.SUB_COMMENT = 24;
HeddleParser.SUB_SKIP_WS = 25;
HeddleParser.DEF_COMMENT = 26;
HeddleParser.DEF_TYPE = 27;
HeddleParser.DEF_WS = 28;
HeddleParser.IMPORT_COMMENT = 29;
HeddleParser.CALL_RETURN_COMMENT = 30;
HeddleParser.CALL_SKIP_WS = 31;
HeddleParser.OUT_COMMENT = 32;
HeddleParser.OUT_SKIP_WS = 33;
HeddleParser.CALL_COMMENT = 34;
HeddleParser.CALL_WS = 35;
HeddleParser.ISTR_DBL_OPEN = 36;
HeddleParser.ISTR_END = 37;
HeddleParser.IVSTR_QUOTE_ESC = 38;
HeddleParser.HOLE_CLOSE = 39;

HeddleParser.RULE_heddle = 0;
HeddleParser.RULE_raw = 1;
HeddleParser.RULE_definition = 2;
HeddleParser.RULE_def = 3;
HeddleParser.RULE_def_base = 4;
HeddleParser.RULE_def_type = 5;
HeddleParser.RULE_default_chain = 6;
HeddleParser.RULE_import_block = 7;
HeddleParser.RULE_outblock = 8;
HeddleParser.RULE_chain = 9;
HeddleParser.RULE_call = 10;
HeddleParser.RULE_member_expression = 11;
HeddleParser.RULE_extension_id = 12;
HeddleParser.RULE_csharp_expression = 13;
HeddleParser.RULE_subtemplate = 14;
HeddleParser.RULE_text = 15;

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
HeddleParser.Def_baseContext = Def_baseContext; 
HeddleParser.Def_typeContext = Def_typeContext; 
HeddleParser.Default_chainContext = Default_chainContext; 
HeddleParser.Import_blockContext = Import_blockContext; 
HeddleParser.OutblockContext = OutblockContext; 
HeddleParser.ChainContext = ChainContext; 
HeddleParser.CallContext = CallContext; 
HeddleParser.Member_expressionContext = Member_expressionContext; 
HeddleParser.Extension_idContext = Extension_idContext; 
HeddleParser.Csharp_expressionContext = Csharp_expressionContext; 
HeddleParser.SubtemplateContext = SubtemplateContext; 
HeddleParser.TextContext = TextContext; 
