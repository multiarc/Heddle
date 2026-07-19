using System.Collections.Generic;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Read-only view over the compiler's retained scope map (phase 6 D2).
    /// <see cref="GetModelTypesAt"/> returns all model types recorded for the innermost body span containing the
    /// offset — one entry per compiled call site (D13).
    /// </summary>
    public sealed class ScopeMapView
    {
        private readonly IReadOnlyList<ScopeMapEntry> _entries;

        internal ScopeMapView(ScopeMap map, ExType rootType = null)
        {
            // The root type after compile (post-@model) overrides the first-recorded entry, whose model was the
            // pre-directive document scope.
            RootType = rootType ?? map?.RootType;
            _entries = map?.Entries ?? (IReadOnlyList<ScopeMapEntry>)System.Array.Empty<ScopeMapEntry>();
        }

        /// <summary>The root (document) model type, or null when the flag was off / nothing compiled.</summary>
        public ExType RootType { get; }

        /// <summary>
        /// All model types recorded for the innermost body span containing <paramref name="offset"/> — one per
        /// compiled call site (the artificial-type set of D13). Empty when the offset is in no recorded body.
        /// </summary>
        public IReadOnlyList<ExType> GetModelTypesAt(int offset)
        {
            // The innermost containing span is the shortest span that contains the offset. Multiple entries may
            // share that exact span (one per call site) — return every model type recorded for it.
            int bestLength = int.MaxValue;
            int bestOffset = -1;
            foreach (var entry in _entries)
            {
                if (offset < entry.Offset || offset >= entry.Offset + entry.Length)
                    continue;
                if (entry.Length < bestLength)
                {
                    bestLength = entry.Length;
                    bestOffset = entry.Offset;
                }
            }

            if (bestOffset < 0)
                return System.Array.Empty<ExType>();

            var result = new List<ExType>();
            foreach (var entry in _entries)
            {
                if (entry.Offset == bestOffset && entry.Length == bestLength && entry.ModelType != null)
                    result.Add(entry.ModelType);
            }

            return result;
        }
    }
}
