using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;

namespace Heddle.Extensions {
    /// <summary>
    /// <para>Partial Template</para>
    /// <para>Optional parameter is sub-template (fully inclusive)</para>
    /// </summary>
    [ExtensionName ("partial")]
    public class PartialExtension: AbstractExtension
    {
        protected HeddleTemplate InnerTemplate;

        public override object ProcessData (in Scope scope)
        {
            return InnerTemplate?.Generate(scope.ModelData, scope.ChainedData);
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render(InnerTemplate?.Generate(scope.ModelData, scope.ChainedData));
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.CompileScope == null)
                throw new ArgumentNullException(nameof(initContext.CompileScope));
            base.InitStart(initContext, dataType, chainedType, parent);
            var nullScope = Scope.Null;
            initContext.ParameterTemplate = GetInnerResult(nullScope);
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
            InnerTemplate = new HeddleTemplate();
            var result = InnerTemplate.Compile(newScope.CompileContext);
            if (!result.Success)
            {
                newScope.CompileErrors.AddRange(result.Errors);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            InnerTemplate?.Dispose();
        }
    }
}