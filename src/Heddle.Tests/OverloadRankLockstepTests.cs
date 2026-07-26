using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Language.Expressions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The characterization pin for the overload-rank extraction.
    /// <para><b>Legacy*</b> is the pre-extraction body, transcribed verbatim from
    /// <c>NativeExpressionCompiler.ConversionRank</c>/<c>Dominates</c> before they moved. The sweep is exhaustive
    /// over a type matrix that includes every numeric primitive, the lifted forms, <c>object</c>, <c>string</c>, a
    /// reference hierarchy and an unrelated struct — so the rank table cannot drift while the runtime delegates.</para>
    /// <para>The rank being pinned is <b>Heddle's</b>, not C#'s: every widening ranks 1, flat. That is precisely why
    /// the generator can no longer hand overload selection to the consumer's compiler.</para>
    /// </summary>
    public class OverloadRankLockstepTests
    {
        private class RefBase { }

        private sealed class RefDerived : RefBase { }

        private struct Unrelated { }

        private static readonly Type[] Matrix =
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
            typeof(long), typeof(ulong), typeof(char), typeof(float), typeof(double), typeof(decimal),
            typeof(bool), typeof(string), typeof(object), typeof(int?), typeof(long?), typeof(double?),
            typeof(bool?), typeof(RefBase), typeof(RefDerived), typeof(Unrelated), typeof(Unrelated?),
            typeof(object[]),
        };

        private static int LegacyConversionRank(Type argType, bool isNullLiteral, Type parameterType)
        {
            if (isNullLiteral)
            {
                if (!parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null)
                    return 1;
                return -1;
            }

            if (argType == parameterType)
                return 0;
            if (parameterType == typeof(object))
                return 2;
            if (LegacyIsImplicitNumeric(argType, parameterType))
                return 1;
            if (!argType.IsValueType && parameterType.IsAssignableFrom(argType))
                return 1;
            if (argType.IsValueType && Nullable.GetUnderlyingType(parameterType) == argType)
                return 1;
            var argUnderlying = Nullable.GetUnderlyingType(argType);
            var paramUnderlying = Nullable.GetUnderlyingType(parameterType);
            if (argUnderlying != null && paramUnderlying != null &&
                (argUnderlying == paramUnderlying || LegacyIsImplicitNumeric(argUnderlying, paramUnderlying)))
                return 1;
            return -1;
        }

        private static readonly Dictionary<Type, HashSet<Type>> LegacyImplicit = new Dictionary<Type, HashSet<Type>>
        {
            [typeof(sbyte)] = new HashSet<Type> { typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) },
            [typeof(byte)] = new HashSet<Type> { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(short)] = new HashSet<Type> { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) },
            [typeof(ushort)] = new HashSet<Type> { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(int)] = new HashSet<Type> { typeof(long), typeof(float), typeof(double), typeof(decimal) },
            [typeof(uint)] = new HashSet<Type> { typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(long)] = new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) },
            [typeof(ulong)] = new HashSet<Type> { typeof(float), typeof(double), typeof(decimal) },
            [typeof(char)] = new HashSet<Type> { typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
            [typeof(float)] = new HashSet<Type> { typeof(double) }
        };

        private static bool LegacyIsImplicitNumeric(Type from, Type to) =>
            from != null && to != null && LegacyImplicit.TryGetValue(from, out var set) && set.Contains(to);

        private static bool LegacyDominates(int[] a, int[] b)
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

        private sealed class TypeRankModel : IRankModel<Type>
        {
            public static readonly TypeRankModel Instance = new TypeRankModel();
            public bool AreSame(Type a, Type b) => a == b;
            public bool IsObject(Type type) => type == typeof(object);
            public bool IsValueType(Type type) => type.IsValueType;

            public bool TryGetNullableUnderlying(Type type, out Type underlying)
            {
                underlying = Nullable.GetUnderlyingType(type);
                return underlying != null;
            }

            public NumericKind KindOf(Type type) => NumericTable.FromClrType(type);
            public bool IsReferenceAssignable(Type from, Type to) => to.IsAssignableFrom(from);
        }

        [Fact]
        public void ConversionRank_MatchesTheLegacyBody_Exhaustively()
        {
            foreach (var arg in Matrix)
            foreach (var parameter in Matrix)
            {
                Assert.Equal(LegacyConversionRank(arg, false, parameter),
                    OverloadRank.ConversionRank(TypeRankModel.Instance, RankArgument<Type>.Of(arg), parameter));
            }

            foreach (var parameter in Matrix)
            {
                Assert.Equal(LegacyConversionRank(null, true, parameter),
                    OverloadRank.ConversionRank(TypeRankModel.Instance, RankArgument<Type>.Null(), parameter));
            }
        }

        [Fact]
        public void RankIsFlat_EveryWideningScoresOne()
        {
            // The property C# betterness does *not* have, and the reason the generator cannot delegate selection.
            Assert.Equal(OverloadRank.Widening,
                OverloadRank.ConversionRank(TypeRankModel.Instance, RankArgument<Type>.Of(typeof(int)), typeof(long)));
            Assert.Equal(OverloadRank.Widening,
                OverloadRank.ConversionRank(TypeRankModel.Instance, RankArgument<Type>.Of(typeof(int)), typeof(decimal)));
            Assert.Equal(OverloadRank.Exact,
                OverloadRank.ConversionRank(TypeRankModel.Instance, RankArgument<Type>.Of(typeof(int)), typeof(int)));
            Assert.Equal(OverloadRank.Boxing,
                OverloadRank.ConversionRank(TypeRankModel.Instance, RankArgument<Type>.Of(typeof(int)), typeof(object)));
        }

        [Fact]
        public void Dominates_MatchesTheLegacyBody()
        {
            var vectors = new[]
            {
                new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 1, 1 }, new[] { 1, 2 }, new[] { 2, 2 },
            };
            foreach (var a in vectors)
            foreach (var b in vectors)
                Assert.Equal(LegacyDominates(a, b), OverloadRank.Dominates(a, b));
        }

        private static RankCandidate<Type> Candidate(params Type[] parameters) =>
            new RankCandidate<Type>(parameters, false, null);

        private static readonly RankCandidate<Type>[] MinCandidates =
        {
            Candidate(typeof(int), typeof(int)),
            Candidate(typeof(long), typeof(long)),
            Candidate(typeof(double), typeof(double)),
            Candidate(typeof(decimal), typeof(decimal)),
        };

        [Fact]
        public void Min_IntInt_ResolvesUniquely()
        {
            var binding = OverloadRank.Bind(TypeRankModel.Instance, MinCandidates,
                new[] { RankArgument<Type>.Of(typeof(int)), RankArgument<Type>.Of(typeof(int)) });
            Assert.Equal(BindOutcome.Bound, binding.Outcome);
            Assert.Equal(0, binding.Index);
        }

        [Fact]
        public void Min_IntUInt_IsAmbiguous_TheShippedCounterExample()
        {
            // (long,long), (double,double) and (decimal,decimal) all rank (1,1); C# betterness would pick
            // Min(long,long) and render. The generator degrades here instead, which is what makes the two tiers
            // agree.
            var binding = OverloadRank.Bind(TypeRankModel.Instance, MinCandidates,
                new[] { RankArgument<Type>.Of(typeof(int)), RankArgument<Type>.Of(typeof(uint)) });
            Assert.Equal(BindOutcome.Ambiguous, binding.Outcome);
        }

        [Fact]
        public void ParamsExpansionIsTheSecondTierOnly()
        {
            var candidates = new[]
            {
                new RankCandidate<Type>(new[] { typeof(object), typeof(string) }, false, null),
                new RankCandidate<Type>(new[] { typeof(string), typeof(object[]) }, true, typeof(object)),
            };

            // format(value, fmt) — the fixed form wins outright, no expansion.
            var fixedForm = OverloadRank.Bind(TypeRankModel.Instance, candidates,
                new[] { RankArgument<Type>.Of(typeof(string)), RankArgument<Type>.Of(typeof(string)) });
            Assert.Equal(BindOutcome.Bound, fixedForm.Outcome);
            Assert.Equal(0, fixedForm.Index);
            Assert.False(fixedForm.Expanded);

            // format(fmt, a, b) — nothing applies unexpanded, so the params tier binds.
            var expanded = OverloadRank.Bind(TypeRankModel.Instance, candidates,
                new[] { RankArgument<Type>.Of(typeof(string)), RankArgument<Type>.Of(typeof(int)), RankArgument<Type>.Of(typeof(int)) });
            Assert.Equal(BindOutcome.Bound, expanded.Outcome);
            Assert.Equal(1, expanded.Index);
            Assert.True(expanded.Expanded);
        }

        [Fact]
        public void RuntimeStillReportsTheAmbiguity_ThroughTheSharedCore()
        {
            var template = new HeddleTemplate("@(min(1, 2u))",
                new CompileContext(new TemplateOptions(), typeof(string)));
            Assert.False(template.CompileResult.Success);
            Assert.Contains(template.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.AmbiguousFunctionCall);

            var ok = new HeddleTemplate("@(min(1, 2))", new CompileContext(new TemplateOptions(), typeof(string)));
            Assert.True(ok.CompileResult.Success, ok.CompileResult.ToString());
            Assert.Equal("1", ok.Generate("x"));
        }
    }
}
