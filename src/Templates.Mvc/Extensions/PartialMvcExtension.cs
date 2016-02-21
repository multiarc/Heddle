using System.Collections.Generic;
using Templates.Attributes;
using Templates.Data;
using Templates.Extensions;
using Templates.Language;
using Templates.Mvc.Extensions;
using Templates.Runtime;

[assembly: ExportExtensions(typeof(PartialMvcExtension))]

namespace Templates.Mvc.Extensions
{
    public class PartialMvcExtension : PartialExtension
    {
        public override void CompleteInit(CompileScope newContext, ParseContext parseContext)
        {
            IEnumerable<string> locations;
            InnerTemplate = TtlViewEngine.Resolver.GetTemplate(newContext.Options.TemplateName,
                newContext.CompileContext.ControllerName, out locations, newContext.CompileContext, TemplatePathType.PartialView);
            if (!InnerTemplate.CompileResult.Success)
            {
                newContext.CompileErrors.AddRange(InnerTemplate.CompileResult.ErrorList);
            }
        }
    }
}