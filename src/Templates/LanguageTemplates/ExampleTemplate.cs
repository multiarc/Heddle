namespace Templates.Runtime
{
    public static class CSharpExpression
    {
        internal static object PreProcessData(ExpressionOptions model, decimal? chained)
        {
            return model.ExtensionName;
        }
    }
}