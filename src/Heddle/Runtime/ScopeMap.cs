using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Runtime
{
    /// <summary>
    /// One recorded body-compile span with effective model and chained types; definition bodies
    /// compile once per call site, so one span can carry multiple entries (one per site's type).
    /// </summary>
    internal readonly struct ScopeMapEntry
    {
        public ScopeMapEntry(int offset, int length, ExType modelType, ExType chainedType)
        {
            Offset = offset;
            Length = length;
            ModelType = modelType;
            ChainedType = chainedType;
        }

        public int Offset { get; }

        public int Length { get; }

        public ExType ModelType { get; }

        public ExType ChainedType { get; }
    }

    /// <summary>
    /// Append-only retention of per-body-span model/chained types; reference-copied via private ctor so
    /// child compiles share one engine-accurate map. Compile-time state, never read at render time.
    /// </summary>
    internal sealed class ScopeMap
    {
        private readonly List<ScopeMapEntry> _entries = new List<ScopeMapEntry>();

        /// <summary>The root (document) model type, recorded once at the first body compile.</summary>
        public ExType RootType { get; private set; }

        /// <summary>All recorded entries, in record (document-compile) order.</summary>
        public IReadOnlyList<ScopeMapEntry> Entries => _entries;

        /// <summary>Records one body-compile span with its effective types (the single recording site).</summary>
        public void Record(int offset, int length, ExType modelType, ExType chainedType)
        {
            RootType ??= modelType;
            _entries.Add(new ScopeMapEntry(offset, length, modelType, chainedType));
        }
    }
}
