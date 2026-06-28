using Heddle.Attributes;
using Heddle.Extensions;
using Heddle.Language;
using Heddle.Mvc.Extensions;
using Heddle.Runtime;

[assembly: ExportExtensions(typeof(PartialMvcExtension))]

namespace Heddle.Mvc.Extensions
{
    public class PartialMvcExtension : PartialExtension
    {
        public override void CompleteInit(CompileScope newContext, ParseContext parseContext)
        {
            //IEnumerable<string> locations;
            //var engine = newContext.CompileContext.ServiceProvider.GetRequiredService<HeddleViewEngine>();
            //InnerTemplate = engine.Resolver.GetTemplate(newContext.Options.TemplateName,
            //    newContext.CompileContext.ControllerName, out locations, newContext.CompileContext, TemplatePathType.PartialView);
            //if (!InnerTemplate.CompileResult.Success)
            //{
            //    newContext.CompileErrors.AddRange(InnerTemplate.CompileResult.ErrorList);
            //}
        }
    }
}