using System.Collections.Generic;

namespace Templates.Collections {
    public static class EnumerableExtension {
        public static SmartList<T> ToSmartList<T> (this IEnumerable<T> values)
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