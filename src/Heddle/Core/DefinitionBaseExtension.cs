using System.Threading;
using Heddle.Data;
using Heddle.Exceptions;

namespace Heddle.Core
{
    internal class DefinitionBaseExtension : AbstractExtension
    {
        public DefinitionBaseExtension DefinitionParameterTemplate { get; set; }
        private readonly ThreadLocal<int> _recursionCount = new ThreadLocal<int>();
        private int _maxRecursionCount;

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            _maxRecursionCount = initContext.CompileScope.Options.MaxRecursionCount;
            return base.InitStart(initContext, dataType, chainedType, parent);
        }

        public override object ProcessData(in Scope scope)
        {
            if (_recursionCount.Value >= _maxRecursionCount)
                throw new TemplateProcessingException("Recursion hit it's maximum");
            _recursionCount.Value++;
            var chained = GetInnerResult(scope);
            var chainedData = scope.Chain(chained);
            var result = DefinitionParameterTemplate?.ProcessData(chainedData) ?? chained;
            _recursionCount.Value--;
            return result;
        }

        public override void RenderData(in Scope scope)
        {
            if (_recursionCount.Value >= _maxRecursionCount)
                throw new TemplateProcessingException("Recursion hit it's maximum");
            _recursionCount.Value++;
            if (DefinitionParameterTemplate != null)
            {
                var chained = GetInnerResult(scope);
                var chainedData = scope.Chain(chained);
                DefinitionParameterTemplate.RenderData(chainedData);
            }
            else
            {
                //var chained = GetInnerResult(ref scope);
                //scope.Render(chained);
                RenderInnerResult(scope);
            }

            _recursionCount.Value--;
        }
    }
}