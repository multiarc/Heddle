using System.Reflection;
using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime {
    public static class RuntimePropertyExtension {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DynamicMethodGateDelegate ToPropertyGate(this PropertyInfo property) {
            return GatesCache.GetPropertyGate(property);
        }
    }
}