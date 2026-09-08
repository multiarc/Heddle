using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Heddle.Data;

namespace Heddle.Runtime.Parameters
{
    internal class RootModelParameter : IRuntimeParameter
    {
        private readonly Func<object, object> _compiledAccessor;

        public RootModelParameter(IEnumerable<(Type, PropertyInfo)> getModelParameter)
        {
            _compiledAccessor = ModelParameter.GetPropertyChainAccessor(getModelParameter).Compile();
        }

        internal RootModelParameter(Func<object, object> accessor)
        {
            _compiledAccessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
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