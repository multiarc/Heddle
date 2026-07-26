using System.Collections.Generic;
using System.Text;

namespace Heddle.Precompiled
{
    /// <summary>
    /// Phase 3 (F1): the <b>one</b> implementation of the manifest identity string
    /// <c>"&lt;CLR full type name&gt;, &lt;assembly simple name&gt;"</c> — the AQN sans version the
    /// <c>PrecompiledGauntlet</c> compares between a manifest row and the live registry.
    /// <para>Before this file the string had two unrelated producers: Roslyn's <c>FullyQualifiedFormat</c> minus
    /// <c>global::</c> on the build tier and <c>Type.FullName + ", " + assembly</c> on the run tier. They agree
    /// only for non-nested, non-generic namespace types — a nested type spells <c>Ns.Outer.Inner</c> against
    /// <c>Ns.Outer+Inner</c>, a generic container <c>Ns.C&lt;T&gt;</c> against <c>Ns.C`1</c> — so every render of
    /// every template touching such an extension failed the gauntlet, permanently and without a build warning.</para>
    /// <para>Shared, netstandard2.0, linked into <c>Heddle.Generator</c>: no reflection, no Roslyn. Each side maps
    /// its own type representation into <em>per-segment metadata names</em> (<c>Type.Name</c> and
    /// <c>ISymbol.MetadataName</c> both already carry the backtick arity suffix) and this file owns the joins:
    /// <c>.</c> between the namespace and the outermost segment, <c>+</c> between nesting segments, <c>", "</c>
    /// before the assembly simple name.</para>
    /// </summary>
    internal static class AqnFormatter
    {
        /// <summary>The string a manifest row carries for a type the runtime could not resolve at all.</summary>
        internal const string Unknown = "<unknown>";

        /// <summary>Formats the CLR full name of a nominal type from its namespace and its nesting chain of
        /// metadata names (outermost declaring type first, the type itself last).</summary>
        internal static string FormatFullName(string @namespace, IReadOnlyList<string> metadataNamesOutermostFirst)
        {
            if (metadataNamesOutermostFirst == null || metadataNamesOutermostFirst.Count == 0)
                return null;

            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(@namespace))
                builder.Append(@namespace).Append('.');

            for (int i = 0; i < metadataNamesOutermostFirst.Count; i++)
            {
                if (i != 0)
                    builder.Append('+');
                builder.Append(metadataNamesOutermostFirst[i]);
            }

            return builder.ToString();
        }

        /// <summary>Formats the full manifest identity string. <paramref name="assemblyName"/> is the assembly's
        /// <em>simple</em> name (no version, culture or public key) — the sans-version half of the contract.</summary>
        internal static string Format(string @namespace, IReadOnlyList<string> metadataNamesOutermostFirst,
            string assemblyName)
        {
            var fullName = FormatFullName(@namespace, metadataNamesOutermostFirst);
            if (fullName == null)
                return Unknown;
            return fullName + ", " + (assemblyName ?? string.Empty);
        }
    }
}
