using System;
using System.Runtime.CompilerServices;
using Heddle.Data;

namespace Heddle.Runtime.Parameters
{
    /// <summary>
    /// The props-aware sibling of <see cref="CompiledParameter"/> (whose 3-arg shape stays untouched). Emitted
    /// only when a native-expression tree contains a prop root (D9); prop-free expressions keep today's delegate
    /// shape bit-identically. The delegate receives <c>PropsData</c> as a fourth argument.
    /// </summary>
    internal sealed class PropsCompiledParameter : IRuntimeParameter
    {
        public Func<object, object, object, object[], object> ParameterImplementation { get; set; }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(in Scope scope)
        {
            return ParameterImplementation?.Invoke(scope.ModelData, scope.ChainedData, scope.RootData, scope.PropsData);
        }
    }
}
