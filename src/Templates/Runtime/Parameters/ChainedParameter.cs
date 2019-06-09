using System;
using System.Runtime.CompilerServices;
using Templates.Data;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(in Scope scope)
        {
            return _callParameterChain?.ProcessData(scope);
        }
    }
}