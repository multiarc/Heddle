using System.Collections.Generic;
using System.Text;

namespace Heddle.Precompiled
{
    /// <summary>The single implementation of manifest identity formatting: CLR full type name,
    /// assembly simple name (no version). Nested types use <c>+</c> joins; namespaces and top-level use <c>.</c>.</summary>
    internal static class AqnFormatter
    {
        /// <summary>The string a manifest row carries for a type the runtime could not resolve at all.</summary>
        internal const string Unknown = "<unknown>";

        /// <summary>Formats the CLR full name from namespace and nesting chain of metadata names (outermost first).</summary>
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

        /// <summary>Formats the full manifest identity string.
        /// <paramref name="assemblyName"/> is the assembly's simple name (no version, culture, or public key).</summary>
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
