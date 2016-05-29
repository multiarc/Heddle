using System;
using System.Collections.Generic;
using Templates.Collections;

namespace Templates.Helpers
{
    internal static class EnumerableExtension {
        public static SmartList<T> ToSmartArray<T> (this IEnumerable<T> values)
        {
            var arr = values as T[];
            if (arr != null)
            {
                return new SmartList<T>(arr);
            }
            var result = new SmartList<T>();
            if (values != null) {
                foreach (T value in values)
                    result.Add(value);
            }
            return result;
        }

        public static T2 AddOrUpdate<T1, T2>(this IDictionary<T1, T2> src, T1 key, Func<T2> valueFactory, Action<T2> updateAction)
            where T2 : class
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));

            T2 existValue;
            if (src.TryGetValue(key, out existValue))
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