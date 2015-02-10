using System;
using System.Collections.Generic;
using Templates.Collections;
using Templates.Strings.Core;

namespace Templates.Helpers
{
    public static class EnumerableExtension {
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
    }
}