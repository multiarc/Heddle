using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Heddle.Precompiled
{
    /// <summary>
    /// The native-tier operator ADAPTERS: what generated code calls where verbatim C# cannot reproduce the
    /// engine's semantics but the engine's own decision procedure can be replayed over the call site's static
    /// types. Each generic instantiation builds the SAME expression tree the engine's
    /// <c>NativeExpressionCompiler</c> builds for the same operand types — same factory calls, same fallback
    /// chain — and caches the compiled delegate per type pair, so parity holds by construction rather than by
    /// imitation, and the build cost is paid once per closed generic type.
    /// <para>The writer only emits a call where that chain is TOTAL for the operands' static kinds — a pair
    /// that could reach the engine's compile-time refusal (HED1008) still degrades to the dynamic tier,
    /// because a refusal the engine states at template compile must not become a render-time throw.</para>
    /// </summary>
    public static class RuntimeOperators
    {
        /// <summary>The engine's <c>==</c> over operand pairs no verbatim C# operator covers: a user-defined
        /// operator where the pair binds one, reference equality where the types share it, and null-safe
        /// <c>object.Equals</c> for the unrelated rest — exactly <c>VisitEquality</c>'s tail.</summary>
        public static bool Equal<TLeft, TRight>(TLeft left, TRight right) =>
            Equality<TLeft, TRight>.EqualOp(left, right);

        /// <summary>The engine's <c>!=</c> twin. Built separately rather than spelled as a negation, because
        /// a type carrying a user-defined <c>!=</c> gets that operator on the engine too.</summary>
        public static bool NotEqual<TLeft, TRight>(TLeft left, TRight right) =>
            Equality<TLeft, TRight>.NotEqualOp(left, right);

        private static readonly MethodInfo ObjectEquals =
            typeof(object).GetMethod(nameof(object.Equals), new[] { typeof(object), typeof(object) });

        private static class Equality<TLeft, TRight>
        {
            internal static readonly Func<TLeft, TRight, bool> EqualOp = Build(equal: true);
            internal static readonly Func<TLeft, TRight, bool> NotEqualOp = Build(equal: false);

            private static Func<TLeft, TRight, bool> Build(bool equal)
            {
                var left = Expression.Parameter(typeof(TLeft), "left");
                var right = Expression.Parameter(typeof(TRight), "right");
                Expression body;
                try
                {
                    body = equal ? Expression.Equal(left, right) : (Expression)Expression.NotEqual(left, right);
                }
                catch (InvalidOperationException)
                {
                    // The factory found no operator for the pair; the engine's fallback for two
                    // null-assignable operands is the boxed, null-safe object.Equals — and the writer only
                    // emits this adapter for pairs that are null-assignable on both sides, which is what
                    // makes this catch a fallback rather than a failure.
                    var equals = Expression.Call(ObjectEquals,
                        Expression.Convert(left, typeof(object)), Expression.Convert(right, typeof(object)));
                    body = equal ? (Expression)equals : Expression.Not(equals);
                }

                return Expression.Lambda<Func<TLeft, TRight, bool>>(body, left, right).Compile();
            }
        }
    }
}
