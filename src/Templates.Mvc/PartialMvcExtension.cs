using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Extensions;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Mvc {
    [Name("partial")]
    [Name("template")]
    public class PartialMvcExtension: PartialExtension {
        public override void CompleteInit(CompileContext newContext, ParseContext parseContext)
        {
            IEnumerable<string> locations;
            InnerTemplate = TtlViewEngine.Resolver.GetTemplate(newContext.Options.TemplateName,
                newContext.ControllerName, out locations, newContext, TemplatePathType.PartialView);
        }
    }
}
