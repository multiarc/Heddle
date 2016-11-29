using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class CompiledParameter : IRuntimeParameter
    {
        public CompiledMethodDelegate ParameterImplementation { get; set; }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(ref Scope scope)
        {
            return ParameterImplementation?.Invoke(scope.ModelData, scope.ChainedData, scope.RootData);
        }
    }
}