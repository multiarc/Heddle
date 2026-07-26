using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Heddle.Helpers
{
    /// <summary>
    /// <para>Single source of truth for C# type aliases, stated once to prevent divergence. <see cref="Aliases"/>
    /// maps template-written aliases to CLR types; <see cref="TryGetDisplayName"/> maps types back to user-readable
    /// aliases for signatures and completion lists. The numeric subset is held in lockstep with
    /// <c>NumericKind</c> by <c>AliasTableLockstepTests</c>. Dependency-free netstandard2.0.</para>
    /// </summary>
    internal static class CSharpTypeNames
    {
        /// <summary>The <c>dynamic</c> alias, which shares <see cref="object"/>'s CLR type so the display
        /// direction prints <c>object</c> instead. Both tiers share this row to avoid silently diverging.
        /// Named as a constant so the one place it is treated specially is greppable.</summary>
        public const string DynamicAlias = "dynamic";

        private static readonly Dictionary<string, Type> AliasToType = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            { "bool", typeof(bool) },
            { "byte", typeof(byte) },
            { "sbyte", typeof(sbyte) },
            { "char", typeof(char) },
            { "decimal", typeof(decimal) },
            { "double", typeof(double) },
            { "float", typeof(float) },
            { "int", typeof(int) },
            { "uint", typeof(uint) },
            { "long", typeof(long) },
            { "ulong", typeof(ulong) },
            { "object", typeof(object) },
            { "short", typeof(short) },
            { "ushort", typeof(ushort) },
            { "string", typeof(string) },
            { DynamicAlias, typeof(object) }
        };

        private static readonly Dictionary<Type, string> TypeToAlias = BuildTypeToAlias();

        private static readonly ReadOnlyCollection<string> Names =
            new ReadOnlyCollection<string>(new List<string>(AliasToType.Keys));

        /// <summary>Alias → CLR type, ordinal-keyed. <c>dynamic</c> is present and resolves to
        /// <see cref="object"/>; every other key is its own primitive.</summary>
        public static IReadOnlyDictionary<string, Type> Aliases => AliasToType;

        /// <summary>The alias key list — the agreed shared boundary with the build tier's symbol-side map and
        /// with the parse-direction spelling parser.</summary>
        public static IReadOnlyList<string> AliasNames => Names;

        /// <summary>Resolves an alias a template wrote to its CLR type.</summary>
        public static bool TryGetType(string alias, out Type type)
        {
            if (alias == null)
            {
                type = null;
                return false;
            }

            return AliasToType.TryGetValue(alias, out type);
        }

        /// <summary>The reader-facing spelling of a type: its C# alias, or a single-rank array of one
        /// (<c>object[]</c>, the params shape function signatures print). <c>false</c> when the type has no
        /// alias — the caller then applies its own fallback, which legitimately differs by surface.</summary>
        public static bool TryGetDisplayName(Type type, out string name)
        {
            if (type != null)
            {
                if (TypeToAlias.TryGetValue(type, out name))
                    return true;

                if (type.IsArray && type.GetArrayRank() == 1 &&
                    TypeToAlias.TryGetValue(type.GetElementType(), out var element))
                {
                    name = element + "[]";
                    return true;
                }
            }

            name = null;
            return false;
        }

        private static Dictionary<Type, string> BuildTypeToAlias()
        {
            var map = new Dictionary<Type, string>();
            foreach (var pair in AliasToType)
            {
                if (pair.Key != DynamicAlias)
                    map[pair.Value] = pair.Key;
            }

            return map;
        }
    }
}
