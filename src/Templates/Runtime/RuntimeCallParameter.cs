using System;
using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime {
    internal class RuntimeCallParameter
    {
        private readonly IDataProcessor _callParameterChain;
        private readonly DynamicMethodGateDelegate _getModelParameter;

        public RuntimeCallParameter(DynamicMethodGateDelegate getModelParameter, TemplateChain callParameterChain)
        {
            if (callParameterChain != null)
            {
                if (callParameterChain.ItemsToExecute.Count == 1)
                {
                    _callParameterChain = callParameterChain.ItemsToExecute[0];
                }
                else
                {
                    _callParameterChain = callParameterChain;
                }
            }
            _getModelParameter = getModelParameter;
        }

        public void Dispose()
        {
            _callParameterChain?.Dispose();
        }

        public object GetParameter(object value, object chainedResult)
        {
            if (_getModelParameter != null)
            {
                return _getModelParameter(value);
            }
            if (_callParameterChain == null) {
                return value;
            }
            return _callParameterChain.ProcessData(value, chainedResult);
        }
    }
}
