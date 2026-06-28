using System;
using System.Runtime.CompilerServices;
using Heddle.Data;

namespace Heddle.Runtime.Parameters
{
    internal class CompiledParameter : IRuntimeParameter
    {
        public Func<object, object, object, object> ParameterImplementation { get; set; }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(in Scope scope)
        {
            return ParameterImplementation?.Invoke(scope.ModelData, scope.ChainedData, scope.RootData);
        }
    }
}