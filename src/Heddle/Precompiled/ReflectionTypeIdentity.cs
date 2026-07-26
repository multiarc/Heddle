using System;
using System.Collections.Generic;

namespace Heddle.Precompiled
{
    /// <summary>
    /// Phase 3 (F1): the <b>reflection</b> adapter of <see cref="AqnFormatter"/> — the run-tier half of the manifest
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
        /// <para><c>Type.Name</c> already carries the per-segment backtick arity (<c>C`1</c>) for generic
        /// definitions <em>and</em> constructed generics; the identity string is therefore the <em>nominal</em>
        /// spelling, without the <c>[[…]]</c> type-argument list <see cref="Type.FullName"/> appends to a
        /// constructed generic. That is deliberate and pinned by test: the manifest's domain is registered
        /// extension types and export containers, which are nominal, and formatting both tiers from the same
        /// decomposition is what makes them agree.</para></summary>
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
