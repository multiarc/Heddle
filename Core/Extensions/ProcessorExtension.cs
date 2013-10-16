using Templates.Core.CompilerServices;

namespace Templates.Core.Extensions {
    internal static class ProcessorExtension {
        public static object GetValueOrDefault (this PropertyGateDelegate gate, object obj)
        {
            return gate == null ? obj : gate(obj);
        }
    }
}