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

        /// <summary>
        /// Ranks the merged overload set for one call. Returns null — meaning "degrade to dynamic" — when any
        /// argument cannot be given a precise static type, when no overload is applicable, or when the flat Pareto
        /// front has more than one member (the runtime's <c>HED1013</c> verdict, which the build tier must reach
        /// too rather than letting C# betterness pick a winner the runtime would refuse).
        /// <para><paramref name="refusal"/> separates the last two from the first. An ambiguous or
        /// inapplicable front over arguments the estimator <i>typed</i> is a proof that the host's own registry will
        /// refuse the call, so the build reports <c>HED7025</c>; an untypeable argument proves nothing and still
        /// degrades in silence. This path carries arbitrary host signatures rather than the shipped built-in table,
        /// so the distinction matters more, not less, than it does for the built-ins.</para>
        /// </summary>
        internal static Binding TryBind(SymbolTypeFacts facts, string name,
            IReadOnlyList<FunctionExportResolver.ExportOverloadInfo> overloads, IReadOnlyList<OperandKind> argKinds,
            out BindRefusal refusal)
        {
            refusal = BindRefusal.Unproven;
            if (facts?.Compilation == null || overloads == null || overloads.Count == 0)
                return null;

            // One candidate — no choice to make, so no ranking is needed and none is imposed. This keeps the
            // ordinary single-overload export in reach (its arguments the estimator often cannot type, e.g.
            // `this`) while the ranker governs exactly the case it exists for: a merged or overloaded name where
            // the runtime and C# betterness could disagree.
            if (overloads.Count == 1 && overloads[0].Method.Parameters.Length == argKinds.Count)
            {
                refusal = BindRefusal.Bound;
                return new Binding
                {
                    Overload = overloads[0],
                    ArgumentCasts = new string[argKinds.Count],
                    ReturnType = overloads[0].Method.ReturnType
                };
            }

            var args = new RankArgument<ITypeSymbol>[argKinds.Count];
            for (int i = 0; i < argKinds.Count; i++)
            {
                if (argKinds[i].Category == OperandCategory.NullLiteral)
                {
                    args[i] = RankArgument<ITypeSymbol>.Null();
                    continue;
                }

                var type = ToSymbol(facts.Compilation, argKinds[i]);
                if (type == null)
                    // THE SIDE CONDITION. Degrade-on-doubt: an untypeable argument cannot be ranked, so any
                    // front computed past this point would describe the generator's ignorance rather than the host
                    // registry's verdict. Leaving before Bind runs is what keeps this a silent degrade.
                    return null;
                args[i] = RankArgument<ITypeSymbol>.Of(type);
            }

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

            var binding = OverloadRank.Bind(new SymbolRankModel(facts), candidates, args);
            if (binding.Outcome != BindOutcome.Bound)
            {
                // Ambiguous / inapplicable over fully typed arguments — the host registry's own verdict.
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

        /// <summary>The <c>HED7025</c> payload for a proven-illegal export call, shaped like the runtime's own
        /// sentence for the same input so the two tiers say the same thing. Candidates are every discovered
        /// overload of the name across every container, which is the merged set the runtime registry holds.</summary>
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
