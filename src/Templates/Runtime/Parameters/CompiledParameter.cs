using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class CompiledParameter : IRuntimeParameter
    {
        public CompiledMethodDelegate ParameterImplementation { get; set; }

        public void Dispose()
        {
        }

        public object GetParameter(object value, object chainedResult, object rootValue)
        {
            if (ParameterImplementation != null)
            {
                return ParameterImplementation(value, chainedResult, rootValue);
            }
            return value;
        }
    }
}