// Generated from C:/Docs/Work/Templater/src/Templates.Language\TtlCommentParser.g4 by ANTLR 4.7
import org.antlr.v4.runtime.tree.ParseTreeVisitor;

/**
 * This interface defines a complete generic visitor for a parse tree produced
 * by {@link TtlCommentParser}.
 *
 * @param <T> The return type of the visit operation. Use {@link Void} for
 * operations with no return type.
 */
public interface TtlCommentParserVisitor<T> extends ParseTreeVisitor<T> {
	/**
	 * Visit a parse tree produced by {@link TtlCommentParser#clean}.
	 * @param ctx the parse tree
	 * @return the visitor result
	 */
	T visitClean(TtlCommentParser.CleanContext ctx);
}