using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime {
    internal class RuntimeCallParameter
    {
        private readonly TemplateChain _callParameterChain;
        private readonly DynamicMethodGateDelegate _getModelParameter;

        public RuntimeCallParameter(DynamicMethodGateDelegate getModelParameter, TemplateChain callParameterChain)
        {
            _callParameterChain = callParameterChain;
            _getModelParameter = getModelParameter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameterResult(object data, object chainedResult)
        {
            if (_getModelParameter == null && _callParameterChain == null)
            {
                return data;
            }
            if (_getModelParameter == null)
            {
                return _callParameterChain.ProcessData(data, chainedResult);
            }
            return _getModelParameter(data);
        }
    }
}
