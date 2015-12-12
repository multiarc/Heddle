using System;
using System.Runtime.CompilerServices;

namespace Templates.Runtime.Parameters
{
    internal class RootDynamicParameter : IRuntimeParameter
    {
        private readonly CallSite<Func<CallSite, object, object>>[] _dynamicModelParameter;

        public RootDynamicParameter(CallSite<Func<CallSite, object, object>>[] dynamicModelParameter)
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
                if (rootValue == null)
                    break;
                var callSite = _dynamicModelParameter[i];
                rootValue = callSite.Target(callSite, rootValue);
            }
            return rootValue;
        }
    }
}