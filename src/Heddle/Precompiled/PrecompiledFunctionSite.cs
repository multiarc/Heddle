using System;
using System.Linq.Expressions;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Language.Expressions;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Heddle.Strings.Core;

namespace Heddle.Precompiled
{
    /// <summary>
    /// A late-bound function call site in a precompiled template — one static field per call site, resolved ONCE
    /// at first use against the render's effective <see cref="Heddle.Data.TemplateOptions.Functions"/> registry and
    /// cached. Called only by generated code; not intended for hand-written use.
    /// <para>The build tier cannot bind what the host registers at run time — a delegate registration is not
    /// representable in metadata — but the call <b>shape</b> (name, argument count, each argument's static type) is
    /// fully known at build time, and it is exactly the input the engine's own compile-time overload selection
    /// takes. So first use replays that selection through the engine's own ranker
    /// (<c>OverloadRank</c>, reached through <c>NativeExpressionCompiler.BindOverload</c>), builds the engine's own
    /// invocation tree (<c>NativeExpressionCompiler.BuildCallArguments</c> plus <c>Call</c>/<c>Invoke</c>, result
    /// boxed to <see cref="object"/>), compiles it and caches the delegate. The same overload wins on both tiers by
    /// construction rather than by agreement, and every later render is one delegate call.</para>
    /// <para><b>Argument types come from the generic parameters</b>, which the consumer's compiler infers from the
    /// emitted argument expressions — the same static types the engine's compiled argument expressions carry. The
    /// one shape inference cannot serve is the untyped <c>null</c> literal, whose site parameter is spelled
    /// <see cref="object"/> and whose position is carried in <c>nullLiteralMask</c> so the ranker still sees the
    /// null literal it would have seen.</para>
    /// <para><b>Steady state allocates nothing:</b> a volatile read, a reference comparison and the delegate call.
    /// The per-arity <see cref="Type"/> array the bind needs is a static field of a generic holder, built once per
    /// instantiation, never per render.</para>
    /// <para><b>Failure is the engine's failure.</b> A name that resolves to no overload, an ambiguous call and a
    /// call no overload accepts each produce the engine's own compile error — same <c>HED</c> id, same sentence,
    /// same position — captured once and re-raised as <see cref="TemplateCompileException"/> on every invoke,
    /// exactly as <see cref="PrecompiledPartialName"/> re-raises a captured <c>@partial</c> compile fault. On the
    /// registry path the gauntlet moves those requests to the dynamic tier before any render, so the engine's own
    /// compile stays the failure authority; on the typed entry point, which has no gauntlet, this site is it.</para>
    /// <para><b>Thread-safe.</b> Racing first renders may each bind; the results are identical by construction (the
    /// ranker is deterministic over a frozen registry) and the last write wins. The cache is keyed on the registry
    /// instance, so a render under different <see cref="Heddle.Data.TemplateOptions.Functions"/> re-binds — the
    /// dynamic tier likewise compiles per options.</para>
    /// </summary>
    public sealed class PrecompiledFunctionSite
    {
        /// <summary>The widest call a generated site binds late — the shared
        /// <see cref="PrecompiledSchema.LateBoundFunctionMaxArity"/>, which the emitter's gate reads too, so the
        /// arity list below and the build tier cannot drift into a missing-method fault.</summary>
        public const int MaxArgumentCount = PrecompiledSchema.LateBoundFunctionMaxArity;

        private readonly string _name;
        private readonly int _nullLiteralMask;
        private readonly bool _inExpression;
        private readonly bool _definitionExists;
        private readonly int _positionStart;
        private readonly int _positionLength;
        private volatile Binding _binding;

        /// <summary>Creates the site. Reached only from a generated static field initializer.</summary>
        /// <param name="name">The called function name; ordinal and case-sensitive, as registration is.</param>
        /// <param name="nullLiteralMask">Bit <c>i</c> set means argument <c>i</c> is the untyped <c>null</c>
        /// literal, which the ranker treats as convertible to any reference or nullable parameter.</param>
        /// <param name="inExpression">Whether the engine compiles this call inside a native expression rather than
        /// as a top-level call item — it selects which of the engine's two unknown-name sentences a bind failure
        /// reproduces.</param>
        /// <param name="definitionExists">Whether the enclosing parse context resolves the name to a definition, a
        /// build-time constant the engine's in-expression unknown-name arm consults.</param>
        /// <param name="positionStart">The call's start offset in the template document.</param>
        /// <param name="positionLength">The call's length; with <paramref name="positionStart"/>, the position the
        /// engine gives the error.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
        public PrecompiledFunctionSite(string name, int nullLiteralMask, bool inExpression, bool definitionExists,
            int positionStart, int positionLength)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            _name = name;
            _nullLiteralMask = nullLiteralMask;
            _inExpression = inExpression;
            _definitionExists = definitionExists;
            _positionStart = positionStart;
            _positionLength = positionLength;
        }

        /// <summary>Invokes the no-argument form.</summary>
        public object Invoke()
            => ((Func<object>)Bound(Type.EmptyTypes, typeof(Func<object>)))();

        /// <summary>Invokes the one-argument form.</summary>
        public object Invoke<T0>(T0 a0)
            => ((Func<T0, object>)Bound(Arity<T0>.Types, typeof(Func<T0, object>)))(a0);

        /// <summary>Invokes the two-argument form.</summary>
        public object Invoke<T0, T1>(T0 a0, T1 a1)
            => ((Func<T0, T1, object>)Bound(Arity<T0, T1>.Types, typeof(Func<T0, T1, object>)))(a0, a1);

        /// <summary>Invokes the three-argument form.</summary>
        public object Invoke<T0, T1, T2>(T0 a0, T1 a1, T2 a2)
            => ((Func<T0, T1, T2, object>)Bound(Arity<T0, T1, T2>.Types,
                typeof(Func<T0, T1, T2, object>)))(a0, a1, a2);

        /// <summary>Invokes the four-argument form.</summary>
        public object Invoke<T0, T1, T2, T3>(T0 a0, T1 a1, T2 a2, T3 a3)
            => ((Func<T0, T1, T2, T3, object>)Bound(Arity<T0, T1, T2, T3>.Types,
                typeof(Func<T0, T1, T2, T3, object>)))(a0, a1, a2, a3);

        /// <summary>Invokes the five-argument form.</summary>
        public object Invoke<T0, T1, T2, T3, T4>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4)
            => ((Func<T0, T1, T2, T3, T4, object>)Bound(Arity<T0, T1, T2, T3, T4>.Types,
                typeof(Func<T0, T1, T2, T3, T4, object>)))(a0, a1, a2, a3, a4);

        /// <summary>Invokes the six-argument form.</summary>
        public object Invoke<T0, T1, T2, T3, T4, T5>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5)
            => ((Func<T0, T1, T2, T3, T4, T5, object>)Bound(Arity<T0, T1, T2, T3, T4, T5>.Types,
                typeof(Func<T0, T1, T2, T3, T4, T5, object>)))(a0, a1, a2, a3, a4, a5);

        /// <summary>Invokes the seven-argument form.</summary>
        public object Invoke<T0, T1, T2, T3, T4, T5, T6>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6)
            => ((Func<T0, T1, T2, T3, T4, T5, T6, object>)Bound(Arity<T0, T1, T2, T3, T4, T5, T6>.Types,
                typeof(Func<T0, T1, T2, T3, T4, T5, T6, object>)))(a0, a1, a2, a3, a4, a5, a6);

        /// <summary>Invokes the eight-argument form — the widest the build tier emits.</summary>
        public object Invoke<T0, T1, T2, T3, T4, T5, T6, T7>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7)
            => ((Func<T0, T1, T2, T3, T4, T5, T6, T7, object>)Bound(Arity<T0, T1, T2, T3, T4, T5, T6, T7>.Types,
                typeof(Func<T0, T1, T2, T3, T4, T5, T6, T7, object>)))(a0, a1, a2, a3, a4, a5, a6, a7);

        // The bind's argument-type vector, one static array per instantiation. Built at type-init of the holder,
        // so the steady-state invoke passes an existing reference instead of allocating a params array per render.
        private static class Arity<T0>
        {
            internal static readonly Type[] Types = { typeof(T0) };
        }

        private static class Arity<T0, T1>
        {
            internal static readonly Type[] Types = { typeof(T0), typeof(T1) };
        }

        private static class Arity<T0, T1, T2>
        {
            internal static readonly Type[] Types = { typeof(T0), typeof(T1), typeof(T2) };
        }

        private static class Arity<T0, T1, T2, T3>
        {
            internal static readonly Type[] Types = { typeof(T0), typeof(T1), typeof(T2), typeof(T3) };
        }

        private static class Arity<T0, T1, T2, T3, T4>
        {
            internal static readonly Type[] Types = { typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4) };
        }

        private static class Arity<T0, T1, T2, T3, T4, T5>
        {
            internal static readonly Type[] Types =
                { typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) };
        }

        private static class Arity<T0, T1, T2, T3, T4, T5, T6>
        {
            internal static readonly Type[] Types =
                { typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6) };
        }

        private static class Arity<T0, T1, T2, T3, T4, T5, T6, T7>
        {
            internal static readonly Type[] Types =
            {
                typeof(T0), typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)
            };
        }

        private sealed class Binding
        {
            public Binding(FunctionRegistry registry, Delegate invoker, HeddleCompileError error)
            {
                Registry = registry;
                Invoker = invoker;
                Error = error;
            }

            public readonly FunctionRegistry Registry;
            public readonly Delegate Invoker;
            public readonly HeddleCompileError Error;
        }

        /// <summary>The cached invoker for the render's effective registry. The steady state is a volatile read
        /// and a reference comparison; nothing here allocates once the first render has bound.</summary>
        private Delegate Bound(Type[] argTypes, Type delegateType)
        {
            var registry = PrecompiledRuntime.EffectiveFunctions;
            var binding = _binding;
            if (binding == null || !ReferenceEquals(binding.Registry, registry))
                _binding = binding = BindCore(registry, argTypes, delegateType);
            if (binding.Error != null)
                throw new TemplateCompileException(new[] { binding.Error });
            return binding.Invoker;
        }

        /// <summary>The engine's own classification and overload selection, replayed once against the live
        /// registry. Reuse, not reimplementation: the rank arguments feed the engine's <c>BindOverload</c>, the
        /// invocation is the engine's <c>BuildCallArguments</c> shape, and every failure sentence comes from the
        /// shared <see cref="FunctionCallMessages"/> the engine's compiler writes with.</summary>
        private Binding BindCore(FunctionRegistry registry, Type[] argTypes, Type delegateType)
        {
            // The one arm with no engine failure to reproduce. A TOP-LEVEL call whose name is a registered
            // extension at render time renders extension semantics on the engine — a body, a scope channel, a
            // render type — which a value site cannot produce. On the registry path the gauntlet moves such a
            // request to the dynamic tier before any render; reaching this throw means the typed entry point,
            // which has no gauntlet, rendered against a host configuration the build never saw. That is a host
            // deployment fault and is reported as one rather than rendered wrongly.
            if (!_inExpression && TemplateFactory.Exists(_name))
                throw new InvalidOperationException(
                    "Late-bound function '" + _name + "' is shadowed by a registered extension of the same name, " +
                    "whose render protocol a precompiled call site cannot reproduce. Resolve this template " +
                    "through the registry (TemplateResolver, or PrecompiledTemplates.TryResolve), where the " +
                    "request falls back to the dynamic tier, or do not register an extension under a name the " +
                    "template calls as a function. PrecompiledTemplates.ValidateAll(options) reports this before " +
                    "any render.");

            // The engine freezes the registry at its first compile use; this late bind IS that first use.
            registry.Freeze();
            var overloads = registry.GetOverloads(_name);
            if (overloads.Count == 0)
            {
                // The engine's two unknown-name sentences, selected by the engine's own predicate. A top-level
                // call meets HeddleCompiler's arm, which raises the same UnknownFunction text under the same id.
                return _inExpression && (TemplateFactory.Exists(_name) || _definitionExists)
                    ? ErrorBinding(registry, HeddleDiagnosticIds.ExtensionCalledAsFunction,
                        FunctionCallMessages.ExtensionCalledAsFunction(_name))
                    : ErrorBinding(registry, HeddleDiagnosticIds.UnknownFunction,
                        FunctionCallMessages.UnknownFunction(_name));
            }

            var rankArgs = new RankArgument<Type>[argTypes.Length];
            for (int i = 0; i < argTypes.Length; i++)
            {
                rankArgs[i] = ((_nullLiteralMask >> i) & 1) != 0
                    ? RankArgument<Type>.Null()
                    : RankArgument<Type>.Of(argTypes[i]);
            }

            var outcome = NativeExpressionCompiler.BindOverload(overloads, rankArgs, out var chosen,
                out var expanded);
            if (outcome == BindOutcome.Ambiguous)
                return ErrorBinding(registry, HeddleDiagnosticIds.AmbiguousFunctionCall,
                    FunctionCallMessages.AmbiguousFunctionCall(_name, overloads));
            if (outcome == BindOutcome.None)
            {
                // The engine names each argument by its compiled expression's own Type, which for the null
                // literal is object — exactly what the site's parameter is spelled as.
                var names = new string[argTypes.Length];
                for (int i = 0; i < argTypes.Length; i++)
                    names[i] = NativeExpressionCompiler.FriendlyName(argTypes[i]);
                return ErrorBinding(registry, HeddleDiagnosticIds.NoFunctionOverload,
                    FunctionCallMessages.NoFunctionOverload(_name, string.Join(", ", names), overloads));
            }

            var parameters = new ParameterExpression[argTypes.Length];
            var arguments = new Expression[argTypes.Length];
            for (int i = 0; i < argTypes.Length; i++)
            {
                parameters[i] = Expression.Parameter(argTypes[i]);
                arguments[i] = parameters[i];
            }

            var finalArgs = NativeExpressionCompiler.BuildCallArguments(chosen, arguments, expanded);
            Expression call = chosen.Method != null
                ? Expression.Call(null, chosen.Method, finalArgs)
                : (Expression)Expression.Invoke(
                    Expression.Constant(chosen.Target, chosen.Target.GetType()), finalArgs);
            var lambda = Expression.Lambda(delegateType, Expression.Convert(call, typeof(object)), parameters);
            return new Binding(registry, lambda.Compile(), null);
        }

        private Binding ErrorBinding(FunctionRegistry registry, string diagnosticId, string message)
            => new Binding(registry, null,
                message.ToError(new BlockPosition(_positionStart, _positionLength), diagnosticId));
    }
}
