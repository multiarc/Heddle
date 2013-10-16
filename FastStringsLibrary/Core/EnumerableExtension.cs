using System.Collections.Generic;

namespace FastStrings.Core {
    public static class EnumerableExtension {
        public static SmartList<T> ToSmartArray<T> (this IEnumerable<T> values)
        {
            var result = new SmartList<T>();
            if (values != null) {
                foreach (T value in values)
                    result.Add(value);
            }
            return result;
        }
    }
}