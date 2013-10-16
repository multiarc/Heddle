using System;
using Templates.Attributes;
using Templates.Core.CompilerServices;
using Templates.Exceptions;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Partial Template</para>
    /// <para>Optional parameter is sub-template (fully incluisive)</para>
    /// </summary>
    [Name ("partial")]
    [Name ("template")]
    [Type (typeof (object))] //External File Name
    public class PartialExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            return GetInnerResult(value);
        }

        public override Type InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (!string.IsNullOrEmpty(parameter)) {
                string templateName = parameter.Trim();
                if (context.Options.TemplateName == templateName)
                    throw new TemplateCreateException("Cannot initialize partial template of template itself.");
                if (!string.IsNullOrEmpty(templateName))
                    InnerTTLTemplate = new TTLTemplate(new CompileContext(context, dataType, templateName), false);
            }
            return typeof (string);
        }
    }
}