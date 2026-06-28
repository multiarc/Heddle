using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(HeadScriptsComponent))]

namespace Heddle.Performance.TestSuite.Extensions
{
    [ExtensionName("head_scripts")]
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