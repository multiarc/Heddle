using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(BodyEndScriptsComponent))]

namespace Templates.Performance.TestSuite.Extensions
{
    [ExtensionName("bodyendscriptscomponent")]
    public class BodyEndScriptsComponent : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            return "<script src=\"/bodyend.js\"></script>";
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render("<script src=\"/bodyend.js\"></script>");
        }
    }
}