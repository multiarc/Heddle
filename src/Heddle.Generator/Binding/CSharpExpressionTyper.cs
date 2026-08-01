using System.Collections.Generic;
using System.Text;
using Heddle.Language.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The static type of an embedded C# expression, answered the way the engine answers it.
    /// <para>The engine hands the same text to <c>CSharpContext.ParseAndGetResultType</c>, which wraps it in a
    /// method returning <c>object</c>, compiles that with Roslyn and reads the semantic type of the wrapped
    /// expression. So an embedded expression is the one call-site value the engine types most definitely of all —
    /// and the emitter had no answer for it at all, which every gate that consults a call-site value's type reads as
    /// permission. This asks the same question of the same compiler over the compilation's own references.</para>
    /// </summary>
    internal sealed class CSharpExpressionTyper
    {
        private readonly Compilation _compilation;

        private readonly Dictionary<(ITypeSymbol Model, string Expression), ITypeSymbol> _typed =
            new Dictionary<(ITypeSymbol, string), ITypeSymbol>(KeyComparer.Instance);

        internal CSharpExpressionTyper(Compilation compilation) => _compilation = compilation;

        /// <summary>
        /// The expression's type, or null where this compilation cannot say — the text does not bind here, or it
        /// binds to nothing with a name. Null is "cannot say", never "no type": the compilation's <c>dynamic</c> is
        /// the definite no-static-type answer, and the engine gives it for the same two cases it does (an anonymous
        /// type and <c>dynamic</c> itself).
        /// </summary>
        internal ITypeSymbol TypeOf(string expression, ITypeSymbol modelType, IReadOnlyList<string> usings)
        {
            if (_compilation == null || modelType == null || string.IsNullOrEmpty(expression))
                return null;

            var key = (modelType, expression);
            if (_typed.TryGetValue(key, out var memoized))
                return memoized;
            var resolved = Resolve(expression, modelType, usings);
            _typed[key] = resolved;
            return resolved;
        }

        private ITypeSymbol Resolve(string expression, ITypeSymbol modelType, IReadOnlyList<string> usings)
        {
            var tree = CSharpSyntaxTree.ParseText(Wrapper(expression, modelType, usings));
            var probe = _compilation.AddSyntaxTrees(tree);
            var root = tree.GetRoot();
            ExpressionSyntax wrapped = null;
            foreach (var node in root.DescendantNodes())
            {
                if (node is ReturnStatementSyntax statement)
                {
                    wrapped = statement.Expression;
                    break;
                }
            }

            if (wrapped == null)
                return null;

            var model = probe.GetSemanticModel(tree, false);
            var type = model.GetTypeInfo(wrapped).Type;
            if (type == null || type.TypeKind == TypeKind.Error)
                return null;

            // The engine's own two arms for a value with no name to bind against: it answers ExType.Dynamic for both
            // rather than a type, and a dynamic value is one its gates then refuse.
            if (type.IsAnonymousType || type.TypeKind == TypeKind.Dynamic)
                return _compilation.DynamicType;

            return type;
        }

        /// <summary>
        /// The compilation unit the engine compiles for this expression, reproduced: every collected namespace as a
        /// <c>using</c>, then the wrapper method whose parameters are the identifiers an embedded expression may
        /// bind. The enclosing namespace is the engine's, because it is part of how a name in the expression
        /// resolves. <c>chained</c> and <c>root</c> stand as <c>object</c> — their runtime types are not reproducible
        /// here, which is why the emitter refuses an expression that mentions either.
        /// </summary>
        private static string Wrapper(string expression, ITypeSymbol modelType, IReadOnlyList<string> usings)
        {
            var source = new StringBuilder();
            if (usings != null)
            {
                foreach (var ns in usings)
                    source.Append("using ").Append(ns).Append(";\n");
            }

            source.Append("namespace Heddle.Runtime {\n")
                .Append("public static class CSharpExpression {\n")
                .Append("public static object PreProcessData(")
                .Append(SymbolTypeResolver.FullyQualified(modelType)).Append(' ').Append(EmbeddedCSharpNames.Model)
                .Append(", object ").Append(EmbeddedCSharpNames.Chained)
                .Append(", object ").Append(EmbeddedCSharpNames.Root).Append(")\n")
                .Append("{\nreturn unchecked(").Append(expression).Append(");\n}\n}\n}\n");
            return source.ToString();
        }

        private sealed class KeyComparer : IEqualityComparer<(ITypeSymbol Model, string Expression)>
        {
            internal static readonly KeyComparer Instance = new KeyComparer();

            public bool Equals((ITypeSymbol Model, string Expression) x, (ITypeSymbol Model, string Expression) y) =>
                SymbolEqualityComparer.Default.Equals(x.Model, y.Model) &&
                string.Equals(x.Expression, y.Expression, System.StringComparison.Ordinal);

            public int GetHashCode((ITypeSymbol Model, string Expression) key) =>
                unchecked((key.Model == null ? 0 : SymbolEqualityComparer.Default.GetHashCode(key.Model)) * 397 ^
                          key.Expression.GetHashCode());
        }
    }
}
