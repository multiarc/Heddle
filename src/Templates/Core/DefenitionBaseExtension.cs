using System.Threading;
using Templates.Data;
using Templates.Exceptions;

namespace Templates.Core {
    internal class DefenitionBaseExtension : AbstractExtension {
        public DefenitionBaseExtension DefenitionTemplate { get; set; }
        private readonly ThreadLocal<int> _recursionCount = new ThreadLocal<int>();
        private int _maxRecursionCount;

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            _maxRecursionCount = initContext.Context.Options.MaxRecursionCount;
            return base.InitStart(initContext, dataType, chainedType, parent);
        }

        public override object ProcessData(object data, object chained, object parent)
        {
            if (_recursionCount.Value >= _maxRecursionCount)
                throw new TemplateProcessingException("Recursion hit it's maximum");
            _recursionCount.Value++;
            chained = GetInnerResult(data, chained);
            var result = DefenitionTemplate?.ProcessData(data, chained, parent) ?? chained;
            _recursionCount.Value--;
            return result;
        }
    }
}