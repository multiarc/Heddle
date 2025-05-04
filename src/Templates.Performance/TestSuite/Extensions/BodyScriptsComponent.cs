using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(BodyScriptsComponent))]

namespace Templates.Performance.TestSuite.Extensions
{
    [ExtensionName("body_scripts")]
    public class BodyScriptsComponent : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            return "<script src=\"/body.js\"></script>";
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render("<script src=\"/body.js\"></script>");
        }
    }
}