using System.Collections.Generic;
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
    /// <para>The wrapper is <see cref="EmbeddedCSharpFragment"/>'s — the same builder that writes the compiled
    /// fragment into the generated file — so the probe's verdict is a verdict about the very text the consumer's
    /// compiler will build.</para>
    /// </summary>
    internal sealed class CSharpExpressionTyper
    {
        private readonly Compilation _compilation;
        private readonly CSharpParseOptions _parseOptions;

        // Keyed by the wrapper source itself, which is the engine's own memo key (PreparseCache stores by the
        // generated code): the same expression under a since-grown namespace set is a different compilation with
        // possibly a different answer, and the wrapper text is exactly the set of terms the answer depends on.
        private readonly Dictionary<string, Answer> _typed =
            new Dictionary<string, Answer>(System.StringComparer.Ordinal);

        internal CSharpExpressionTyper(Compilation compilation)
        {
            _compilation = compilation;
            _parseOptions = ProbeParseOptions.For(compilation);
        }

        /// <summary>What this compilation can say about one embedded expression.</summary>
        private readonly struct Answer
        {
            internal Answer(bool compiles, ITypeSymbol type, bool bindsRoot, bool needsRuntimeBinder = false)
            {
                Compiles = compiles;
                Type = type;
                BindsRoot = bindsRoot;
                NeedsRuntimeBinder = needsRuntimeBinder;
            }

            /// <summary>Whether the compilation unit the engine builds for this expression is free of errors.
            /// False is a definite refusal, not "cannot say" — the engine bails on any error diagnostic.</summary>
            internal bool Compiles { get; }

            internal ITypeSymbol Type { get; }

            /// <summary>Whether some identifier in the expression binds to the wrapper's <c>root</c> parameter.
            /// Read only where the root type is not pinned by the template — a template with no <c>@model</c> is
            /// typed by whatever context the host hands the engine, which is a value the build does not hold.</summary>
            internal bool BindsRoot { get; }

            /// <summary>True when the compile failed for want of dynamic runtime binding (CS0656/CS1969) — the
            /// consumer compilation carries no Microsoft.CSharp reference, so its own compiler would hit the same
            /// wall on the emitted fragment. A legitimate degrade, distinguishable from an expression the engine
            /// rejects.</summary>
            internal bool NeedsRuntimeBinder { get; }
        }

        /// <summary>
        /// The expression's type, or null where this compilation cannot say — the text does not bind here, or it
        /// binds to nothing with a name. Null is "cannot say", never "no type": the compilation's <c>dynamic</c> is
        /// the definite no-static-type answer, and the engine gives it for the same two cases it does (an anonymous
        /// type and <c>dynamic</c> itself).
        /// </summary>
        internal ITypeSymbol TypeOf(string expression, ITypeSymbol modelType, ITypeSymbol rootType,
            IReadOnlyList<string> usings) =>
            Ask(expression, modelType, rootType, usings).Type;

        /// <summary>
        /// Whether the compilation the <b>engine</b> builds for this expression compiles. The engine hands the text
        /// to Roslyn, calls <c>GetDiagnostics()</c> on the result and refuses the whole template on any error, so a
        /// misspelt member, an unbalanced expression, a wrong argument count, an unimported extension method and an
        /// <c>[Obsolete(error: true)]</c> reference are all refusals it reports and this tier has to reach too.
        /// Answering "cannot say" for them left the emitter pasting text the consumer's compiler then rejected.
        /// <para>True where no compilation is available to ask: this gate may only take a template off the
        /// precompiled tier on evidence.</para>
        /// </summary>
        internal bool Compiles(string expression, ITypeSymbol modelType, ITypeSymbol rootType,
            IReadOnlyList<string> usings) =>
            Ask(expression, modelType, rootType, usings).Compiles;

        /// <summary>Whether a failed compile failed for want of dynamic runtime binding — see
        /// <see cref="Answer.NeedsRuntimeBinder"/>.</summary>
        internal bool NeedsRuntimeBinder(string expression, ITypeSymbol modelType, ITypeSymbol rootType,
            IReadOnlyList<string> usings) =>
            Ask(expression, modelType, rootType, usings).NeedsRuntimeBinder;

        /// <summary>
        /// Whether the expression actually reads the engine's <c>root</c> parameter, asked of the binder rather
        /// than of the text: a lambda parameter of the same name shadows it, a member can be called <c>root</c>,
        /// and a string literal is not an identifier at all.
        /// </summary>
        internal bool BindsRoot(string expression, ITypeSymbol modelType, ITypeSymbol rootType,
            IReadOnlyList<string> usings)
            => Ask(expression, modelType, rootType, usings).BindsRoot;

        private Answer Ask(string expression, ITypeSymbol modelType, ITypeSymbol rootType,
            IReadOnlyList<string> usings)
        {
            if (_compilation == null || modelType == null || string.IsNullOrEmpty(expression))
                return new Answer(true, null, bindsRoot: true);

            var wrapper = EmbeddedCSharpFragment.Build(
                usings ?? (IReadOnlyList<string>) new string[0],
                new[]
                {
                    new EmbeddedCSharpFragment.Method(EmbeddedCSharpFragment.ProbeMethodName,
                        EmbeddedCSharpFragment.TypeName(modelType), EmbeddedCSharpFragment.TypeName(rootType),
                        expression)
                },
                EmbeddedCSharpFragment.ProbeClassName);
            if (_typed.TryGetValue(wrapper, out var memoized))
                return memoized;
            var resolved = Resolve(wrapper);
            _typed[wrapper] = resolved;
            return resolved;
        }

        private Answer Resolve(string wrapper)
        {
            var tree = CSharpSyntaxTree.ParseText(wrapper, _parseOptions);
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
                return new Answer(true, null, bindsRoot: true);

            var model = probe.GetSemanticModel(tree, false);
            var bindsRoot = BindsRootParameter(model, wrapped);
            bool failed = false, needsRuntimeBinder = false;
            foreach (var diagnostic in model.GetDiagnostics())
            {
                if (diagnostic.Severity != DiagnosticSeverity.Error)
                    continue;
                failed = true;
                // CS0656 (missing Microsoft.CSharp.RuntimeBinder member) / CS1969 (no type for a dynamic
                // expression): the consumer compilation cannot compile dynamic operations at all.
                if (diagnostic.Id == "CS0656" || diagnostic.Id == "CS1969")
                    needsRuntimeBinder = true;
            }

            if (failed)
                return new Answer(false, null, bindsRoot, needsRuntimeBinder);

            // The engine asks for the constant value first and types the expression by the value it gets back, not
            // by the semantic type: a constant null becomes `typeof(object)`, which is a different answer from the
            // `string` Roslyn gives `default(string)`, and the gates read it.
            var constant = model.GetConstantValue(wrapped);
            if (constant.HasValue)
                return new Answer(true, ConstantType(constant.Value), bindsRoot);

            var type = model.GetTypeInfo(wrapped).Type;
            if (type == null || type.TypeKind == TypeKind.Error)
                return new Answer(true, null, bindsRoot);

            // The engine's own two arms for a value with no name to bind against: it answers ExType.Dynamic for both
            // rather than a type, and a dynamic value is decided at render rather than checked at compile.
            if (type.IsAnonymousType || type.TypeKind == TypeKind.Dynamic)
                return new Answer(true, _compilation.DynamicType, bindsRoot);

            return new Answer(true, type, bindsRoot);
        }

        /// <summary>Whether any identifier under <paramref name="wrapped"/> binds to the wrapper method's
        /// <c>root</c> parameter. A lambda parameter of the same name shadows it and binds to itself, so it is not
        /// a reference — which is the whole difference from matching the word.</summary>
        private static bool BindsRootParameter(SemanticModel model, ExpressionSyntax wrapped)
        {
            foreach (var node in wrapped.DescendantNodesAndSelf())
            {
                if (!(node is IdentifierNameSyntax identifier))
                    continue;
                if (!string.Equals(identifier.Identifier.ValueText, EmbeddedCSharpNames.Root,
                        System.StringComparison.Ordinal))
                    continue;
                if (model.GetSymbolInfo(identifier).Symbol is IParameterSymbol parameter &&
                    parameter.ContainingSymbol is IMethodSymbol method &&
                    string.Equals(method.Name, EmbeddedCSharpFragment.ProbeMethodName,
                        System.StringComparison.Ordinal))
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
    }
}
