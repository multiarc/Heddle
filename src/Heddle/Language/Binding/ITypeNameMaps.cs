using System;
using System.Collections.Generic;

namespace Heddle.Language.Binding
{
    /// <summary>
    /// The name-index seam the type-name ladder closes over: one published snapshot of the runtime's own two maps,
    /// plus the loader arm. The snapshot is what keeps the ladder off <c>ReflectionHelper</c>'s internals — a
    /// resolve must see both maps from the same rebuild, so the pair is handed over as one object rather than
    /// looked up twice.
    /// <para>The two maps are the runtime's own: every type is keyed under its metadata short name
    /// (<c>C`1</c>, <c>Outer+Inner</c>, plus a dotted <c>Outer.Inner</c> alias because the template lexer cannot
    /// accept <c>+</c>) and under <c>Namespace + "." + shortName</c> — a type with no namespace therefore answers
    /// to a key carrying a leading dot, which is a spelling nothing writes and the ladder looks up deliberately.</para>
    /// </summary>
    internal interface ITypeNameMaps
    {
        /// <summary>The types keyed under a metadata short name. False leaves <paramref name="types"/> null.</summary>
        bool TryGetByShortName(string key, out IReadOnlyList<Type> types);

        /// <summary>The types keyed under a namespace-qualified full name.</summary>
        bool TryGetByFullName(string key, out IReadOnlyList<Type> types);

        /// <summary>An assembly-qualified spelling (<c>Ns.T, Some.Assembly</c>), handed to the CLR loader over the
        /// assemblies the host has registered.</summary>
        bool TryResolveAssemblyQualified(string spelling, out Type type);
    }
}
