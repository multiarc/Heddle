using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Templates.Data;

namespace Templates.Runtime {
    internal static class GatesCache {

        private static readonly PropertyInfoComparer Comparer = new PropertyInfoComparer();

        private static readonly MethodCacheKeyEqualityComparer MethodCacheKeyComparer =
            new MethodCacheKeyEqualityComparer();

        private static readonly ConcurrentDictionary<PropertyInfo, DynamicMethodGateDelegate> GateCache =
            new ConcurrentDictionary<PropertyInfo, DynamicMethodGateDelegate>(Comparer);

        private static readonly ConcurrentDictionary<MethodCacheKey, CompiledMethodDelegate> MethodsCache =
            new ConcurrentDictionary<MethodCacheKey, CompiledMethodDelegate>(MethodCacheKeyComparer);

        public static DynamicMethodGateDelegate GetPropertyGate(PropertyInfo property)
        {
            if (property == null)
                return null;

            return GateCache.GetOrAdd(property, m => CreateGenericGate(property));
        }

        public static CompiledMethodDelegate GetCompiledDelegate(MethodInfo method, Type model, Type chained,
            Type rootType)
        {
            return MethodsCache.GetOrAdd(new MethodCacheKey(method, model, chained, rootType),
                m => CreateCompiledDelegate(m.Method, m.Model, m.Chained, m.RootType));
        }

        private static CompiledMethodDelegate CreateCompiledDelegate(MethodInfo method, Type model, Type chained, Type rootType)
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
            var getMethod = property.GetGetMethod(true);
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

        private struct MethodCacheKey : IEquatable<MethodCacheKey>
        {
            public readonly MethodInfo Method;
            public readonly Type Model;
            public readonly Type Chained;
            public readonly Type RootType;

            public MethodCacheKey(MethodInfo method, Type model, Type chained, Type rootType)
            {
                Method = method;
                Model = model;
                Chained = chained;
                RootType = rootType;
            }

            public bool Equals(MethodCacheKey other)
            {
                return Equals(Method, other.Method) && Equals(Model, other.Model) && Equals(Chained, other.Chained) &&
                       Equals(RootType, other.RootType);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;
                return obj is MethodCacheKey && Equals((MethodCacheKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Method != null ? Method.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Model != null ? Model.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Chained != null ? Chained.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (RootType != null ? RootType.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public static bool operator ==(MethodCacheKey left, MethodCacheKey right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(MethodCacheKey left, MethodCacheKey right)
            {
                return !left.Equals(right);
            }
        }

        private sealed class MethodCacheKeyEqualityComparer : IEqualityComparer<MethodCacheKey>
        {
            public bool Equals(MethodCacheKey x, MethodCacheKey y)
            {
                return Equals(x.Method, y.Method) && Equals(x.Model, y.Model) && Equals(x.Chained, y.Chained) &&
                       Equals(x.RootType, y.RootType);
            }

            public int GetHashCode(MethodCacheKey obj)
            {
                unchecked
                {
                    var hashCode = (obj.Method != null ? obj.Method.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (obj.Model != null ? obj.Model.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (obj.Chained != null ? obj.Chained.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (obj.RootType != null ? obj.RootType.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private sealed class PropertyInfoComparer : IEqualityComparer<PropertyInfo>
        {
            public bool Equals(PropertyInfo x, PropertyInfo y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if ((object)x == null || (object)y == null)
                {
                    return false;
                }

                return x.Equals(y);
            }
            
            public int GetHashCode(PropertyInfo obj)
            {
                return obj?.GetHashCode() ?? 0;
            }
        }
    }
}