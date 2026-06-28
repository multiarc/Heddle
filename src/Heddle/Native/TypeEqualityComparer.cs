using System;
using System.Collections.Generic;

namespace Heddle.Native
{
    internal class TypeEqualityComparer : IEqualityComparer<Type>
    {
        public static TypeEqualityComparer Instance { get; } = new TypeEqualityComparer();

        public bool Equals(Type x, Type y)
        {
            return x == y;
        }

        public int GetHashCode(Type obj) => obj.GetHashCode();

    }
}