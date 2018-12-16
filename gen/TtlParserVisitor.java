// Generated from C:/Docs/Work/Templater/src/Templates.Language\TtlParser.g4 by ANTLR 4.7
import org.antlr.v4.runtime.tree.ParseTreeVisitor;

/**
 * This interface defines a complete generic visitor for a parse tree produced
 * by {@link TtlParser}.
 *
 * @param <T> The return type of the visit operation. Use {@link Void} for
 * operations with no return type.
 */
public interface TtlParserVisitor<T> extends ParseTreeVisitor<T> {
	/**
	 * Visit a parse tree produced by {@link TtlParser#ttl}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitTtl(TtlParser.TtlContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#raw}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitRaw(TtlParser.RawContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#definition}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitDefinition(TtlParser.DefinitionContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#def}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitDef(TtlParser.DefContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#inherited_def}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitInherited_def(TtlParser.Inherited_defContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#simple_def}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitSimple_def(TtlParser.Simple_defContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#default_chain}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitDefault_chain(TtlParser.Default_chainContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#import_block}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitImport_block(TtlParser.Import_blockContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#outblock}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitOutblock(TtlParser.OutblockContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#chain}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitChain(TtlParser.ChainContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#call}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitCall(TtlParser.CallContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#named_call}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitNamed_call(TtlParser.Named_callContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#unnamed_call}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitUnnamed_call(TtlParser.Unnamed_callContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#csharp_expression}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitCsharp_expression(TtlParser.Csharp_expressionContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#subtemplate}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitSubtemplate(TtlParser.SubtemplateContext ctx);
	/**
	 * Visit a parse tree produced by {@link TtlParser#text}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitText(TtlParser.TextContext ctx);
}