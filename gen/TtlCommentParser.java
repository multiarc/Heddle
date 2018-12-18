// Generated from C:/Docs/Work/Templater/src/Templates.Language\TtlCommentParser.g4 by ANTLR 4.7
import org.antlr.v4.runtime.atn.*;
import org.antlr.v4.runtime.dfa.DFA;
import org.antlr.v4.runtime.*;
import org.antlr.v4.runtime.misc.*;
import org.antlr.v4.runtime.tree.*;
import java.util.List;
import java.util.Iterator;
import java.util.ArrayList;

@SuppressWarnings({"all", "warnings", "unchecked", "unused", "cast"})
public class TtlCommentParser extends Parser {
	static { RuntimeMetaData.checkVersion("4.7", RuntimeMetaData.VERSION); }

	protected static final DFA[] _decisionToDFA;
	protected static final PredictionContextCache _sharedContextCache =
		new PredictionContextCache();
	public static final int
		COMMENT=1, RAW=2, RW_LINE=3, START_IMPORT=4, START_OUT=5, DEF_OUT=6, TEXT=7, 
		IMPORT_COMMENT=8, IMPORT_SUBSTART=9, IMPORT_SUBEND=10, OUT_COMMENT=11, 
		OUT_WS=12, OUT_ID=13, OUT_OUTPARAMSTART=14, OUT_DELIM=15, OUT_OUT_START=16, 
		OUT_DEF_START=17, OUT_SUB_START=18, OUT_SUB_CL=19, OUT_OTHER=20, CALL_COMMENT=21, 
		CSHARP_START=22, CALL_PARAMSTART=23, CALL_PARAMEND=24, CALL_DELIM=25, 
		CALL_ROOT_REF=26, CALL_ID=27, CALL_MEMB_P=28, CALL_WS=29, CS_CSHARP_WS=30, 
		CS_CSHARP_START=31, CS_CSHARP_END=32, CS_CSHARP_TOKEN=33;
	public static final int
		RULE_clean = 0;
	public static final String[] ruleNames = {
		"clean"
	};

	private static final String[] _LITERAL_NAMES = {
	};
	private static final String[] _SYMBOLIC_NAMES = {
		null, "COMMENT", "RAW", "RW_LINE", "START_IMPORT", "START_OUT", "DEF_OUT", 
		"TEXT", "IMPORT_COMMENT", "IMPORT_SUBSTART", "IMPORT_SUBEND", "OUT_COMMENT", 
		"OUT_WS", "OUT_ID", "OUT_OUTPARAMSTART", "OUT_DELIM", "OUT_OUT_START", 
		"OUT_DEF_START", "OUT_SUB_START", "OUT_SUB_CL", "OUT_OTHER", "CALL_COMMENT", 
		"CSHARP_START", "CALL_PARAMSTART", "CALL_PARAMEND", "CALL_DELIM", "CALL_ROOT_REF", 
		"CALL_ID", "CALL_MEMB_P", "CALL_WS", "CS_CSHARP_WS", "CS_CSHARP_START", 
		"CS_CSHARP_END", "CS_CSHARP_TOKEN"
	};
	public static final Vocabulary VOCABULARY = new VocabularyImpl(_LITERAL_NAMES, _SYMBOLIC_NAMES);

	/**
	 * @deprecated Use {@link #VOCABULARY} instead.
	 */
	@Deprecated
	public static final String[] tokenNames;
	static {
		tokenNames = new String[_SYMBOLIC_NAMES.length];
		for (int i = 0; i < tokenNames.length; i++) {
			tokenNames[i] = VOCABULARY.getLiteralName(i);
			if (tokenNames[i] == null) {
				tokenNames[i] = VOCABULARY.getSymbolicName(i);
			}

			if (tokenNames[i] == null) {
				tokenNames[i] = "<INVALID>";
			}
		}
	}

	@Override
	@Deprecated
	public String[] getTokenNames() {
		return tokenNames;
	}

	@Override

	public Vocabulary getVocabulary() {
		return VOCABULARY;
	}

	@Override
	public String getGrammarFileName() { return "TtlCommentParser.g4"; }

	@Override
	public String[] getRuleNames() { return ruleNames; }

	@Override
	public String getSerializedATN() { return _serializedATN; }

	@Override
	public ATN getATN() { return _ATN; }

	public TtlCommentParser(TokenStream input) {
		super(input);
		_interp = new ParserATNSimulator(this,_ATN,_decisionToDFA,_sharedContextCache);
	}
	public static class CleanContext extends ParserRuleContext {
		public CleanContext(ParserRuleContext parent, int invokingState) {
			super(parent, invokingState);
		}
		@Override public int getRuleIndex() { return RULE_clean; }
		@Override
		public void enterRule(ParseTreeListener listener) {
			if ( listener instanceof TtlCommentParserListener ) ((TtlCommentParserListener)listener).enterClean(this);
		}
		@Override
		public void exitRule(ParseTreeListener listener) {
			if ( listener instanceof TtlCommentParserListener ) ((TtlCommentParserListener)listener).exitClean(this);
		}
		@Override
		public <T> T accept(ParseTreeVisitor<? extends T> visitor) {
			if ( visitor instanceof TtlCommentParserVisitor ) return ((TtlCommentParserVisitor<? extends T>)visitor).visitClean(this);
			else return visitor.visitChildren(this);
		}
	}

	public final CleanContext clean() throws RecognitionException {
		CleanContext _localctx = new CleanContext(_ctx, getState());
		enterRule(_localctx, 0, RULE_clean);
		try {
			int _alt;
			enterOuterAlt(_localctx, 1);
			{
			setState(5);
			_errHandler.sync(this);
			_alt = getInterpreter().adaptivePredict(_input,0,_ctx);
			while ( _alt!=1 && _alt!=org.antlr.v4.runtime.atn.ATN.INVALID_ALT_NUMBER ) {
				if ( _alt==1+1 ) {
					{
					{
					setState(2);
					matchWildcard();
					}
					} 
				}
				setState(7);
				_errHandler.sync(this);
				_alt = getInterpreter().adaptivePredict(_input,0,_ctx);
			}
			}
		}
		catch (RecognitionException re) {
			_localctx.exception = re;
			_errHandler.reportError(this, re);
			_errHandler.recover(this, re);
		}
		finally {
			exitRule();
		}
		return _localctx;
	}

	public static final String _serializedATN =
		"\3\u608b\ua72a\u8133\ub9ed\u417c\u3be7\u7786\u5964\3#\13\4\2\t\2\3\2\7"+
		"\2\6\n\2\f\2\16\2\t\13\2\3\2\3\7\2\3\2\2\2\2\n\2\7\3\2\2\2\4\6\13\2\2"+
		"\2\5\4\3\2\2\2\6\t\3\2\2\2\7\b\3\2\2\2\7\5\3\2\2\2\b\3\3\2\2\2\t\7\3\2"+
		"\2\2\3\7";
	public static final ATN _ATN =
		new ATNDeserializer().deserialize(_serializedATN.toCharArray());
	static {
		_decisionToDFA = new DFA[_ATN.getNumberOfDecisions()];
		for (int i = 0; i < _ATN.getNumberOfDecisions(); i++) {
			_decisionToDFA[i] = new DFA(_ATN.getDecisionState(i), i);
		}
	}
}