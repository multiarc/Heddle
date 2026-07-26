namespace Heddle.Language.Expressions
{
    /// <summary>
    /// The one <see cref="ExprOperator"/> → C# lexeme table (phase 4 D4/F5). Before this file the same list lived in
    /// three places — the generator's <c>BinarySymbol</c> plus its inline unary lexemes, the runtime's error-text
    /// <c>Symbol</c> (which never carried <c>&amp;&amp;</c>/<c>||</c>/<c>??</c>), and the AST builder's token
    /// mapping — so adding an operator meant four touch points and a transposed <c>&amp;</c>/<c>&amp;&amp;</c> in the
    /// generator was a silent miscompile rather than a build break.
    /// <para>Roslyn-free and netstandard2.0-clean: this file compiles into <c>Heddle</c> and is linked into the
    /// generator by the existing <c>Language\**</c> glob, beside <see cref="ExprOperator"/> itself.</para>
    /// </summary>
    internal static class OperatorLexeme
    {
        /// <summary>The lexeme for an operator in <b>binary</b> position, or null when the value is a unary-only
        /// operator (the exact contract the generator's <c>BinarySymbol</c> had: null means "do not emit").</summary>
        public static string ForBinary(ExprOperator op)
        {
            switch (op)
            {
                case ExprOperator.Add: return "+";
                case ExprOperator.Subtract: return "-";
                case ExprOperator.Multiply: return "*";
                case ExprOperator.Divide: return "/";
                case ExprOperator.Modulo: return "%";
                case ExprOperator.LeftShift: return "<<";
                case ExprOperator.RightShift: return ">>";
                case ExprOperator.LessThan: return "<";
                case ExprOperator.LessThanOrEqual: return "<=";
                case ExprOperator.GreaterThan: return ">";
                case ExprOperator.GreaterThanOrEqual: return ">=";
                case ExprOperator.Equal: return "==";
                case ExprOperator.NotEqual: return "!=";
                case ExprOperator.And: return "&";
                case ExprOperator.ExclusiveOr: return "^";
                case ExprOperator.Or: return "|";
                case ExprOperator.AndAlso: return "&&";
                case ExprOperator.OrElse: return "||";
                case ExprOperator.Coalesce: return "??";
                default: return null;
            }
        }

        /// <summary>The lexeme for an operator in <b>unary</b> position, or null when the value is a binary-only
        /// operator.</summary>
        public static string ForUnary(ExprOperator op)
        {
            switch (op)
            {
                case ExprOperator.Not: return "!";
                case ExprOperator.Negate: return "-";
                case ExprOperator.UnaryPlus: return "+";
                case ExprOperator.OnesComplement: return "~";
                default: return null;
            }
        }

        /// <summary>The lexeme for an operator in whichever position it is defined for — the display form used in
        /// diagnostics, where the node type has already disambiguated the arity.</summary>
        public static string For(ExprOperator op) => ForBinary(op) ?? ForUnary(op);

        /// <summary>Whether the operator has a binary lexeme (the generator's emittable-binary set).</summary>
        public static bool IsBinary(ExprOperator op) => ForBinary(op) != null;

        /// <summary>Whether the operator has a unary lexeme (the generator's emittable-unary set).</summary>
        public static bool IsUnary(ExprOperator op) => ForUnary(op) != null;
    }
}
