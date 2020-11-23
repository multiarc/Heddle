using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Templates.Native;

namespace Templates.Helpers
{
    /// <summary>
    /// Extends AttributeSet to perform more helper methods for Type reflection
    /// </summary>
    internal class ReflectionHelper
    {
        private static readonly Regex GenericExpression = new Regex
            (@"^(?<main_type>[_a-zA-Z@][a-zA-Z0-9\.]*)<(?<generic_parameters>[_a-zA-Z@][a-zA-Z0-9\.\+\[\]]*)>$",
                RegexOptions.Compiled | RegexOptions.Singleline);
        
        private static readonly Regex TupleExpression = new Regex
        (@"^\((?<tuple_types>(?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex ArrayExpression = new Regex
        (@"^(?<main_type>[_a-zA-Z@][a-zA-Z0-9\.]*)(\[\])+$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex WhitespaceChars = new Regex(@"\s+", RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Type _innerType;

        private static readonly Dictionary<string, Type> CSharpTypes;

        private static Dictionary<string, List<Type>> _shortNames;

        private static Dictionary<string, List<Type>> _fullNames;

        static ReflectionHelper()
        {
            CSharpTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                {"bool", typeof(bool)},
                {"byte", typeof(byte)},
                {"sbyte", typeof(sbyte)},
                {"char", typeof(char)},
                {"decimal", typeof(decimal)},
                {"double", typeof(double)},
                {"float", typeof(float)},
                {"int", typeof(int)},
                {"uint", typeof(uint)},
                {"long", typeof(long)},
                {"ulong", typeof(ulong)},
                {"object", typeof(object)},
                {"short", typeof(short)},
                {"ushort", typeof(ushort)},
                {"string", typeof(string)},
                {"dynamic", typeof(object)}
            };

            Reconfigure();
        }

        public static void Reconfigure()
        {
            var assemblies = AssemblyHelper.GetAssemblies();
            lock (assemblies)
            {
                _shortNames = new Dictionary<string, List<Type>>();
                _fullNames = new Dictionary<string, List<Type>>();

                foreach (var type in assemblies.SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Enumerable.Empty<Type>();
                    }
                }))
                {
                    string shortName;
                    if (type.IsNested)
                    {
                        StringBuilder shortNameBuilder = new StringBuilder();
                        shortNameBuilder.Append(type.Name);
                        var parent = type.DeclaringType;
                        while (parent != null)
                        {
                            shortNameBuilder.Insert(0, parent.Name + "+");
                            parent = parent.IsNested ? parent.DeclaringType : null;
                        }
                        shortName = shortNameBuilder.ToString();
                    }
                    else
                    {
                        shortName = type.Name;
                    }
                    _shortNames.AddOrUpdate(shortName, () => new List<Type> {type}, l => l.Add(type));
                    _fullNames.AddOrUpdate(type.Namespace + "." + shortName, () => new List<Type> {type}, l => l.Add(type));
                }
            }
        }

        public ReflectionHelper(Type innerType)
        {
            if (innerType == null)
                throw new ArgumentNullException(nameof(innerType));

            _innerType = innerType;
        }

        public ReflectionHelper(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _innerType = value.GetType();
        }

        public Type InnerType => _innerType;

        public bool IsInterface => _innerType.GetTypeInfo().IsInterface;

        public bool IsObject => _innerType == typeof(object);

        public bool IsClass => _innerType.GetTypeInfo().IsClass;

        public bool IsImplement(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _innerType.GetInterfaces().Any(i => i == type);
        }

        public bool IsType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return _innerType.IsType(type);
        }

        public bool IsType(object value)
        {
            if (value == null)
                return false;
            return IsType(value.GetType());
        }

        private static Type ResolveCsharpType(string typeName)
        {
            if (CSharpTypes.TryGetValue(typeName, out var result))
            {
                return result;
            }
            return null;
        }

        private static Type ResolveSimpleType(string typeName, ICollection<string> imports)
        {
            if (typeName.Contains(","))
            {
                var result = Type.GetType(typeName, false);
                if (result == null)
                {
                    throw new InvalidOperationException($"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)})");
                }
                return result;
            }
            if (typeName.Contains("."))
            {
                if (_fullNames.TryGetValue(typeName, out var types))
                {
                    if (types.Count == 1)
                    {
                        return types[0];
                    }
                    foreach (var import in imports)
                    {
                        var fullName = import + "." + typeName;
                        if (_fullNames.TryGetValue(fullName, out types))
                        {
                            if (types.Count == 1)
                            {
                                return types[0];
                            }
                            throw new InvalidOperationException(
                                $"Couldn't resolve type <{fullName}> ({string.Join(", ", imports)}), the type name is ambigous");
                        }
                    }
                    throw new InvalidOperationException(
                        $"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)}), the type name is ambigous");
                }
                foreach (var import in imports)
                {
                    var fullName = import + "." + typeName;
                    if (_fullNames.TryGetValue(fullName, out types))
                    {
                        if (types.Count == 1)
                        {
                            return types[0];
                        }
                        throw new InvalidOperationException(
                            $"Couldn't resolve type <{fullName}> ({string.Join(", ", imports)}), the type name is ambigous");
                    }
                }
                throw new InvalidOperationException($"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)})");
            }
            else
            {
                Type result = ResolveCsharpType(typeName);
                if (result != null)
                    return result;
                if (_shortNames.TryGetValue(typeName, out var types))
                {
                    if (types.Count == 1)
                    {
                        return types[0];
                    }
                    result = types.FirstOrDefault(t => imports.Contains(t.Namespace));
                    if (result == null)
                    {
                        throw new InvalidOperationException($"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)})");
                    }
                    return result;
                }
                throw new InvalidOperationException($"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)})");
            }
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

        public static Type ResolveType(string typeName, params string[] imports)
        {
            return ResolveType(typeName, (ICollection<string>) imports);
        }

        public static Type ResolveType(string typeName, ICollection<string> imports)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException();

            if (typeName.StartsWith("("))
            {
                var match = TupleExpression.Match(typeName);
                if (match.Success)
                {
                    typeName = $"System.ValueTuple<{match.Groups["tuple_types"]}>";
                    return ResolveGenericType(typeName, imports);
                }
            }

            if (typeName.EndsWith("]"))
            {
                return ResolveArrayType(typeName, imports);
            }

            if (typeName.Contains("<"))
            {
                return ResolveGenericType(typeName, imports);
            }

            return ResolveSimpleType(typeName, imports ?? new string[0]);
        }

        private static Type ResolveGenericType(string typeName, ICollection<string> imports)
        {
            var match = GenericExpression.Match(typeName);
            if (match.Success)
            {
                var importsArray = imports ?? new string[0];
                var genericParameters = match.Groups["generic_parameters"].Value.Split(',');
                Type modelType = ResolveSimpleType(match.Groups["main_type"].Value + "`" + genericParameters.Length,
                    importsArray);
                modelType = modelType.MakeGenericType(genericParameters
                    .Select(parameter => ResolveType(parameter, importsArray)).ToArray());
                {
                    return modelType;
                }
            }

            return ResolveSimpleType(typeName, imports ?? new string[0]);
        }

        private static Type ResolveArrayType(string typeName, ICollection<string> imports)
        {
            var match = ArrayExpression.Match(typeName);
            if (match.Success)
            {
                var importsArray = imports ?? new string[0];
                var modelType = ResolveType(match.Groups["main_type"].Value, importsArray);
                return modelType.MakeArrayType();
            }
            
            return ResolveSimpleType(typeName, imports ?? new string[0]); 
        }
    }
}