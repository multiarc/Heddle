using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(CustomStyles))]

namespace Templates.Performance.TestSuite.Extensions
{
    [ExtensionName("customstyles")]
    public class CustomStyles : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            return "/* CSS Comment Test */";
        }

        public override void RenderData(in Scope scope)
        {
            scope.Render("/* CSS Comment Test */");
        }
    }
}