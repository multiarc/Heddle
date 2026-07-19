using System;
using System.Runtime.CompilerServices;
using Heddle.Data;

namespace Heddle.Runtime.Parameters
{
    /// <summary>
    /// A body-level prop read (D9): a direct <c>scope.PropsData[index]</c> load for a bare single-segment read,
    /// or the phase 1 null-safe property chain rooted at <c>Convert(slot, propType)</c> for a multi-hop read.
    /// Render never sees a name — the slot index is resolved at compile time.
    /// </summary>
    internal sealed class PropsSlotParameter : IRuntimeParameter
    {
        private readonly int _index;
        private readonly Func<object, object> _hops;

        internal PropsSlotParameter(int index, Func<object, object> hops = null)
        {
            _index = index;
            _hops = hops;
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(in Scope scope)
        {
            var slot = scope.PropsData[_index];
            return _hops == null ? slot : _hops(slot);
        }
    }
}
