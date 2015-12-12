using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class RootModelParameter : IRuntimeParameter
    {
        private readonly DynamicMethodGateDelegate[] _getModelParameter;

        public RootModelParameter(DynamicMethodGateDelegate[] getModelParameter)
        {
            _getModelParameter = getModelParameter;
        }

        public void Dispose()
        {
        }

        public object GetParameter(object value, object chainedResult, object rootValue)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < _getModelParameter.Length; i++)
            {
                if (rootValue == null)
                    break;
                rootValue = _getModelParameter[i](value);
            }
            return rootValue;
        }
    }
}