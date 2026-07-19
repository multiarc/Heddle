using System.Collections.Generic;
using Heddle.Strings.Core;

namespace Heddle.Language.Expressions
{
    /// <summary>
    /// Base of the native-expression AST. Nodes are created by the parser-side builder; instances are
    /// immutable after construction and safe to share.
    /// </summary>
    public abstract class ExprNode
    {
        protected ExprNode(BlockPosition position)
        {
            Position = position;
        }

        /// <summary>Absolute template position (same convention as <see cref="OutputItem.Position"/>).</summary>
        public BlockPosition Position { get; }
    }

    /// <summary>A binary operator applied to two operands.</summary>
    public sealed class BinaryNode : ExprNode
    {
        public BinaryNode(ExprOperator op, ExprNode left, ExprNode right, BlockPosition position) : base(position)
        {
            Operator = op;
            Left = left;
            Right = right;
        }

        public ExprOperator Operator { get; }
        public ExprNode Left { get; }
        public ExprNode Right { get; }
    }

    /// <summary>A unary operator applied to a single operand.</summary>
    public sealed class UnaryNode : ExprNode
    {
        public UnaryNode(ExprOperator op, ExprNode operand, BlockPosition position) : base(position)
        {
            Operator = op;
            Operand = operand;
        }

        public ExprOperator Operator { get; }
        public ExprNode Operand { get; }
    }

    /// <summary>The conditional operator <c>condition ? whenTrue : whenFalse</c>.</summary>
    public sealed class TernaryNode : ExprNode
    {
        public TernaryNode(ExprNode condition, ExprNode whenTrue, ExprNode whenFalse, BlockPosition position)
            : base(position)
        {
            Condition = condition;
            WhenTrue = whenTrue;
            WhenFalse = whenFalse;
        }

        public ExprNode Condition { get; }
        public ExprNode WhenTrue { get; }
        public ExprNode WhenFalse { get; }
    }

    /// <summary>
    /// A literal. <see cref="Value"/> is the decoded CLR value
    /// (int/long/uint/ulong/float/double/decimal/string/char/bool) or <c>null</c> for the null literal.
    /// </summary>
    public sealed class LiteralNode : ExprNode
    {
        public LiteralNode(object value, BlockPosition position) : base(position)
        {
            Value = value;
        }

        public object Value { get; }

        /// <summary>Set by the builder when a numeric literal was out of range; the compiler reports HED1008.</summary>
        internal string LiteralError { get; set; }
    }

    /// <summary>
    /// A property path. <see cref="Target"/> == <c>null</c> roots the path at the scope (<see cref="RootRef"/>
    /// selects the root scope); <see cref="Target"/> != <c>null</c> hops off that expression's static type.
    /// </summary>
    public sealed class PathNode : ExprNode
    {
        public PathNode(bool rootRef, IReadOnlyList<string> segments, ExprNode target, BlockPosition position)
            : base(position)
        {
            RootRef = rootRef;
            Segments = segments;
            Target = target;
        }

        public bool RootRef { get; }
        public IReadOnlyList<string> Segments { get; }
        public ExprNode Target { get; }
    }

    /// <summary>An indexer access <c>target[args]</c>.</summary>
    public sealed class IndexNode : ExprNode
    {
        public IndexNode(ExprNode target, IReadOnlyList<ExprNode> arguments, BlockPosition position) : base(position)
        {
            Target = target;
            Arguments = arguments;
        }

        public ExprNode Target { get; }
        public IReadOnlyList<ExprNode> Arguments { get; }
    }

    /// <summary>A registered-function call: <c>name(args)</c>.</summary>
    public sealed class CallNode : ExprNode
    {
        public CallNode(string name, IReadOnlyList<ExprNode> arguments, BlockPosition position) : base(position)
        {
            Name = name;
            Arguments = arguments;
        }

        public string Name { get; }
        public IReadOnlyList<ExprNode> Arguments { get; }
    }

    /// <summary>
    /// Method-call syntax <c>target.Name(args)</c> — parsed for diagnostics, always rejected at compile time
    /// with HED1003.
    /// </summary>
    public sealed class MethodCallNode : ExprNode
    {
        public MethodCallNode(ExprNode target, string name, IReadOnlyList<ExprNode> arguments, BlockPosition position)
            : base(position)
        {
            Target = target;
            Name = name;
            Arguments = arguments;
        }

        public ExprNode Target { get; }
        public string Name { get; }
        public IReadOnlyList<ExprNode> Arguments { get; }
    }
}
