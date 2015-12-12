using System;
using System.Runtime.CompilerServices;

namespace Templates.Runtime.Parameters
{
    internal class DynamicParameter : IRuntimeParameter
    {
        private readonly CallSite<Func<CallSite, object, object>>[] _dynamicModelParameter;

        public DynamicParameter(CallSite<Func<CallSite, object, object>>[] dynamicModelParameter)
        {
            _dynamicModelParameter = dynamicModelParameter;
        }

        public void Dispose()
        {
        }

        public object GetParameter(object value, object chainedResult, object rootValue)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < _dynamicModelParameter.Length; i++)
            {
                if (value == null)
                    break;
                var callSite = _dynamicModelParameter[i];
                value = callSite.Target(callSite, value);
            }
            return value;
        }
    }
}