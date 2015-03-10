using System;
using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime {
    internal class RuntimeCallParameter
    {
        private readonly IDataProcessor _callParameterChain;
        private readonly DynamicMethodGateDelegate _getModelParameter;
        public CompiledMethodDelegate ParameterImplementation { get; set; }

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
            if (ParameterImplementation != null)
            {
                return ParameterImplementation(value, chainedResult);
            }
            return _callParameterChain == null ? value : _callParameterChain.ProcessData(value, chainedResult);
        }
    }
}
