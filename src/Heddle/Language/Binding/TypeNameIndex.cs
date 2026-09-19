using System;
using System.Collections.Generic;

namespace Heddle.Language.Binding
{
    /// <summary>
    /// The one type-name resolution ladder, run by both tiers over their own <see cref="ITypeNameMaps{TType}"/>.
    /// <para>Order is C#'s, and each arm's position is load-bearing: <c>global::</c> first, being a qualifier no
    /// index key carries and one C# reads past every alias; then an alias claiming the <b>head</b> of the spelling,
    /// because that is the order C# reads a namespace-or-type-name in — the scope's alias directives, then the
    /// namespaces the scope imports — and claiming the head <b>commits</b>, so a target naming nothing is an error
    /// rather than a fallback; then the name index, whose three arms (assembly-qualified, dotted, bare) disambiguate
    /// differently and are not interchangeable; and last a <c>using static</c> target's nested types, which C# puts
    /// in the same bucket as an imported namespace's types — the index is a superset of that bucket, so moving the
    /// arm forward would narrow rather than reorder.</para>
    /// <para>Ambiguity is decided here too, and deliberately: it is where two independent implementations of this
    /// ladder would most plausibly disagree. A tie no import settles is the error C# reports as CS0104 — the
    /// runtime's "the type name is ambigous" throw and the build tier's <c>HED7023</c> — never a pick by
    /// declaration order, assembly-scan order or reference order.</para>
    /// <para>Generic arity is not this file's business: the shared <see cref="TypeSpelling"/> grammar rewrites
    /// <c>Ns.Outer&lt;int&gt;.Inner</c> into the backtick definition name <c>Ns.Outer`1.Inner</c> before a name
    /// reaches here, so the index is asked for the name it actually keys.</para>
    /// </summary>
    internal static class TypeNameIndex
    {
        private static readonly string[] NoImports = new string[0];

        /// <summary>Resolves one simple name — no type arguments, no array suffix, no tuple — against
        /// <paramref name="maps"/> and the collected <c>@using</c> bodies. <paramref name="fault"/> tells an
        /// unresolved name from an ambiguous one, which is the difference between two diagnostics on both tiers.</summary>
        internal static bool TryResolve<TType>(string name, IReadOnlyList<string> imports,
            ITypeNameMaps<TType> maps, out TType type, out TypeSpellingFault fault)
            where TType : class
        {
            type = null;
            fault = TypeSpellingFault.Unresolved;
            if (name == null || maps == null)
                return false;

            imports = imports ?? NoImports;

            if (UsingDirectives.TryStripGlobalQualifier(name, out var globalName))
            {
                if (TryLookupQualified(globalName, maps, out type, out var globalAmbiguity))
                {
                    fault = TypeSpellingFault.None;
                    return true;
                }

                fault = globalAmbiguity ? TypeSpellingFault.Ambiguous : TypeSpellingFault.Unresolved;
                return false;
            }

            var directives = UsingDirectives.Parse(imports);

            if (directives.ClaimsHead(name))
            {
                if (TryResolveThroughAlias(name, directives, maps, out type, out var aliasAmbiguity))
                {
                    fault = TypeSpellingFault.None;
                    return true;
                }

                fault = aliasAmbiguity ? TypeSpellingFault.Ambiguous : TypeSpellingFault.Unresolved;
                return false;
            }

            if (TryResolveIndexed(name, imports, maps, out type, out fault))
                return true;

            if (directives.IsEmpty)
                return false;

            if (TryResolveThroughStaticImport(name, directives, maps, out type, out var staticAmbiguity))
            {
                fault = TypeSpellingFault.None;
                return true;
            }

            // The index's own fault stands unless the `static` arm found a tie of its own, which is the more
            // specific answer: a name two targets both contribute is ambiguous, not absent.
            if (staticAmbiguity)
                fault = TypeSpellingFault.Ambiguous;
            return false;
        }

        /// <summary>The name-index arms. Kept apart from the directive arms so each can be ordered against the
        /// index on its own: the <c>static</c> arm still fires only where these had no answer, while the alias arm
        /// is reached before them.</summary>
        private static bool TryResolveIndexed<TType>(string name, IReadOnlyList<string> imports,
            ITypeNameMaps<TType> maps, out TType type, out TypeSpellingFault fault)
            where TType : class
        {
            type = null;

            if (name.IndexOf(',') >= 0)
            {
                if (maps.TryResolveAssemblyQualified(name, out type))
                {
                    fault = TypeSpellingFault.None;
                    return true;
                }

                fault = TypeSpellingFault.Unresolved;
                return false;
            }

            if (name.IndexOf('.') >= 0)
            {
                if (maps.TryGetByFullName(name, out var qualified))
                {
                    if (qualified.Count == 1)
                    {
                        type = qualified[0];
                        fault = TypeSpellingFault.None;
                        return true;
                    }

                    // Several types answer to the whole spelling. An import may still name one of them by
                    // re-qualifying it; nothing else settles it, and what is left is the ambiguity error.
                    if (TryResolveThroughImports(name, imports, maps, out type, out fault))
                        return true;
                    fault = TypeSpellingFault.Ambiguous;
                    return false;
                }

                return TryResolveThroughImports(name, imports, maps, out type, out fault);
            }

            // A predefined-type alias is checked here rather than at the top of the ladder, because `global::` and
            // an alias directive both bind ahead of it: `@using(){{ int = Ns.T }}` names a type the C# compiler
            // would reach as `@int`, and the ladder that answered `System.Int32` there was resolving a spelling the
            // directive had already claimed.
            if (maps.TryResolveKeyword(name, out type))
            {
                fault = TypeSpellingFault.None;
                return true;
            }

            if (!maps.TryGetByShortName(name, out var candidates))
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

            // A short-name tie is settled by whether a candidate's own namespace was imported, and by nothing else:
            // an unsettled tie is the ambiguity error, never the first candidate the scan happened to reach.
            TType single = null;
            var matches = 0;
            foreach (var candidate in candidates)
            {
                var ns = maps.NamespaceOf(candidate);
                if (ns == null || !Contains(imports, ns))
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
        /// The dotted-spelling arm of a <c>using</c> namespace import. A namespace import brings the types
        /// <b>declared in</b> that namespace into scope and nothing else — not the namespaces nested inside it — so
        /// a candidate counts only when its own namespace <i>is</i> the import. A nested type reports its outer
        /// type's namespace, which is what keeps <c>using A;</c> + <c>Outer.Inner</c> resolving while
        /// <c>using A;</c> + <c>Sub.Deep</c> stops: the first is a type in <c>A</c> with a type inside it, the
        /// second is a type in <c>A.Sub</c>, a namespace nobody imported.
        /// <para>Every import is read before any candidate wins: a spelling two imports each complete is the
        /// ambiguity C# reports as CS0104, not a question <c>@using</c> declaration order may answer.</para>
        /// <para>A namespace <b>alias</b> is the opposite case and does not come through here: <c>using X = A;</c>
        /// names the namespace itself, so <c>X.Sub.Deep</c> binds — see <see cref="TryResolveThroughAlias"/>.</para>
        /// </summary>
        private static bool TryResolveThroughImports<TType>(string name, IReadOnlyList<string> imports,
            ITypeNameMaps<TType> maps, out TType type, out TypeSpellingFault fault)
            where TType : class
        {
            type = null;
            TType declared = null;
            for (var i = 0; i < imports.Count; i++)
            {
                var import = imports[i];
                if (import == null || !maps.TryGetByFullName(import + "." + name, out var candidates))
                    continue;

                foreach (var candidate in candidates)
                {
                    if (!string.Equals(maps.NamespaceOf(candidate), import, StringComparison.Ordinal))
                        continue;
                    // The same type reached twice (a duplicate import) is no tie; two distinct types are.
                    if (declared != null && !maps.SameType(declared, candidate))
                    {
                        fault = TypeSpellingFault.Ambiguous;
                        return false;
                    }

                    declared = candidate;
                }
            }

            if (declared == null)
            {
                fault = TypeSpellingFault.Unresolved;
                return false;
            }

            type = declared;
            fault = TypeSpellingFault.None;
            return true;
        }

        /// <summary>Looks a fully-qualified spelling up in the global namespace, consulting no import and no alias.
        /// The second key is how a type with no namespace is stored: the index writes
        /// <c>Namespace + "." + name</c> and the namespace is null for it, so its key carries a leading dot that no
        /// spelling has.</summary>
        private static bool TryLookupQualified<TType>(string name, ITypeNameMaps<TType> maps, out TType type,
            out bool ambiguous)
            where TType : class
        {
            ambiguous = false;
            type = null;
            if (!maps.TryGetByFullName(name, out var candidates) &&
                !maps.TryGetByFullName("." + name, out candidates))
                return false;

            if (candidates.Count != 1)
            {
                ambiguous = true;
                return false;
            }

            type = candidates[0];
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
        private static bool TryResolveThroughAlias<TType>(string name, UsingDirectives directives,
            ITypeNameMaps<TType> maps, out TType type, out bool ambiguous)
            where TType : class
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
                // `using X = int;` is a legal alias, and the keyword is the one spelling the index does not carry.
                if (maps.TryResolveKeyword(qualified, out type))
                    return true;
            }
            else
            {
                qualified = qualified + name.Substring(dot);
            }

            return TryLookupQualified(qualified, maps, out type, out ambiguous);
        }

        /// <summary>
        /// The <c>static Some.Target</c> arm, which for type resolution contributes the target's nested types under
        /// their own names — <c>using static Outer;</c> makes <c>Outer.Inner</c> answer to <c>Inner</c>. Two targets
        /// contributing the same name is the ambiguity C# reports as CS0104, not a pick.
        /// <para>Static <b>member</b> access is a different question and is not asked here.</para>
        /// </summary>
        private static bool TryResolveThroughStaticImport<TType>(string name, UsingDirectives directives,
            ITypeNameMaps<TType> maps, out TType type, out bool ambiguous)
            where TType : class
        {
            type = null;
            ambiguous = false;

            var matches = 0;
            foreach (var target in directives.StaticTargets)
            {
                UsingDirectives.TryStripGlobalQualifier(target, out var qualified);
                if (!TryLookupQualified(qualified + "." + name, maps, out var candidate, out var targetAmbiguity))
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
            for (var i = 0; i < imports.Count; i++)
                if (string.Equals(imports[i], value, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
