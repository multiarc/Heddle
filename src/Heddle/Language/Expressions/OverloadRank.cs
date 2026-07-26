using System.Collections.Generic;

namespace Heddle.Language.Expressions
{
    /// <summary>The type-system facts the shared overload ranker needs. The runtime implements it over reflected
    /// <c>Type</c>s; the generator implements it over the CLR type-name strings <c>DefaultFunctionRow</c> already
    /// carries.</summary>
    internal interface IRankModel<TType>
    {
        bool AreSame(TType a, TType b);

        bool IsObject(TType type);

        bool IsValueType(TType type);

        bool TryGetNullableUnderlying(TType type, out TType underlying);

        NumericKind KindOf(TType type);

        /// <summary>Reference conversion source → target (the <c>IsAssignableFrom</c> arm). A model that cannot
        /// decide this must answer false: under-ranking makes the generator degrade, over-ranking would make it pick
        /// an overload the runtime does not.</summary>
        bool IsReferenceAssignable(TType from, TType to);
    }

    /// <summary>One argument as the ranker sees it: a type, or the untyped <c>null</c> literal.</summary>
    internal readonly struct RankArgument<TType>
    {
        private RankArgument(bool isNullLiteral, TType type)
        {
            IsNullLiteral = isNullLiteral;
            Type = type;
        }

        public bool IsNullLiteral { get; }

        public TType Type { get; }

        public static RankArgument<TType> Of(TType type) => new RankArgument<TType>(false, type);

        public static RankArgument<TType> Null() => new RankArgument<TType>(true, default);
    }

    /// <summary>One candidate signature.</summary>
    internal readonly struct RankCandidate<TType>
    {
        public RankCandidate(IReadOnlyList<TType> parameterTypes, bool hasParamsArray, TType paramsElementType)
        {
            ParameterTypes = parameterTypes;
            HasParamsArray = hasParamsArray;
            ParamsElementType = paramsElementType;
        }

        public IReadOnlyList<TType> ParameterTypes { get; }

        public bool HasParamsArray { get; }

        public TType ParamsElementType { get; }
    }

    internal enum BindOutcome
    {
        Bound,
        None,
        Ambiguous
    }

    /// <summary>The chosen candidate, or why none was.</summary>
    internal readonly struct RankBinding
    {
        public RankBinding(BindOutcome outcome, int index, bool expanded)
        {
            Outcome = outcome;
            Index = index;
            Expanded = expanded;
        }

        public BindOutcome Outcome { get; }

        /// <summary>Index into the candidate list, or -1.</summary>
        public int Index { get; }

        /// <summary>Whether the binding used the params-expanded tier.</summary>
        public bool Expanded { get; }
    }

    /// <summary>
    /// Heddle's function-overload selection rule: rank exact 0, widening/reference/lifting 1 — <b>flat</b> — and
    /// boxing to <c>object</c> 2, with params-expanded arguments ranked one worse; then take the Pareto
    /// non-dominated front and call any non-singleton front ambiguous.
    /// <para>The <i>candidate set</i> was already shared (<c>DefaultFunctionTable</c> mirrors the registry); the
    /// <i>selection rule</i> was not. The generator's comment claimed the consumer's C# compiler reproduces this
    /// rank "by construction" — that claim was false because the rank vector here is flat while C# prefers the
    /// closest target. <c>min(1, 2u)</c> is the counter-example: <c>(long,long)</c>, <c>(double,double)</c> and
    /// <c>(decimal,decimal)</c> all rank <c>(1,1)</c>, so the runtime raises HED1013 while C# picks <c>Min(long,long)</c>
    /// and renders.</para>
    /// <para>The flat Pareto rank is the semantics of record. The generator consults this core as an emit guard —
    /// degrade on ambiguity, and emit cast-pinned to the chosen signature otherwise, which makes the "by
    /// construction" claim actually true.</para>
    /// </summary>
    internal static class OverloadRank
    {
        public const int Exact = 0;
        public const int Widening = 1;
        public const int Boxing = 2;
        public const int NoConversion = -1;

        /// <summary>Conversion rank: exact = 0, widening/reference/lifting = 1, boxing to object = 2; -1 = none.</summary>
        public static int ConversionRank<TType>(IRankModel<TType> model, in RankArgument<TType> argument,
            TType parameterType)
        {
            if (argument.IsNullLiteral)
            {
                return !model.IsValueType(parameterType) || model.TryGetNullableUnderlying(parameterType, out _)
                    ? Widening
                    : NoConversion;
            }

            var argType = argument.Type;
            if (model.AreSame(argType, parameterType))
                return Exact;
            if (model.IsObject(parameterType))
                return Boxing;
            if (NumericTable.IsImplicit(model.KindOf(argType), model.KindOf(parameterType)))
                return Widening;
            if (!model.IsValueType(argType) && model.IsReferenceAssignable(argType, parameterType))
                return Widening;
            if (model.IsValueType(argType) && model.TryGetNullableUnderlying(parameterType, out var lifted) &&
                model.AreSame(lifted, argType))
                return Widening;
            if (model.TryGetNullableUnderlying(argType, out var argUnderlying) &&
                model.TryGetNullableUnderlying(parameterType, out var paramUnderlying) &&
                (model.AreSame(argUnderlying, paramUnderlying) ||
                 NumericTable.IsImplicit(model.KindOf(argUnderlying), model.KindOf(paramUnderlying))))
                return Widening;
            return NoConversion;
        }

        /// <summary>Pareto domination: no component worse, at least one strictly better.</summary>
        public static bool Dominates(int[] a, int[] b)
        {
            bool strictlyBetter = false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] > b[i])
                    return false;
                if (a[i] < b[i])
                    strictlyBetter = true;
            }

            return strictlyBetter;
        }

        /// <summary>The per-candidate rank vector, or false when the candidate is not applicable at all.</summary>
        public static bool TryRank<TType>(IRankModel<TType> model, in RankCandidate<TType> candidate,
            IReadOnlyList<RankArgument<TType>> args, bool expanded, out int[] ranks)
        {
            ranks = null;
            if (!expanded)
            {
                if (candidate.ParameterTypes.Count != args.Count)
                    return false;
                var result = new int[args.Count];
                for (int i = 0; i < args.Count; i++)
                {
                    int rank = ConversionRank(model, args[i], candidate.ParameterTypes[i]);
                    if (rank < 0)
                        return false;
                    result[i] = rank;
                }

                ranks = result;
                return true;
            }

            if (!candidate.HasParamsArray)
                return false;
            int fixedCount = candidate.ParameterTypes.Count - 1;
            if (args.Count < fixedCount)
                return false;
            var vector = new int[args.Count];
            for (int i = 0; i < fixedCount; i++)
            {
                int rank = ConversionRank(model, args[i], candidate.ParameterTypes[i]);
                if (rank < 0)
                    return false;
                vector[i] = rank;
            }

            for (int i = fixedCount; i < args.Count; i++)
            {
                int rank = ConversionRank(model, args[i], candidate.ParamsElementType);
                if (rank < 0)
                    return false;
                vector[i] = rank + 1;   // expanded params ranked slightly worse than a fixed match
            }

            ranks = vector;
            return true;
        }

        /// <summary>The two-tier bind: normal form first, params-expanded only if nothing was applicable.</summary>
        public static RankBinding Bind<TType>(IRankModel<TType> model, IReadOnlyList<RankCandidate<TType>> candidates,
            IReadOnlyList<RankArgument<TType>> args)
        {
            var binding = BindTier(model, candidates, args, expanded: false);
            if (binding.Outcome != BindOutcome.None)
                return binding;
            return BindTier(model, candidates, args, expanded: true);
        }

        private static RankBinding BindTier<TType>(IRankModel<TType> model,
            IReadOnlyList<RankCandidate<TType>> candidates, IReadOnlyList<RankArgument<TType>> args, bool expanded)
        {
            var applicable = new List<(int index, int[] ranks)>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (TryRank(model, candidates[i], args, expanded, out var ranks))
                    applicable.Add((i, ranks));
            }

            if (applicable.Count == 0)
                return new RankBinding(BindOutcome.None, -1, expanded);

            int winner = -1;
            int frontSize = 0;
            foreach (var candidate in applicable)
            {
                bool dominated = false;
                foreach (var other in applicable)
                {
                    if (other.index != candidate.index && Dominates(other.ranks, candidate.ranks))
                    {
                        dominated = true;
                        break;
                    }
                }

                if (dominated)
                    continue;
                frontSize++;
                winner = candidate.index;
            }

            if (frontSize == 1)
                return new RankBinding(BindOutcome.Bound, winner, expanded);
            return new RankBinding(BindOutcome.Ambiguous, -1, expanded);
        }
    }
}
