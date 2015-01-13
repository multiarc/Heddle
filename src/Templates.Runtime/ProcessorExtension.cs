using Templates.Data;

namespace Templates.Runtime {
    internal static class ProcessorExtension {
        public static object GetValueOrDefault (this PropertyGateDelegate gate, object obj)
        {
            return gate == null ? obj : gate(obj);
        }
    }
}