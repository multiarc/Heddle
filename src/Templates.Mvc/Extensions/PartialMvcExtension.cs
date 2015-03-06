using System.Collections.Generic;
using Templates.Attributes;
using Templates.Data;
using Templates.Extensions;
using Templates.Language;
using Templates.Mvc.Extensions;
using Templates.Runtime;

[assembly: ExportExtensions(typeof(PartialMvcExtension))]

namespace Templates.Mvc.Extensions {
    public class PartialMvcExtension: PartialExtension {
        public override void CompleteInit(CompileContext newContext, ParseContext parseContext)
        {
            IEnumerable<string> locations;
            InnerTemplate = TtlViewEngine.Resolver.GetTemplate(newContext.Options.TemplateName,
                newContext.ControllerName, out locations, newContext, TemplatePathType.PartialView);
        }
    }
}
