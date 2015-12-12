using Templates.Data;

namespace Templates.Runtime.Parameters
{
    internal class RootModelParameter : IRuntimeParameter
    {
        private readonly DynamicMethodGateDelegate[] _getModelParameter;

        public RootModelParameter(DynamicMethodGateDelegate[] getModelParameter)
        {
            _getModelParameter = getModelParameter;
        }

        public void Dispose()
        {
        }

        public object GetParameter(Scope scope)
        {
            var root = scope.RootData;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < _getModelParameter.Length; i++)
            {
                if (root == null)
                    break;
                root = _getModelParameter[i](root);
            }
            return root;
        }
    }
}