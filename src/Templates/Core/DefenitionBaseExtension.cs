using System;
using System.Threading;
using Templates.Data;
using Templates.Exceptions;

namespace Templates.Core {
    internal class DefenitionBaseExtension : AbstractExtension {
        public DefenitionBaseExtension DefenitionParameterTemplate { get; set; }
        private readonly ThreadLocal<int> _recursionCount = new ThreadLocal<int>();
        private int _maxRecursionCount;

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            _maxRecursionCount = initContext.Context.Options.MaxRecursionCount;
            return base.InitStart(initContext, dataType, chainedType, parent);
        }

        public override object ProcessData(object data, object chained, object parent, object root)
        {
            if (_recursionCount.Value >= _maxRecursionCount)
                throw new TemplateProcessingException("Recursion hit it's maximum");
            _recursionCount.Value++;
            chained = GetInnerResult(data, chained, root);
            var result = DefenitionParameterTemplate?.ProcessData(data, chained, parent, root) ?? chained;
            _recursionCount.Value--;
            return result;
        }

        public override object ProcessData(object data, object chained, object parent, Func<object, object, string> getInnerResult)
        {
            return null;
        }
    }
}