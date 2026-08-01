using System.Collections.Generic;
using Heddle.Language.Binding;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The build tier's mirror of <c>ReflectionHelper</c>'s global name index, so model type names resolve by
    /// the <b>runtime's</b> rule rather than a different one.
    /// <para>The runtime indexes every type of every loaded assembly under its metadata short name
    /// (<c>C`1</c>, <c>Outer+Inner</c>, plus a dotted <c>Outer.Inner</c> alias because the template lexer cannot
    /// accept <c>+</c>) and its namespace-qualified full name, then resolves: exactly one hit wins; several hits are
    /// settled by the template's <c>@using</c> imports; a tie the imports do not settle is the "ambigous" error.
    /// This class builds the same index over the compilation and its referenced assemblies.</para>
    /// <para>The generator's previous rule was unrelated: first <c>@using</c> match, then an <b>implicit</b>
    /// <c>System</c>/<c>System.Collections.Generic</c> fallback the runtime does not have. This created a risk where
    /// <c>:: List</c> could bind to different types on the two tiers, and emitted typed code would type member hops
    /// off the wrong type. The implicit namespaces are gone.</para>
    /// </summary>
    internal sealed class SymbolTypeIndex
    {
        private readonly Dictionary<string, List<INamedTypeSymbol>> _shortNames =
            new Dictionary<string, List<INamedTypeSymbol>>(System.StringComparer.Ordinal);

        private readonly Dictionary<string, List<INamedTypeSymbol>> _fullNames =
            new Dictionary<string, List<INamedTypeSymbol>>(System.StringComparer.Ordinal);

        /// <summary>Held only so an alias to a predefined type (<c>using X = int;</c>) can be answered — the
        /// keyword table is the one spelling this index does not carry.</summary>
        private readonly Compilation _compilation;

        private SymbolTypeIndex(Compilation compilation) => _compilation = compilation;

        /// <summary>One index per compilation, cached in <see cref="SymbolTypeIndexCache"/>.</summary>
        internal static SymbolTypeIndex For(Compilation compilation) => SymbolTypeIndexCache.Shared.Get(compilation);

        /// <summary>Builds the index from scratch. A <c>null</c> compilation yields an empty index, so a caller
        /// with no compilation resolves nothing rather than throwing.</summary>
        internal static SymbolTypeIndex Build(Compilation compilation)
        {
            var index = new SymbolTypeIndex(compilation);
            if (compilation == null)
                return index;

            var assemblies = new List<IAssemblySymbol> { compilation.Assembly };
            assemblies.AddRange(compilation.SourceModule.ReferencedAssemblySymbols);
            foreach (var assembly in assemblies)
                index.AddNamespace(assembly.GlobalNamespace);
            return index;
        }

        private void AddNamespace(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
                AddType(type);
            foreach (var child in ns.GetNamespaceMembers())
                AddNamespace(child);
        }

        private void AddType(INamedTypeSymbol type)
        {
            var ns = type.ContainingNamespace;
            var namespaceName = ns == null || ns.IsGlobalNamespace ? null : ns.ToDisplayString();

            string shortName;
            if (type.ContainingType != null)
            {
                var chain = new List<string>();
                for (var t = type; t != null; t = t.ContainingType)
                    chain.Add(t.MetadataName);
                chain.Reverse();
                shortName = string.Join("+", chain);

                var dotted = shortName.Replace('+', '.');
                Add(_shortNames, dotted, type);
                Add(_fullNames, Qualify(namespaceName, dotted), type);
            }
            else
            {
                shortName = type.MetadataName;
            }

            Add(_shortNames, shortName, type);
            Add(_fullNames, Qualify(namespaceName, shortName), type);

            foreach (var nested in type.GetTypeMembers())
                AddType(nested);
        }

        /// <summary>The full-name key, which is the runtime's <c>type.Namespace + "." + shortName</c> — and
        /// <c>Type.Namespace</c> is <b>null</b> for a type in the global namespace, so its key carries a leading dot
        /// and nothing answers to the undotted spelling. Both directions of dropping that dot were divergences:
        /// <c>.Global</c> is a name the engine resolves and the build called a typo, and <c>Global.Inner</c> is one
        /// the engine refuses and the build resolved.</summary>
        private static string Qualify(string namespaceName, string name) =>
            namespaceName == null ? "." + name : namespaceName + "." + name;

        private static void Add(Dictionary<string, List<INamedTypeSymbol>> map, string key, INamedTypeSymbol type)
        {
            if (!map.TryGetValue(key, out var list))
                map[key] = list = new List<INamedTypeSymbol>();
            foreach (var existing in list)
                if (SymbolEqualityComparer.Default.Equals(existing, type))
                    return;
            list.Add(type);
        }

        /// <summary>
        /// The runtime's <c>ResolveSimpleType</c> rule over the build-time universe, arm for arm: an
        /// assembly-qualified spelling first, then a dotted one, then a bare short name. The three arms disambiguate
        /// differently and swapping one for another changes the answer — the dotted arm re-qualifies the whole
        /// spelling with each import, where the short-name arm asks which candidate's own namespace was imported.
        /// <para>Then the runtime's directive arms, in the runtime's order: an alias, and a <c>using static</c>
        /// target's nested types. Both come <b>after</b> the index, so neither can move a spelling the index already
        /// answers; <c>global::</c> comes before it, being a qualifier no index key carries.</para>
        /// </summary>
        internal bool TryResolve(string name, IReadOnlyList<string> imports, out INamedTypeSymbol type,
            out TypeSpellingFault fault)
        {
            imports = imports ?? new string[0];

            if (UsingDirectives.TryStripGlobalQualifier(name, out var globalName))
            {
                if (TryLookupQualified(globalName, out type, out var globalAmbiguity))
                {
                    fault = TypeSpellingFault.None;
                    return true;
                }

                fault = globalAmbiguity ? TypeSpellingFault.Ambiguous : TypeSpellingFault.Unresolved;
                return false;
            }

            if (TryResolveIndexed(name, imports, out type, out fault))
                return true;

            var directives = UsingDirectives.Parse(imports);
            if (directives.IsEmpty)
                return false;

            if (TryResolveThroughAlias(name, directives, out type, out var aliasAmbiguity))
            {
                fault = TypeSpellingFault.None;
                return true;
            }

            if (aliasAmbiguity)
            {
                fault = TypeSpellingFault.Ambiguous;
                return false;
            }

            if (TryResolveThroughStaticImport(name, directives, out type, out var staticAmbiguity))
            {
                fault = TypeSpellingFault.None;
                return true;
            }

            if (staticAmbiguity)
                fault = TypeSpellingFault.Ambiguous;
            return false;
        }

        /// <summary>The name-index arms, unchanged. Kept apart from the directive arms so the directive arms can
        /// only fire where these already had no answer — the property that makes them additive.</summary>
        private bool TryResolveIndexed(string name, IReadOnlyList<string> imports, out INamedTypeSymbol type,
            out TypeSpellingFault fault)
        {
            type = null;

            if (name.IndexOf(',') >= 0)
                return TryResolveAssemblyQualified(name, out type, out fault);

            if (name.IndexOf('.') >= 0)
            {
                if (_fullNames.TryGetValue(name, out var qualified))
                {
                    if (qualified.Count == 1)
                    {
                        type = qualified[0];
                        fault = TypeSpellingFault.None;
                        return true;
                    }

                    // Several types answer to the whole spelling. An import may still name one of them by
                    // re-qualifying it; nothing else settles it, and the runtime raises its "ambigous" error.
                    if (TryResolveThroughImports(name, imports, out type, out fault))
                        return true;
                    fault = TypeSpellingFault.Ambiguous;
                    return false;
                }

                return TryResolveThroughImports(name, imports, out type, out fault);
            }

            if (!_shortNames.TryGetValue(name, out var candidates))
            {
                fault = TypeSpellingFault.Unresolved;
                return false;
            }

            if (candidates.Count == 1)
            {
                type = candidates[0];
                fault = TypeSpellingFault.None;
                return true;
            }

            // A short-name tie is settled by whether a candidate's own namespace was imported. Both tiers raise
            // ambiguity instead of using assembly-order matching.
            INamedTypeSymbol single = null;
            int matches = 0;
            foreach (var candidate in candidates)
            {
                var ns = candidate.ContainingNamespace;
                var nsName = ns == null || ns.IsGlobalNamespace ? null : ns.ToDisplayString();
                if (nsName == null || !Contains(imports, nsName))
                    continue;
                matches++;
                single = candidate;
            }

            if (matches == 1)
            {
                type = single;
                fault = TypeSpellingFault.None;
                return true;
            }

            fault = matches > 1 ? TypeSpellingFault.Ambiguous : TypeSpellingFault.Unresolved;
            return false;
        }

        /// <summary>
        /// The runtime's <c>Type.GetType(spelling)</c> arm, over this compilation's references instead of the loaded
        /// assemblies. A template spells a nested type with dots because the lexer rejects <c>+</c>, and the runtime
        /// retries the CLR name one <c>.</c>-to-<c>+</c> conversion at a time; the index already carries the dotted
        /// alias beside the metadata form, so both spellings are looked up by the one map read.
        /// <para>The assembly half is parsed by Roslyn's own display-name parser rather than by cutting the string.
        /// A component the spelling does not state binds to any, which is what the CLR's own load does with it.</para>
        /// <para><b>A stated version binds nothing.</b> Measured against the loader the engine goes through:
        /// <c>Type.GetType("X, Asm, Version=99.0.0.0")</c> resolves whenever <c>Asm</c> is already loaded in the
        /// default load context, strong-named or not — the loaded assembly is matched by simple name and the rest of
        /// the identity is advice. An assembly the template names is one the host runs on, so that is the case that
        /// happens; requiring an exact match instead took a template off the precompiled tier every time a host
        /// bumped an assembly version without editing the template, silently and with nothing reported.
        /// <para>The public key token is still required when stated, and the asymmetry is deliberate: a version
        /// drifts on its own with every build, a public key token does not. For an assembly the default context has
        /// <b>not</b> loaded the CLR's binder is stricter than either rule — it refuses a version above the one on
        /// disk — but which assemblies are loaded is not a question a build can ask.</para></para>
        /// </summary>
        private bool TryResolveAssemblyQualified(string name, out INamedTypeSymbol type, out TypeSpellingFault fault)
        {
            type = null;
            fault = TypeSpellingFault.Unresolved;

            int comma = name.IndexOf(',');
            var typeName = name.Substring(0, comma).Trim();
            if (typeName.Length == 0 ||
                !AssemblyIdentity.TryParseDisplayName(name.Substring(comma + 1).Trim(), out var wanted))
                return false;

            // The second spelling is how a type in the global namespace is keyed here: the index carries the
            // runtime's `Namespace + "." + name`, and `Type.GetType` — which has no namespace to prefix — sees the
            // same type under the undotted name.
            if (!_fullNames.TryGetValue(typeName, out var candidates) &&
                !_fullNames.TryGetValue("." + typeName, out candidates))
                return false;

            foreach (var candidate in candidates)
            {
                var identity = candidate.ContainingAssembly?.Identity;
                if (identity == null ||
                    !string.Equals(identity.Name, wanted.Name, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!wanted.PublicKeyToken.IsDefaultOrEmpty &&
                    !SameToken(identity.PublicKeyToken, wanted.PublicKeyToken))
                    continue;

                type = candidate;
                fault = TypeSpellingFault.None;
                return true;
            }

            return false;
        }

        /// <summary>Byte-wise, because <c>ImmutableArray&lt;byte&gt;.Equals</c> compares the underlying array
        /// reference and two identities never share one.</summary>
        private static bool SameToken(System.Collections.Immutable.ImmutableArray<byte> left,
            System.Collections.Immutable.ImmutableArray<byte> right)
        {
            if (left.IsDefaultOrEmpty || left.Length != right.Length)
                return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i] != right[i])
                    return false;
            return true;
        }

        private bool TryResolveThroughImports(string name, IReadOnlyList<string> imports,
            out INamedTypeSymbol type, out TypeSpellingFault fault)
        {
            type = null;
            foreach (var import in imports)
            {
                if (!_fullNames.TryGetValue(import + "." + name, out var candidates))
                    continue;
                if (candidates.Count == 1)
                {
                    type = candidates[0];
                    fault = TypeSpellingFault.None;
                    return true;
                }

                fault = TypeSpellingFault.Ambiguous;
                return false;
            }

            fault = TypeSpellingFault.Unresolved;
            return false;
        }

        /// <summary>The runtime's global-namespace lookup, which consults no import and no alias. The second key is
        /// how a type with no namespace is stored here — see <see cref="Qualify"/>.</summary>
        private bool TryLookupQualified(string name, out INamedTypeSymbol type, out bool ambiguous)
        {
            ambiguous = false;
            type = null;
            if (!_fullNames.TryGetValue(name, out var candidates) &&
                !_fullNames.TryGetValue("." + name, out candidates))
                return false;

            if (candidates.Count != 1)
            {
                ambiguous = true;
                return false;
            }

            type = candidates[0];
            return true;
        }

        /// <summary>The runtime's <c>X = Some.Target</c> arm: the alias stands for its target wherever a spelling
        /// starts with it, which is one substitution for a type alias used alone, a namespace alias qualifying a
        /// type, and a type alias reaching a nested type.</summary>
        private bool TryResolveThroughAlias(string name, UsingDirectives directives, out INamedTypeSymbol type,
            out bool ambiguous)
        {
            type = null;
            ambiguous = false;
            if (directives.Aliases.Count == 0)
                return false;

            var dot = name.IndexOf('.');
            var head = dot < 0 ? name : name.Substring(0, dot);
            if (!directives.Aliases.TryGetValue(head, out var target))
                return false;

            UsingDirectives.TryStripGlobalQualifier(target, out var qualified);
            if (dot < 0)
            {
                // `using X = int;` is a legal alias, and a keyword is the one spelling this index does not carry.
                if (SymbolTypeResolver.Keywords.TryGetValue(qualified, out var special))
                {
                    type = _compilation?.GetSpecialType(special);
                    if (type != null)
                        return true;
                }
            }
            else
            {
                qualified = qualified + name.Substring(dot);
            }

            return TryLookupQualified(qualified, out type, out ambiguous);
        }

        /// <summary>The runtime's <c>static Some.Target</c> arm, which for type resolution contributes the target's
        /// nested types under their own names. Two targets contributing one name is the ambiguity C# reports as
        /// CS0104.</summary>
        private bool TryResolveThroughStaticImport(string name, UsingDirectives directives, out INamedTypeSymbol type,
            out bool ambiguous)
        {
            type = null;
            ambiguous = false;

            var matches = 0;
            foreach (var target in directives.StaticTargets)
            {
                UsingDirectives.TryStripGlobalQualifier(target, out var qualified);
                if (!TryLookupQualified(qualified + "." + name, out var candidate, out var targetAmbiguity))
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

        private static bool Contains(IReadOnlyList<string> imports, string value)
        {
            for (int i = 0; i < imports.Count; i++)
                if (string.Equals(imports[i], value, System.StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>Whether any type answers to the (final segment of the) name anywhere in the universe — the
        /// <c>HED7007</c> guard's question.</summary>
        internal bool NameExistsAnywhere(string simpleName) => _shortNames.ContainsKey(simpleName);
    }
}
