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
        private readonly CSharpParseOptions _parseOptions;

        private readonly Dictionary<(ITypeSymbol Model, string Expression), Answer> _typed =
            new Dictionary<(ITypeSymbol, string), Answer>(KeyComparer.Instance);

        internal CSharpExpressionTyper(Compilation compilation)
        {
            _compilation = compilation;
            _parseOptions = ProbeParseOptions.For(compilation);
        }

        /// <summary>What this compilation can say about one embedded expression.</summary>
        private readonly struct Answer
        {
            internal Answer(bool compiles, ITypeSymbol type, bool referencesChainedOrRoot)
            {
                Compiles = compiles;
                Type = type;
                ReferencesChainedOrRoot = referencesChainedOrRoot;
            }

            /// <summary>Whether the compilation unit the engine builds for this expression is free of errors.
            /// False is a definite refusal, not "cannot say" — the engine bails on any error diagnostic.</summary>
            internal bool Compiles { get; }

            internal ITypeSymbol Type { get; }

            /// <summary>Whether some identifier in the expression binds to the wrapper's <c>chained</c> or
            /// <c>root</c> parameter — the two whose runtime types the emitter cannot reproduce.</summary>
            internal bool ReferencesChainedOrRoot { get; }
        }

        /// <summary>
        /// The expression's type, or null where this compilation cannot say — the text does not bind here, or it
        /// binds to nothing with a name. Null is "cannot say", never "no type": the compilation's <c>dynamic</c> is
        /// the definite no-static-type answer, and the engine gives it for the same two cases it does (an anonymous
        /// type and <c>dynamic</c> itself).
        /// </summary>
        internal ITypeSymbol TypeOf(string expression, ITypeSymbol modelType, IReadOnlyList<string> usings) =>
            Ask(expression, modelType, usings).Type;

        /// <summary>
        /// Whether the compilation the <b>engine</b> builds for this expression compiles. The engine hands the text
        /// to Roslyn, calls <c>GetDiagnostics()</c> on the result and refuses the whole template on any error, so a
        /// misspelt member, an unbalanced expression, a wrong argument count, an unimported extension method and an
        /// <c>[Obsolete(error: true)]</c> reference are all refusals it reports and this tier has to reach too.
        /// Answering "cannot say" for them left the emitter pasting text the consumer's compiler then rejected.
        /// <para>True where no compilation is available to ask: this gate may only take a template off the
        /// precompiled tier on evidence.</para>
        /// </summary>
        internal bool Compiles(string expression, ITypeSymbol modelType, IReadOnlyList<string> usings) =>
            Ask(expression, modelType, usings).Compiles;

        /// <summary>
        /// Whether the expression actually reads the engine's <c>chained</c> or <c>root</c> parameter, asked of the
        /// binder rather than of the text. A word-boundary search over the raw characters called every other
        /// occurrence of those two words a reference — a string literal, a lambda parameter, a member name, a
        /// comment — and took templates the engine renders off the tier for them.
        /// </summary>
        internal bool ReferencesChainedOrRoot(string expression, ITypeSymbol modelType, IReadOnlyList<string> usings)
            => Ask(expression, modelType, usings).ReferencesChainedOrRoot;

        private Answer Ask(string expression, ITypeSymbol modelType, IReadOnlyList<string> usings)
        {
            if (_compilation == null || modelType == null || string.IsNullOrEmpty(expression))
                return new Answer(true, null, referencesChainedOrRoot: true);

            var key = (modelType, expression);
            if (_typed.TryGetValue(key, out var memoized))
                return memoized;
            var resolved = Resolve(expression, modelType, usings);
            _typed[key] = resolved;
            return resolved;
        }

        private Answer Resolve(string expression, ITypeSymbol modelType, IReadOnlyList<string> usings)
        {
            var tree = CSharpSyntaxTree.ParseText(Wrapper(expression, modelType, usings), _parseOptions);
            // The consumer's own compilation, because the model type and everything the expression reaches through
            // it are declared in its source rather than in a reference. The engine compiles standalone against the
            // consumer's assembly as a reference, where its internals are not visible; that difference is not
            // reproduced here and is the one term of the unit this probe does not hold.
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
                return new Answer(true, null, referencesChainedOrRoot: true);

            var model = probe.GetSemanticModel(tree, false);
            var touchesChainedOrRoot = TouchesChainedOrRoot(model, wrapped);
            foreach (var diagnostic in model.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    return new Answer(false, null, touchesChainedOrRoot);
            }

            // The engine asks for the constant value first and types the expression by the value it gets back, not
            // by the semantic type: a constant null becomes `typeof(object)`, which is a different answer from the
            // `string` Roslyn gives `default(string)`, and the gates read it.
            var constant = model.GetConstantValue(wrapped);
            if (constant.HasValue)
                return new Answer(true, ConstantType(constant.Value), touchesChainedOrRoot);

            var type = model.GetTypeInfo(wrapped).Type;
            if (type == null || type.TypeKind == TypeKind.Error)
                return new Answer(true, null, touchesChainedOrRoot);

            // The engine's own two arms for a value with no name to bind against: it answers ExType.Dynamic for both
            // rather than a type, and a dynamic value is one its gates then refuse.
            if (type.IsAnonymousType || type.TypeKind == TypeKind.Dynamic)
                return new Answer(true, _compilation.DynamicType, touchesChainedOrRoot);

            return new Answer(true, type, touchesChainedOrRoot);
        }

        /// <summary>Whether any identifier under <paramref name="wrapped"/> binds to the wrapper method's
        /// <c>chained</c> or <c>root</c> parameter. A lambda parameter of the same name shadows it and binds to
        /// itself, so it is not a reference — which is the whole difference from matching the word.</summary>
        private static bool TouchesChainedOrRoot(SemanticModel model, ExpressionSyntax wrapped)
        {
            foreach (var node in wrapped.DescendantNodesAndSelf())
            {
                if (!(node is IdentifierNameSyntax identifier))
                    continue;
                var name = identifier.Identifier.ValueText;
                if (!string.Equals(name, EmbeddedCSharpNames.Chained, System.StringComparison.Ordinal) &&
                    !string.Equals(name, EmbeddedCSharpNames.Root, System.StringComparison.Ordinal))
                    continue;
                if (model.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter &&
                    parameter.ContainingSymbol is IMethodSymbol method &&
                    string.Equals(method.Name, "PreProcessData", System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>The engine's <c>constantValue.Value?.GetType() ?? typeof(object)</c>, over the boxed CLR value
        /// Roslyn hands back for a constant expression. An enum constant arrives as its underlying integer there
        /// too, so the answer is the same on both sides.</summary>
        private ITypeSymbol ConstantType(object value)
        {
            switch (value)
            {
                case null: return _compilation.GetSpecialType(SpecialType.System_Object);
                case bool _: return _compilation.GetSpecialType(SpecialType.System_Boolean);
                case char _: return _compilation.GetSpecialType(SpecialType.System_Char);
                case sbyte _: return _compilation.GetSpecialType(SpecialType.System_SByte);
                case byte _: return _compilation.GetSpecialType(SpecialType.System_Byte);
                case short _: return _compilation.GetSpecialType(SpecialType.System_Int16);
                case ushort _: return _compilation.GetSpecialType(SpecialType.System_UInt16);
                case int _: return _compilation.GetSpecialType(SpecialType.System_Int32);
                case uint _: return _compilation.GetSpecialType(SpecialType.System_UInt32);
                case long _: return _compilation.GetSpecialType(SpecialType.System_Int64);
                case ulong _: return _compilation.GetSpecialType(SpecialType.System_UInt64);
                case float _: return _compilation.GetSpecialType(SpecialType.System_Single);
                case double _: return _compilation.GetSpecialType(SpecialType.System_Double);
                case decimal _: return _compilation.GetSpecialType(SpecialType.System_Decimal);
                case string _: return _compilation.GetSpecialType(SpecialType.System_String);
                default: return _compilation.GetSpecialType(SpecialType.System_Object);
            }
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
            var written = new HashSet<string>(System.StringComparer.Ordinal);
            if (usings != null)
            {
                foreach (var ns in usings)
                {
                    if (written.Add(ns))
                        source.Append("using ").Append(ns).Append(";\n");
                }
            }

            // The engine imports the model's own namespace and, when the model is generic, each type argument's —
            // so an identifier in the expression resolves against them there and has to here. Left out, the same
            // expression bound to nothing on this side and the emitter read that as permission.
            foreach (var ns in ModelNamespaces(modelType))
            {
                if (written.Add(ns))
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

        /// <summary>The namespaces the engine imports off the model type itself: its own, and each of its generic
        /// type arguments'. Nested generics are walked, because the reflection the engine reads them from flattens
        /// a constructed type's arguments the same way one level at a time.</summary>
        internal static IEnumerable<string> ModelNamespaces(ITypeSymbol modelType)
        {
            var ns = NamespaceOf(modelType);
            if (ns != null)
                yield return ns;

            if (!(modelType is INamedTypeSymbol named) || !named.IsGenericType)
                yield break;

            foreach (var argument in named.TypeArguments)
            {
                var argumentNamespace = NamespaceOf(argument);
                if (argumentNamespace != null)
                    yield return argumentNamespace;
            }
        }

        private static string NamespaceOf(ITypeSymbol type)
        {
            var containing = type?.ContainingNamespace;
            if (containing == null || containing.IsGlobalNamespace)
                return null;
            return containing.ToDisplayString();
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
