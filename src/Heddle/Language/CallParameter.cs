using System.Collections.Generic;
using Heddle.Language.Expressions;

namespace Heddle.Language
{
    public class CallParameter
    {
        /// <summary>
        /// Use directly the ModelParameter or ChainParameter
        /// </summary>
        public string[] ModelParameter { get; set; }

        public bool RootReference { get; set; }

        /// <summary>
        /// Use directly the ModelParameter or ChainParameter
        /// </summary>
        public List<OutputItem> ChainParameter { get; set; }

        public bool IsModelTypeParameter =>
            ChainParameter == null && CSharpExpression == null && NativeExpression == null;

        public string CSharpExpression { get; set; }

        /// <summary>
        /// The parsed native expression when the parameter used the expression tier; null for the
        /// member-path, chain, and C# shapes.
        /// </summary>
        public ExprNode NativeExpression { get; set; }

        /// <summary>
        /// Named prop arguments of this call (<c>name: expr</c>), in source order; <c>null</c> when the call
        /// passes none. Orthogonal to the positional parameter shapes — <see cref="IsModelTypeParameter"/> is
        /// unaffected.
        /// </summary>
        public IReadOnlyList<NamedArgument> PropArguments { get; set; }
    }
}