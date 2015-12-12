using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class EmptyParameter : IRuntimeParameter
    {
        public void Dispose()
        {
        }

        public object GetParameter(Scope scope)
        {
            return scope.ModelData;
        }
    }
}
