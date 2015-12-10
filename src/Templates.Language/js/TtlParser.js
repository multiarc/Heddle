// Generated from d:\Work\Templater\src\Templates.Language\TtlParser.g4 by ANTLR 4.5.1
// jshint ignore: start
var antlr4 = require('antlr4/index');
var TtlParserListener = require('./TtlParserListener').TtlParserListener;
var grammarFileName = "TtlParser.g4";

var serializedATN = ["\u0003\u0430\ud6d1\u8206\uad2d\u4417\uaef1\u8d80\uaadd",
    "\u0003\u001d\u00ae\u0004\u0002\t\u0002\u0004\u0003\t\u0003\u0004\u0004",
    "\t\u0004\u0004\u0005\t\u0005\u0004\u0006\t\u0006\u0004\u0007\t\u0007",
    "\u0004\b\t\b\u0004\t\t\t\u0004\n\t\n\u0004\u000b\t\u000b\u0004\f\t\f",
    "\u0004\r\t\r\u0004\u000e\t\u000e\u0004\u000f\t\u000f\u0004\u0010\t\u0010",
    "\u0003\u0002\u0003\u0002\u0003\u0002\u0003\u0002\u0003\u0002\u0007\u0002",
    "&\n\u0002\f\u0002\u000e\u0002)\u000b\u0002\u0003\u0003\u0003\u0003\u0003",
    "\u0004\u0003\u0004\u0003\u0005\u0003\u0005\u0006\u00051\n\u0005\r\u0005",
    "\u000e\u00052\u0003\u0005\u0003\u0005\u0003\u0006\u0003\u0006\u0005",
    "\u00069\n\u0006\u0003\u0007\u0003\u0007\u0003\u0007\u0003\u0007\u0003",
    "\u0007\u0003\u0007\u0005\u0007A\n\u0007\u0003\u0007\u0003\u0007\u0003",
    "\u0007\u0003\u0007\u0003\u0007\u0003\u0007\u0003\u0007\u0003\u0007\u0003",
    "\u0007\u0003\u0007\u0005\u0007M\n\u0007\u0003\u0007\u0005\u0007P\n\u0007",
    "\u0003\b\u0003\b\u0003\b\u0003\b\u0005\bV\n\b\u0003\b\u0003\b\u0003",
    "\b\u0003\b\u0003\b\u0003\b\u0003\b\u0003\b\u0005\b`\n\b\u0003\b\u0005",
    "\bc\n\b\u0003\t\u0003\t\u0003\t\u0003\n\u0003\n\u0003\n\u0005\nk\n\n",
    "\u0003\n\u0003\n\u0003\n\u0005\np\n\n\u0003\n\u0003\n\u0005\nt\n\n\u0003",
    "\u000b\u0003\u000b\u0003\u000b\u0007\u000by\n\u000b\f\u000b\u000e\u000b",
    "|\u000b\u000b\u0003\f\u0003\f\u0005\f\u0080\n\f\u0003\r\u0003\r\u0003",
    "\r\u0005\r\u0085\n\r\u0003\r\u0003\r\u0003\r\u0003\r\u0003\r\u0003\r",
    "\u0003\r\u0003\r\u0003\r\u0003\r\u0003\r\u0003\r\u0005\r\u0093\n\r\u0003",
    "\u000e\u0003\u000e\u0005\u000e\u0097\n\u000e\u0003\u000e\u0003\u000e",
    "\u0003\u000e\u0003\u000e\u0003\u000e\u0003\u000e\u0003\u000e\u0003\u000e",
    "\u0003\u000e\u0003\u000e\u0005\u000e\u00a3\n\u000e\u0003\u000f\u0006",
    "\u000f\u00a6\n\u000f\r\u000f\u000e\u000f\u00a7\u0003\u0010\u0003\u0010",
    "\u0003\u0010\u0003\u0010\u0003\u0010\u0002\u0002\u0011\u0002\u0004\u0006",
    "\b\n\f\u000e\u0010\u0012\u0014\u0016\u0018\u001a\u001c\u001e\u0002\u0002",
    "\u00b7\u0002\'\u0003\u0002\u0002\u0002\u0004*\u0003\u0002\u0002\u0002",
    "\u0006,\u0003\u0002\u0002\u0002\b.\u0003\u0002\u0002\u0002\n8\u0003",
    "\u0002\u0002\u0002\fO\u0003\u0002\u0002\u0002\u000eb\u0003\u0002\u0002",
    "\u0002\u0010d\u0003\u0002\u0002\u0002\u0012s\u0003\u0002\u0002\u0002",
    "\u0014u\u0003\u0002\u0002\u0002\u0016\u007f\u0003\u0002\u0002\u0002",
    "\u0018\u0092\u0003\u0002\u0002\u0002\u001a\u00a2\u0003\u0002\u0002\u0002",
    "\u001c\u00a5\u0003\u0002\u0002\u0002\u001e\u00a9\u0003\u0002\u0002\u0002",
    " &\u0005\b\u0005\u0002!&\u0005\u0012\n\u0002\"&\u0005\u0006\u0004\u0002",
    "#&\u0005\u0004\u0003\u0002$&\u0007\u0003\u0002\u0002% \u0003\u0002\u0002",
    "\u0002%!\u0003\u0002\u0002\u0002%\"\u0003\u0002\u0002\u0002%#\u0003",
    "\u0002\u0002\u0002%$\u0003\u0002\u0002\u0002&)\u0003\u0002\u0002\u0002",
    "\'%\u0003\u0002\u0002\u0002\'(\u0003\u0002\u0002\u0002(\u0003\u0003",
    "\u0002\u0002\u0002)\'\u0003\u0002\u0002\u0002*+\u0007\u0011\u0002\u0002",
    "+\u0005\u0003\u0002\u0002\u0002,-\u0007\u0012\u0002\u0002-\u0007\u0003",
    "\u0002\u0002\u0002.0\u0007\u000f\u0002\u0002/1\u0005\n\u0006\u00020",
    "/\u0003\u0002\u0002\u000212\u0003\u0002\u0002\u000220\u0003\u0002\u0002",
    "\u000223\u0003\u0002\u0002\u000234\u0003\u0002\u0002\u000245\u0007\u0010",
    "\u0002\u00025\t\u0003\u0002\u0002\u000269\u0005\u000e\b\u000279\u0005",
    "\f\u0007\u000286\u0003\u0002\u0002\u000287\u0003\u0002\u0002\u00029",
    "\u000b\u0003\u0002\u0002\u0002:;\u0007\u000b\u0002\u0002;<\u0007\u0004",
    "\u0002\u0002<=\u0007\u000e\u0002\u0002=>\u0007\u0004\u0002\u0002>@\u0007",
    "\f\u0002\u0002?A\u0005\u0010\t\u0002@?\u0003\u0002\u0002\u0002@A\u0003",
    "\u0002\u0002\u0002AB\u0003\u0002\u0002\u0002BC\u0005\u001e\u0010\u0002",
    "CD\u0007\r\u0002\u0002DE\u0007\u0004\u0002\u0002EP\u0003\u0002\u0002",
    "\u0002FG\u0007\u000b\u0002\u0002GH\u0007\u0004\u0002\u0002HI\u0007\u000e",
    "\u0002\u0002IJ\u0007\u0004\u0002\u0002JL\u0007\f\u0002\u0002KM\u0005",
    "\u0010\t\u0002LK\u0003\u0002\u0002\u0002LM\u0003\u0002\u0002\u0002M",
    "N\u0003\u0002\u0002\u0002NP\u0005\u001e\u0010\u0002O:\u0003\u0002\u0002",
    "\u0002OF\u0003\u0002\u0002\u0002P\r\u0003\u0002\u0002\u0002QR\u0007",
    "\u000b\u0002\u0002RS\u0007\u0004\u0002\u0002SU\u0007\f\u0002\u0002T",
    "V\u0005\u0010\t\u0002UT\u0003\u0002\u0002\u0002UV\u0003\u0002\u0002",
    "\u0002VW\u0003\u0002\u0002\u0002WX\u0005\u001e\u0010\u0002XY\u0007\r",
    "\u0002\u0002YZ\u0007\u0004\u0002\u0002Zc\u0003\u0002\u0002\u0002[\\",
    "\u0007\u000b\u0002\u0002\\]\u0007\u0004\u0002\u0002]_\u0007\f\u0002",
    "\u0002^`\u0005\u0010\t\u0002_^\u0003\u0002\u0002\u0002_`\u0003\u0002",
    "\u0002\u0002`a\u0003\u0002\u0002\u0002ac\u0005\u001e\u0010\u0002bQ\u0003",
    "\u0002\u0002\u0002b[\u0003\u0002\u0002\u0002c\u000f\u0003\u0002\u0002",
    "\u0002de\u0007\u0016\u0002\u0002ef\u0005\u0014\u000b\u0002f\u0011\u0003",
    "\u0002\u0002\u0002gh\u0007\u0005\u0002\u0002hj\u0005\u0014\u000b\u0002",
    "ik\u0005\u001e\u0010\u0002ji\u0003\u0002\u0002\u0002jk\u0003\u0002\u0002",
    "\u0002kt\u0003\u0002\u0002\u0002lm\u0007\u0005\u0002\u0002mo\u0005\u0014",
    "\u000b\u0002np\u0005\u001e\u0010\u0002on\u0003\u0002\u0002\u0002op\u0003",
    "\u0002\u0002\u0002pq\u0003\u0002\u0002\u0002qr\u0007\u0015\u0002\u0002",
    "rt\u0003\u0002\u0002\u0002sg\u0003\u0002\u0002\u0002sl\u0003\u0002\u0002",
    "\u0002t\u0013\u0003\u0002\u0002\u0002uz\u0005\u0016\f\u0002vw\u0007",
    "\u000e\u0002\u0002wy\u0005\u0016\f\u0002xv\u0003\u0002\u0002\u0002y",
    "|\u0003\u0002\u0002\u0002zx\u0003\u0002\u0002\u0002z{\u0003\u0002\u0002",
    "\u0002{\u0015\u0003\u0002\u0002\u0002|z\u0003\u0002\u0002\u0002}\u0080",
    "\u0005\u0018\r\u0002~\u0080\u0005\u001a\u000e\u0002\u007f}\u0003\u0002",
    "\u0002\u0002\u007f~\u0003\u0002\u0002\u0002\u0080\u0017\u0003\u0002",
    "\u0002\u0002\u0081\u0082\u0007\u0004\u0002\u0002\u0082\u0084\u0007\u0013",
    "\u0002\u0002\u0083\u0085\u0007\u0004\u0002\u0002\u0084\u0083\u0003\u0002",
    "\u0002\u0002\u0084\u0085\u0003\u0002\u0002\u0002\u0085\u0086\u0003\u0002",
    "\u0002\u0002\u0086\u0093\u0007\u0014\u0002\u0002\u0087\u0088\u0007\u0004",
    "\u0002\u0002\u0088\u0089\u0007\u0013\u0002\u0002\u0089\u008a\u0005\u0014",
    "\u000b\u0002\u008a\u008b\u0007\u0014\u0002\u0002\u008b\u0093\u0003\u0002",
    "\u0002\u0002\u008c\u008d\u0007\u0004\u0002\u0002\u008d\u008e\u0007\u0013",
    "\u0002\u0002\u008e\u008f\u0007\n\u0002\u0002\u008f\u0090\u0005\u001c",
    "\u000f\u0002\u0090\u0091\u0007\u0014\u0002\u0002\u0091\u0093\u0003\u0002",
    "\u0002\u0002\u0092\u0081\u0003\u0002\u0002\u0002\u0092\u0087\u0003\u0002",
    "\u0002\u0002\u0092\u008c\u0003\u0002\u0002\u0002\u0093\u0019\u0003\u0002",
    "\u0002\u0002\u0094\u0096\u0007\u0013\u0002\u0002\u0095\u0097\u0007\u0004",
    "\u0002\u0002\u0096\u0095\u0003\u0002\u0002\u0002\u0096\u0097\u0003\u0002",
    "\u0002\u0002\u0097\u0098\u0003\u0002\u0002\u0002\u0098\u00a3\u0007\u0014",
    "\u0002\u0002\u0099\u009a\u0007\u0013\u0002\u0002\u009a\u009b\u0005\u0014",
    "\u000b\u0002\u009b\u009c\u0007\u0014\u0002\u0002\u009c\u00a3\u0003\u0002",
    "\u0002\u0002\u009d\u009e\u0007\u0013\u0002\u0002\u009e\u009f\u0007\n",
    "\u0002\u0002\u009f\u00a0\u0005\u001c\u000f\u0002\u00a0\u00a1\u0007\u0014",
    "\u0002\u0002\u00a1\u00a3\u0003\u0002\u0002\u0002\u00a2\u0094\u0003\u0002",
    "\u0002\u0002\u00a2\u0099\u0003\u0002\u0002\u0002\u00a2\u009d\u0003\u0002",
    "\u0002\u0002\u00a3\u001b\u0003\u0002\u0002\u0002\u00a4\u00a6\u0007\t",
    "\u0002\u0002\u00a5\u00a4\u0003\u0002\u0002\u0002\u00a6\u00a7\u0003\u0002",
    "\u0002\u0002\u00a7\u00a5\u0003\u0002\u0002\u0002\u00a7\u00a8\u0003\u0002",
    "\u0002\u0002\u00a8\u001d\u0003\u0002\u0002\u0002\u00a9\u00aa\u0007\u0006",
    "\u0002\u0002\u00aa\u00ab\u0005\u0002\u0002\u0002\u00ab\u00ac\u0007\u0007",
    "\u0002\u0002\u00ac\u001f\u0003\u0002\u0002\u0002\u0016%\'28@LOU_bjo",
    "sz\u007f\u0084\u0092\u0096\u00a2\u00a7"].join("");


var atn = new antlr4.atn.ATNDeserializer().deserialize(serializedATN);

var decisionsToDFA = atn.decisionToState.map( function(ds, index) { return new antlr4.dfa.DFA(ds, index); });

var sharedContextCache = new antlr4.PredictionContextCache();

var literalNames = [  ];

var symbolicNames = [ 'null', "TEXT", "ID", "OUT", "SUB_START", "SUB_CLOSE", 
                      "CSHARP_END", "CSHARP_TOKEN", "CSHARP_START", "DEF_STARTNAME", 
                      "DEF_ENDNAME", "DEF_TYPE", "DELIM", "DEF_START", "DEF_CLOSE", 
                      "COMMENT", "RAW", "OUT_PARAMSTART", "OUT_PARAMEND", 
                      "LINE_TERMINATE", "DEF_OUTPUTONEND", "START_COMMENT", 
                      "DEF_WS", "DEF_OUT_COMMENT", "DEF_OUT_WS", "OUT_WS", 
                      "CALL_COMMENT", "CALL_OUT_WS" ];

var ruleNames =  [ "ttl", "comment", "raw", "definition", "def", "inherited_def", 
                   "simple_def", "default_chain", "outblock", "chain", "call", 
                   "named_call", "unnamed_call", "csharp_expression", "subtemplate" ];

function TtlParser (input) {
	antlr4.Parser.call(this, input);
    this._interp = new antlr4.atn.ParserATNSimulator(this, atn, decisionsToDFA, sharedContextCache);
    this.ruleNames = ruleNames;
    this.literalNames = literalNames;
    this.symbolicNames = symbolicNames;
    return this;
}

TtlParser.prototype = Object.create(antlr4.Parser.prototype);
TtlParser.prototype.constructor = TtlParser;

Object.defineProperty(TtlParser.prototype, "atn", {
	get : function() {
		return atn;
	}
});

TtlParser.EOF = antlr4.Token.EOF;
TtlParser.TEXT = 1;
TtlParser.ID = 2;
TtlParser.OUT = 3;
TtlParser.SUB_START = 4;
TtlParser.SUB_CLOSE = 5;
TtlParser.CSHARP_END = 6;
TtlParser.CSHARP_TOKEN = 7;
TtlParser.CSHARP_START = 8;
TtlParser.DEF_STARTNAME = 9;
TtlParser.DEF_ENDNAME = 10;
TtlParser.DEF_TYPE = 11;
TtlParser.DELIM = 12;
TtlParser.DEF_START = 13;
TtlParser.DEF_CLOSE = 14;
TtlParser.COMMENT = 15;
TtlParser.RAW = 16;
TtlParser.OUT_PARAMSTART = 17;
TtlParser.OUT_PARAMEND = 18;
TtlParser.LINE_TERMINATE = 19;
TtlParser.DEF_OUTPUTONEND = 20;
TtlParser.START_COMMENT = 21;
TtlParser.DEF_WS = 22;
TtlParser.DEF_OUT_COMMENT = 23;
TtlParser.DEF_OUT_WS = 24;
TtlParser.OUT_WS = 25;
TtlParser.CALL_COMMENT = 26;
TtlParser.CALL_OUT_WS = 27;

TtlParser.RULE_ttl = 0;
TtlParser.RULE_comment = 1;
TtlParser.RULE_raw = 2;
TtlParser.RULE_definition = 3;
TtlParser.RULE_def = 4;
TtlParser.RULE_inherited_def = 5;
TtlParser.RULE_simple_def = 6;
TtlParser.RULE_default_chain = 7;
TtlParser.RULE_outblock = 8;
TtlParser.RULE_chain = 9;
TtlParser.RULE_call = 10;
TtlParser.RULE_named_call = 11;
TtlParser.RULE_unnamed_call = 12;
TtlParser.RULE_csharp_expression = 13;
TtlParser.RULE_subtemplate = 14;

function TtlContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_ttl;
    return this;
}

TtlContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
TtlContext.prototype.constructor = TtlContext;

TtlContext.prototype.definition = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(DefinitionContext);
    } else {
        return this.getTypedRuleContext(DefinitionContext,i);
    }
};

TtlContext.prototype.outblock = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(OutblockContext);
    } else {
        return this.getTypedRuleContext(OutblockContext,i);
    }
};

TtlContext.prototype.raw = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(RawContext);
    } else {
        return this.getTypedRuleContext(RawContext,i);
    }
};

TtlContext.prototype.comment = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(CommentContext);
    } else {
        return this.getTypedRuleContext(CommentContext,i);
    }
};

TtlContext.prototype.TEXT = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(TtlParser.TEXT);
    } else {
        return this.getToken(TtlParser.TEXT, i);
    }
};


TtlContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterTtl(this);
	}
};

TtlContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitTtl(this);
	}
};




TtlParser.TtlContext = TtlContext;

TtlParser.prototype.ttl = function() {

    var localctx = new TtlContext(this, this._ctx, this.state);
    this.enterRule(localctx, 0, TtlParser.RULE_ttl);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 37;
        this._errHandler.sync(this);
        _la = this._input.LA(1);
        while((((_la) & ~0x1f) == 0 && ((1 << _la) & ((1 << TtlParser.TEXT) | (1 << TtlParser.OUT) | (1 << TtlParser.DEF_START) | (1 << TtlParser.COMMENT) | (1 << TtlParser.RAW))) !== 0)) {
            this.state = 35;
            switch(this._input.LA(1)) {
            case TtlParser.DEF_START:
                this.state = 30;
                this.definition();
                break;
            case TtlParser.OUT:
                this.state = 31;
                this.outblock();
                break;
            case TtlParser.RAW:
                this.state = 32;
                this.raw();
                break;
            case TtlParser.COMMENT:
                this.state = 33;
                this.comment();
                break;
            case TtlParser.TEXT:
                this.state = 34;
                this.match(TtlParser.TEXT);
                break;
            default:
                throw new antlr4.error.NoViableAltException(this);
            }
            this.state = 39;
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
};

function CommentContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_comment;
    return this;
}

CommentContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
CommentContext.prototype.constructor = CommentContext;

CommentContext.prototype.COMMENT = function() {
    return this.getToken(TtlParser.COMMENT, 0);
};

CommentContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterComment(this);
	}
};

CommentContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitComment(this);
	}
};




TtlParser.CommentContext = CommentContext;

TtlParser.prototype.comment = function() {

    var localctx = new CommentContext(this, this._ctx, this.state);
    this.enterRule(localctx, 2, TtlParser.RULE_comment);
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 40;
        this.match(TtlParser.COMMENT);
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
};

function RawContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_raw;
    return this;
}

RawContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
RawContext.prototype.constructor = RawContext;

RawContext.prototype.RAW = function() {
    return this.getToken(TtlParser.RAW, 0);
};

RawContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterRaw(this);
	}
};

RawContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitRaw(this);
	}
};




TtlParser.RawContext = RawContext;

TtlParser.prototype.raw = function() {

    var localctx = new RawContext(this, this._ctx, this.state);
    this.enterRule(localctx, 4, TtlParser.RULE_raw);
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 42;
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
};

function DefinitionContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_definition;
    return this;
}

DefinitionContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
DefinitionContext.prototype.constructor = DefinitionContext;

DefinitionContext.prototype.DEF_START = function() {
    return this.getToken(TtlParser.DEF_START, 0);
};

DefinitionContext.prototype.DEF_CLOSE = function() {
    return this.getToken(TtlParser.DEF_CLOSE, 0);
};

DefinitionContext.prototype.def = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(DefContext);
    } else {
        return this.getTypedRuleContext(DefContext,i);
    }
};

DefinitionContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterDefinition(this);
	}
};

DefinitionContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitDefinition(this);
	}
};




TtlParser.DefinitionContext = DefinitionContext;

TtlParser.prototype.definition = function() {

    var localctx = new DefinitionContext(this, this._ctx, this.state);
    this.enterRule(localctx, 6, TtlParser.RULE_definition);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 44;
        this.match(TtlParser.DEF_START);
        this.state = 46; 
        this._errHandler.sync(this);
        _la = this._input.LA(1);
        do {
            this.state = 45;
            this.def();
            this.state = 48; 
            this._errHandler.sync(this);
            _la = this._input.LA(1);
        } while(_la===TtlParser.DEF_STARTNAME);
        this.state = 50;
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
};

function DefContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_def;
    return this;
}

DefContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
DefContext.prototype.constructor = DefContext;

DefContext.prototype.simple_def = function() {
    return this.getTypedRuleContext(Simple_defContext,0);
};

DefContext.prototype.inherited_def = function() {
    return this.getTypedRuleContext(Inherited_defContext,0);
};

DefContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterDef(this);
	}
};

DefContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitDef(this);
	}
};




TtlParser.DefContext = DefContext;

TtlParser.prototype.def = function() {

    var localctx = new DefContext(this, this._ctx, this.state);
    this.enterRule(localctx, 8, TtlParser.RULE_def);
    try {
        this.state = 54;
        var la_ = this._interp.adaptivePredict(this._input,3,this._ctx);
        switch(la_) {
        case 1:
            this.enterOuterAlt(localctx, 1);
            this.state = 52;
            this.simple_def();
            break;

        case 2:
            this.enterOuterAlt(localctx, 2);
            this.state = 53;
            this.inherited_def();
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
};

function Inherited_defContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_inherited_def;
    return this;
}

Inherited_defContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
Inherited_defContext.prototype.constructor = Inherited_defContext;

Inherited_defContext.prototype.DEF_STARTNAME = function() {
    return this.getToken(TtlParser.DEF_STARTNAME, 0);
};

Inherited_defContext.prototype.ID = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(TtlParser.ID);
    } else {
        return this.getToken(TtlParser.ID, i);
    }
};


Inherited_defContext.prototype.DELIM = function() {
    return this.getToken(TtlParser.DELIM, 0);
};

Inherited_defContext.prototype.DEF_ENDNAME = function() {
    return this.getToken(TtlParser.DEF_ENDNAME, 0);
};

Inherited_defContext.prototype.subtemplate = function() {
    return this.getTypedRuleContext(SubtemplateContext,0);
};

Inherited_defContext.prototype.DEF_TYPE = function() {
    return this.getToken(TtlParser.DEF_TYPE, 0);
};

Inherited_defContext.prototype.default_chain = function() {
    return this.getTypedRuleContext(Default_chainContext,0);
};

Inherited_defContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterInherited_def(this);
	}
};

Inherited_defContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitInherited_def(this);
	}
};




TtlParser.Inherited_defContext = Inherited_defContext;

TtlParser.prototype.inherited_def = function() {

    var localctx = new Inherited_defContext(this, this._ctx, this.state);
    this.enterRule(localctx, 10, TtlParser.RULE_inherited_def);
    var _la = 0; // Token type
    try {
        this.state = 77;
        var la_ = this._interp.adaptivePredict(this._input,6,this._ctx);
        switch(la_) {
        case 1:
            this.enterOuterAlt(localctx, 1);
            this.state = 56;
            this.match(TtlParser.DEF_STARTNAME);
            this.state = 57;
            this.match(TtlParser.ID);
            this.state = 58;
            this.match(TtlParser.DELIM);
            this.state = 59;
            this.match(TtlParser.ID);
            this.state = 60;
            this.match(TtlParser.DEF_ENDNAME);
            this.state = 62;
            _la = this._input.LA(1);
            if(_la===TtlParser.DEF_OUTPUTONEND) {
                this.state = 61;
                this.default_chain();
            }

            this.state = 64;
            this.subtemplate();
            this.state = 65;
            this.match(TtlParser.DEF_TYPE);
            this.state = 66;
            this.match(TtlParser.ID);
            break;

        case 2:
            this.enterOuterAlt(localctx, 2);
            this.state = 68;
            this.match(TtlParser.DEF_STARTNAME);
            this.state = 69;
            this.match(TtlParser.ID);
            this.state = 70;
            this.match(TtlParser.DELIM);
            this.state = 71;
            this.match(TtlParser.ID);
            this.state = 72;
            this.match(TtlParser.DEF_ENDNAME);
            this.state = 74;
            _la = this._input.LA(1);
            if(_la===TtlParser.DEF_OUTPUTONEND) {
                this.state = 73;
                this.default_chain();
            }

            this.state = 76;
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
};

function Simple_defContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_simple_def;
    return this;
}

Simple_defContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
Simple_defContext.prototype.constructor = Simple_defContext;

Simple_defContext.prototype.DEF_STARTNAME = function() {
    return this.getToken(TtlParser.DEF_STARTNAME, 0);
};

Simple_defContext.prototype.ID = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(TtlParser.ID);
    } else {
        return this.getToken(TtlParser.ID, i);
    }
};


Simple_defContext.prototype.DEF_ENDNAME = function() {
    return this.getToken(TtlParser.DEF_ENDNAME, 0);
};

Simple_defContext.prototype.subtemplate = function() {
    return this.getTypedRuleContext(SubtemplateContext,0);
};

Simple_defContext.prototype.DEF_TYPE = function() {
    return this.getToken(TtlParser.DEF_TYPE, 0);
};

Simple_defContext.prototype.default_chain = function() {
    return this.getTypedRuleContext(Default_chainContext,0);
};

Simple_defContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterSimple_def(this);
	}
};

Simple_defContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitSimple_def(this);
	}
};




TtlParser.Simple_defContext = Simple_defContext;

TtlParser.prototype.simple_def = function() {

    var localctx = new Simple_defContext(this, this._ctx, this.state);
    this.enterRule(localctx, 12, TtlParser.RULE_simple_def);
    var _la = 0; // Token type
    try {
        this.state = 96;
        var la_ = this._interp.adaptivePredict(this._input,9,this._ctx);
        switch(la_) {
        case 1:
            this.enterOuterAlt(localctx, 1);
            this.state = 79;
            this.match(TtlParser.DEF_STARTNAME);
            this.state = 80;
            this.match(TtlParser.ID);
            this.state = 81;
            this.match(TtlParser.DEF_ENDNAME);
            this.state = 83;
            _la = this._input.LA(1);
            if(_la===TtlParser.DEF_OUTPUTONEND) {
                this.state = 82;
                this.default_chain();
            }

            this.state = 85;
            this.subtemplate();
            this.state = 86;
            this.match(TtlParser.DEF_TYPE);
            this.state = 87;
            this.match(TtlParser.ID);
            break;

        case 2:
            this.enterOuterAlt(localctx, 2);
            this.state = 89;
            this.match(TtlParser.DEF_STARTNAME);
            this.state = 90;
            this.match(TtlParser.ID);
            this.state = 91;
            this.match(TtlParser.DEF_ENDNAME);
            this.state = 93;
            _la = this._input.LA(1);
            if(_la===TtlParser.DEF_OUTPUTONEND) {
                this.state = 92;
                this.default_chain();
            }

            this.state = 95;
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
};

function Default_chainContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_default_chain;
    return this;
}

Default_chainContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
Default_chainContext.prototype.constructor = Default_chainContext;

Default_chainContext.prototype.DEF_OUTPUTONEND = function() {
    return this.getToken(TtlParser.DEF_OUTPUTONEND, 0);
};

Default_chainContext.prototype.chain = function() {
    return this.getTypedRuleContext(ChainContext,0);
};

Default_chainContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterDefault_chain(this);
	}
};

Default_chainContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitDefault_chain(this);
	}
};




TtlParser.Default_chainContext = Default_chainContext;

TtlParser.prototype.default_chain = function() {

    var localctx = new Default_chainContext(this, this._ctx, this.state);
    this.enterRule(localctx, 14, TtlParser.RULE_default_chain);
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 98;
        this.match(TtlParser.DEF_OUTPUTONEND);
        this.state = 99;
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
};

function OutblockContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_outblock;
    return this;
}

OutblockContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
OutblockContext.prototype.constructor = OutblockContext;

OutblockContext.prototype.OUT = function() {
    return this.getToken(TtlParser.OUT, 0);
};

OutblockContext.prototype.chain = function() {
    return this.getTypedRuleContext(ChainContext,0);
};

OutblockContext.prototype.subtemplate = function() {
    return this.getTypedRuleContext(SubtemplateContext,0);
};

OutblockContext.prototype.LINE_TERMINATE = function() {
    return this.getToken(TtlParser.LINE_TERMINATE, 0);
};

OutblockContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterOutblock(this);
	}
};

OutblockContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitOutblock(this);
	}
};




TtlParser.OutblockContext = OutblockContext;

TtlParser.prototype.outblock = function() {

    var localctx = new OutblockContext(this, this._ctx, this.state);
    this.enterRule(localctx, 16, TtlParser.RULE_outblock);
    var _la = 0; // Token type
    try {
        this.state = 113;
        var la_ = this._interp.adaptivePredict(this._input,12,this._ctx);
        switch(la_) {
        case 1:
            this.enterOuterAlt(localctx, 1);
            this.state = 101;
            this.match(TtlParser.OUT);
            this.state = 102;
            this.chain();
            this.state = 104;
            _la = this._input.LA(1);
            if(_la===TtlParser.SUB_START) {
                this.state = 103;
                this.subtemplate();
            }

            break;

        case 2:
            this.enterOuterAlt(localctx, 2);
            this.state = 106;
            this.match(TtlParser.OUT);
            this.state = 107;
            this.chain();
            this.state = 109;
            _la = this._input.LA(1);
            if(_la===TtlParser.SUB_START) {
                this.state = 108;
                this.subtemplate();
            }

            this.state = 111;
            this.match(TtlParser.LINE_TERMINATE);
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
};

function ChainContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_chain;
    return this;
}

ChainContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
ChainContext.prototype.constructor = ChainContext;

ChainContext.prototype.call = function(i) {
    if(i===undefined) {
        i = null;
    }
    if(i===null) {
        return this.getTypedRuleContexts(CallContext);
    } else {
        return this.getTypedRuleContext(CallContext,i);
    }
};

ChainContext.prototype.DELIM = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(TtlParser.DELIM);
    } else {
        return this.getToken(TtlParser.DELIM, i);
    }
};


ChainContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterChain(this);
	}
};

ChainContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitChain(this);
	}
};




TtlParser.ChainContext = ChainContext;

TtlParser.prototype.chain = function() {

    var localctx = new ChainContext(this, this._ctx, this.state);
    this.enterRule(localctx, 18, TtlParser.RULE_chain);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 115;
        this.call();
        this.state = 120;
        this._errHandler.sync(this);
        _la = this._input.LA(1);
        while(_la===TtlParser.DELIM) {
            this.state = 116;
            this.match(TtlParser.DELIM);
            this.state = 117;
            this.call();
            this.state = 122;
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
};

function CallContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_call;
    return this;
}

CallContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
CallContext.prototype.constructor = CallContext;

CallContext.prototype.named_call = function() {
    return this.getTypedRuleContext(Named_callContext,0);
};

CallContext.prototype.unnamed_call = function() {
    return this.getTypedRuleContext(Unnamed_callContext,0);
};

CallContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterCall(this);
	}
};

CallContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitCall(this);
	}
};




TtlParser.CallContext = CallContext;

TtlParser.prototype.call = function() {

    var localctx = new CallContext(this, this._ctx, this.state);
    this.enterRule(localctx, 20, TtlParser.RULE_call);
    try {
        this.state = 125;
        switch(this._input.LA(1)) {
        case TtlParser.ID:
            this.enterOuterAlt(localctx, 1);
            this.state = 123;
            this.named_call();
            break;
        case TtlParser.OUT_PARAMSTART:
            this.enterOuterAlt(localctx, 2);
            this.state = 124;
            this.unnamed_call();
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
};

function Named_callContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_named_call;
    return this;
}

Named_callContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
Named_callContext.prototype.constructor = Named_callContext;

Named_callContext.prototype.ID = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(TtlParser.ID);
    } else {
        return this.getToken(TtlParser.ID, i);
    }
};


Named_callContext.prototype.OUT_PARAMSTART = function() {
    return this.getToken(TtlParser.OUT_PARAMSTART, 0);
};

Named_callContext.prototype.OUT_PARAMEND = function() {
    return this.getToken(TtlParser.OUT_PARAMEND, 0);
};

Named_callContext.prototype.chain = function() {
    return this.getTypedRuleContext(ChainContext,0);
};

Named_callContext.prototype.CSHARP_START = function() {
    return this.getToken(TtlParser.CSHARP_START, 0);
};

Named_callContext.prototype.csharp_expression = function() {
    return this.getTypedRuleContext(Csharp_expressionContext,0);
};

Named_callContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterNamed_call(this);
	}
};

Named_callContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitNamed_call(this);
	}
};




TtlParser.Named_callContext = Named_callContext;

TtlParser.prototype.named_call = function() {

    var localctx = new Named_callContext(this, this._ctx, this.state);
    this.enterRule(localctx, 22, TtlParser.RULE_named_call);
    var _la = 0; // Token type
    try {
        this.state = 144;
        var la_ = this._interp.adaptivePredict(this._input,16,this._ctx);
        switch(la_) {
        case 1:
            this.enterOuterAlt(localctx, 1);
            this.state = 127;
            this.match(TtlParser.ID);
            this.state = 128;
            this.match(TtlParser.OUT_PARAMSTART);
            this.state = 130;
            _la = this._input.LA(1);
            if(_la===TtlParser.ID) {
                this.state = 129;
                this.match(TtlParser.ID);
            }

            this.state = 132;
            this.match(TtlParser.OUT_PARAMEND);
            break;

        case 2:
            this.enterOuterAlt(localctx, 2);
            this.state = 133;
            this.match(TtlParser.ID);
            this.state = 134;
            this.match(TtlParser.OUT_PARAMSTART);
            this.state = 135;
            this.chain();
            this.state = 136;
            this.match(TtlParser.OUT_PARAMEND);
            break;

        case 3:
            this.enterOuterAlt(localctx, 3);
            this.state = 138;
            this.match(TtlParser.ID);
            this.state = 139;
            this.match(TtlParser.OUT_PARAMSTART);
            this.state = 140;
            this.match(TtlParser.CSHARP_START);
            this.state = 141;
            this.csharp_expression();
            this.state = 142;
            this.match(TtlParser.OUT_PARAMEND);
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
};

function Unnamed_callContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_unnamed_call;
    return this;
}

Unnamed_callContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
Unnamed_callContext.prototype.constructor = Unnamed_callContext;

Unnamed_callContext.prototype.OUT_PARAMSTART = function() {
    return this.getToken(TtlParser.OUT_PARAMSTART, 0);
};

Unnamed_callContext.prototype.OUT_PARAMEND = function() {
    return this.getToken(TtlParser.OUT_PARAMEND, 0);
};

Unnamed_callContext.prototype.ID = function() {
    return this.getToken(TtlParser.ID, 0);
};

Unnamed_callContext.prototype.chain = function() {
    return this.getTypedRuleContext(ChainContext,0);
};

Unnamed_callContext.prototype.CSHARP_START = function() {
    return this.getToken(TtlParser.CSHARP_START, 0);
};

Unnamed_callContext.prototype.csharp_expression = function() {
    return this.getTypedRuleContext(Csharp_expressionContext,0);
};

Unnamed_callContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterUnnamed_call(this);
	}
};

Unnamed_callContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitUnnamed_call(this);
	}
};




TtlParser.Unnamed_callContext = Unnamed_callContext;

TtlParser.prototype.unnamed_call = function() {

    var localctx = new Unnamed_callContext(this, this._ctx, this.state);
    this.enterRule(localctx, 24, TtlParser.RULE_unnamed_call);
    var _la = 0; // Token type
    try {
        this.state = 160;
        var la_ = this._interp.adaptivePredict(this._input,18,this._ctx);
        switch(la_) {
        case 1:
            this.enterOuterAlt(localctx, 1);
            this.state = 146;
            this.match(TtlParser.OUT_PARAMSTART);
            this.state = 148;
            _la = this._input.LA(1);
            if(_la===TtlParser.ID) {
                this.state = 147;
                this.match(TtlParser.ID);
            }

            this.state = 150;
            this.match(TtlParser.OUT_PARAMEND);
            break;

        case 2:
            this.enterOuterAlt(localctx, 2);
            this.state = 151;
            this.match(TtlParser.OUT_PARAMSTART);
            this.state = 152;
            this.chain();
            this.state = 153;
            this.match(TtlParser.OUT_PARAMEND);
            break;

        case 3:
            this.enterOuterAlt(localctx, 3);
            this.state = 155;
            this.match(TtlParser.OUT_PARAMSTART);
            this.state = 156;
            this.match(TtlParser.CSHARP_START);
            this.state = 157;
            this.csharp_expression();
            this.state = 158;
            this.match(TtlParser.OUT_PARAMEND);
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
};

function Csharp_expressionContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_csharp_expression;
    return this;
}

Csharp_expressionContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
Csharp_expressionContext.prototype.constructor = Csharp_expressionContext;

Csharp_expressionContext.prototype.CSHARP_TOKEN = function(i) {
	if(i===undefined) {
		i = null;
	}
    if(i===null) {
        return this.getTokens(TtlParser.CSHARP_TOKEN);
    } else {
        return this.getToken(TtlParser.CSHARP_TOKEN, i);
    }
};


Csharp_expressionContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterCsharp_expression(this);
	}
};

Csharp_expressionContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitCsharp_expression(this);
	}
};




TtlParser.Csharp_expressionContext = Csharp_expressionContext;

TtlParser.prototype.csharp_expression = function() {

    var localctx = new Csharp_expressionContext(this, this._ctx, this.state);
    this.enterRule(localctx, 26, TtlParser.RULE_csharp_expression);
    var _la = 0; // Token type
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 163; 
        this._errHandler.sync(this);
        _la = this._input.LA(1);
        do {
            this.state = 162;
            this.match(TtlParser.CSHARP_TOKEN);
            this.state = 165; 
            this._errHandler.sync(this);
            _la = this._input.LA(1);
        } while(_la===TtlParser.CSHARP_TOKEN);
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
};

function SubtemplateContext(parser, parent, invokingState) {
	if(parent===undefined) {
	    parent = null;
	}
	if(invokingState===undefined || invokingState===null) {
		invokingState = -1;
	}
	antlr4.ParserRuleContext.call(this, parent, invokingState);
    this.parser = parser;
    this.ruleIndex = TtlParser.RULE_subtemplate;
    return this;
}

SubtemplateContext.prototype = Object.create(antlr4.ParserRuleContext.prototype);
SubtemplateContext.prototype.constructor = SubtemplateContext;

SubtemplateContext.prototype.SUB_START = function() {
    return this.getToken(TtlParser.SUB_START, 0);
};

SubtemplateContext.prototype.ttl = function() {
    return this.getTypedRuleContext(TtlContext,0);
};

SubtemplateContext.prototype.SUB_CLOSE = function() {
    return this.getToken(TtlParser.SUB_CLOSE, 0);
};

SubtemplateContext.prototype.enterRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.enterSubtemplate(this);
	}
};

SubtemplateContext.prototype.exitRule = function(listener) {
    if(listener instanceof TtlParserListener ) {
        listener.exitSubtemplate(this);
	}
};




TtlParser.SubtemplateContext = SubtemplateContext;

TtlParser.prototype.subtemplate = function() {

    var localctx = new SubtemplateContext(this, this._ctx, this.state);
    this.enterRule(localctx, 28, TtlParser.RULE_subtemplate);
    try {
        this.enterOuterAlt(localctx, 1);
        this.state = 167;
        this.match(TtlParser.SUB_START);
        this.state = 168;
        this.ttl();
        this.state = 169;
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
};


exports.TtlParser = TtlParser;
