using Heddle.Runtime.Parameters;

namespace Heddle.Runtime
{
    internal class ExpressionCompilation: ExpressionOptions {
        public ExpressionCompilation(ExpressionOptions options)
        {
            ChainedType = options.ChainedType;
            Expression = options.Expression;
            ExtensionName = options.ExtensionName;
            ModelType = options.ModelType;
            Position = options.Position;
        }

        public int MethodNumber { get; set; }
        public IRuntimeParameter RuntimeCallParameter { get; set; }
    }
}