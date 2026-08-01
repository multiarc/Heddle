using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Heddle.Language.Binding;
using Heddle.Native;

namespace Heddle.Helpers
{
    internal class ReflectionHelper
    {
        private static readonly Regex WhitespaceChars = new Regex(@"\s+", RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly Type _innerType;

        /// <summary>
        /// The two name maps, published as one immutable object. A reader must never see a half-built map: the old
        /// shape assigned each field a fresh empty dictionary and then filled it, while readers looked them up without
        /// the lock — so a concurrent `Register` made a valid template fail to resolve a type it had just resolved.
        /// Both maps live here so a reader cannot observe a new short-name map against an old full-name one either.
        /// </summary>
        private sealed class NameMaps
        {
            public NameMaps(Dictionary<string, List<Type>> shortNames, Dictionary<string, List<Type>> fullNames,
                int generation)
            {
                ShortNames = shortNames;
                FullNames = fullNames;
                Generation = generation;
            }

            public Dictionary<string, List<Type>> ShortNames { get; }

            public Dictionary<string, List<Type>> FullNames { get; }

            /// <summary>The assembly-set stamp these maps were built from; a newer stamp means they are stale.</summary>
            public int Generation { get; }
        }

        private static NameMaps _maps = new NameMaps(
            new Dictionary<string, List<Type>>(), new Dictionary<string, List<Type>>(), -1);

        static ReflectionHelper()
        {
            Reconfigure();
        }

        /// <summary>
        /// Rebuilds the name maps from the current assembly set and publishes them in one write, so a concurrent
        /// resolve sees either the whole old set or the whole new one.
        /// </summary>
        public static void Reconfigure()
        {
            var shortNames = new Dictionary<string, List<Type>>();
            var fullNames = new Dictionary<string, List<Type>>();
            var assemblies = AssemblyHelper.GetAssemblies();
            var generation = AssemblyHelper.Generation;
            lock (assemblies)
            {
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
                        // Store both CLR metadata form (Outer+Nested) and dotted alias (Outer.Nested)
                        // since the template lexer cannot accept '+'.
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
                        shortNames.AddOrUpdate(dottedAlias, () => new List<Type> {type}, l => l.Add(type));
                        fullNames.AddOrUpdate(type.Namespace + "." + dottedAlias, () => new List<Type> {type}, l => l.Add(type));
                    }
                    else
                    {
                        shortName = type.Name;
                    }
                    shortNames.AddOrUpdate(shortName, () => new List<Type> {type}, l => l.Add(type));
                    fullNames.AddOrUpdate(type.Namespace + "." + shortName, () => new List<Type> {type}, l => l.Add(type));
                }
            }

            Volatile.Write(ref _maps, new NameMaps(shortNames, fullNames, generation));
        }

        /// <summary>
        /// The name maps, rebuilt first if the assembly set has changed since they were built. Without this, an
        /// assembly the host loads *after* the first type resolution was permanently invisible — so whether
        /// <c>@model Some.Late.Type</c> resolved depended on whether an unrelated earlier compile had happened, which
        /// is not a property any host can reason about.
        /// </summary>
        private static NameMaps CurrentMaps()
        {
            // Observe before comparing: the stamp only advances when something looks at the assembly set, so reading it
            // without observing would leave a late-loaded assembly invisible until an unrelated call happened to look.
            // This costs one AppDomain enumeration per type resolution — a compile-time path, never a render one.
            AssemblyHelper.GetAssemblies();

            var maps = Volatile.Read(ref _maps);
            if (maps.Generation == AssemblyHelper.Generation)
                return maps;

            Reconfigure();
            return Volatile.Read(ref _maps);
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

        // The alias table lives in CSharpTypeNames — single source for parse direction here,
        // display direction in signature/hover text, and build tier's symbol-side adapter.
        private static Type ResolveCsharpType(string typeName) =>
            CSharpTypeNames.TryGetType(typeName, out var result) ? result : null;

        /// <summary>
        /// The name-index arms — assembly-qualified, dotted, bare — over one snapshot of the maps. Every one of them
        /// answers exactly as it always has; where they all miss the throw is handed back rather than raised, so the
        /// directive arms can try and the original message still reaches the caller when they do not.
        /// </summary>
        private static Type ResolveIndexedType(string typeName, ICollection<string> imports, NameMaps maps,
            out InvalidOperationException failure)
        {
            failure = null;
            try
            {
                return ResolveIndexedTypeCore(typeName, imports, maps);
            }
            catch (InvalidOperationException e)
            {
                failure = e;
                return null;
            }
        }

        /// <summary>
        /// Resolves <paramref name="typeName"/> using the collected <c>@using</c> bodies that bind a name rather
        /// than open a namespace, plus the <c>global::</c> qualifier.
        /// <para>Ordered after <see cref="ResolveIndexedType"/> on purpose, so these arms can only fire where the
        /// index already had no answer: a template whose spelling resolves today keeps the type it resolves to,
        /// whatever directives sit beside it. The one exception is <c>global::</c>, which is handled before the
        /// index because it is a qualifier no index key carries and so has never resolved to anything.</para>
        /// </summary>
        private static Type ResolveSimpleType(string typeName, ICollection<string> imports)
        {
            // One read of the published snapshot: both maps must come from the same rebuild, or a resolve racing a
            // Register can consult a new short-name map against an old full-name one. The directive arms are handed
            // that same read for the same reason.
            var maps = CurrentMaps();

            if (UsingDirectives.TryStripGlobalQualifier(typeName, out var globalName))
            {
                if (TryLookupQualified(globalName, maps, out var fromGlobal, out var globalAmbiguity))
                    return fromGlobal;
                throw ResolveSimpleError(typeName, imports, globalAmbiguity);
            }

            var directives = UsingDirectives.Parse(imports);

            // An alias binds the head ahead of the name index, because that is the order C# reads a
            // namespace-or-type-name in: the scope's alias directives, then the namespaces the scope imports.
            // Claiming the head commits — the index is not consulted afterwards — for the same reason.
            if (directives.ClaimsHead(typeName))
            {
                if (TryResolveThroughAlias(typeName, directives, maps, out var aliased, out var aliasAmbiguity))
                    return aliased;
                throw ResolveSimpleError(typeName, imports, aliasAmbiguity);
            }

            var resolved = ResolveIndexedType(typeName, imports, maps, out var failure);
            if (resolved != null)
                return resolved;

            if (!directives.IsEmpty)
            {
                // A `using static` target's nested types stay behind the index. C# puts them in the same bucket
                // as an imported namespace's types, and the index is a superset of that bucket, so moving this
                // arm forward would narrow rather than reorder.
                if (TryResolveThroughStaticImport(typeName, directives, maps, out var nested,
                        out var staticAmbiguity))
                    return nested;
                if (staticAmbiguity)
                    throw ResolveSimpleError(typeName, imports, true);
            }

            throw failure ?? ResolveSimpleError(typeName, imports, false);
        }

        /// <summary>
        /// The dotted-spelling arm of a <c>using</c> namespace import. A namespace import brings the types
        /// <b>declared in</b> that namespace into scope and nothing else — not the namespaces nested inside it — so
        /// a candidate counts only when its own namespace <i>is</i> the import. A nested type reports its outer
        /// type's namespace, which is what keeps <c>using A;</c> + <c>Outer.Inner</c> resolving while
        /// <c>using A;</c> + <c>Sub.Deep</c> stops: the first is a type in <c>A</c> with a type inside it, the
        /// second is a type in <c>A.Sub</c>, a namespace nobody imported.
        /// <para>A namespace <b>alias</b> is the opposite case and does not come through here: <c>using X = A;</c>
        /// names the namespace itself, so <c>X.Sub.Deep</c> binds — see <see cref="TryResolveThroughAlias"/>.</para>
        /// </summary>
        private static bool TryResolveThroughImports(string typeName, ICollection<string> imports, NameMaps maps,
            out Type type, out bool ambiguous)
        {
            type = null;
            ambiguous = false;
            foreach (var import in imports)
            {
                if (!maps.FullNames.TryGetValue(import + "." + typeName, out var types))
                    continue;

                Type declared = null;
                int matches = 0;
                foreach (var candidate in types)
                {
                    if (!string.Equals(candidate.Namespace, import, StringComparison.Ordinal))
                        continue;
                    matches++;
                    declared = candidate;
                }

                if (matches == 0)
                    continue;
                if (matches == 1)
                {
                    type = declared;
                    return true;
                }

                ambiguous = true;
                return false;
            }

            return false;
        }

        /// <summary>Looks a fully-qualified spelling up in the global namespace, consulting no import and no alias.
        /// The second key is how a type with no namespace is stored: the index writes
        /// <c>type.Namespace + "." + name</c> and <c>Type.Namespace</c> is null for it, so its key carries a leading
        /// dot that no spelling has.</summary>
        private static bool TryLookupQualified(string name, NameMaps maps, out Type type, out bool ambiguous)
        {
            ambiguous = false;
            type = null;
            if (!maps.FullNames.TryGetValue(name, out var types) &&
                !maps.FullNames.TryGetValue("." + name, out types))
                return false;

            if (types.Count != 1)
            {
                ambiguous = true;
                return false;
            }

            type = types[0];
            return true;
        }

        /// <summary>
        /// The <c>X = Some.Target</c> arm. The alias stands for its target wherever the spelling starts with it, so
        /// one substitution covers all three things C# allows through one: the alias alone naming a type
        /// (<c>X = System.Linq.Enumerable</c>, <c>X</c>), a namespace alias qualifying a type
        /// (<c>X = System.Linq</c>, <c>X.Enumerable</c>), and a type alias reaching a nested type
        /// (<c>X = Outer</c>, <c>X.Inner</c>) — the index keys a nested type under its dotted chain, so the last two
        /// are the same lookup.
        /// </summary>
        private static bool TryResolveThroughAlias(string typeName, UsingDirectives directives, NameMaps maps,
            out Type type, out bool ambiguous)
        {
            type = null;
            ambiguous = false;
            if (directives.Aliases.Count == 0)
                return false;

            var dot = typeName.IndexOf('.');
            var head = dot < 0 ? typeName : typeName.Substring(0, dot);
            if (!directives.Aliases.TryGetValue(head, out var target))
                return false;

            UsingDirectives.TryStripGlobalQualifier(target, out var qualified);
            if (dot < 0)
            {
                // `using X = int;` is a legal alias, and the keyword is the one spelling the index does not carry.
                type = ResolveCsharpType(qualified);
                if (type != null)
                    return true;
            }
            else
            {
                qualified = qualified + typeName.Substring(dot);
            }

            return TryLookupQualified(qualified, maps, out type, out ambiguous);
        }

        /// <summary>
        /// The <c>static Some.Target</c> arm, which for type resolution contributes the target's nested types under
        /// their own names — <c>using static Outer;</c> makes <c>Outer.Inner</c> answer to <c>Inner</c>. Two targets
        /// contributing the same name is the ambiguity C# reports as CS0104, not a pick.
        /// <para>Static <b>member</b> access is a different question and is not asked here.</para>
        /// </summary>
        private static bool TryResolveThroughStaticImport(string typeName, UsingDirectives directives, NameMaps maps,
            out Type type, out bool ambiguous)
        {
            type = null;
            ambiguous = false;

            var matches = 0;
            foreach (var target in directives.StaticTargets)
            {
                UsingDirectives.TryStripGlobalQualifier(target, out var qualified);
                if (!TryLookupQualified(qualified + "." + typeName, maps, out var candidate, out var targetAmbiguity))
                {
                    ambiguous |= targetAmbiguity;
                    continue;
                }

                matches++;
                type = candidate;
            }

            if (matches == 1 && !ambiguous)
                return true;

            ambiguous |= matches > 1;
            type = null;
            return false;
        }

        private static InvalidOperationException ResolveSimpleError(string typeName, ICollection<string> imports,
            bool ambiguous)
        {
            return ambiguous
                ? new InvalidOperationException(
                    $"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)}), the type name is ambigous")
                : new InvalidOperationException($"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)})");
        }

        private static Type ResolveIndexedTypeCore(string typeName, ICollection<string> imports, NameMaps maps)
        {
            var shortNames = maps.ShortNames;
            var fullNames = maps.FullNames;

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
                if (fullNames.TryGetValue(typeName, out var types))
                {
                    if (types.Count == 1)
                    {
                        return types[0];
                    }

                    // Several types answer to the whole spelling; an import may still name one of them by
                    // re-qualifying it, and nothing else settles it.
                    if (TryResolveThroughImports(typeName, imports, maps, out var disambiguated, out _))
                        return disambiguated;
                    throw new InvalidOperationException(
                        $"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)}), the type name is ambigous");
                }

                if (TryResolveThroughImports(typeName, imports, maps, out var imported, out var ambiguousInImport))
                    return imported;
                if (ambiguousInImport)
                    throw new InvalidOperationException(
                        $"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)}), the type name is ambigous");
                throw new InvalidOperationException($"Couldn't resolve type <{typeName}> ({string.Join(", ", imports)})");
            }
            else
            {
                Type result = ResolveCsharpType(typeName);
                if (result != null)
                    return result;
                if (shortNames.TryGetValue(typeName, out var types))
                {
                    if (types.Count == 1)
                    {
                        return types[0];
                    }

                    // A short-name tie is disambiguated by imports; a tie the imports do NOT settle
                    // is the same "ambigous" error that dotted/full-name arms raise. This matches the build tier
                    // behavior (the generator raises HED7023 for the same input).
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
        /// Resolves a template-spelled type name using the shared <see cref="TypeSpelling"/> parser for grammar and
        /// <see cref="ReflectionTypeLookup"/> for the reflection type universe. Maps parser faults back to
        /// <see cref="InvalidOperationException"/> messages.
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