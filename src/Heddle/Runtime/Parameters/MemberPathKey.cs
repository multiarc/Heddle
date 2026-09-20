using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Heddle.Runtime.Parameters
{
    /// <summary>A member path by the hops it reads — the receiver type and property of each — for sharing one
    /// compiled accessor between the sites of a compile that read the same path. Hops are compared as the
    /// reflection objects they are, never by name: two loaded copies of an assembly have same-named types that an
    /// accessor built for one cannot read from the other.
    /// <para>The hash asks each hop for nothing but its own hash code, which every reflection implementation
    /// answers and which agrees with the equality used below. A metadata token does not: a published NativeAOT
    /// app serves reflection from a form that has none and throws when asked, which made every member read of a
    /// dynamic-tier compile there a failed compile.</para></summary>
    internal sealed class MemberPathKey : IEquatable<MemberPathKey>
    {
        private readonly (Type Type, PropertyInfo Property)[] _hops;
        private readonly int _hash;

        internal MemberPathKey(List<(Type Type, PropertyInfo Property)> hops)
        {
            _hops = hops.ToArray();
            int hash = _hops.Length;
            foreach (var hop in _hops)
                hash = unchecked((hash * 31 + RuntimeHelpers.GetHashCode(hop.Type)) * 31 + hop.Property.GetHashCode());
            _hash = hash;
        }

        public bool Equals(MemberPathKey other)
        {
            if (other == null || other._hops.Length != _hops.Length)
                return false;
            for (int i = 0; i < _hops.Length; i++)
            {
                if (!ReferenceEquals(_hops[i].Type, other._hops[i].Type) || _hops[i].Property != other._hops[i].Property)
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as MemberPathKey);

        public override int GetHashCode() => _hash;
    }
}
