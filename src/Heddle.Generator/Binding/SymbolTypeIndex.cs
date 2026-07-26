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

        private SymbolTypeIndex() { }

        /// <summary>One index per compilation — the walk is the same shape <c>ExtensionBinder</c> already makes,
        /// and every model-type resolution in a compilation asks the same question of the same universe. The
        /// retention policy (occupancy bound, staleness eviction) lives in
        /// <see cref="SymbolTypeIndexCache"/>; this stays the one call site the binder knows about.</summary>
        internal static SymbolTypeIndex For(Compilation compilation) => SymbolTypeIndexCache.Shared.Get(compilation);

        /// <summary>Builds the index from scratch. A <c>null</c> compilation yields an empty index, so a caller
        /// with no compilation resolves nothing rather than throwing.</summary>
        internal static SymbolTypeIndex Build(Compilation compilation)
        {
            var index = new SymbolTypeIndex();
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

        private static string Qualify(string namespaceName, string name) =>
            namespaceName == null ? name : namespaceName + "." + name;

        private static void Add(Dictionary<string, List<INamedTypeSymbol>> map, string key, INamedTypeSymbol type)
        {
            if (!map.TryGetValue(key, out var list))
                map[key] = list = new List<INamedTypeSymbol>();
            foreach (var existing in list)
                if (SymbolEqualityComparer.Default.Equals(existing, type))
                    return;
            list.Add(type);
        }

        /// <summary>The runtime's <c>ResolveSimpleType</c> rule over the build-time universe.</summary>
        internal bool TryResolve(string name, IReadOnlyList<string> imports, out INamedTypeSymbol type,
            out TypeSpellingFault fault)
        {
            type = null;
            imports = imports ?? new string[0];

            var map = name.IndexOf('.') >= 0 ? _fullNames : _shortNames;
            if (!map.TryGetValue(name, out var candidates))
            {
                // A dotted spelling may also be a bare name qualified by an import (the runtime's second arm).
                if (name.IndexOf('.') >= 0)
                    return TryResolveThroughImports(name, imports, out type, out fault);
                fault = TypeSpellingFault.Unresolved;
                return false;
            }

            if (candidates.Count == 1)
            {
                type = candidates[0];
                fault = TypeSpellingFault.None;
                return true;
            }

            // Several claimants — the imports settle it, or the name is ambiguous. This is the rule the runtime's
            // short-name arm used to break by taking the first assembly-scan-ordered import match: an
            // order-dependent silent pick the build tier cannot reproduce by construction. Both tiers now raise
            // the ambiguity instead.
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
