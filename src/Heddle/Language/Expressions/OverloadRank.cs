using System;
using System.Collections.Generic;

namespace Heddle.Language.Expressions
{
    /// <summary>One argument as the ranker sees it: a type, or the untyped <c>null</c> literal.</summary>
    internal readonly struct RankArgument
    {
        private RankArgument(bool isNullLiteral, Type type)
        {
            IsNullLiteral = isNullLiteral;
            Type = type;
        }

        public bool IsNullLiteral { get; }

        public Type Type { get; }

        public static RankArgument Of(Type type) => new RankArgument(false, type);

        public static RankArgument Null() => new RankArgument(true, null);
    }

    /// <summary>One candidate signature.</summary>
    internal readonly struct RankCandidate
    {
        public RankCandidate(IReadOnlyList<Type> parameterTypes, bool hasParamsArray, Type paramsElementType)
        {
            ParameterTypes = parameterTypes;
            HasParamsArray = hasParamsArray;
            ParamsElementType = paramsElementType;
        }

        public IReadOnlyList<Type> ParameterTypes { get; }

        public bool HasParamsArray { get; }

        public Type ParamsElementType { get; }
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
    /// Heddle's function-overload selection rule: flat Pareto rank (exact 0, widening/reference/lifting 1, boxing 2,
    /// params-expanded one worse), then take the non-dominated front. Used as an emit guard to degrade on ambiguity
    /// and emit cast-pinned otherwise.
    /// </summary>
    internal static class OverloadRank
    {
        public const int Exact = 0;
        public const int Widening = 1;
        public const int Boxing = 2;
        public const int NoConversion = -1;

        /// <summary>Conversion rank: exact = 0, widening/reference/lifting = 1, boxing to object = 2; -1 = none.</summary>
        public static int ConversionRank(in RankArgument argument, Type parameterType)
        {
            if (argument.IsNullLiteral)
            {
                return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null
                    ? Widening
                    : NoConversion;
            }

            var argType = argument.Type;
            if (argType == parameterType)
                return Exact;
            if (parameterType == typeof(object))
                return Boxing;
            if (NumericTable.IsImplicit(NumericTable.FromClrType(argType), NumericTable.FromClrType(parameterType)))
                return Widening;
            if (!argType.IsValueType && parameterType.IsAssignableFrom(argType))
                return Widening;
            if (argType.IsValueType && Nullable.GetUnderlyingType(parameterType) == argType)
                return Widening;
            var argUnderlying = Nullable.GetUnderlyingType(argType);
            var paramUnderlying = Nullable.GetUnderlyingType(parameterType);
            if (argUnderlying != null && paramUnderlying != null &&
                (argUnderlying == paramUnderlying ||
                 NumericTable.IsImplicit(NumericTable.FromClrType(argUnderlying),
                     NumericTable.FromClrType(paramUnderlying))))
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
        public static bool TryRank(in RankCandidate candidate, IReadOnlyList<RankArgument> args, bool expanded,
            out int[] ranks)
        {
            ranks = null;
            if (!expanded)
            {
                if (candidate.ParameterTypes.Count != args.Count)
                    return false;
                var result = new int[args.Count];
                for (int i = 0; i < args.Count; i++)
                {
                    int rank = ConversionRank(args[i], candidate.ParameterTypes[i]);
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
                int rank = ConversionRank(args[i], candidate.ParameterTypes[i]);
                if (rank < 0)
                    return false;
                vector[i] = rank;
            }

            for (int i = fixedCount; i < args.Count; i++)
            {
                int rank = ConversionRank(args[i], candidate.ParamsElementType);
                if (rank < 0)
                    return false;
                vector[i] = rank + 1;   // expanded params ranked slightly worse than a fixed match
            }

            ranks = vector;
            return true;
        }

        /// <summary>The two-tier bind: normal form first, params-expanded only if nothing was applicable.</summary>
        public static RankBinding Bind(IReadOnlyList<RankCandidate> candidates, IReadOnlyList<RankArgument> args)
        {
            var binding = BindTier(candidates, args, expanded: false);
            if (binding.Outcome != BindOutcome.None)
                return binding;
            return BindTier(candidates, args, expanded: true);
        }

        private static RankBinding BindTier(IReadOnlyList<RankCandidate> candidates,
            IReadOnlyList<RankArgument> args, bool expanded)
        {
            var applicable = new List<(int index, int[] ranks)>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (TryRank(candidates[i], args, expanded, out var ranks))
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
