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

        /// <summary>The engine's unchecked <c>+</c> over one promoted type — <c>Expression.Add</c> exactly,
        /// including its render-time throws. Non-constant to the consumer's compiler, so constant arithmetic
        /// the two compilers fold differently is evaluated the engine's way instead of being folded.</summary>
        public static T Add<T>(T left, T right) => Arithmetic<T>.AddOp(left, right);

        /// <summary>The engine's unchecked <c>-</c>; see <see cref="Add{T}"/>.</summary>
        public static T Subtract<T>(T left, T right) => Arithmetic<T>.SubtractOp(left, right);

        /// <summary>The engine's unchecked <c>*</c>; see <see cref="Add{T}"/>.</summary>
        public static T Multiply<T>(T left, T right) => Arithmetic<T>.MultiplyOp(left, right);

        /// <summary>The engine's <c>/</c>, throws and all — the smallest signed value over -1 raises
        /// <see cref="OverflowException"/> at render exactly as the engine's tree does, where C# would have
        /// folded the constant silently to a number the engine never produces.</summary>
        public static T Divide<T>(T left, T right) => Arithmetic<T>.DivideOp(left, right);

        /// <summary>The engine's <c>%</c>; see <see cref="Divide{T}"/>.</summary>
        public static T Modulo<T>(T left, T right) => Arithmetic<T>.ModuloOp(left, right);

        /// <summary>The engine's <c>+</c> where a user-defined operator binds for the pair —
        /// <c>Expression.Add</c> over the operands' static types, which is the operator the engine's tree
        /// binds, never a user-defined conversion verbatim C# might prefer.</summary>
        public static TResult Add<TLeft, TRight, TResult>(TLeft left, TRight right) =>
            UserBinary<TLeft, TRight, TResult>.AddOp.Value(left, right);

        /// <summary>The engine's <c>-</c> for a user-operator pair; see <see cref="Add{TLeft,TRight,TResult}"/>.</summary>
        public static TResult Subtract<TLeft, TRight, TResult>(TLeft left, TRight right) =>
            UserBinary<TLeft, TRight, TResult>.SubtractOp.Value(left, right);

        /// <summary>The engine's <c>*</c> for a user-operator pair; see <see cref="Add{TLeft,TRight,TResult}"/>.</summary>
        public static TResult Multiply<TLeft, TRight, TResult>(TLeft left, TRight right) =>
            UserBinary<TLeft, TRight, TResult>.MultiplyOp.Value(left, right);

        /// <summary>The engine's <c>/</c> for a user-operator pair; see <see cref="Add{TLeft,TRight,TResult}"/>.</summary>
        public static TResult Divide<TLeft, TRight, TResult>(TLeft left, TRight right) =>
            UserBinary<TLeft, TRight, TResult>.DivideOp.Value(left, right);

        /// <summary>The engine's <c>%</c> for a user-operator pair; see <see cref="Add{TLeft,TRight,TResult}"/>.</summary>
        public static TResult Modulo<TLeft, TRight, TResult>(TLeft left, TRight right) =>
            UserBinary<TLeft, TRight, TResult>.ModuloOp.Value(left, right);

        /// <summary>The engine's <c>&lt;</c> where a user-defined comparison binds —
        /// <c>Expression.LessThan</c> over the operands' static types.</summary>
        public static bool LessThan<TLeft, TRight>(TLeft left, TRight right) =>
            Relational<TLeft, TRight>.LessThanOp.Value(left, right);

        /// <summary>The engine's <c>&lt;=</c>; see <see cref="LessThan{TLeft,TRight}"/>.</summary>
        public static bool LessOrEqual<TLeft, TRight>(TLeft left, TRight right) =>
            Relational<TLeft, TRight>.LessOrEqualOp.Value(left, right);

        /// <summary>The engine's <c>&gt;</c>; see <see cref="LessThan{TLeft,TRight}"/>.</summary>
        public static bool GreaterThan<TLeft, TRight>(TLeft left, TRight right) =>
            Relational<TLeft, TRight>.GreaterThanOp.Value(left, right);

        /// <summary>The engine's <c>&gt;=</c>; see <see cref="LessThan{TLeft,TRight}"/>.</summary>
        public static bool GreaterOrEqual<TLeft, TRight>(TLeft left, TRight right) =>
            Relational<TLeft, TRight>.GreaterOrEqualOp.Value(left, right);

        private static readonly MethodInfo ObjectEquals =
            typeof(object).GetMethod(nameof(object.Equals), new[] { typeof(object), typeof(object) });

        // Lazy per operator: a type may declare '+' but not '-', or '<'/'>' but not their -or-equal twins,
        // and an eager sibling build would fault the whole cache for operators the template never uses.
        private static class UserBinary<TLeft, TRight, TResult>
        {
            internal static readonly Lazy<Func<TLeft, TRight, TResult>> AddOp = Create(Expression.Add);
            internal static readonly Lazy<Func<TLeft, TRight, TResult>> SubtractOp = Create(Expression.Subtract);
            internal static readonly Lazy<Func<TLeft, TRight, TResult>> MultiplyOp = Create(Expression.Multiply);
            internal static readonly Lazy<Func<TLeft, TRight, TResult>> DivideOp = Create(Expression.Divide);
            internal static readonly Lazy<Func<TLeft, TRight, TResult>> ModuloOp = Create(Expression.Modulo);

            private static Lazy<Func<TLeft, TRight, TResult>> Create(
                Func<Expression, Expression, BinaryExpression> factory)
            {
                return new Lazy<Func<TLeft, TRight, TResult>>(() =>
                {
                    var left = Expression.Parameter(typeof(TLeft), "left");
                    var right = Expression.Parameter(typeof(TRight), "right");
                    Expression body = factory(left, right);
                    if (body.Type != typeof(TResult))
                        body = Expression.Convert(body, typeof(TResult));
                    return Expression.Lambda<Func<TLeft, TRight, TResult>>(body, left, right).Compile();
                });
            }
        }

        private static class Relational<TLeft, TRight>
        {
            internal static readonly Lazy<Func<TLeft, TRight, bool>> LessThanOp =
                Create((l, r) => Expression.LessThan(l, r, false, null));

            internal static readonly Lazy<Func<TLeft, TRight, bool>> LessOrEqualOp =
                Create((l, r) => Expression.LessThanOrEqual(l, r, false, null));

            internal static readonly Lazy<Func<TLeft, TRight, bool>> GreaterThanOp =
                Create((l, r) => Expression.GreaterThan(l, r, false, null));

            internal static readonly Lazy<Func<TLeft, TRight, bool>> GreaterOrEqualOp =
                Create((l, r) => Expression.GreaterThanOrEqual(l, r, false, null));

            private static Lazy<Func<TLeft, TRight, bool>> Create(
                Func<Expression, Expression, BinaryExpression> factory)
            {
                return new Lazy<Func<TLeft, TRight, bool>>(() =>
                {
                    var left = Expression.Parameter(typeof(TLeft), "left");
                    var right = Expression.Parameter(typeof(TRight), "right");
                    return Expression.Lambda<Func<TLeft, TRight, bool>>(factory(left, right), left, right)
                        .Compile();
                });
            }
        }

        private static class Arithmetic<T>
        {
            internal static readonly Func<T, T, T> AddOp = Build(Expression.Add);
            internal static readonly Func<T, T, T> SubtractOp = Build(Expression.Subtract);
            internal static readonly Func<T, T, T> MultiplyOp = Build(Expression.Multiply);
            internal static readonly Func<T, T, T> DivideOp = Build(Expression.Divide);
            internal static readonly Func<T, T, T> ModuloOp = Build(Expression.Modulo);

            private static Func<T, T, T> Build(Func<Expression, Expression, BinaryExpression> factory)
            {
                var left = Expression.Parameter(typeof(T), "left");
                var right = Expression.Parameter(typeof(T), "right");
                return Expression.Lambda<Func<T, T, T>>(factory(left, right), left, right).Compile();
            }
        }

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
