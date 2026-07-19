using Heddle.Language.Expressions;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>One named argument at a call site (<c>name: expr</c>). Immutable after parse.</summary>
    public sealed class NamedArgument
    {
        public NamedArgument(string name, ExprNode value, BlockPosition position)
        {
            Name = name;
            Value = value;
            Position = position;
        }

        /// <summary>The prop name (ordinal, case-sensitive).</summary>
        public string Name { get; }

        /// <summary>The value expression (native tier), evaluated in the caller's context.</summary>
        public ExprNode Value { get; }

        /// <summary>Absolute template position of the argument (<c>OutputItem.Position</c> convention).</summary>
        public BlockPosition Position { get; }
    }
}
