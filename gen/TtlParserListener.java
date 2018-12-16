// Generated from C:/Docs/Work/Templater/src/Templates.Language\TtlParser.g4 by ANTLR 4.7
import org.antlr.v4.runtime.tree.ParseTreeListener;

/**
 * This interface defines a complete listener for a parse tree produced by
 * {@link TtlParser}.
 */
public interface TtlParserListener extends ParseTreeListener {
	/**
	 * Enter a parse tree produced by {@link TtlParser#ttl}.
	 * @param ctx the parse tree
	 */
	void enterTtl(TtlParser.TtlContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#ttl}.
	 * @param ctx the parse tree
	 */
	void exitTtl(TtlParser.TtlContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#comment}.
	 * @param ctx the parse tree
	 */
	void enterComment(TtlParser.CommentContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#comment}.
	 * @param ctx the parse tree
	 */
	void exitComment(TtlParser.CommentContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#raw}.
	 * @param ctx the parse tree
	 */
	void enterRaw(TtlParser.RawContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#raw}.
	 * @param ctx the parse tree
	 */
	void exitRaw(TtlParser.RawContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#definition}.
	 * @param ctx the parse tree
	 */
	void enterDefinition(TtlParser.DefinitionContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#definition}.
	 * @param ctx the parse tree
	 */
	void exitDefinition(TtlParser.DefinitionContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#def}.
	 * @param ctx the parse tree
	 */
	void enterDef(TtlParser.DefContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#def}.
	 * @param ctx the parse tree
	 */
	void exitDef(TtlParser.DefContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#inherited_def}.
	 * @param ctx the parse tree
	 */
	void enterInherited_def(TtlParser.Inherited_defContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#inherited_def}.
	 * @param ctx the parse tree
	 */
	void exitInherited_def(TtlParser.Inherited_defContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#simple_def}.
	 * @param ctx the parse tree
	 */
	void enterSimple_def(TtlParser.Simple_defContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#simple_def}.
	 * @param ctx the parse tree
	 */
	void exitSimple_def(TtlParser.Simple_defContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#default_chain}.
	 * @param ctx the parse tree
	 */
	void enterDefault_chain(TtlParser.Default_chainContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#default_chain}.
	 * @param ctx the parse tree
	 */
	void exitDefault_chain(TtlParser.Default_chainContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#import_block}.
	 * @param ctx the parse tree
	 */
	void enterImport_block(TtlParser.Import_blockContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#import_block}.
	 * @param ctx the parse tree
	 */
	void exitImport_block(TtlParser.Import_blockContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#outblock}.
	 * @param ctx the parse tree
	 */
	void enterOutblock(TtlParser.OutblockContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#outblock}.
	 * @param ctx the parse tree
	 */
	void exitOutblock(TtlParser.OutblockContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#chain}.
	 * @param ctx the parse tree
	 */
	void enterChain(TtlParser.ChainContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#chain}.
	 * @param ctx the parse tree
	 */
	void exitChain(TtlParser.ChainContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#call}.
	 * @param ctx the parse tree
	 */
	void enterCall(TtlParser.CallContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#call}.
	 * @param ctx the parse tree
	 */
	void exitCall(TtlParser.CallContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#named_call}.
	 * @param ctx the parse tree
	 */
	void enterNamed_call(TtlParser.Named_callContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#named_call}.
	 * @param ctx the parse tree
	 */
	void exitNamed_call(TtlParser.Named_callContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#unnamed_call}.
	 * @param ctx the parse tree
	 */
	void enterUnnamed_call(TtlParser.Unnamed_callContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#unnamed_call}.
	 * @param ctx the parse tree
	 */
	void exitUnnamed_call(TtlParser.Unnamed_callContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#csharp_expression}.
	 * @param ctx the parse tree
	 */
	void enterCsharp_expression(TtlParser.Csharp_expressionContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#csharp_expression}.
	 * @param ctx the parse tree
	 */
	void exitCsharp_expression(TtlParser.Csharp_expressionContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#subtemplate}.
	 * @param ctx the parse tree
	 */
	void enterSubtemplate(TtlParser.SubtemplateContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#subtemplate}.
	 * @param ctx the parse tree
	 */
	void exitSubtemplate(TtlParser.SubtemplateContext ctx);
	/**
	 * Enter a parse tree produced by {@link TtlParser#text}.
	 * @param ctx the parse tree
	 */
	void enterText(TtlParser.TextContext ctx);
	/**
	 * Exit a parse tree produced by {@link TtlParser#text}.
	 * @param ctx the parse tree
	 */
	void exitText(TtlParser.TextContext ctx);
}