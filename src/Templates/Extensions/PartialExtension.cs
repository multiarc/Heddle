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
    [Name ("partial")]
    public class PartialExtension: AbstractExtension
    {
        protected TtlTemplate InnerTemplate;

        public override object ProcessData (object value, object chainedResult)
        {
            return InnerTemplate?.Generate(value);
        }

        public override ExType InitStart(string parameterTemplate, ExType dataType, ExType chainedType, CompileContext context, ParseContext parseContext)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            base.InitStart(parameterTemplate, dataType, chainedType, context, parseContext);
            parameterTemplate = GetInnerResult(null, null);
            if (!string.IsNullOrEmpty(parameterTemplate)) {
                string templateName = parameterTemplate.Trim();
                if (!string.IsNullOrEmpty(templateName))
                {
                    context.AddDelayedCompileTemplate(new CompileContext(context, dataType, templateName), parseContext, this);
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