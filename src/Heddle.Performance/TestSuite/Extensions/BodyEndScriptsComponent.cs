using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(BodyEndScriptsComponent))]

namespace Heddle.Performance.TestSuite.Extensions
{
    [ExtensionName("body_end_scripts")]
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