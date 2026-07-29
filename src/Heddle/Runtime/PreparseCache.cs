using System.Collections.Concurrent;
using System.Threading;
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
        public PreparseResult(OptionalValue<object> value, ExType type, string[] diagnostics, int generation,
            int epoch)
        {
            Value = value;
            Type = type;
            Diagnostics = diagnostics;
            Generation = generation;
            Epoch = epoch;
        }

        public OptionalValue<object> Value { get; }
        public ExType Type { get; }
        public string[] Diagnostics { get; }
        public int Generation { get; }

        /// <summary>The <see cref="PreparseCache.Epoch"/> in force when this entry's compile began.</summary>
        public int Epoch { get; }

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

        private static int _epoch;

        /// <summary>
        /// Counts the times the cache has been dropped. An entry is produced by a compile that takes measurable time,
        /// and one already running when the drop happens still stores its result afterwards — into the map that was
        /// just emptied. Emptying alone therefore does not mean the next reader sees nothing: it can see exactly the
        /// entry the drop existed to remove, naming a type from the load context that has since gone.
        /// <para>So a reader compares epochs rather than trusting the map. A compile stamps the epoch it began under,
        /// and an entry stamped with an older one is not served no matter when it arrived.</para>
        /// </summary>
        internal static int Epoch => Volatile.Read(ref _epoch);

        /// <summary>Serves an entry only if the cache has not been dropped since its compile began, and removes it
        /// otherwise: an entry from a spent epoch names types from a load context on its way out, and refusing to
        /// read it while still holding it defeats the point of dropping the cache at all.</summary>
        internal static bool TryGet(string generatedCode, out PreparseResult result)
        {
            if (!Entries.TryGetValue(generatedCode, out result))
                return false;
            if (result.Epoch == Epoch)
                return true;

            Entries.TryRemove(generatedCode, out _);
            result = null;
            return false;
        }

        /// <summary>Stores a result only if its epoch is still current. A compile that began before a drop is exactly
        /// the one that would otherwise refill the emptied map, and the entry it wants to add is the one holding the
        /// context alive.</summary>
        internal static void Store(string generatedCode, PreparseResult result)
        {
            if (result.Epoch == Epoch)
                Entries[generatedCode] = result;
        }

        /// <summary>Drops every entry and moves the epoch on. Called when model assemblies are unregistered, because
        /// the types the entries name are about to go away with their load context.
        /// <para>Epoch first, empty second. A reader looks at the map before it looks at the epoch, so emptying
        /// first leaves a window where it can find an entry and then read an epoch that still matches it.</para>
        /// </summary>
        internal static void Clear()
        {
            Interlocked.Increment(ref _epoch);
            Entries.Clear();
        }
    }
}
