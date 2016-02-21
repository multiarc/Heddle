using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Partial Template</para>
    /// <para>Optional parameter is sub-template (fully inclusive)</para>
    /// </summary>
    [ExtensionName ("partial")]
    public class PartialExtension: AbstractExtension
    {
        protected TtlTemplate InnerTemplate;

        public override object ProcessData (Scope scope)
        {
            return InnerTemplate?.Generate(scope.ModelData, scope.ChainedData);
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.CompileScope == null)
                throw new ArgumentNullException(nameof(initContext.CompileScope));
            base.InitStart(initContext, dataType, chainedType, parent);
            initContext.ParameterTemplate = GetInnerResult(Scope.Null);
            if (!string.IsNullOrEmpty(initContext.ParameterTemplate)) {
                string templateName = initContext.ParameterTemplate.Trim();
                if (!string.IsNullOrEmpty(templateName))
                {
                    initContext.CompileScope.CompileContext.AddDelayedCompileTemplate(
                        new CompileScope(new CompileContext(initContext.CompileScope.CompileContext, dataType, templateName),
                            initContext.CompileScope.CSharpContext), initContext.ParseContext, this);
                }
            }
            return typeof (string);
        }

        public override void CompleteInit(CompileScope newScope, ParseContext parseContext)
        {
            InnerTemplate = new TtlTemplate();
            var result = InnerTemplate.Compile(newScope.CompileContext);
            if (!result.Success)
            {
                newScope.CompileErrors.AddRange(result.Errors);
            }
        }
    }
}