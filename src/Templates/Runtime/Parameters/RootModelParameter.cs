using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class RootModelParameter : IRuntimeParameter
    {
        private readonly DynamicMethodGateDelegate _getModelParameter;

        public RootModelParameter(DynamicMethodGateDelegate getModelParameter)
        {
            _getModelParameter = getModelParameter;
        }

        public void Dispose()
        {
        }

        public object GetParameter(object value, object chainedResult, object rootValue)
        {
            return rootValue == null ? null : _getModelParameter(rootValue);
        }
    }
}