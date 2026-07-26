using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Heddle.Language.Binding;
using Heddle.Native;

namespace Heddle.Helpers
{
    /// <summary>
    /// Extends AttributeSet to perform more helper methods for Type reflection
    /// </summary>
    internal class ReflectionHelper
    {
        private static readonly Regex WhitespaceChars = new Regex(@"\s+", RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Type _innerType;

        private static Dictionary<string, List<Type>> _shortNames;

        private static Dictionary<string, List<Type>> _fullNames;

        static ReflectionHelper()
        {
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
                        // (Phase 3 note: that was true of the full-name arm only until the short-name arm was
                        // fixed to match — see ResolveSimpleType. It is now true of both, as written.)
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

        // The alias table lives in CSharpTypeNames (phase 6 D7) — one data source for the parse direction here,
        // the display direction in signature/hover text, and the build tier's symbol-side adapter.
        private static Type ResolveCsharpType(string typeName) =>
            CSharpTypeNames.TryGetType(typeName, out var result) ? result : null;

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

                    // Phase 3 (Q3.5 / OQ5 escape clause): a short-name tie is disambiguated by the imports, and a
                    // tie the imports do NOT settle is the SAME "ambigous" error the dotted/full-name arms above
                    // already raise. It used to be `types.FirstOrDefault(t => imports.Contains(t.Namespace))` — a
                    // first-match pick over an assembly-scan-ordered list, so with two imported namespaces both
                    // carrying the name the resolved type depended on assembly load order. That is not a rule the
                    // build tier can match by construction, and reproducing it would bake load order into build
                    // output; it also contradicted this file's own comment at RegisterType, which states that a
                    // dotted-alias collision "surfaces as the existing ambiguous error rather than a silent pick".
                    // Both tiers are fixed together and stay matched (the generator raises HED7023 for the same
                    // input). Behaviour is otherwise unchanged: one match still resolves, no match still fails.
                    var matches = types.Where(t => imports.Contains(t.Namespace)).ToList();
                    if (matches.Count == 1)
                    {
                        return matches[0];
                    }
                    if (matches.Count > 1)
                    {
                        throw new InvalidOperationException(
                            $"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)}), the type name is ambigous");
                    }
                    throw new InvalidOperationException($"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)})");
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

        /// <summary>
        /// Resolves a template-spelled type name. The <b>grammar</b> — the tuple/array/generic/simple dispatch, the
        /// dotted-chain-with-per-segment-arity rewrite, top-level argument splitting and angle-bracket matching — is
        /// the shared <see cref="TypeSpelling"/> parser (Q8.3); this method supplies the reflection type universe
        /// through <see cref="ReflectionTypeLookup"/> and maps the parser's fault back onto the
        /// <see cref="InvalidOperationException"/> messages callers already catch.
        /// <para>Phase 3 recorded the parser as "split out of <c>ReflectionHelper</c>". It was not: the shared file
        /// was a re-implementation and these methods stayed live, so the generator and the runtime were parsing the
        /// same grammar twice again. This is the actual split.</para>
        /// </summary>
        public static Type ResolveType(string typeName, ICollection<string> imports)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException();

            imports = imports ?? new string[0];

            var lookup = new ReflectionTypeLookup(imports);
            if (TypeSpelling.TryResolve(typeName, lookup, out var resolved, out var fault))
                return resolved;

            // The lookup's own resolution failures already carry the precise message (ambiguity, unresolved name);
            // rethrowing it preserves them verbatim. A fault raised by the grammar itself has no such exception.
            if (lookup.Failure != null)
                throw lookup.Failure;

            throw ResolveError(typeName, imports, FaultReason(fault));
        }

        private static InvalidOperationException ResolveError(string typeName, ICollection<string> imports,
            string reason)
        {
            return new InvalidOperationException(
                $"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)}), {reason}");
        }

        private static string FaultReason(TypeSpellingFault fault)
        {
            switch (fault)
            {
                case TypeSpellingFault.ArityMismatch:
                    return "the generic argument count does not match the type";
                case TypeSpellingFault.Ambiguous:
                    return "the type name is ambigous";
                case TypeSpellingFault.Malformed:
                    return "unbalanced angle brackets or an empty generic argument";
                default:
                    return "no such type";
            }
        }

        /// <summary>
        /// The reflection type universe as the shared parser sees it. Simple-name resolution stays
        /// <see cref="ResolveSimpleType"/> — the assembly-scan index, the <c>.</c>→<c>+</c> retry ladder and the
        /// ambiguity rule are the reflection tier's own and are not grammar — but its exception is captured rather
        /// than thrown through the parser, so the parser stays exception-free for both tiers.
        /// </summary>
        private sealed class ReflectionTypeLookup : ITypeLookup<Type>
        {
            private readonly ICollection<string> _imports;

            internal ReflectionTypeLookup(ICollection<string> imports) => _imports = imports;

            /// <summary>The first resolution failure, so <see cref="ResolveType"/> can rethrow its exact message.</summary>
            internal InvalidOperationException Failure { get; private set; }

            public bool TryResolveSimple(string name, int backtickArity, out Type type,
                out TypeSpellingFault fault)
            {
                fault = TypeSpellingFault.None;
                try
                {
                    type = ResolveSimpleType(name, _imports);
                    return type != null;
                }
                catch (InvalidOperationException e)
                {
                    Failure = Failure ?? e;
                    type = null;
                    fault = e.Message.IndexOf("ambigous", StringComparison.Ordinal) >= 0
                        ? TypeSpellingFault.Ambiguous
                        : TypeSpellingFault.Unresolved;
                    return false;
                }
            }

            public Type MakeArray(Type elementType) => elementType.MakeArrayType();

            public bool TryMakeGeneric(Type definition, IReadOnlyList<Type> arguments, out Type constructed)
            {
                constructed = null;
                if (definition.GetGenericArguments().Length != arguments.Count)
                    return false;

                var array = new Type[arguments.Count];
                for (var i = 0; i < arguments.Count; i++)
                    array[i] = arguments[i];
                constructed = definition.MakeGenericType(array);
                return true;
            }

            public bool TryGetValueTupleDefinition(int arity, out Type definition)
            {
                definition = Type.GetType("System.ValueTuple`" + arity, false);
                return definition != null;
            }
        }
    }
}