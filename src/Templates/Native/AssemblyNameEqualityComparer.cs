using System;
using System.Collections.Generic;
using System.Reflection;

namespace Templates.Native
{
    internal class AssemblyNameEqualityComparer : IEqualityComparer<AssemblyName>
    {
        public static AssemblyNameEqualityComparer Instance { get; } = new AssemblyNameEqualityComparer();

        public bool Equals(AssemblyName x, AssemblyName y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            if (x.Name != null && y.Name != null)
            {
                if (string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }
            }
            else if (x.Name != null || y.Name != null)
            {
                return false;
            }

            if (x.Version != null && y.Version != null)
            {
                if (x.Version != y.Version)
                {
                    return false;
                }
            }
            else if (!(x.Version == null) || !(y.Version == null))
            {
                return false;
            }

            if (x.CultureInfo != null && y.CultureInfo != null)
            {
                if (!x.CultureInfo.Equals(y.CultureInfo))
                {
                    return false;
                }
            }
            else if (x.CultureInfo != null || y.CultureInfo != null)
            {
                return false;
            }

            return x.GetPublicKeyToken().AreEqualsTo(y.GetPublicKeyToken());
        }

        public int GetHashCode(AssemblyName obj)
        {
            var hashCode = 0;
            if (obj.Name != null)
            {
                hashCode = obj.Name.GetHashCode();
            }

            if (obj.Version != null)
            {
                hashCode = (hashCode * 397) ^ obj.Version.GetHashCode();
            }

            if (obj.CultureInfo != null)
            {
                hashCode = (hashCode * 397) ^ obj.CultureInfo.GetHashCode();
            }

            var token = obj.GetPublicKeyToken();
            if (token != null)
            {
                hashCode = (hashCode * 97) ^ token.Length;

                if (token.Length > 0)
                    hashCode = (hashCode * 397) ^ BitConverter.ToUInt64(token, 0).GetHashCode();
            }

            return hashCode;
        }
    }
}