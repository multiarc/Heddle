using System.Collections.Generic;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The <b>Roslyn</b> adapter of the shared <see cref="AqnFormatter"/> — the build-tier half of the
    /// manifest identity contract. Replaces hand-typed copies of the <c>FullyQualifiedFormat</c> formatting rule,
    /// ensuring nested types like <c>Ns.Outer.Inner</c> match reflection's <c>Ns.Outer+Inner</c> and generics
    /// like <c>Ns.C&lt;T&gt;</c> match reflection's <c>Ns.C`1</c>.
    /// </summary>
    internal static class SymbolTypeIdentity
    {
        /// <summary>The manifest identity string (<c>Ns.Outer+Inner, Assembly</c>) of a compile-time type symbol.</summary>
        internal static string AqnSansVersion(INamedTypeSymbol type)
        {
            if (type == null)
                return AqnFormatter.Unknown;

            return AqnFormatter.Format(NamespaceOf(type), MetadataChain(type), AssemblyNameOf(type));
        }

        /// <summary>The CLR full name alone (no assembly) — what the emitter's body-extension type name wants.</summary>
        internal static string FullName(INamedTypeSymbol type)
        {
            if (type == null)
                return null;
            return AqnFormatter.FormatFullName(NamespaceOf(type), MetadataChain(type));
        }

        internal static string AssemblyNameOf(INamedTypeSymbol type) =>
            type?.ContainingAssembly?.Identity.Name ?? string.Empty;

        private static string NamespaceOf(INamedTypeSymbol type)
        {
            // A nested type's ContainingNamespace is the namespace of its outermost declaring type, matching
            // Type.Namespace on the reflection side.
            var ns = type.ContainingNamespace;
            return ns == null || ns.IsGlobalNamespace ? null : ns.ToDisplayString();
        }

        /// <summary>The nesting chain as CLR metadata names, outermost declaring type first.
        /// <c>ISymbol.MetadataName</c> already carries the per-segment backtick arity (<c>C`1</c>), matching
        /// <c>Type.Name</c> on the reflection side.</summary>
        private static IReadOnlyList<string> MetadataChain(INamedTypeSymbol type)
        {
            var chain = new List<string>(2);
            for (var t = type; t != null; t = t.ContainingType)
                chain.Add(t.MetadataName);
            chain.Reverse();
            return chain;
        }
    }
}
