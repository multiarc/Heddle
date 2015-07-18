namespace Templates.Runtime.Parameters
{
    internal class ConstantParameter : IRuntimeParameter
    {
        private readonly object _constantResult;

        public ConstantParameter(object constantResult)
        {
            _constantResult = constantResult;
        }

        public void Dispose()
        {
        }

        public object GetParameter(object value, object chainedResult)
        {
            return _constantResult;
        }
    }
}