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
        public override object ProcessData(ref Scope scope)
        {
            return "<script src=\"/head.js\"></script>";
        }

        public override void RenderData(ref Scope scope)
        {
            scope.Render("<script src=\"/head.js\"></script>");
        }
    }
}