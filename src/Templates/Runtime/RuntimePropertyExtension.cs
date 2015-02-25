using System.Reflection;
using Templates.Data;

namespace Templates.Runtime {
    public static class RuntimePropertyExtension {
        public static DynamicMethodGateDelegate GetPropertyGate(this PropertyInfo property) {
            return GatesCache.GetPropertyGate(property);
        }
    }
}