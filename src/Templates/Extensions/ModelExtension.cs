using System;
using System.Linq;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Exceptions;

namespace Templates.Extensions
{
    [ExtensionName("model")]
    public class ModelExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.CompileScope == null)
                throw new ArgumentNullException(nameof(initContext.CompileScope));
            base.InitStart(initContext, dataType, chainedType, parent);
            initContext.ParameterTemplate = GetInnerResult(Scope.Null);
            if (!string.IsNullOrWhiteSpace(initContext.ParameterTemplate))
            {
                try
                {
                    ExType modelType = new ExType(initContext.ParameterTemplate, initContext.CompileScope.Namespaces);
                    initContext.CompileScope.ScopeType = modelType;
                    initContext.CompileScope.RootScopeType = modelType;
                }
                catch (InvalidOperationException e)
                {
                    initContext.CompileScope.CompileErrors.Add(e.ToError(Position));
                }
            }
            return null;
        }

        public override object ProcessData(Scope scope)
        {
            return null;
        }
    }
}