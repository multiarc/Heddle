namespace Templates.Runtime.Parameters
{
    internal class EmptyParameter : IRuntimeParameter
    {
        public void Dispose()
        {
        }

        public object GetParameter(object value, object chainedResult, object rootValue)
        {
            return value;
        }
    }
}
