using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(BodyScriptsComponent))]

namespace Heddle.Performance.TestSuite.Extensions
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