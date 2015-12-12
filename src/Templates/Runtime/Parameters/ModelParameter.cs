using System;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class ModelParameter : IRuntimeParameter
    {
        private readonly DynamicMethodGateDelegate[] _getModelParameter;

        public ModelParameter(DynamicMethodGateDelegate[] getModelParameter)
        {
            _getModelParameter = getModelParameter;
        }

        public void Dispose()
        {
        }

        public object GetParameter(Scope scope)
        {
            var model = scope.ModelData;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < _getModelParameter.Length; i++)
            {
                if (model == null)
                    break;
                model = _getModelParameter[i](model);
            }
            return model;
        }
    }
}