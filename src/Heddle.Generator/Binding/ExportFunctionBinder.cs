using System.Collections.Generic;
using Heddle.Language.Expressions;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// Resolves an <c>[ExportFunctions]</c> call with the <b>shared</b> <see cref="OverloadRank"/> core.
    /// <para>Without parameter-type metadata the shared ranker has nothing to rank and the emitted call falls to
    /// the consumer's C# compiler, whose betterness rules are not Heddle's flat Pareto rank.
    /// <c>FunctionExportResolver</c> discovers full signatures, so an
    /// export call ranks exactly as a built-in does: degrade when the ranker reports ambiguity or inapplicability,
    /// otherwise emit cast-pinned to the winning signature so the consumer's compiler has no choice left to make.</para>
    /// </summary>
    internal static class ExportFunctionBinder
    {
        internal sealed class Binding
        {
            public FunctionExportResolver.ExportOverloadInfo Overload;

            /// <summary>Per-argument cast to the chosen parameter type (<c>global::</c>-qualified), or null when the
            /// argument already matches exactly.</summary>
            public string[] ArgumentCasts;

            public ITypeSymbol ReturnType;
        }

        private sealed class SymbolRankModel : IRankModel<ITypeSymbol>
        {
            private readonly SymbolTypeFacts _facts;

            internal SymbolRankModel(SymbolTypeFacts facts) => _facts = facts;

            public bool AreSame(ITypeSymbol a, ITypeSymbol b) => SymbolEqualityComparer.Default.Equals(a, b);

            public bool IsObject(ITypeSymbol type) => _facts.IsObject(type);

            public bool IsValueType(ITypeSymbol type) => _facts.IsValueType(type);

            public bool TryGetNullableUnderlying(ITypeSymbol type, out ITypeSymbol underlying) =>
                _facts.TryGetNullableUnderlying(type, out underlying);

            public NumericKind KindOf(ITypeSymbol type) => _facts.GetNumericKind(type);

            /// <summary>The reference-conversion arm, answered by the same CLR relation the rest of the binder
            /// uses — the generator can decide it here (unlike the default table's name-keyed model, which has to
            /// answer false).</summary>
            public bool IsReferenceAssignable(ITypeSymbol from, ITypeSymbol to) =>
                !_facts.IsValueType(from) && _facts.IsAssignableFrom(to, from);
        }

        /// <summary>Ranks the merged overload set. Returns null (degrade to dynamic) when any argument cannot be
        /// typed, no overload is applicable, or when multiple overloads tie (the flat Pareto front has &gt; 1 member).
        /// <paramref name="refusal"/> distinguishes ambiguous/inapplicable over typed arguments (reports <c>HED7025</c>)
        /// from untypeable arguments (silent degrade).</summary>
        internal static Binding TryBind(SymbolTypeFacts facts, string name,
            IReadOnlyList<FunctionExportResolver.ExportOverloadInfo> overloads, IReadOnlyList<OperandKind> argKinds,
            out BindRefusal refusal)
        {
            refusal = BindRefusal.Unproven;
            if (facts?.Compilation == null || overloads == null || overloads.Count == 0)
                return null;

            var model = new SymbolRankModel(facts);
            var args = new RankArgument<ITypeSymbol>[argKinds.Count];
            var typed = new bool[argKinds.Count];
            bool allTyped = true;
            for (int i = 0; i < argKinds.Count; i++)
            {
                if (argKinds[i].Category == OperandCategory.NullLiteral)
                {
                    args[i] = RankArgument<ITypeSymbol>.Null();
                    typed[i] = true;
                    continue;
                }

                var type = ToSymbol(facts.Compilation, argKinds[i]);
                if (type == null)
                {
                    allTyped = false;
                    continue;
                }

                args[i] = RankArgument<ITypeSymbol>.Of(type);
                typed[i] = true;
            }

            // One candidate — no overload to choose, so no ranking is imposed, and the ordinary single-overload
            // export stays in reach with arguments the estimator often cannot type at all (`this`, a member path
            // to a model type). What is NOT skipped with the choice is whether the candidate is applicable: the
            // engine runs the ranker over one candidate as over ten, so `only("ab")` against a sole `only(int)`
            // is HED1012 there, while binding it here emitted CS1503 into the consumer's build. An argument the
            // estimator did type and that converts to nothing rules the candidate out on its own, whatever the
            // others turn out to be, because applicability is decided argument by argument.
            if (overloads.Count == 1 && overloads[0].Method.Parameters.Length == argKinds.Count &&
                !ExcludedByTypedArguments(model, overloads[0].Method, args, typed))
            {
                refusal = BindRefusal.Bound;
                return new Binding
                {
                    Overload = overloads[0],
                    ArgumentCasts = new string[argKinds.Count],
                    ReturnType = overloads[0].Method.ReturnType
                };
            }

            if (!allTyped)
                // Untypeable argument: degrade before ranking, so front describes host registry only.
                return null;

            var candidates = new RankCandidate<ITypeSymbol>[overloads.Count];
            for (int i = 0; i < overloads.Count; i++)
            {
                var method = overloads[i].Method;
                var parameterTypes = new ITypeSymbol[method.Parameters.Length];
                for (int p = 0; p < method.Parameters.Length; p++)
                    parameterTypes[p] = method.Parameters[p].Type;

                bool hasParams = method.Parameters.Length > 0 &&
                                 method.Parameters[method.Parameters.Length - 1].IsParams;
                ITypeSymbol elementType = null;
                if (hasParams &&
                    method.Parameters[method.Parameters.Length - 1].Type is IArrayTypeSymbol array)
                    elementType = array.ElementType;
                else
                    hasParams = false;

                candidates[i] = new RankCandidate<ITypeSymbol>(parameterTypes, hasParams, elementType);
            }

            var binding = OverloadRank.Bind(model, candidates, args);
            if (binding.Outcome != BindOutcome.Bound)
            {
                refusal = Refuse(name, binding.Outcome, overloads, args);
                return null;
            }

            if (binding.Expanded)
                return null;   // params-expanded: bound, but not a shape this writer emits cast-pinned

            var winner = overloads[binding.Index];
            var casts = new string[argKinds.Count];
            for (int i = 0; i < casts.Length; i++)
            {
                var parameterType = winner.Method.Parameters[i].Type;
                casts[i] = args[i].IsNullLiteral ||
                           SymbolEqualityComparer.Default.Equals(args[i].Type, parameterType)
                    ? null
                    : parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            refusal = BindRefusal.Bound;
            return new Binding { Overload = winner, ArgumentCasts = casts, ReturnType = winner.Method.ReturnType };
        }

        /// <summary>
        /// Whether an argument the estimator could type rules <paramref name="method"/> out. A candidate is
        /// applicable only if every argument converts to its parameter, and the shared ranker decides that argument
        /// by argument, so one that converts to nothing settles the candidate without the untyped arguments being
        /// known. Both tiers of the ranker's two-tier bind have to say no: an argument at or past the fixed count of
        /// a <c>params</c> signature may convert to the element type instead.
        /// </summary>
        private static bool ExcludedByTypedArguments(IRankModel<ITypeSymbol> model, IMethodSymbol method,
            RankArgument<ITypeSymbol>[] args, bool[] typed)
        {
            var parameters = method.Parameters;
            var paramsElement = parameters.Length > 0 && parameters[parameters.Length - 1].IsParams
                ? (parameters[parameters.Length - 1].Type as IArrayTypeSymbol)?.ElementType
                : null;

            for (int i = 0; i < args.Length; i++)
            {
                if (!typed[i] ||
                    OverloadRank.ConversionRank(model, args[i], parameters[i].Type) >= 0)
                    continue;
                if (paramsElement != null && i >= parameters.Length - 1 &&
                    OverloadRank.ConversionRank(model, args[i], paramsElement) >= 0)
                    continue;
                return true;
            }

            return false;
        }

        /// <summary>Formats <c>HED7025</c> payload to match the runtime's verdict message. Candidates are every
        /// discovered overload of the name across containers.</summary>
        private static BindRefusal Refuse(string name, BindOutcome outcome,
            IReadOnlyList<FunctionExportResolver.ExportOverloadInfo> overloads,
            IReadOnlyList<RankArgument<ITypeSymbol>> args)
        {
            var candidates = new List<string>(overloads.Count);
            foreach (var overload in overloads)
            {
                var parts = new List<string>(overload.Method.Parameters.Length);
                foreach (var parameter in overload.Method.Parameters)
                    parts.Add(Display(parameter.Type));
                candidates.Add(name + "(" + string.Join(", ", parts) + ")");
            }

            var candidateText = string.Join(", ", candidates);
            if (outcome == BindOutcome.Ambiguous)
                return BindRefusal.ProvenIllegal(
                    "The call to function '" + name + "' is ambiguous between: " + candidateText + ".",
                    Heddle.Data.HeddleDiagnosticIds.AmbiguousFunctionCall);

            var argTexts = new List<string>(args.Count);
            foreach (var arg in args)
                argTexts.Add(arg.IsNullLiteral ? "null" : Display(arg.Type));
            return BindRefusal.ProvenIllegal(
                "No overload of function '" + name + "' takes (" + string.Join(", ", argTexts) + "). Candidates: " +
                candidateText + ".",
                Heddle.Data.HeddleDiagnosticIds.NoFunctionOverload);
        }

        /// <summary>Signature-text spelling for a parameter/argument type: the C# alias where one exists (so the
        /// build error reads the way the runtime's <c>HED1013</c> reads), else the minimally-qualified name.</summary>
        private static string Display(ITypeSymbol type) =>
            type == null ? "?" : type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        /// <summary>Maps the estimator's <see cref="OperandKind"/> back onto a compilation type symbol. Only the
        /// categories the estimator can type precisely are mapped; everything else answers null, which degrades.</summary>
        private static ITypeSymbol ToSymbol(Compilation compilation, OperandKind kind)
        {
            ITypeSymbol underlying;
            switch (kind.Category)
            {
                case OperandCategory.Bool:
                    underlying = compilation.GetSpecialType(SpecialType.System_Boolean);
                    break;
                case OperandCategory.String:
                    return compilation.GetSpecialType(SpecialType.System_String);
                case OperandCategory.Numeric:
                    underlying = NumericSymbol(compilation, kind.Kind);
                    break;
                default:
                    return null;
            }

            if (underlying == null)
                return null;
            if (!kind.IsNullable)
                return underlying;

            var nullable = compilation.GetTypeByMetadataName("System.Nullable`1");
            return nullable?.Construct(underlying);
        }

        private static ITypeSymbol NumericSymbol(Compilation compilation, NumericKind kind)
        {
            switch (kind)
            {
                case NumericKind.SByte: return compilation.GetSpecialType(SpecialType.System_SByte);
                case NumericKind.Byte: return compilation.GetSpecialType(SpecialType.System_Byte);
                case NumericKind.Int16: return compilation.GetSpecialType(SpecialType.System_Int16);
                case NumericKind.UInt16: return compilation.GetSpecialType(SpecialType.System_UInt16);
                case NumericKind.Int32: return compilation.GetSpecialType(SpecialType.System_Int32);
                case NumericKind.UInt32: return compilation.GetSpecialType(SpecialType.System_UInt32);
                case NumericKind.Int64: return compilation.GetSpecialType(SpecialType.System_Int64);
                case NumericKind.UInt64: return compilation.GetSpecialType(SpecialType.System_UInt64);
                case NumericKind.Char: return compilation.GetSpecialType(SpecialType.System_Char);
                case NumericKind.Single: return compilation.GetSpecialType(SpecialType.System_Single);
                case NumericKind.Double: return compilation.GetSpecialType(SpecialType.System_Double);
                case NumericKind.Decimal: return compilation.GetSpecialType(SpecialType.System_Decimal);
                default: return null;
            }
        }
    }
}
