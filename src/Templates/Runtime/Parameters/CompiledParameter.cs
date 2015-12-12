using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class CompiledParameter : IRuntimeParameter
    {
        public CompiledMethodDelegate ParameterImplementation { get; set; }

        public void Dispose()
        {
        }

        public object GetParameter(Scope scope)
        {
            if (ParameterImplementation != null)
            {
                return ParameterImplementation(scope.ModelData, scope.ChainedData, scope.RootData);
            }
            return scope.ModelData;
        }
    }
}