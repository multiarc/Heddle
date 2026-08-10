using System.Collections.Generic;
using System.Text;
using Heddle.Language.Expressions;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The one writer of the engine's embedded-expression wrapper shape: a static method
    /// <c>object {name} ({model} model, dynamic chained, {root} root) { return unchecked({expr}); }</c> inside a
    /// class in <c>namespace Heddle.Runtime</c>, under the <c>using</c> set the engine's <c>CSharpContext</c>
    /// collects. The engine renders this shape from <c>CSharpPreparseTemplate.tcs</c> to type an expression and
    /// from <c>CSharpClassTemplate.tcs</c> to compile it, and both this tier's probe and its emitted fragment go
    /// through here — so the compilation the probe answers for is, term for term, the compilation the consumer
    /// builds, and neither can drift from the other.
    /// <para>The enclosing namespace is part of the shape, not packaging: <c>Heddle.Runtime</c> participates in
    /// name lookup before any <c>using</c>, so an expression naming <c>CSharpContext</c> or <c>Parameters</c>
    /// binds the engine's way only under the engine's namespace. The class template's own two extra directives
    /// (<c>System.Runtime.CompilerServices</c>, <c>System.Reflection</c>) are deliberately NOT part of the shape:
    /// they serve the engine's generated attributes, and the compilation whose verdict decides whether a template
    /// compiles at all — the preparse — does not have them, so writing them here would render expressions the
    /// engine refuses.</para>
    /// </summary>
    internal static class EmbeddedCSharpFragment
    {
        /// <summary>The engine's enclosing namespace for every compiled expression.</summary>
        internal const string EnclosingNamespace = "Heddle.Runtime";

        /// <summary>The probe's class and method names — the preparse template's own, because the typer selects
        /// the wrapped expression by the method it sits in.</summary>
        internal const string ProbeClassName = "CSharpExpression";

        internal const string ProbeMethodName = "PreProcessData";

        internal readonly struct Method
        {
            internal Method(string name, string modelType, string rootType, string expression)
            {
                Name = name;
                ModelType = modelType;
                RootType = rootType;
                Expression = expression;
            }

            internal string Name { get; }

            internal string ModelType { get; }

            internal string RootType { get; }

            internal string Expression { get; }
        }

        /// <summary>The spelling of a wrapper parameter type: <c>dynamic</c> where there is no static type — the
        /// literal the engine writes for <c>ExType.Dynamic</c> — and the fully qualified name everywhere else.</summary>
        internal static string TypeName(ITypeSymbol type) =>
            type == null || type.TypeKind == TypeKind.Dynamic ? "dynamic" : SymbolTypeResolver.FullyQualified(type);

        /// <summary>
        /// The namespaces the engine's <c>CSharpContext.ParseAndGetResultType</c> imports for one expression, in
        /// its order: the model type's namespace, the chained type's namespace, then each one-level generic type
        /// argument's. The chained type is <c>ExType.Dynamic</c> for every expression this tier can reach, whose
        /// CLR type is <c>object</c> — which is how <c>using System;</c> is always in the engine's set. The sink
        /// is cumulative on purpose: the engine's set is one <c>HashSet</c> shared by every expression of a
        /// template, so a later expression compiles under the namespaces the earlier ones imported.
        /// </summary>
        internal static void AddExpressionNamespaces(ITypeSymbol modelType, IList<string> sink)
        {
            Add(sink, NamespaceOf(modelType) ?? "System");
            Add(sink, "System");
            if (modelType is INamedTypeSymbol named && named.IsGenericType)
            {
                foreach (var argument in named.TypeArguments)
                    Add(sink, NamespaceOf(argument));
            }
        }

        private static void Add(IList<string> sink, string ns)
        {
            if (ns == null)
                return;
            for (int i = 0; i < sink.Count; i++)
            {
                if (string.Equals(sink[i], ns, System.StringComparison.Ordinal))
                    return;
            }

            sink.Add(ns);
        }

        private static string NamespaceOf(ITypeSymbol type)
        {
            var containing = type?.ContainingNamespace;
            if (containing == null || containing.IsGlobalNamespace)
                return null;
            return containing.ToDisplayString();
        }

        /// <summary>Renders the wrapper: the namespace block, its <c>using</c> directives (exact-string deduped,
        /// first spelling wins — the engine's set is exact-string too), the class, and one method per entry.</summary>
        internal static string Build(IEnumerable<string> usings, IEnumerable<Method> methods, string className)
        {
            var source = new StringBuilder();
            source.Append("namespace ").Append(EnclosingNamespace).Append("\n{\n");
            var written = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var ns in usings)
            {
                if (written.Add(ns))
                    source.Append("    using ").Append(ns).Append(";\n");
            }

            source.Append('\n')
                .Append("    internal static class ").Append(className).Append("\n    {\n");
            bool first = true;
            foreach (var method in methods)
            {
                if (!first)
                    source.Append('\n');
                first = false;
                source.Append("        internal static object ").Append(method.Name)
                    .Append(" (").Append(method.ModelType).Append(' ').Append(EmbeddedCSharpNames.Model)
                    .Append(", dynamic ").Append(EmbeddedCSharpNames.Chained)
                    .Append(", ").Append(method.RootType).Append(' ').Append(EmbeddedCSharpNames.Root).Append(")\n")
                    .Append("        {\n")
                    .Append("            return unchecked(").Append(method.Expression).Append(");\n")
                    .Append("        }\n");
            }

            source.Append("    }\n}\n");
            return source.ToString();
        }
    }
}
