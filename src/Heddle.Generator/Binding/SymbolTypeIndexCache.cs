using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// Per-<see cref="Compilation"/> index cache with bounded retention. Every compilation is immutable and
    /// unreachable once newer ones arrive, so entries must evict: by age (untouched beyond
    /// <see cref="MaxIdleGenerations"/> generations) and by occupancy (at most <see cref="Capacity"/> entries,
    /// least recently used evicted first). Eviction is safe: every miss rebuilds a value indistinguishable from
    /// the one evicted, since entries are pure functions of immutable compilations.
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

        /// <summary>Capacity: enough for concurrent projects, bounded to avoid accumulation during editing.</summary>
        private const int DefaultCapacity = 8;

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

        /// <summary>Gets or builds the index, cached until eviction. Null compilation returns empty index, never cached.</summary>
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
