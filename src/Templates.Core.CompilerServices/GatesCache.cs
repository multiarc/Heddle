using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Templates.Core.CompilerServices {
    internal static class GatesCache {
        private static readonly Dictionary<RuntimeMethodHandle, PropertyGateDelegate> Cache =
            new Dictionary<RuntimeMethodHandle, PropertyGateDelegate>();

        public static PropertyGateDelegate GetPropertyGate (PropertyInfo property)
        {
            if (property == null)
                return null;
            RuntimeMethodHandle methodHandle = property.GetGetMethod().MethodHandle;
            PropertyGateDelegate result;
            if (Cache.TryGetValue(methodHandle, out result))
                return result;
            result = CreateGenericGate(property);
            Cache.Add(methodHandle, result);
            return result;
        }

        private static PropertyGateDelegate CreateGenericGate (PropertyInfo property)
        {
            MethodInfo getMethod = property.GetGetMethod();
            var dynamic = new DynamicMethod
                (getMethod.Name, typeof (object), new[]
                {
                    typeof (object)
                }, typeof (EntityCompiler), true);
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
            if (propertyType.IsValueType)
                il.Emit(OpCodes.Box, propertyType);
            il.Emit(OpCodes.Ret);
            return (PropertyGateDelegate) dynamic.CreateDelegate(typeof (PropertyGateDelegate));
        }
    }
}