using System.Collections.Generic;

namespace Heddle.Language.Binding
{
    /// <summary>
    /// The name-index seam the shared type-name ladder closes over. <typeparamref name="TType"/> is opaque:
    /// reflection adapter over <c>System.Type</c> keyed on the loaded assembly set, Roslyn adapter over
    /// <c>INamedTypeSymbol</c> keyed on the compilation's reference closure; neither appears in shared files.
    /// <para>The two maps are the runtime's own: every type is keyed under its metadata short name
    /// (<c>C`1</c>, <c>Outer+Inner</c>, plus a dotted <c>Outer.Inner</c> alias because the template lexer cannot
    /// accept <c>+</c>) and under <c>Namespace + "." + shortName</c> — a type with no namespace therefore answers
    /// to a key carrying a leading dot, which is a spelling nothing writes and the ladder looks up deliberately.</para>
    /// <para>Only the three members below are tier-shaped. The keyword arm is a table lookup on one side and a
    /// <c>SpecialType</c> request on the other; the assembly-qualified arm is a loader question on one side and a
    /// reference-identity question on the other; identity is reference equality on one side and a symbol comparer
    /// on the other. Everything else about resolving a name is the ladder, and the ladder is shared.</para>
    /// </summary>
    internal interface ITypeNameMaps<TType> where TType : class
    {
        /// <summary>The types keyed under a metadata short name. False leaves <paramref name="types"/> null.</summary>
        bool TryGetByShortName(string key, out IReadOnlyList<TType> types);

        /// <summary>The types keyed under a namespace-qualified full name.</summary>
        bool TryGetByFullName(string key, out IReadOnlyList<TType> types);

        /// <summary>The runtime's <c>Type.Namespace</c>: the nearest enclosing namespace, which for a nested type is
        /// its outer type's, and <c>null</c> for the global namespace.</summary>
        string NamespaceOf(TType type);

        /// <summary>Type identity. Reference equality does for reflection; symbols need their comparer.</summary>
        bool SameType(TType left, TType right);

        /// <summary>A C# predefined-type alias (<c>int</c>, <c>string</c>, <c>dynamic</c>, …) — the one spelling
        /// neither index carries, because no type's metadata name is spelled that way.</summary>
        bool TryResolveKeyword(string name, out TType type);

        /// <summary>An assembly-qualified spelling (<c>Ns.T, Some.Assembly</c>). Genuinely tier-shaped: the runtime
        /// hands it to the CLR loader, the build tier matches it against the compilation's references.</summary>
        bool TryResolveAssemblyQualified(string spelling, out TType type);
    }
}
