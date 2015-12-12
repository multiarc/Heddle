using System;
using System.Runtime.CompilerServices;
using Templates.Data;

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

        public object GetParameter(Scope scope)
        {
            var model = scope.ModelData;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < _dynamicModelParameter.Length; i++)
            {
                if (model == null)
                    break;
                var callSite = _dynamicModelParameter[i];
                model = callSite.Target(callSite, model);
            }
            return model;
        }
    }
}