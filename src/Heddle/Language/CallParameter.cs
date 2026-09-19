using System.Collections.Generic;
using Heddle.Language.Expressions;

namespace Heddle.Language
{
    /// <summary>The argument of one extension call, in exactly one of its shapes: a member path, a
    /// chain, a C# expression or a native expression.</summary>
    public class CallParameter
    {
        /// <summary>The member path of a <c>@name(a.b.c)</c> argument, one segment per hop; empty when
        /// the call passes no path.</summary>
        public string[] ModelParameter { get; set; }

        /// <summary>True when the path starts at the root model (<c>::Member</c>) rather than the current one.</summary>
        public bool RootReference { get; set; }

        /// <summary>The output items of a chain argument (<c>@name(@inner())</c>); null for the other shapes.</summary>
        public List<OutputItem> ChainParameter { get; set; }

        /// <summary>True when the argument is a member path (possibly empty) rather than a chain, a C#
        /// expression or a native expression.</summary>
        public bool IsModelTypeParameter =>
            ChainParameter == null && CSharpExpression == null && NativeExpression == null;

        /// <summary>The C# source of the argument under the FullCSharp tier; null for the other shapes.</summary>
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