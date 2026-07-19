using System;
using System.Collections.Generic;

namespace Heddle.Data
{
    /// <summary>
    /// <para>The per-body-execution local-context frame reached through <see cref="Scope"/> (phase 3 D4).</para>
    /// <para>Holds a dedicated inline slot for the branch protocol (<see cref="BranchState"/>) plus a lazily
    /// created overflow map for arbitrary public keys — the first publish of a user key costs one dictionary
    /// allocation; the branch protocol never touches the map. Identical code on every target framework.</para>
    /// <para>Unsynchronized by design: one instance belongs to one body execution of one render lineage, so it
    /// is never shared across threads. All key validation (null key, reserved prefix, <see cref="BranchState"/>
    /// type check) lives in <see cref="Scope.Publish"/>/<see cref="Scope.TryRead"/> — the frame trusts its
    /// callers.</para>
    /// </summary>
    internal sealed class ScopeLocals
    {
        private BranchState? _branch;
        private Dictionary<string, object> _overflow;

        internal void SetBranch(in BranchState state)
        {
            _branch = state;
        }

        internal bool TryGetBranch(out BranchState state)
        {
            if (_branch.HasValue)
            {
                state = _branch.Value;
                return true;
            }

            state = default;
            return false;
        }

        internal void ClearBranch()
        {
            _branch = null;
        }

        internal void Set(string key, object value)
        {
            _overflow ??= new Dictionary<string, object>(StringComparer.Ordinal);
            _overflow[key] = value;
        }

        internal bool TryGet(string key, out object value)
        {
            if (_overflow != null)
                return _overflow.TryGetValue(key, out value);

            value = null;
            return false;
        }
    }
}
