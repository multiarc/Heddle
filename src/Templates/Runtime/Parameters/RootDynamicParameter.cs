using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class RootDynamicParameter : IRuntimeParameter
    {
        private readonly Func<object, object> _compiledAccessor;

        public RootDynamicParameter(string[] names)
        {
            _compiledAccessor = DynamicParameter.GetDynamicPropertyChainAccessor(names).Compile();
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(ref Scope scope)
        {
            return _compiledAccessor(scope.RootData);
        }
    }
}