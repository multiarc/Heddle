using System.Collections.Generic;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Read-only view over the compiler's retained scope map.
    /// <see cref="GetModelTypesAt"/> returns all model types recorded for the innermost body span containing the
    /// offset — one entry per compiled call site — and, for an offset at the top level of the document, the
    /// document's own model type: the document is a scope too, the one no call body encloses.
    /// </summary>
    public sealed class ScopeMapView
    {
        private readonly IReadOnlyList<ScopeMapEntry> _entries;

        private readonly IReadOnlyList<KeyValuePair<int, int>> _bodies;

        internal ScopeMapView(ScopeMap map, ExType rootType = null,
            IReadOnlyList<KeyValuePair<int, int>> bodies = null)
        {
            _bodies = bodies;
            // Post-@model root type overrides the first-recorded entry.
            RootType = rootType ?? map?.RootType;
            _entries = map?.Entries ?? (IReadOnlyList<ScopeMapEntry>)System.Array.Empty<ScopeMapEntry>();
        }

        /// <summary>The root (document) model type, or null when the flag was off / nothing compiled.</summary>
        public ExType RootType { get; }

        /// <summary>
        /// All model types recorded for the innermost body span containing <paramref name="offset"/> — one per
        /// compiled call site. Empty when the offset is in no recorded body.
        /// </summary>
        public IReadOnlyList<ExType> GetModelTypesAt(int offset)
        {
            // Find the innermost (shortest) span containing the offset, return all model types recorded for it.
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

            // The document's own span is recorded when its compile starts — before any @model directive has
            // typed it — so the type on that entry is not the document's model; RootType is. And only the
            // document level is the document's scope: a body the compiler recorded nothing for (an abstract
            // definition nobody calls) has a model of its own that is simply not known.
            bool documentSpan = bestOffset < 0 ||
                (_entries.Count != 0 && bestOffset == _entries[0].Offset && bestLength == _entries[0].Length);
            if (documentSpan)
            {
                return RootType != null && IsDocumentLevel(offset)
                    ? new[] { RootType }
                    : System.Array.Empty<ExType>();
            }

            var result = new List<ExType>();
            foreach (var entry in _entries)
            {
                if (entry.Offset == bestOffset && entry.Length == bestLength && entry.ModelType != null)
                    result.Add(entry.ModelType);
            }

            return result;
        }

        /// <summary>Whether no <c>{{ … }}</c> body encloses <paramref name="offset"/>. The bodies are the
        /// parser's — a brace pair in a string, a comment, a raw block or plain text opens none.</summary>
        private bool IsDocumentLevel(int offset)
        {
            if (_bodies == null)
                return false;
            foreach (var body in _bodies)
            {
                if (offset > body.Key && offset <= body.Value)
                    return false;
            }

            return true;
        }
    }
}
