using System;

namespace Templates.Runtime.Parameters
{
    internal class ChainedParameter : IRuntimeParameter
    {
        private readonly IDataProcessor _callParameterChain;

        public ChainedParameter(TemplateChain callParameterChain)
        {
            if (callParameterChain == null)
                throw new ArgumentNullException(nameof(callParameterChain));

            if (callParameterChain.ItemsToExecute.Count == 1)
            {
                _callParameterChain = callParameterChain.ItemsToExecute[0];
            }
            else
            {
                _callParameterChain = callParameterChain;
            }
        }

        public void Dispose()
        {
            _callParameterChain.Dispose();
        }

        public object GetParameter(object value, object chainedResult)
        {
            return _callParameterChain == null ? value : _callParameterChain.ProcessData(value, chainedResult);
        }
    }
}