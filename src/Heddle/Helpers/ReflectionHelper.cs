using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Heddle.Native;

namespace Heddle.Helpers
{
    /// <summary>
    /// Extends AttributeSet to perform more helper methods for Type reflection
    /// </summary>
    internal class ReflectionHelper
    {
        private static readonly Regex TupleExpression = new Regex
        (@"^\((?<tuple_types>(?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!)))\)$",
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
                        // Canonical stored keys are CLR metadata names ('+' between declaring types,
                        // e.g. Outer+Nested). A dotted alias (Outer.Nested) is registered additionally,
                        // because the template lexer cannot accept '+'; both keys point at the same Type.
                        // If a real namespaced type shares the dotted spelling, the alias lands in the
                        // same list and surfaces as the existing "ambiguous" error rather than a silent pick.
                        StringBuilder shortNameBuilder = new StringBuilder();
                        shortNameBuilder.Append(type.Name);
                        var parent = type.DeclaringType;
                        while (parent != null)
                        {
                            shortNameBuilder.Insert(0, parent.Name + "+");
                            parent = parent.IsNested ? parent.DeclaringType : null;
                        }
                        shortName = shortNameBuilder.ToString();

                        var dottedAlias = shortName.Replace('+', '.');
                        _shortNames.AddOrUpdate(dottedAlias, () => new List<Type> {type}, l => l.Add(type));
                        _fullNames.AddOrUpdate(type.Namespace + "." + dottedAlias, () => new List<Type> {type}, l => l.Add(type));
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
                    // Templates spell nested types with dots (the lexer rejects '+'), but CLR metadata
                    // names use '+'. Retry with one more trailing dot converted to '+' per attempt
                    // (A.B.C.D → A.B.C+D → A.B+C+D → ...), stopping at the first hit.
                    var candidate = typeName.ToCharArray();
                    for (var i = typeName.IndexOf(',') - 1; result == null && i >= 0; i--)
                    {
                        if (candidate[i] != '.')
                            continue;
                        candidate[i] = '+';
                        result = Type.GetType(new string(candidate), false);
                    }
                }
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

            imports = imports ?? new string[0];

            if (typeName.StartsWith("("))
            {
                var match = TupleExpression.Match(typeName);
                if (match.Success)
                {
                    return ResolveGenericType($"System.ValueTuple<{match.Groups["tuple_types"]}>", imports);
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

            return ResolveSimpleType(typeName, imports);
        }

        /// <summary>
        /// Resolves a C#-style generic name — a dotted chain where any segment may carry a type-argument
        /// list, e.g. <c>Ns.Outer&lt;int&gt;.Inner&lt;string&gt;</c>: the definition is looked up by its
        /// backtick spelling (<c>Ns.Outer`1.Inner`1</c>) and closed over all arguments left to right
        /// (nested types inherit their outers' generic parameters, so the counts add up).
        /// </summary>
        private static Type ResolveGenericType(string typeName, ICollection<string> imports)
        {
            var argumentNames = ExtractGenericArguments(typeName, imports, out var definitionName);
            var definition = ResolveSimpleType(definitionName, imports);

            if (definition.GetGenericArguments().Length != argumentNames.Count)
                throw ResolveError(typeName, imports,
                    $"the type takes {definition.GetGenericArguments().Length} generic argument(s), not {argumentNames.Count}");

            return definition.MakeGenericType(argumentNames.Select(name => ResolveType(name, imports)).ToArray());
        }

        /// <summary>
        /// Rewrites <c>Ns.Outer&lt;int&gt;.Inner&lt;string&gt;</c> into the backtick definition name
        /// <c>Ns.Outer`1.Inner`1</c> and returns the extracted argument names, left to right.
        /// </summary>
        private static List<string> ExtractGenericArguments(string typeName, ICollection<string> imports,
            out string definitionName)
        {
            var definition = new StringBuilder(typeName.Length);
            var arguments = new List<string>();

            for (var i = 0; i < typeName.Length; i++)
            {
                if (typeName[i] != '<')
                {
                    definition.Append(typeName[i]);
                    continue;
                }

                if (!TryFindMatchingAngleBracket(typeName, i, out var close))
                    throw ResolveError(typeName, imports, "unbalanced angle brackets");

                var list = SplitTopLevelArguments(typeName.Substring(i + 1, close - i - 1));
                if (list.Any(string.IsNullOrEmpty))
                    throw ResolveError(typeName, imports, "empty generic argument");

                definition.Append('`').Append(list.Count);
                arguments.AddRange(list);
                i = close;
            }

            definitionName = definition.ToString();
            return arguments;
        }

        /// <summary>Finds the <c>&gt;</c> matching the <c>&lt;</c> at <paramref name="open"/>.</summary>
        private static bool TryFindMatchingAngleBracket(string text, int open, out int close)
        {
            var depth = 0;
            for (close = open; close < text.Length; close++)
            {
                if (text[close] == '<') depth++;
                else if (text[close] == '>' && --depth == 0) return true;
            }
            close = -1;
            return false;
        }

        private static InvalidOperationException ResolveError(string typeName, ICollection<string> imports, string reason)
        {
            return new InvalidOperationException(
                $"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)}), {reason}");
        }

        /// <summary>
        /// Splits an argument list on commas that sit outside any <c>&lt;&gt;</c>, <c>()</c> or <c>[]</c>
        /// pair; parts are trimmed.
        /// </summary>
        private static List<string> SplitTopLevelArguments(string argumentList)
        {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i < argumentList.Length; i++)
            {
                var c = argumentList[i];
                if (c == '<' || c == '(' || c == '[') depth++;
                else if (c == '>' || c == ')' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    parts.Add(argumentList.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            parts.Add(argumentList.Substring(start).Trim());
            return parts;
        }

        private static Type ResolveArrayType(string typeName, ICollection<string> imports)
        {
            if (!typeName.EndsWith("[]", StringComparison.Ordinal))
            {
                // A trailing ']' that is not an "[]" suffix — not an array spelling we support.
                return ResolveSimpleType(typeName, imports);
            }

            var elementName = typeName.Substring(0, typeName.Length - 2).TrimEnd();
            return ResolveType(elementName, imports).MakeArrayType();
        }
    }
}