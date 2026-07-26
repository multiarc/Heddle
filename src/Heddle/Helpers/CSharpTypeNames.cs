using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Heddle.Helpers
{
    /// <summary>
    /// <para>The C# type-alias table, stated once (generator plan phase 6 D7). Two projections of one data
    /// source: <see cref="Aliases"/> maps an alias a template may <i>write</i> (<c>int</c>, <c>string</c>,
    /// <c>dynamic</c>) to its CLR type, and <see cref="TryGetDisplayName"/> maps a CLR type back to the alias a
    /// user should <i>read</i> in signature text, hover cards and completion lists.</para>
    /// <para>Before this file the same knowledge existed as five tables across four projects whose key sets
    /// already differed, so adding an alias changed what a template could write without changing what the build
    /// tier could bind or what an error message displayed. The build tier's Roslyn-side map
    /// (<c>SymbolTypeResolver</c>) cannot share the <see cref="Type"/> values, so it stays an adapter keyed on
    /// <see cref="AliasNames"/> — a key-set equality, and since phase 3 (Q3.5) a
    /// <i>value</i> agreement, that its own test asserts, which turns a silent three-way divergence into a red
    /// build. <b>There is no symbol-side exclusion any more:</b> phase 6 shipped one, <c>dynamic</c>, and phase 3
    /// removed it — the run tier resolves <c>dynamic</c> to <c>typeof(object)</c>, so a build-tier-only refusal
    /// of the spelling contradicted the match principle. The alias key sets are equal in full.</para>
    /// <para>The numeric subset of this table and phase 4's <c>NumericKind</c> lattice are held in lockstep by
    /// <c>AliasTableLockstepTests</c>: every alias whose CLR type has a numeric kind, and every numeric kind, must
    /// account for each other, so an <c>nint</c>/<c>nuint</c> addition cannot land on one side alone.</para>
    /// <para>Dependency-free and netstandard2.0: the generator links this file, and a later phase's
    /// spelling parser consumes <see cref="Aliases"/> without restructuring it.</para>
    /// </summary>
    internal static class CSharpTypeNames
    {
        /// <summary>The <c>dynamic</c> alias. It is the one alias that does not own its CLR type — it shares
        /// <see cref="object"/>'s — so the display direction skips it and prints <c>object</c>. It is <b>not</b> a
        /// symbol-side exclusion: phase 3 (Q3.5) gave the build tier the same row, mapped to
        /// <c>System.Object</c>. Named rather than spelled inline so the one place it is treated specially is
        /// greppable from the lockstep tests.</summary>
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

        // The display direction. `dynamic` shares typeof(object) with `object`, and `object` is the spelling a
        // reader expects for a CLR type, so the display map carries the alias set minus that one duplicate.
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
