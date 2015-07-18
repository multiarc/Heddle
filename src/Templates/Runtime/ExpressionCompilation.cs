using Templates.Runtime.Parameters;

namespace Templates.Runtime
{
    internal class ExpressionCompilation: ExpressionOptions {
        public ExpressionCompilation(ExpressionOptions options)
        {
            ChainedType = options.ChainedType;
            Expression = options.Expression;
            ExtensionName = options.ExtensionName;
            ModelType = options.ModelType;
        }

        public int MethodNumber { get; set; }
        public IRuntimeParameter RuntimeCallParameter { get; set; }
    }
}