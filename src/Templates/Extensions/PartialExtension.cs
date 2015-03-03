using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Partial Template</para>
    /// <para>Optional parameter is sub-template (fully inclusive)</para>
    /// </summary>
    [Name ("partial")]
    [Name ("template")]
    public class PartialExtension: AbstractExtension
    {
        private TtlTemplate _innerTemplate;

        public override object ProcessData (object value, object chainedResult)
        {
            return _innerTemplate?.Generate(value);
        }

        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext)
        {
            if (context == null)
                throw new ArgumentNullException("context");

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
            _innerTemplate = new TtlTemplate(newContext);
        }
    }
}