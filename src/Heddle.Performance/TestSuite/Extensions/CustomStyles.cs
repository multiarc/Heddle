using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(CustomStyles))]

namespace Heddle.Performance.TestSuite.Extensions
{
    [ExtensionName("custom_styles")]
    public class CustomStyles : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            return "/* CSS Comment Test */";
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render("/* CSS Comment Test */");
        }
    }
}