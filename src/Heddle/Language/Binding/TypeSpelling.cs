using System.Collections.Generic;
using System.Text;

namespace Heddle.Language.Binding
{
    /// <summary>Why a type spelling did not resolve. <see cref="None"/> means it did.</summary>
    internal enum TypeSpellingFault
    {
        None = 0,

        /// <summary>Empty, unbalanced angle brackets, or an empty generic argument.</summary>
        Malformed,

        /// <summary>No type of that name is reachable.</summary>
        Unresolved,

        /// <summary>Several types answer to the name and the imports do not settle it — the runtime's
        /// "the type name is ambigous" throw.</summary>
        Ambiguous,

        /// <summary>The generic definition takes a different number of type arguments than the spelling supplies.</summary>
        ArityMismatch
    }

    /// <summary>The per-tier type universe the shared spelling parser closes over.</summary>
    internal interface ITypeLookup<TType>
    {
        /// <summary>Resolves a non-generic, non-array, non-tuple name — a C# keyword alias, a dotted full name, or
        /// a bare short name — applying the imports and the ambiguity rule. <paramref name="backtickArity"/> is the
        /// declared arity when the spelling carried type arguments (<c>0</c> otherwise), so the lookup can ask for
        /// <c>Ns.C`1</c> rather than <c>Ns.C</c>.</summary>
        bool TryResolveSimple(string name, int backtickArity, out TType type, out TypeSpellingFault fault);

        TType MakeArray(TType elementType);

        /// <summary>Closes <paramref name="definition"/> over <paramref name="arguments"/>; false when the arity
        /// does not match.</summary>
        bool TryMakeGeneric(TType definition, IReadOnlyList<TType> arguments, out TType constructed);

        /// <summary>The <c>System.ValueTuple`n</c> definition for a tuple spelling, or false when unavailable.</summary>
        bool TryGetValueTupleDefinition(int arity, out TType definition);
    }

    /// <summary>
    /// The reflection-free type-spelling parser, shared.
    /// <para>It was already reflection-free imperative code inside <c>ReflectionHelper</c>
    /// (<c>ExtractGenericArguments</c>, <c>TryFindMatchingAngleBracket</c>, <c>SplitTopLevelArguments</c>) —
    /// directly extractable to share between tiers. The generator supported <b>none</b> of
    /// <c>List&lt;int&gt;</c>, <c>T[]</c>, <c>(int, string)</c> or dotted-nested spellings, so whole feature areas
    /// silently never precompiled: fallback-safe, but permanent.</para>
    /// <para>The grammar handled here: a dotted chain where any segment may carry a type-argument list
    /// (<c>Ns.Outer&lt;int&gt;.Inner&lt;string&gt;</c> → definition <c>Ns.Outer`1.Inner`1</c> closed over all
    /// arguments left to right — nested types inherit their outers' parameters, so the counts add up), an
    /// <c>[]</c> array suffix, and a parenthesised tuple spelling rewritten to <c>System.ValueTuple&lt;…&gt;</c>.</para>
    /// </summary>
    internal static class TypeSpelling
    {
        internal static bool TryResolve<TType>(string spelling, ITypeLookup<TType> lookup, out TType type,
            out TypeSpellingFault fault)
        {
            type = default;
            fault = TypeSpellingFault.None;
            if (lookup == null || spelling == null)
            {
                fault = TypeSpellingFault.Malformed;
                return false;
            }

            spelling = spelling.Trim();
            if (spelling.Length == 0)
            {
                fault = TypeSpellingFault.Malformed;
                return false;
            }

            // Tuple: "(a, b)" — the whole spelling parenthesised, split at top level.
            if (spelling[0] == '(' && spelling[spelling.Length - 1] == ')')
            {
                // Arity 1 is legal: `(int)` is `System.ValueTuple<int>`, which is what the reflection tier has
                // always resolved it to. The shared parser originally required two elements, so the build tier
                // refused a spelling the run tier accepted — a drift introduced by the extraction itself, which
                // was found and fixed when the runtime was folded onto this parser. An EMPTY element is still malformed: `()`
                // splits to one empty part, which fails when the element is resolved below.
                var parts = SplitTopLevelArguments(spelling.Substring(1, spelling.Length - 2));
                if (parts.Count == 0)
                {
                    fault = TypeSpellingFault.Malformed;
                    return false;
                }

                if (!lookup.TryGetValueTupleDefinition(parts.Count, out var tupleDefinition))
                {
                    fault = TypeSpellingFault.Unresolved;
                    return false;
                }

                return TryClose(parts, tupleDefinition, lookup, out type, out fault);
            }

            // Array: one "[]" suffix at a time, innermost element resolved first.
            if (spelling.EndsWith("[]", System.StringComparison.Ordinal))
            {
                var elementSpelling = spelling.Substring(0, spelling.Length - 2).TrimEnd();
                if (!TryResolve(elementSpelling, lookup, out var element, out fault))
                    return false;
                type = lookup.MakeArray(element);
                return true;
            }

            if (spelling.IndexOf('<') < 0)
                return lookup.TryResolveSimple(spelling, 0, out type, out fault);

            var argumentSpellings = ExtractGenericArguments(spelling, out var definitionName, out var totalArity);
            if (argumentSpellings == null)
            {
                fault = TypeSpellingFault.Malformed;
                return false;
            }

            if (!lookup.TryResolveSimple(definitionName, totalArity, out var definition, out fault))
                return false;

            return TryClose(argumentSpellings, definition, lookup, out type, out fault);
        }

        private static bool TryClose<TType>(IReadOnlyList<string> argumentSpellings, TType definition,
            ITypeLookup<TType> lookup, out TType type, out TypeSpellingFault fault)
        {
            type = default;
            var arguments = new TType[argumentSpellings.Count];
            for (int i = 0; i < argumentSpellings.Count; i++)
            {
                if (!TryResolve(argumentSpellings[i], lookup, out arguments[i], out fault))
                    return false;
            }

            if (!lookup.TryMakeGeneric(definition, arguments, out type))
            {
                fault = TypeSpellingFault.ArityMismatch;
                return false;
            }

            fault = TypeSpellingFault.None;
            return true;
        }

        /// <summary>Rewrites <c>Ns.Outer&lt;int&gt;.Inner&lt;string&gt;</c> into the backtick definition name
        /// <c>Ns.Outer`1.Inner`1</c> and returns the extracted argument spellings left to right; null when the
        /// spelling is malformed.</summary>
        internal static List<string> ExtractGenericArguments(string spelling, out string definitionName,
            out int totalArity)
        {
            var definition = new StringBuilder(spelling.Length);
            var arguments = new List<string>();
            totalArity = 0;
            definitionName = null;

            for (var i = 0; i < spelling.Length; i++)
            {
                if (spelling[i] != '<')
                {
                    definition.Append(spelling[i]);
                    continue;
                }

                if (!TryFindMatchingAngleBracket(spelling, i, out var close))
                    return null;

                var list = SplitTopLevelArguments(spelling.Substring(i + 1, close - i - 1));
                foreach (var part in list)
                    if (part.Length == 0)
                        return null;

                definition.Append('`').Append(list.Count);
                totalArity += list.Count;
                arguments.AddRange(list);
                i = close;
            }

            definitionName = definition.ToString();
            return arguments;
        }

        /// <summary>Finds the <c>&gt;</c> matching the <c>&lt;</c> at <paramref name="open"/>.</summary>
        internal static bool TryFindMatchingAngleBracket(string text, int open, out int close)
        {
            var depth = 0;
            for (close = open; close < text.Length; close++)
            {
                if (text[close] == '<') depth++;
                else if (text[close] == '>' && --depth == 0) return true;
            }

            close = -1;
            return false;
        }

        /// <summary>Splits an argument list on commas outside any <c>&lt;&gt;</c>, <c>()</c> or <c>[]</c> pair;
        /// parts are trimmed.</summary>
        internal static List<string> SplitTopLevelArguments(string argumentList)
        {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i < argumentList.Length; i++)
            {
                var c = argumentList[i];
                if (c == '<' || c == '(' || c == '[') depth++;
                else if (c == '>' || c == ')' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    parts.Add(argumentList.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            parts.Add(argumentList.Substring(start).Trim());
            return parts;
        }
    }
}
