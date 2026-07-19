using System;
using System.Collections.Generic;

namespace Heddle.Data
{
    /// <summary>
    /// Phase 8 (D2): the immutable per-call-site name→index map of a parameter-declaring extension's layout.
    /// Built once at bind from the layout's ordered slot names; shared across renders and threads (never written
    /// after construction). <see cref="Scope.TryGetParameter"/> resolves through it into the carried values array.
    /// </summary>
    internal sealed class ExtensionParameterMap
    {
        private readonly Dictionary<string, int> _byName;

        internal ExtensionParameterMap(string[] orderedNames)
        {
            if (orderedNames == null)
                throw new ArgumentNullException(nameof(orderedNames));
            _byName = new Dictionary<string, int>(orderedNames.Length, StringComparer.Ordinal);
            for (int i = 0; i < orderedNames.Length; i++)
                _byName[orderedNames[i]] = i;
        }

        internal bool TryGetIndex(string name, out int index) => _byName.TryGetValue(name, out index);

        internal int Count => _byName.Count;
    }
}
