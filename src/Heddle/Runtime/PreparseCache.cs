using System.Collections.Concurrent;
using Heddle.Data;

namespace Heddle.Runtime
{
    /// <summary>
    /// One preparse outcome: what the expression evaluated to, its type, and the diagnostics producing it raised.
    /// <para>The diagnostics belong to the entry because the same generated code must report the same thing every
    /// time it is compiled. Caching only the value made diagnostics an accident of ordering — the first caller
    /// received them and every later one silently got none.</para>
    /// <para><b>Only the message survives; the position does not.</b> The key is the generated C#, which says
    /// nothing about where in which document the expression sits, so replaying a stored position stamped the
    /// first caller's coordinates onto every later one — a one-line document being told its error is on line
    /// four. Each caller re-stamps its own. This was a real regression the first time these were cached, and it
    /// was worse than the fault it replaced: an editor navigates by position.</para>
    /// <para><see cref="Generation"/> is the observed-assembly generation the entry was produced under. A
    /// failure that only failed because an assembly had not been registered yet must not outlive the
    /// registration — cached forever, it turned a fault that healed on the next compile into a permanent one
    /// decided by load order.</para>
    /// </summary>
    internal sealed class PreparseResult
    {
        public PreparseResult(OptionalValue<object> value, ExType type, string[] diagnostics, int generation)
        {
            Value = value;
            Type = type;
            Diagnostics = diagnostics;
            Generation = generation;
        }

        public OptionalValue<object> Value { get; }
        public ExType Type { get; }
        public string[] Diagnostics { get; }
        public int Generation { get; }

        public bool Failed => Diagnostics.Length > 0;
    }

    /// <summary>
    /// The C# tier's preparse results, keyed on the generated source. Held apart from
    /// <see cref="CSharpContext"/> so that dropping them costs nothing: an entry carries the expression's result
    /// <see cref="ExType"/>, and for an expression naming a workspace model type that is a <c>Type</c> from a
    /// collectible load context. Keeping one alive kept the whole context alive, so unloading a workspace's model
    /// assemblies freed nothing and every reload leaked another copy.
    /// </summary>
    internal static class PreparseCache
    {
        private static readonly ConcurrentDictionary<string, PreparseResult> Entries =
            new ConcurrentDictionary<string, PreparseResult>();

        internal static bool TryGet(string generatedCode, out PreparseResult result) =>
            Entries.TryGetValue(generatedCode, out result);

        internal static void Store(string generatedCode, PreparseResult result) =>
            Entries[generatedCode] = result;

        /// <summary>Drops every entry. Called when model assemblies are unregistered, because the types the
        /// entries name are about to go away with their load context.</summary>
        internal static void Clear() => Entries.Clear();
    }
}
