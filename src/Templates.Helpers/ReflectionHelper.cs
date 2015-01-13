using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Templates.Exceptions;

namespace Templates.Helpers {
    /// <summary>
    /// Extends AttributeSet to perform more helper methods for Type reflection
    /// </summary>
    public class ReflectionHelper {
        private static readonly Regex GenericExpression = new Regex
            (@"^(?<main_type>(@?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]+?((\.)|(\+))?)+)<(?<generic_parameters>((@?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]+?((\.)|(\+))?)+(,)?)+)>$",
             RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Type _innerType;

        public ReflectionHelper (Type innerType)
        {
            if (innerType == null)
                throw new ArgumentNullException("innerType");

            _innerType = innerType;
        }

        public ReflectionHelper (object value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            _innerType = value.GetType();
        }

        public Type InnerType
        {
            get { return _innerType; }
        }

        public bool IsInterface
        {
            get { return _innerType.IsInterface; }
        }

        public bool IsObject
        {
            get { return _innerType == typeof (object); }
        }

        public bool IsClass
        {
            get { return _innerType.IsClass; }
        }

        public bool IsImplement (Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            return _innerType.GetInterfaces().Any(i => i == type);
        }

        public bool IsType (Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            return _innerType.IsType(type);
        }

        public bool IsType (object value)
        {
            if (value == null)
                return false;
            return IsType(value.GetType());
        }

        public object Invoke (object o, string methodName, params object[] parameters)
        {
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentException();
            return _innerType.InvokeMember(methodName, BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public, null, o, parameters);
        }

        public T Invoke<T> (object o, string methodName, params object[] parameters)
        {
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentException();
            return
                (T) _innerType.InvokeMember(methodName, BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public, null, o, parameters);
        }

        private static Type ResolveSimpleType (string typeName, IEnumerable<string> imports)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException();

            Type modelType = Type.GetType(typeName, false);
            if (modelType == null) {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies) {
                    modelType = assembly.GetType(typeName, false);
                    if (modelType != null)
                        return modelType;
                }
                string[] importsArray = imports.Select(namespc => namespc + "." + typeName).ToArray();
                foreach (Assembly assembly in assemblies) {
                    foreach (string import in importsArray) {
                        modelType = assembly.GetType(import, false);
                        if (modelType != null)
                            return modelType;
                    }
                }
            } else
                return modelType;
            throw new TemplateCompileException(string.Format(CultureInfo.InvariantCulture, "Couldn't resolve type [{0}]", typeName));
        }

        public static Type ResolveType (string typeName, IEnumerable<string> imports)
        {
            if (imports == null)
                throw new ArgumentNullException("imports");

            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException();

            Match match = GenericExpression.Match(typeName);
            if (match.Success) {
                string[] importsArray = imports.ToArray();
                string[] genericParameters = match.Groups["generic_parameters"].Value.Split(',');
                Type modelType = ResolveSimpleType(match.Groups["main_type"].Value + "`" + genericParameters.Length, importsArray);
                modelType = modelType.MakeGenericType(genericParameters.Select(parameter => ResolveSimpleType(parameter, importsArray)).ToArray());
                return modelType;
            }
            return ResolveSimpleType(typeName, imports);
        }
    }
}