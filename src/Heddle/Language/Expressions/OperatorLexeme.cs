namespace Heddle.Language.Expressions
{
    /// <summary>
    /// The single <see cref="ExprOperator"/> → C# lexeme table, consolidated to prevent drift across
    /// the generator, runtime, and AST builder. Netstandard2.0-clean and linked into the generator.
    /// </summary>
    internal static class OperatorLexeme
    {
        /// <summary>The lexeme for an operator in binary position, or null for unary-only operators.</summary>
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

        /// <summary>The lexeme for an operator in unary position, or null for binary-only operators.</summary>
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

        /// <summary>Whether the operator has a binary lexeme.</summary>
        public static bool IsBinary(ExprOperator op) => ForBinary(op) != null;

        /// <summary>Whether the operator has a unary lexeme.</summary>
        public static bool IsUnary(ExprOperator op) => ForUnary(op) != null;
    }
}
