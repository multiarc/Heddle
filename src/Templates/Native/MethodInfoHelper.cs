using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Templates.Native
{
    public static class MethodInfoHelper
    {
        public static Delegate CompileStaticDelegateAccessor<TDelegate>(this MethodInfo method)
        {
            if (!(typeof(TDelegate).GetTypeInfo().IsSubclassOf(typeof(Delegate))))
                throw new ArgumentException("TDelegate should be a delegate");
            var delegateMethod = typeof (TDelegate).GetMethod("Invoke");
            var dynamic = EmitDynamic(method.ReturnType, method, delegateMethod.GetParameters().Select(p => p.ParameterType).ToArray());
            return dynamic.CreateDelegate(typeof(TDelegate));
        }

        /// <summary>
        /// Emit and compile delegate using Accessor via DynamicMethod.
        /// Static parameterless methods only, can be void result.
        /// </summary>
        /// <typeparam name="TResult">The result of calee</typeparam>
        /// <param name="method">MethodInfo to compile accessor to</param>
        /// <returns>Delegate to invoke method</returns>
        public static Func<TResult> CompileStaticAccessor<TResult>(this MethodInfo method) {
            var dynamic = EmitDynamic<TResult>(method);
            return (Func<TResult>)dynamic.CreateDelegate(typeof(Func<TResult>));
        }

        /// <summary>
        /// Emit and compile delegate using Accessor via DynamicMethod
        /// </summary>
        /// <typeparam name="T">Either object to call method from or method parameter (depends weather method is static)</typeparam>
        /// <typeparam name="TResult">The result of calee</typeparam>
        /// <param name="method">MethodInfo to compile accessor to</param>
        /// <returns>Delegate to invoke method</returns>
        public static Func<T, TResult> CompileAccessor<T, TResult>(this MethodInfo method) {
            var dynamic = EmitDynamic<TResult>(method, typeof(T));
            return (Func<T, TResult>)dynamic.CreateDelegate(typeof(Func<T, TResult>));
        }

        /// <summary>
        /// Emit and compile delegate using Accessor via DynamicMethod
        /// </summary>
        /// <typeparam name="T1">Either object to call method from or method parameter (depends weather method is static)</typeparam>
        /// <typeparam name="TResult">The result of calee</typeparam>
        /// <param name="method">MethodInfo to compile accessor to</param>
        /// <returns>Delegate to invoke method</returns>
#pragma warning disable 1712
        public static Func<T1, T2, TResult> CompileAccessor<T1, T2, TResult>(this MethodInfo method) {
#pragma warning restore 1712
            var dynamic = EmitDynamic<TResult>(method, typeof(T1), typeof(T2));
            return (Func<T1, T2, TResult>)dynamic.CreateDelegate(typeof(Func<T1, T2, TResult>));
        }

        /// <summary>
        /// Emit and compile delegate using Accessor via DynamicMethod
        /// </summary>
        /// <typeparam name="T1">Either object to call method from or method parameter (depends weather method is static)</typeparam>
        /// <typeparam name="TResult">The result of calee</typeparam>
        /// <param name="method">MethodInfo to compile accessor to</param>
        /// <returns>Delegate to invoke method</returns>
#pragma warning disable 1712
        public static Func<T1, T2, T3, TResult> CompileAccessor<T1, T2, T3, TResult>(this MethodInfo method) {
#pragma warning restore 1712
            var dynamic = EmitDynamic<TResult>(method, typeof(T1), typeof(T2), typeof(T3));
            return (Func<T1, T2, T3, TResult>)dynamic.CreateDelegate(typeof(Func<T1, T2, T3, TResult>));
        }

        /// <summary>
        /// Emit and compile delegate using Accessor via DynamicMethod
        /// </summary>
        /// <typeparam name="T1">Either object to call method from or method parameter (depends weather method is static)</typeparam>
        /// <typeparam name="TResult">The result of calee</typeparam>
        /// <param name="method">MethodInfo to compile accessor to</param>
        /// <returns>Delegate to invoke method</returns>
#pragma warning disable 1712
        public static Func<T1, T2, T3, T4, TResult> CompileAccessor<T1, T2, T3, T4, TResult>(this MethodInfo method) {
#pragma warning restore 1712
            var dynamic = EmitDynamic<TResult>(method, typeof(T1), typeof(T2), typeof(T3), typeof(T4));
            return (Func<T1, T2, T3, T4, TResult>)dynamic.CreateDelegate(typeof(Func<T1, T2, T3, T4, TResult>));
        }

        /// <summary>
        /// Emit and compile delegate using Accessor via DynamicMethod
        /// </summary>
        /// <typeparam name="T1">Either object to call method from or method parameter (depends weather method is static)</typeparam>
        /// <typeparam name="TResult">The result of calee</typeparam>
        /// <param name="method">MethodInfo to compile accessor to</param>
        /// <returns>Delegate to invoke method</returns>
#pragma warning disable 1712
        public static Func<T1, T2, T3, T4, T5, TResult> CompileAccessor<T1, T2, T3, T4, T5, TResult>(this MethodInfo method) {
#pragma warning restore 1712
            var dynamic = EmitDynamic<TResult>(method, typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));
            return (Func<T1, T2, T3, T4, T5, TResult>)dynamic.CreateDelegate(typeof(Func<T1, T2, T3, T4, T5, TResult>));
        }

        private static DynamicMethod EmitDynamic(Type returnType, MethodInfo method, params Type[] typeParameters) {
            if (method.IsStatic && method.GetParameters().Length != typeParameters.Length || !method.IsStatic && method.GetParameters().Length + 1 != typeParameters.Length)
                throw new ArgumentException("Method has different number of arguments");
            var dynamic = new DynamicMethod(method.Name, returnType, typeParameters,
                typeof(MethodInfoHelper), true);
            ILGenerator il = dynamic.GetILGenerator();
            il.EmitParameters(method, typeParameters);
            if (method.IsVirtual || method.IsAbstract) {
                il.Emit(OpCodes.Callvirt, method);
            }
            else {
                il.Emit(OpCodes.Call, method);
            }
            if (returnType != method.ReturnType)
            {
                il.Emit(OpCodes.Castclass, returnType);
            }
            il.Emit(OpCodes.Ret);
            return dynamic;
        }

        private static DynamicMethod EmitDynamic<TResult>(MethodInfo method, params Type[] typeParameters) {
            if (method.IsStatic && method.GetParameters().Length != typeParameters.Length || !method.IsStatic && method.GetParameters().Length + 1 != typeParameters.Length)
                throw new ArgumentException("Method has different number of arguments");
            var dynamic = new DynamicMethod(method.Name, typeof(TResult), typeParameters,
                typeof(MethodInfoHelper), true);
            ILGenerator il = dynamic.GetILGenerator();
            il.EmitParameters(method, typeParameters);
            if (method.IsVirtual || method.IsAbstract) {
                il.Emit(OpCodes.Callvirt, method);
            }
            else {
                il.Emit(OpCodes.Call, method);
            }
            if (method.ReturnType != typeof (TResult))
            {
                il.Emit(OpCodes.Castclass, typeof(TResult));
            }
            il.Emit(OpCodes.Ret);
            return dynamic;
        }

        private static void EmitCastIfNeeded(this ILGenerator il, Type one, Type toOther)
        {
            if (one == toOther)
                return;
            if (one.GetTypeInfo().IsValueType || toOther.GetTypeInfo().IsValueType)
            {
                if (one == typeof (object))
                {
                    il.Emit(OpCodes.Unbox_Any, toOther);
                }
                if (toOther == typeof (object))
                {
                    il.Emit(OpCodes.Box, toOther);
                }
                throw new InvalidOperationException("The ValueType parameters cast isn't allowed with this compiler");
            }
            il.Emit(OpCodes.Castclass, toOther);
        }

        private static void EmitParameters(this ILGenerator il, MethodInfo method, Type[] parameterTypes) {
            if (parameterTypes == null) throw new ArgumentNullException(nameof(parameterTypes));
            if (parameterTypes.Length == 0)
                return;
            int seed = method.IsStatic ? 0 : 1;
            var methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
            if (parameterTypes.Length > 0)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCastIfNeeded(parameterTypes[0], seed == 0 ? methodParameterTypes[0] : method.DeclaringType);
            }
            if (parameterTypes.Length > 1 + seed)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.EmitCastIfNeeded(parameterTypes[1], methodParameterTypes[1 - seed]);
            }
            if (parameterTypes.Length > 2)
            {
                il.Emit(OpCodes.Ldarg_2);
                il.EmitCastIfNeeded(parameterTypes[2], methodParameterTypes[2 - seed]);
            }
            if (parameterTypes.Length > 3)
            {
                il.Emit(OpCodes.Ldarg_3);
                il.EmitCastIfNeeded(parameterTypes[3], methodParameterTypes[3 - seed]);
            }
            if (parameterTypes.Length > 4) {
                for (int i = 4; i < parameterTypes.Length; i++) {
                    il.Emit(OpCodes.Ldarg, i);
                    il.EmitCastIfNeeded(parameterTypes[i], methodParameterTypes[i - seed]);
                }
            }
        }

    }
}