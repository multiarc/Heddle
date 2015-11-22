using System;
using System.Runtime.CompilerServices;

namespace Templates.Runtime.Parameters
{
    internal class DynamicParameter : IRuntimeParameter
    {
        private readonly CallSite<Func<CallSite, object, object>> _dynamicModelParameter;

        public DynamicParameter(CallSite<Func<CallSite, object, object>> dynamicModelParameter)
        {
            _dynamicModelParameter = dynamicModelParameter;
        }

        public void Dispose()
        {
        }

        public object GetParameter(object value, object chainedResult)
        {
            return value == null ? null : _dynamicModelParameter.Target(_dynamicModelParameter, value);
        }
    }
}