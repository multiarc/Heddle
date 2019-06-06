using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    [ExtensionName("using")]
    public class UsingExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.CompileScope == null)
                throw new ArgumentNullException(nameof(initContext.CompileScope));
            base.InitStart(initContext, dataType, chainedType, parent);
            var nullScope = Scope.Null;
            initContext.ParameterTemplate = GetInnerResult(ref nullScope);
            if (!string.IsNullOrWhiteSpace(initContext.ParameterTemplate))
                initContext.CompileScope.CSharpContext.ImportNamespace(initContext.ParameterTemplate);
            return null;
        }

        public override object ProcessData(ref Scope scope)
        {
            return null;
        }

        public override void RenderData(ref Scope scope)
        {
        }
    }
}