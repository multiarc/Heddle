using System.Collections.Generic;

namespace Templates.Native
{
    internal static class ArrayExtensions
    {
        public static bool AreEqualsTo(this byte[] left, byte[] right)
        {
            if (left == right)
                return true;
            if (right == null || left == null)
            {
                return false;
            }

            if (right.Length != left.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static T GetValueOrDefault<T, TKey>(this IReadOnlyDictionary<TKey, T> dict, TKey key)
        {
            if (dict.TryGetValue(key, out var result))
            {
                return result;
            }

            return default;
        }
    }
}