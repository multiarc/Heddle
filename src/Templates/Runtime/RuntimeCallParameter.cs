using System;
using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime {
    internal class RuntimeCallParameter
    {
        private readonly IDataProcessor _callParameterChain;
        private readonly DynamicMethodGateDelegate _getModelParameter;
        private readonly CallSite<Func<CallSite, object, object>> _dynamicModelParameter;
        public CompiledMethodDelegate ParameterImplementation { get; set; }

        public RuntimeCallParameter(DynamicMethodGateDelegate getModelParameter = null, TemplateChain callParameterChain = null, CallSite<Func<CallSite, object, object>> dynamicModelParameter = null)
        {
            _dynamicModelParameter = dynamicModelParameter;
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
            if (_dynamicModelParameter != null) {
                return _dynamicModelParameter.Target(_dynamicModelParameter, value);
            }
            if (ParameterImplementation != null)
            {
                return ParameterImplementation(value, chainedResult);
            }
            return _callParameterChain == null ? value : _callParameterChain.ProcessData(value, chainedResult);
        }
    }
}
