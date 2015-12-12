using System;
using System.Runtime.CompilerServices;
using Templates.Data;

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

        public object GetParameter(Scope scope)
        {
            var root = scope.RootData;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < _dynamicModelParameter.Length; i++)
            {
                if (root == null)
                    break;
                var callSite = _dynamicModelParameter[i];
                root = callSite.Target(callSite, root);
            }
            return root;
        }
    }
}