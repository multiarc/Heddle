using Heddle.Strings.Core;

namespace Heddle.Language.Expressions
{
    /// <summary>
    /// The <c>this</c> keyword: the current scope's model, statically typed as the current scope type.
    /// Immutable, like every <see cref="ExprNode"/>. As a whole expression it is the model passthrough
    /// (the empty parameter); as an operand or path root it is a typed operand following the phase 1
    /// dynamic-operand rule.
    /// </summary>
    public sealed class ThisNode : ExprNode
    {
        public ThisNode(BlockPosition position) : base(position)
        {
        }
    }
}
