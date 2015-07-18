using System;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class ModelParameter : IRuntimeParameter
    {
        private readonly DynamicMethodGateDelegate _getModelParameter;

        public ModelParameter(DynamicMethodGateDelegate getModelParameter)
        {
            if (getModelParameter == null)
                throw new ArgumentNullException(nameof(getModelParameter));

            _getModelParameter = getModelParameter;
        }

        public void Dispose()
        {
        }

        public object GetParameter(object value, object chainedResult)
        {
            return _getModelParameter(value);
        }
    }
}