using System.Collections.Generic;
using System.Reflection;

namespace Templates.Runtime {
    internal static class GeneratedAssemblyCache {
        private static readonly Dictionary<string, Assembly> Cache = new Dictionary<string, Assembly>();

        public static Assembly TryGetCached(string code) {
            Assembly result;
            if (Cache.TryGetValue(code, out result)) {
                return result;
            }
            return null;
        }

        public static void AddToCache(string code, Assembly assembly) {
            Cache.Add(code, assembly);
        }
    }
}
