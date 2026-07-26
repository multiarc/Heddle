using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The retention contract around <see cref="SymbolTypeIndex"/>'s per-<see cref="Compilation"/> index (Q8.17).
    /// <para>Building the index walks every type of the compilation and of every referenced assembly, so reuse
    /// across the many model-type resolutions of one generator pass is worth having. Keeping the built indexes
    /// forever is not: a <see cref="Compilation"/> is immutable, the IDE creates a new one per keystroke-batch,
    /// and it never hands an older one back — so every entry except the newest is unreachable by construction and
    /// a strong-keyed dictionary merely pins it, along with the whole symbol universe behind it. (Editing a
    /// template and editing it back does <b>not</b> restore the earlier entry either: the reverted state is yet
    /// another new <see cref="Compilation"/> instance.)</para>
    /// <para><b>Eviction rule.</b> Two bounds, both stated here and both observable:</para>
    /// <list type="number">
    /// <item><description><b>Age.</b> A <i>generation</i> is one compilation admitted to the cache — an edit
    /// epoch. Every ask stamps the entry it serves with the current generation. An entry untouched for more than
    /// <see cref="MaxIdleGenerations"/> generations is evicted, whether or not the cache is full: the compilations
    /// that keep arriving are the evidence that the old one is behind, and the process needs no clock to see it.
    /// Hits do not advance the generation, so a busy project cannot age out its neighbour by asking a lot.</description></item>
    /// <item><description><b>Occupancy.</b> At most <see cref="Capacity"/> entries; admitting past it drops the
    /// least recently used. This is the hard bound, so retention is constant even if every entry keeps being
    /// touched.</description></item>
    /// </list>
    /// <para><b>Eviction cannot change an answer.</b> An entry is a pure function of its compilation, which is
    /// immutable, so a miss rebuilds a value indistinguishable from the one evicted. Generation counting is
    /// deliberately derived from the cache's own operation sequence and never from a clock: nothing on the
    /// generation path is read by emission, and no generator output depends on whether a lookup hit or missed.
    /// <c>SymbolTypeIndexCacheTests.EvictionCannotChangeWhatTheIndexAnswers</c> pins that.</para>
    /// </summary>
    internal sealed class SymbolTypeIndexCache
    {
        /// <summary>The process-wide cache <see cref="SymbolTypeIndex.For"/> serves from.</summary>
        internal static readonly SymbolTypeIndexCache Shared = new SymbolTypeIndexCache();

        private readonly object _gate = new object();

        private readonly Dictionary<Compilation, Entry> _entries = new Dictionary<Compilation, Entry>();

        /// <summary>Keys in most-recently-used order, so the occupancy bound has a total order to evict by.
        /// Bounded by <see cref="Capacity"/>, which is small; a list beats the bookkeeping of anything cleverer.</summary>
        private readonly List<Compilation> _order = new List<Compilation>();

        private int _generation;

        /// <summary>A handful of projects' worth of indexes, and no more.</summary>
        private const int DefaultCapacity = 8;

        /// <summary>Two edit epochs of tolerance: enough that an interleaved build of two projects keeps both
        /// indexes, small enough that a session of edits does not accumulate them.</summary>
        private const int DefaultMaxIdleGenerations = 2;

        internal SymbolTypeIndexCache() : this(DefaultCapacity, DefaultMaxIdleGenerations) { }

        internal SymbolTypeIndexCache(int capacity, int maxIdleGenerations)
        {
            Capacity = capacity < 1 ? 1 : capacity;
            MaxIdleGenerations = maxIdleGenerations < 0 ? 0 : maxIdleGenerations;
        }

        /// <summary>The most compilations that may hold an index at once.</summary>
        internal int Capacity { get; }

        /// <summary>How many newer compilations may be admitted before an untouched entry counts as stale.</summary>
        internal int MaxIdleGenerations { get; }

        /// <summary>How many compilations currently hold an index.</summary>
        internal int Count
        {
            get { lock (_gate) return _entries.Count; }
        }

        /// <summary>The number of compilations admitted so far — the age clock, exposed so the staleness rule is
        /// observable rather than inferred.</summary>
        internal int Generation
        {
            get { lock (_gate) return _generation; }
        }

        /// <summary>Whether this compilation currently holds an index.</summary>
        internal bool Contains(Compilation compilation)
        {
            if (compilation == null)
                return false;
            lock (_gate) return _entries.ContainsKey(compilation);
        }

        /// <summary>Drops every entry. The next ask rebuilds; nothing observable changes but occupancy.</summary>
        internal void Clear()
        {
            lock (_gate)
            {
                _entries.Clear();
                _order.Clear();
            }
        }

        /// <summary>The index for this compilation, built on first ask and reused until it is evicted. A
        /// <c>null</c> compilation is answered with an empty index and is never admitted.</summary>
        internal SymbolTypeIndex Get(Compilation compilation)
        {
            if (compilation == null)
                return SymbolTypeIndex.Build(null);

            lock (_gate)
            {
                if (_entries.TryGetValue(compilation, out var cached))
                {
                    // No sweep on a hit: staleness is measured in generations, a hit does not advance the
                    // generation, and refreshing one entry cannot make another one stale.
                    cached.LastUsedGeneration = _generation;
                    Touch(compilation);
                    return cached.Index;
                }

                _generation++;
                EvictStale();
                while (_entries.Count >= Capacity && _order.Count > 0)
                    Remove(_order[_order.Count - 1]);

                var built = SymbolTypeIndex.Build(compilation);
                _entries[compilation] = new Entry(built, _generation);
                Touch(compilation);
                return built;
            }
        }

        /// <summary>Drops every entry that has sat unused for more than <see cref="MaxIdleGenerations"/>
        /// generations. Runs on admission — the only moment at which an entry can <i>become</i> stale, since the
        /// generation advances there and nowhere else — so staleness is acted on before the capacity bound is,
        /// and an entry can leave while the cache is nearly empty.</summary>
        private void EvictStale()
        {
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                var key = _order[i];
                if (_generation - _entries[key].LastUsedGeneration > MaxIdleGenerations)
                {
                    _entries.Remove(key);
                    _order.RemoveAt(i);
                }
            }
        }

        private void Touch(Compilation compilation)
        {
            _order.Remove(compilation);
            _order.Insert(0, compilation);
        }

        private void Remove(Compilation compilation)
        {
            _entries.Remove(compilation);
            _order.Remove(compilation);
        }

        private sealed class Entry
        {
            internal Entry(SymbolTypeIndex index, int generation)
            {
                Index = index;
                LastUsedGeneration = generation;
            }

            internal SymbolTypeIndex Index { get; }

            internal int LastUsedGeneration { get; set; }
        }
    }
}
