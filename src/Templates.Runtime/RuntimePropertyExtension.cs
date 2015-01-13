using System.Reflection;
using Templates.Core.Data;

namespace Templates.Runtime {
    public static class RuntimePropertyExtension {
        public static PropertyGateDelegate GetPropertyGate(this PropertyInfo property) {
            return GatesCache.GetPropertyGate(property);
        }
    }
}