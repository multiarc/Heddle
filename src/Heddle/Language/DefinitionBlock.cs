using System.Collections.Generic;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// The definitions visible in a parse context.
    /// <para>A context is created from another far more often than its definitions are read: every body gets a
    /// context of its own that starts with its parent's definitions, and every output chain keeps an isolated
    /// copy of the context it was written in. Filling a table per context made parsing a page over a
    /// definitions library cost its calls times its definitions, so a block created from another only remembers
    /// where it came from — the parent block and how much of the parent's history it may see — and answers a
    /// lookup by asking the parent. What it sees never changes afterwards: a parent's later definitions and
    /// replacements are later entries in the parent's history, beyond what this block looks at.</para>
    /// <para><see cref="Definitions"/> is the whole table, built on first read. Code outside the engine may
    /// change that table directly, which no history records; so once it has been handed out, a block created
    /// from this one takes its copy immediately, as every block once did.</para>
    /// </summary>
    public class DefinitionBlock
    {
        public List<BlockPosition> Positions
        {
            get
            {
                if (System.Threading.Volatile.Read(ref _deferredPositions) != null)
                {
                    lock (ParseContext.SharedLock)
                    {
                        var source = _deferredPositions;
                        if (source != null)
                        {
                            var positions = source();
                            for (int i = 0; i < _deferredPositionCount && i < positions.Count; i++)
                                _positions.Insert(i, positions[i]);
                            System.Threading.Volatile.Write(ref _deferredPositions, null);
                        }
                    }
                }

                return _positions;
            }
        }

        private readonly List<BlockPosition> _positions;
        private System.Func<IList<BlockPosition>> _deferredPositions;
        private int _deferredPositionCount;

        /// <summary>Takes the first <paramref name="count"/> positions of another block when this one's are first
        /// read, rather than now.</summary>
        internal void DeferPositions(System.Func<IList<BlockPosition>> source, int count)
        {
            _deferredPositions = source;
            _deferredPositionCount = count;
        }

        internal void TakePositionsOf(DefinitionBlock other)
        {
            _deferredPositions = other._deferredPositions;
            _deferredPositionCount = other._deferredPositionCount;
        }

        private struct Change
        {
            internal string Name;
            internal DefinitionItem Item;
            // The isolation stamp the change was made under: an isolation stamped later saw it.
            internal long At;
        }

        // The block this one was created from, and how many of that block's changes it may see.
        private DefinitionBlock _parent;
        private int _parentChanges;

        // Set on the block of an isolated context: what it takes from its parent it takes as a private copy.
        private ParseContext.IsolationTree _copyTree;
        private long _copyAsOf;
        private Dictionary<string, DefinitionItem> _copies;

        // This block's own history. A null name marks a reset: nothing before it, the parent included, is seen.
        private readonly List<Change> _changes = new List<Change>();
        private Dictionary<string, List<int>> _changesByName;
        private int _lastReset = -1;

        private Dictionary<string, DefinitionItem> _definitions;
        private bool _handedOut;
        private long _handedOutAt;

        public DefinitionBlock(DefinitionBlock definitions = null)
        {
            _positions = new List<BlockPosition>();
            if (definitions == null)
                return;
            if (definitions._handedOut)
            {
                _definitions = new Dictionary<string, DefinitionItem>();
                foreach (var pair in definitions._definitions)
                    Set(pair.Key, pair.Value);
                return;
            }

            _parent = definitions;
            _parentChanges = definitions._changes.Count;
        }

        /// <summary>The block of an isolated context: everything <paramref name="source"/> shows now, each
        /// definition as a private copy whose body is isolated within <paramref name="tree"/> when first read.</summary>
        internal static DefinitionBlock IsolatedFrom(DefinitionBlock source, long asOf, ParseContext.IsolationTree tree)
        {
            var block = new DefinitionBlock();
            block._copyTree = tree;
            block._copyAsOf = asOf;
            // A table handed out may have been written to directly, which no history records; an isolation
            // older than the handing-out still reads the history, which is what it saw.
            if (source._handedOut && asOf > source._handedOutAt)
            {
                block._definitions = new Dictionary<string, DefinitionItem>();
                foreach (var pair in source._definitions)
                    block.Set(pair.Key, block.CopyOf(pair.Value), asOf);
                return block;
            }

            block._parent = source;
            block._parentChanges = source.ChangesAsOf(asOf);
            return block;
        }

        public void AddNewBlockPosition(BlockPosition position)
        {
            Positions.Add(position);
        }

        /// <summary>The whole table. Reading it builds it, and from then on it is the truth for this block.</summary>
        public Dictionary<string, DefinitionItem> Definitions
        {
            get
            {
                lock (ParseContext.SharedLock)
                {
                    if (!_handedOut)
                    {
                        _handedOutAt = ParseContext.CurrentIsolationStamp;
                        _handedOut = true;
                    }

                    return Table();
                }
            }
        }

        internal bool TryGet(string name, out DefinitionItem item)
        {
            lock (ParseContext.SharedLock)
            {
                if (_definitions != null)
                    return _definitions.TryGetValue(name, out item);
                item = Find(name, _changes.Count);
                return item != null;
            }
        }

        /// <summary>The definition an isolation stamped <paramref name="stamp"/> saw under the name.</summary>
        internal bool TryGetAsOf(string name, long stamp, out DefinitionItem item)
        {
            lock (ParseContext.SharedLock)
            {
                if (_handedOut && stamp > _handedOutAt)
                    return _definitions.TryGetValue(name, out item);
                item = Find(name, ChangesAsOf(stamp));
                return item != null;
            }
        }

        private int ChangesAsOf(long stamp)
        {
            int count = _changes.Count;
            while (count > 0 && _changes[count - 1].At >= stamp)
                count--;
            return count;
        }

        internal bool Contains(string name) => TryGet(name, out _);

        /// <summary>Adds the definition, or replaces the one registered under the name.</summary>
        internal void Set(string name, DefinitionItem item)
        {
            Set(name, item, ParseContext.CurrentIsolationStamp);
        }

        /// <summary>As <see cref="Set(string, DefinitionItem)"/>, for an entry an isolation stamped
        /// <paramref name="at"/> brings with it: it is part of what that isolation is, however late it is made.</summary>
        internal void Set(string name, DefinitionItem item, long at)
        {
            lock (ParseContext.SharedLock)
            {
                if (_definitions != null)
                    _definitions[name] = item;
                Record(name, item, at);
            }
        }

        /// <summary>Replaces everything with <paramref name="entries"/>, in their order.</summary>
        internal void Reset(IEnumerable<KeyValuePair<string, DefinitionItem>> entries)
        {
            var incoming = new List<KeyValuePair<string, DefinitionItem>>(entries);
            lock (ParseContext.SharedLock)
            {
                if (_definitions != null)
                    _definitions.Clear();
                _lastReset = _changes.Count;
                _changes.Add(new Change { At = ParseContext.CurrentIsolationStamp });
                foreach (var pair in incoming)
                    Set(pair.Key, pair.Value);
            }
        }

        /// <summary>Every entry in table order, without handing the table out.</summary>
        internal IEnumerable<KeyValuePair<string, DefinitionItem>> Entries()
        {
            lock (ParseContext.SharedLock)
                return Table();
        }

        /// <summary>Every name in table order. Builds nothing: a name needs no private copy.</summary>
        internal List<string> Names()
        {
            lock (ParseContext.SharedLock)
            {
                if (_definitions != null)
                    return new List<string>(_definitions.Keys);
                var names = new Dictionary<string, DefinitionItem>();
                Replay(names, _changes.Count, copy: false);
                return new List<string>(names.Keys);
            }
        }

        private void Record(string name, DefinitionItem item, long at)
        {
            if (_changesByName == null)
                _changesByName = new Dictionary<string, List<int>>();
            if (!_changesByName.TryGetValue(name, out var indexes))
            {
                indexes = new List<int>(1);
                _changesByName.Add(name, indexes);
            }

            indexes.Add(_changes.Count);
            _changes.Add(new Change { Name = name, Item = item, At = at });
        }

        private Dictionary<string, DefinitionItem> Table()
        {
            if (_definitions == null)
            {
                var table = new Dictionary<string, DefinitionItem>();
                Replay(table, _changes.Count, copy: true);
                _definitions = table;
            }

            return _definitions;
        }

        /// <summary>The definition registered under <paramref name="name"/> as this block stood after its first
        /// <paramref name="changes"/> changes, or null.</summary>
        private DefinitionItem Find(string name, int changes)
        {
            int reset = ResetBefore(changes);
            if (_changesByName != null && _changesByName.TryGetValue(name, out var indexes))
            {
                for (int i = indexes.Count - 1; i >= 0; i--)
                {
                    if (indexes[i] < changes)
                        return indexes[i] > reset ? _changes[indexes[i]].Item : null;
                }
            }

            if (reset >= 0 || _parent == null)
                return null;
            if (_copyTree == null)
                return _parent.Find(name, _parentChanges);
            if (_copies != null && _copies.TryGetValue(name, out var copy))
                return copy;
            var source = _parent.Find(name, _parentChanges);
            if (source == null)
                return null;
            copy = CopyOf(source);
            if (_copies == null)
                _copies = new Dictionary<string, DefinitionItem>();
            _copies.Add(name, copy);
            return copy;
        }

        private int ResetBefore(int changes)
        {
            if (_lastReset < 0)
                return -1;
            if (_lastReset < changes)
                return _lastReset;
            for (int i = changes - 1; i >= 0; i--)
            {
                if (_changes[i].Name == null)
                    return i;
            }

            return -1;
        }

        /// <summary>Rebuilds the table as it stood after the first <paramref name="changes"/> changes, by doing
        /// again what was done: the parent's entries in the parent's order, then this block's own changes, a
        /// replacement keeping the place of what it replaces.</summary>
        private void Replay(Dictionary<string, DefinitionItem> table, int changes, bool copy)
        {
            int reset = ResetBefore(changes);
            if (reset < 0 && _parent != null)
            {
                var inherited = new Dictionary<string, DefinitionItem>();
                _parent.Replay(inherited, _parentChanges, copy: copy);
                foreach (var pair in inherited)
                {
                    DefinitionItem item = pair.Value;
                    if (_copyTree != null && copy)
                    {
                        if (_copies == null)
                            _copies = new Dictionary<string, DefinitionItem>();
                        if (!_copies.TryGetValue(pair.Key, out item))
                        {
                            item = CopyOf(pair.Value);
                            _copies.Add(pair.Key, item);
                        }
                    }

                    table.Add(pair.Key, item);
                }
            }

            for (int i = reset + 1; i < changes; i++)
                table[_changes[i].Name] = _changes[i].Item;
        }

        private DefinitionItem CopyOf(DefinitionItem source)
        {
            var item = new DefinitionItem(source.AsOf(_copyAsOf), _copyAsOf);
            item.DeferContextIsolation(_copyTree, _copyAsOf);
            item.BaseDefinition?.DeferContextIsolation(_copyTree, _copyAsOf);
            return item;
        }
    }
}
