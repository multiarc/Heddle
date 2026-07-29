using System.Collections.Generic;
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
    /// <para><see cref="Generation"/> is the observed-assembly generation the entry was produced under, taken
    /// together with the assembly set itself so the two cannot disagree
    /// (<c>AssemblyHelper.GetApplicationReferences</c>). It is the entry's whole identity beyond its key: an entry is
    /// served only at that generation and admitted only at the newest one.</para>
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
    /// <para><b>The cache holds one assembly generation at a time.</b> Everything it must not do follows from that
    /// single rule rather than from a second counter kept in step with the first: an entry computed against a
    /// superseded set is refused admission, an entry from a newer one retires the whole map on the way in, and a
    /// reader asking at a generation the map is not holding gets nothing and retires it. The rule is enforced under
    /// one monitor, so an unregistration cannot slip between a store deciding it is current and writing — which is
    /// exactly how a result computed against assemblies being unloaded used to land in the map that had just been
    /// emptied to release them.</para>
    /// </summary>
    internal static class PreparseCache
    {
        private static readonly object Gate = new object();

        private static readonly Dictionary<string, PreparseResult> Entries =
            new Dictionary<string, PreparseResult>();

        /// <summary>The generation every entry in <see cref="Entries"/> was computed against, or a sentinel below
        /// every real one while there are none. Nothing turns on which sentinel: an empty map serves nothing whatever
        /// generation it claims, and the first store adopts the caller's either way. It reads as "no generation held
        /// yet" rather than as a generation, and that is all it is for.</summary>
        private static int _generation = -1;

        /// <summary>
        /// Serves the entry for <paramref name="generatedCode"/> if the map is holding
        /// <paramref name="generation"/>, and retires it otherwise.
        /// <para>Both a failure and a success are bound to their generation. The failure case is obvious — an
        /// expression naming a type in an assembly the host had not registered yet fails, and cached forever that
        /// turns a fault which heals on the next compile into a permanent one decided by load order. The success case
        /// is the one it is tempting to skip: "nothing a later registration adds can take a type away" is wrong in
        /// the direction that matters, because adding an assembly can make a name ambiguous where it was not
        /// (<c>CS0104</c>) and can introduce a better overload candidate. An expression that compiled against the
        /// smaller set does not necessarily compile, or mean the same thing, against the larger one.</para>
        /// </summary>
        internal static bool TryGet(string generatedCode, int generation, out PreparseResult result)
        {
            lock (Gate)
            {
                if (generation != _generation)
                {
                    // Retiring on a miss rather than only on unregistration matters: refusing to read a spent entry
                    // is not the same as letting go of it, and one held but never read pins its load context just as
                    // hard as one that is served.
                    //
                    // Forward only. A reader reads the generation, then takes this lock, and an unregistration can
                    // land between the two — so an older generation than the map's does arrive here. It has nothing
                    // to be served (the map holds one generation and it is not this one) but it must not drag the
                    // map back to it, or the newer entries are thrown away and every reader on the current set
                    // recompiles until a store moves it forward again.
                    if (generation > _generation)
                        Retire(generation);
                    result = null;
                    return false;
                }

                return Entries.TryGetValue(generatedCode, out result);
            }
        }

        /// <summary>Admits a result at its own generation: refused outright if the set it was computed against has
        /// since been superseded, and retiring everything older if it is the first arrival from a newer one.</summary>
        internal static void Store(string generatedCode, PreparseResult result)
        {
            lock (Gate)
            {
                if (result.Generation < _generation)
                    return;
                if (result.Generation > _generation)
                    Retire(result.Generation);
                Entries[generatedCode] = result;
            }
        }

        /// <summary>Drops every entry and moves to <paramref name="generation"/>, refusing anything computed against
        /// an older set from that moment on. Called with the generation the assembly-set change produced, in the same
        /// expression that produces it, so the two cannot be sequenced wrongly or drift apart.</summary>
        internal static void Retarget(int generation)
        {
            lock (Gate)
                Retire(generation);
        }

        private static void Retire(int generation)
        {
            Entries.Clear();
            _generation = generation;
        }
    }
}
