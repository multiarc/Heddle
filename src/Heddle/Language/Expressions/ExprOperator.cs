namespace Heddle.Language.Expressions
{
    /// <summary>
    /// The operators representable in the native-expression AST. Binary and unary flavors share the enum;
    /// which node type carries the value disambiguates them.
    /// </summary>
    public enum ExprOperator
    {
        // Binary
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        LeftShift,
        RightShift,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal,
        NotEqual,
        And,
        ExclusiveOr,
        Or,
        AndAlso,
        OrElse,
        Coalesce,

        // Unary
        Not,
        Negate,
        UnaryPlus,
        OnesComplement
    }
}
