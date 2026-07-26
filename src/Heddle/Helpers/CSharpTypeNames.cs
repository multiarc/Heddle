using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Heddle.Helpers
{
    /// <summary>
    /// Single source of truth for C# type aliases to prevent divergence. The numeric subset is kept
    /// in lockstep with <c>NumericKind</c> by <c>AliasTableLockstepTests</c>.
    /// </summary>
    internal static class CSharpTypeNames
    {
        /// <summary>The <c>dynamic</c> alias (mapped to <see cref="object"/> so display prints <c>object</c>);
        /// named constant to keep special handling greppable.</summary>
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

        /// <summary>Alias to CLR type (ordinal-keyed); <c>dynamic</c> resolves to <see cref="object"/>.</summary>
        public static IReadOnlyDictionary<string, Type> Aliases => AliasToType;

        /// <summary>The alias key list.</summary>
        public static IReadOnlyList<string> AliasNames => Names;

        public static bool TryGetType(string alias, out Type type)
        {
            if (alias == null)
            {
                type = null;
                return false;
            }

            return AliasToType.TryGetValue(alias, out type);
        }

        /// <summary>The reader-facing spelling of a type (C# alias or single-rank array). <c>false</c> when
        /// the type has no alias.</summary>
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
