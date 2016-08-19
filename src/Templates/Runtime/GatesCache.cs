using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Templates.Data;

namespace Templates.Runtime {
    internal static class GatesCache {
        private static readonly object LockObject = new object();

        private static Dictionary<MethodInfo, DynamicMethodGateDelegate> _cache =
            new Dictionary<MethodInfo, DynamicMethodGateDelegate>();

        public static DynamicMethodGateDelegate GetPropertyGate(PropertyInfo property)
        {
            if (property == null)
                return null;
            MethodInfo methodInfo = property.GetGetMethod(true);
            DynamicMethodGateDelegate result;
            if (_cache.TryGetValue(methodInfo, out result))
                return result;
            lock (LockObject)
            {
                if (_cache.TryGetValue(methodInfo, out result))
                    return result;

                result = CreateGenericGate(property);
                _cache = new Dictionary<MethodInfo, DynamicMethodGateDelegate>(_cache) {{methodInfo, result}};
            }
            return result;
        }

        public static CompiledMethodDelegate CreateCompiledDelegate(MethodInfo method, Type model, Type chained, Type rootType)
        {
            if (!method.IsStatic)
                throw new ArgumentException("Method should be static only");
            var dynamic = new DynamicMethod(method.Name, typeof (object), new[] {typeof (object), typeof (object), typeof(object)},
                typeof (CompileContext), false);
            ILGenerator il = dynamic.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            if (model.GetTypeInfo().IsValueType)
                il.Emit(OpCodes.Unbox_Any, model);
            else if (model != typeof(object))
                il.Emit(OpCodes.Castclass, model);

            il.Emit(OpCodes.Ldarg_1);
            if (chained.GetTypeInfo().IsValueType)
                il.Emit(OpCodes.Unbox_Any, chained);
            else if (chained != typeof(object))
                il.Emit(OpCodes.Castclass, chained);

            il.Emit(OpCodes.Ldarg_2);
            if (rootType.GetTypeInfo().IsValueType)
                il.Emit(OpCodes.Unbox_Any, rootType);
            else if (rootType != typeof(object))
                il.Emit(OpCodes.Castclass, rootType);

            il.Emit(OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return (CompiledMethodDelegate)dynamic.CreateDelegate(typeof(CompiledMethodDelegate));
        }

        private static DynamicMethodGateDelegate CreateGenericGate(PropertyInfo property)
        {
            MethodInfo getMethod = property.GetGetMethod(true);
            var dynamic = new DynamicMethod
                (getMethod.Name, typeof (object), new[]
                {
                    typeof (object)
                }, typeof (TtlCompiler), true);
            Type propertyType = property.PropertyType;
            ILGenerator il = dynamic.GetILGenerator();
            if (getMethod.IsVirtual || getMethod.IsAbstract) {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, getMethod);
            } else if (getMethod.IsStatic)
                il.Emit(OpCodes.Call, getMethod);
            else {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, getMethod);
            }
            if (propertyType.GetTypeInfo().IsValueType)
                il.Emit(OpCodes.Box, propertyType);
            il.Emit(OpCodes.Ret);
            return (DynamicMethodGateDelegate)dynamic.CreateDelegate(typeof(DynamicMethodGateDelegate));
        }
        

        #region Garbage

        //public static DynamicMethodGateDelegate GetMethodGate(MethodInfo method) {
        //    if (method == null)
        //        return null;
        //    RuntimeMethodHandle methodHandle = method.MethodHandle;
        //    DynamicMethodGateDelegate result;
        //    if (Cache.TryGetValue(methodHandle, out result))
        //        return result;
        //    result = CompileMethodReference(method);
        //    Cache.Add(methodHandle, result);
        //    return result;
        //}

        //private static void PushParameter(ILGenerator il, Type parameterType) {
        //    il.Emit(OpCodes.Ldarg_0);
        //    if (parameterType.IsValueType)
        //        il.Emit(OpCodes.Unbox_Any, parameterType);
        //    else if (parameterType != typeof(object))
        //        il.Emit(OpCodes.Castclass, parameterType);
        //}

        //private static DynamicMethodGateDelegate CompileMethodReference(MethodInfo method) {
        //    var dynamic = new DynamicMethod
        //        (method.Name, typeof(object), new[]
        //        {
        //            typeof (object), typeof (object)
        //        }, typeof(OutputChainCompiler), true);
        //    ILGenerator il = dynamic.GetILGenerator();
        //    ParameterInfo[] parameters = method.GetParameters();
        //    ParameterInfo firstParameter = null;
        //    ParameterInfo secondParameter = null;
        //    if (parameters.Any())
        //        firstParameter = parameters[0];
        //    if (parameters.Count() > 1)
        //        secondParameter = parameters[1];
        //    if (firstParameter != null) {
        //        PushParameter(il, firstParameter.ParameterType);
        //        if (secondParameter != null)
        //            PushParameter(il, secondParameter.ParameterType);
        //    }
        //    il.Emit(OpCodes.Call, method);
        //    if (method.ReturnType.IsValueType)
        //        il.Emit(OpCodes.Box, method.ReturnType);
        //    il.Emit(OpCodes.Ret);
        //    return (DynamicMethodGateDelegate)dynamic.CreateDelegate(typeof(DynamicMethodGateDelegate));
        //}

        #endregion
    }
}