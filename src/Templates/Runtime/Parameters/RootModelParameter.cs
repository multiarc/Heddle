using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class RootModelParameter : IRuntimeParameter
    {
        private readonly Func<object, object> _compiledAccessor;

        public RootModelParameter(KeyValuePair<Type, PropertyInfo>[] getModelParameter)
        {
            _compiledAccessor = ModelParameter.GetPropertyChainAccessor(getModelParameter).Compile();
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetParameter(in Scope scope)
        {
            return _compiledAccessor(scope.RootData);
        }
    }
}