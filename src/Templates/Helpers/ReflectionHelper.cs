#if DNXCORE50
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Loader;
#endif
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
#if DNXCORE50
using System.Runtime.Loader;
#endif
using System.Text.RegularExpressions;
using System.Threading;
using Templates.Data;
using Templates.Exceptions;
using Templates.Native;

namespace Templates.Helpers {
    /// <summary>
    /// Extends AttributeSet to perform more helper methods for Type reflection
    /// </summary>
    public class ReflectionHelper {
        private static readonly Regex GenericExpression = new Regex
            (@"^(?<main_type>(@?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]+?\.?)+)<(?<generic_parameters>((@?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]+?((\.)|(\+))?)+(,)?)+)>$",
             RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Type _innerType;

        private static readonly Dictionary<string, Type> CSharpTypes;

        static ReflectionHelper()
        {
            CSharpTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                {"bool", typeof (bool)},
                {"byte", typeof (byte)},
                {"sbyte", typeof (sbyte)},
                {"char", typeof (char)},
                {"decimal", typeof (decimal)},
                {"double", typeof (double)},
                {"float", typeof (float)},
                {"int", typeof (int)},
                {"uint", typeof (uint)},
                {"long", typeof (long)},
                {"ulong", typeof (ulong)},
                {"object", typeof (object)},
                {"short", typeof (short)},
                {"ushort", typeof (ushort)},
                {"string", typeof (string)},
                {"dynamic", typeof (object)}
            };
        }

        public ReflectionHelper (Type innerType)
        {
            if (innerType == null)
                throw new ArgumentNullException(nameof(innerType));

            _innerType = innerType;
        }

        public ReflectionHelper (object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _innerType = value.GetType();
        }

        public Type InnerType => _innerType;

        public bool IsInterface => _innerType.GetTypeInfo().IsInterface;

        public bool IsObject => _innerType == typeof (object);

        public bool IsClass => _innerType.GetTypeInfo().IsClass;

        public bool IsImplement (Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _innerType.GetInterfaces().Any(i => i == type);
        }

        public bool IsType (Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _innerType.IsType(type);
        }

        public bool IsType (object value)
        {
            if (value == null)
                return false;
            return IsType(value.GetType());
        }

        private static Type ResolveCsharpType(string typeName)
        {
            Type result;
            if (CSharpTypes.TryGetValue(typeName, out result))
            {
                return result;
            }
            return null;
        }

        private static Type ResolveSimpleType (string typeName, IEnumerable<string> imports)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException();
            Type modelType = ResolveCsharpType(typeName);
            if (modelType != null)
                return modelType;
            modelType = Type.GetType(typeName, false);
            if (modelType == null) {
#if !DNXCORE50
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
#if DNXCORE50
                Assembly[] assemblies = NativeHelper.GetAssemblies();
#endif
                foreach (Assembly assembly in assemblies) {
                    modelType = assembly.GetType(typeName);
                    if (modelType != null)
                        return modelType;
                }
                string[] importsArray = imports.Select(namespc => namespc + "." + typeName).ToArray();
                foreach (Assembly assembly in assemblies) {
                    foreach (string import in importsArray) {
                        modelType = assembly.GetType(import);
                        if (modelType != null)
                            return modelType;
                    }
                }
            } else
                return modelType;
            throw new TemplateCompileException(string.Format(CultureInfo.InvariantCulture, "Couldn't resolve type [{0}]", typeName).ToError());
        }

        /*public static PropertyInfo ResolveProperty(string propertyName, Type sourceType = null)
        {
            string[] accessList = null;
            if (propertyName.Contains("."))
            {
                accessList = propertyName.Split('.');
            }
            if (sourceType != null)
            {
                if (accessList == null)
                {
                    return sourceType.GetProperty(propertyName);
                }
                PropertyInfo result = null;
                foreach (var accessor in accessList)
                {
                    if (result == null)
                    {
                        result = sourceType.GetProperty(accessor);
                        if (result == null)
                            return ResolveProperty(propertyName);
                    }
                    else
                    {
                        result = sourceType.GetProperty(accessor);
                    }
                    if (result == null || !result.CanRead || result.IsHaveAttribute<HiddenAttribute>())
                        return null;
                    sourceType = result.PropertyType;
                }
            }
            else
            {
                
            }
        }*/

        public static Type ResolveType (string typeName, params string[] imports)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException();

            Match match = GenericExpression.Match(typeName);
            if (match.Success)
            {
                string[] importsArray = imports ?? new string[0];
                string[] genericParameters = match.Groups["generic_parameters"].Value.Split(',');
                Type modelType = ResolveSimpleType(match.Groups["main_type"].Value + "`" + genericParameters.Length, importsArray);
                modelType = modelType.MakeGenericType(genericParameters.Select(parameter => ResolveType(parameter, importsArray)).ToArray());
                return modelType;
            }
            return ResolveSimpleType(typeName, imports ?? new string[0]);
        }
    }
}