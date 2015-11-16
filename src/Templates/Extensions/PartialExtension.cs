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

        public override object ProcessData (object data, object chained)
        {
            return InnerTemplate?.Generate(data);
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)
        {
            if (initContext.Context == null)
                throw new ArgumentNullException(nameof(initContext.Context));
            base.InitStart(initContext, dataType, chainedType);
            initContext.ParameterTemplate = GetInnerResult(null, null);
            if (!string.IsNullOrEmpty(initContext.ParameterTemplate)) {
                string templateName = initContext.ParameterTemplate.Trim();
                if (!string.IsNullOrEmpty(templateName))
                {
                    initContext.Context.AddDelayedCompileTemplate(new CompileContext(initContext.Context, dataType, templateName), initContext.ParseContext, this);
                }
            }
            return typeof (string);
        }

        public override void CompleteInit(CompileContext newContext, ParseContext parseContext)
        {
            InnerTemplate = new TtlTemplate(newContext);
        }
    }
}