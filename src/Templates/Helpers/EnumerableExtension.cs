using System;
using System.Collections.Generic;

namespace Templates.Helpers
{
    internal static class EnumerableExtension {
        public static T2 AddOrUpdate<T1, T2>(this IDictionary<T1, T2> src, T1 key, Func<T2> valueFactory, Action<T2> updateAction)
            where T2 : class
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));

            if (src.TryGetValue(key, out var existValue))
            {
                updateAction(existValue);
                return existValue;
            }
            var result = valueFactory();
            src.Add(key, result);
            return result;
        }
    }
}