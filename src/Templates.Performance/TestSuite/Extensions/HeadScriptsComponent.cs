using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(HeadScriptsComponent))]

namespace Templates.Performance.TestSuite.Extensions
{
    [ExtensionName("headscriptscomponent")]
    public class HeadScriptsComponent : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            return "<script src=\"/head.js\"></script>";
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render("<script src=\"/head.js\"></script>");
        }
    }
}