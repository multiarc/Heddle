using System;
using System.Collections.Generic;

namespace Heddle.Precompiled
{
    /// <summary>
    /// The <b>reflection</b> adapter of <see cref="AqnFormatter"/> — the run-tier half of the manifest
    /// identity contract. Decomposes a <see cref="Type"/> into the namespace / nesting-chain / assembly-simple-name
    /// triple the shared formatter joins; the Roslyn adapter in <c>Heddle.Generator</c> does the same over
    /// <c>INamedTypeSymbol</c>, so the two spellings can no longer drift.
    /// </summary>
    internal static class ReflectionTypeIdentity
    {
        /// <summary>The manifest identity string (<c>Ns.Outer+Inner, Assembly</c>) of a live type.</summary>
        internal static string AqnSansVersion(Type type)
        {
            if (type == null)
                return AqnFormatter.Unknown;

            return AqnFormatter.Format(type.Namespace, MetadataChain(type), type.Assembly.GetName().Name);
        }

        /// <summary>The type's nesting chain as CLR metadata names, outermost declaring type first.
        /// Uses nominal spelling without type-argument lists (pinned by test) to ensure the manifest's nominal domain stays consistent.</summary>
        private static IReadOnlyList<string> MetadataChain(Type type)
        {
            var chain = new List<string>(2);
            for (var t = type; t != null; t = t.IsNested ? t.DeclaringType : null)
                chain.Add(t.Name);
            chain.Reverse();
            return chain;
        }
    }
}
