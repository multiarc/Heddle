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
    /// <para>The resolution <b>rule</b> itself is no longer mirrored here: this class is the build tier's
    /// <see cref="ITypeNameMaps{TType}"/>, and <see cref="TypeNameIndex"/> — shared source, run unchanged by the
    /// runtime over its reflection maps — is the ladder. What stays is what only a compilation can answer: how the
    /// index is built, what an assembly-qualified spelling means against references rather than a loader, and
    /// whether a name exists anywhere at all.</para>
    /// </summary>
    internal sealed class SymbolTypeIndex : ITypeNameMaps<INamedTypeSymbol>
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

        /// <summary>Resolves a simple name through the shared ladder over this index. The arms, their order and
        /// the ambiguity rule are <see cref="TypeNameIndex"/>'s and are the runtime's.</summary>
        internal bool TryResolve(string name, IReadOnlyList<string> imports, out INamedTypeSymbol type,
            out TypeSpellingFault fault) =>
            TypeNameIndex.TryResolve(name, imports, this, out type, out fault);

        public bool TryGetByShortName(string key, out IReadOnlyList<INamedTypeSymbol> types)
        {
            var found = _shortNames.TryGetValue(key, out var list);
            types = list;
            return found;
        }

        public bool TryGetByFullName(string key, out IReadOnlyList<INamedTypeSymbol> types)
        {
            var found = _fullNames.TryGetValue(key, out var list);
            types = list;
            return found;
        }

        /// <summary>The runtime's <c>Type.Namespace</c>: the nearest enclosing namespace, which for a nested type is
        /// its outer type's, and null for the global namespace.</summary>
        public string NamespaceOf(INamedTypeSymbol type)
        {
            var ns = type.ContainingNamespace;
            return ns == null || ns.IsGlobalNamespace ? null : ns.ToDisplayString();
        }

        public bool SameType(INamedTypeSymbol left, INamedTypeSymbol right) =>
            SymbolEqualityComparer.Default.Equals(left, right);

        /// <summary>The Roslyn projection of the shared alias table — the one spelling this index does not carry,
        /// because no type's metadata name is <c>int</c>.</summary>
        public bool TryResolveKeyword(string name, out INamedTypeSymbol type)
        {
            type = null;
            if (_compilation == null || !SymbolTypeResolver.Keywords.TryGetValue(name, out var special))
                return false;

            type = _compilation.GetSpecialType(special);
            return type != null;
        }

        /// <summary>
        /// The runtime's <c>Type.GetType(spelling)</c> arm, over this compilation's references instead of the loaded
        /// assemblies. A template spells a nested type with dots because the lexer rejects <c>+</c>, and the runtime
        /// retries the CLR name one <c>.</c>-to-<c>+</c> conversion at a time; the index already carries the dotted
        /// alias beside the metadata form, so both spellings are looked up by the one map read.
        /// <para>The assembly half is parsed by Roslyn's own display-name parser rather than by cutting the string.
        /// A component the spelling does not state binds to any, which is what the CLR's own load does with it.</para>
        /// <para><b>A stated version binds at or below the assembly's own and refuses above it.</b> Measured against
        /// the loader the engine goes through: the default load context satisfies a request from an already-loaded
        /// assembly only when the loaded version is at least the requested one — an older request is advice the
        /// loaded assembly upgrades, a newer one falls through to probing and finds the same too-old file. Requiring
        /// an exact match instead took a template off the precompiled tier every time a host bumped an assembly
        /// version without editing the template, silently and with nothing reported.
        /// <para>The public key token is likewise required when stated: a version drifts on its own with every
        /// build, a public key token does not.</para></para>
        /// </summary>
        public bool TryResolveAssemblyQualified(string name, out INamedTypeSymbol type)
        {
            type = null;

            int comma = name.IndexOf(',');
            var typeName = name.Substring(0, comma).Trim();
            if (typeName.Length == 0 ||
                !AssemblyIdentity.TryParseDisplayName(name.Substring(comma + 1).Trim(), out var wanted, out var parts))
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
                if ((parts & AssemblyIdentityParts.Version) != 0 && wanted.Version > identity.Version)
                    continue;

                type = candidate;
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

        /// <summary>Whether any type answers to the (final segment of the) name anywhere in the universe — the
        /// <c>HED7007</c> guard's question.</summary>
        internal bool NameExistsAnywhere(string simpleName) => _shortNames.ContainsKey(simpleName);
    }
}
