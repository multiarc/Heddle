using System;
using System.Linq;
using System.Reflection;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// One registered function signature. Bound either to a static <see cref="MethodInfo"/> (emitted as a
    /// direct static call) or to a <see cref="Delegate"/> (emitted as an invoke over a constant delegate).
    /// Immutable.
    /// </summary>
    internal sealed class FunctionEntry
    {
        private FunctionEntry(string name, MethodInfo method, Delegate target, Type[] parameterTypes,
            Type returnType, bool hasParamsArray)
        {
            Name = name;
            Method = method;
            Target = target;
            ParameterTypes = parameterTypes;
            ReturnType = returnType;
            HasParamsArray = hasParamsArray;
        }

        public string Name { get; }

        /// <summary>Non-null for a <see cref="MethodInfo"/> registration (incl. built-ins).</summary>
        public MethodInfo Method { get; }

        /// <summary>Non-null for a delegate registration.</summary>
        public Delegate Target { get; }

        public Type[] ParameterTypes { get; }

        public Type ReturnType { get; }

        /// <summary>True when the last parameter is a <c>params</c> array.</summary>
        public bool HasParamsArray { get; }

        /// <summary>Element type of the trailing <c>params</c> array (valid only when <see cref="HasParamsArray"/>).</summary>
        public Type ParamsElementType =>
            HasParamsArray ? ParameterTypes[ParameterTypes.Length - 1].GetElementType() : null;

        public static FunctionEntry FromMethod(string name, MethodInfo method)
        {
            var parameters = method.GetParameters();
            bool hasParams = parameters.Length > 0 &&
                             parameters[parameters.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);
            return new FunctionEntry(name, method, null,
                parameters.Select(p => p.ParameterType).ToArray(), method.ReturnType, hasParams);
        }

        public static FunctionEntry FromDelegate(string name, Delegate target)
        {
            var invoke = target.GetType().GetMethod("Invoke");
            var parameters = invoke.GetParameters();
            bool hasParams = parameters.Length > 0 &&
                             parameters[parameters.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);
            return new FunctionEntry(name, null, target,
                parameters.Select(p => p.ParameterType).ToArray(), invoke.ReturnType, hasParams);
        }

        /// <summary>True when this entry has the same name and identical parameter types as another.</summary>
        public bool SameSignature(FunctionEntry other)
        {
            if (!string.Equals(Name, other.Name, StringComparison.Ordinal))
                return false;
            if (ParameterTypes.Length != other.ParameterTypes.Length)
                return false;
            for (int i = 0; i < ParameterTypes.Length; i++)
            {
                if (ParameterTypes[i] != other.ParameterTypes[i])
                    return false;
            }

            return true;
        }

        /// <summary>Human-readable signature for diagnostics, e.g. <c>min(int, int)</c>.</summary>
        public string ToSignatureString()
        {
            return $"{Name}({string.Join(", ", ParameterTypes.Select(FriendlyName))})";
        }

        private static string FriendlyName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(double)) return "double";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(string)) return "string";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(object)) return "object";
            if (type == typeof(object[])) return "object[]";
            return type.Name;
        }
    }
}
