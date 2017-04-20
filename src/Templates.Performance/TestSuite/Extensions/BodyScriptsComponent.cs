using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Performance.TestSuite.Extensions;

[assembly: ExportExtensions(typeof(BodyScriptsComponent))]

namespace Templates.Performance.TestSuite.Extensions
{
    [ExtensionName("bodyscriptscomponent")]
    public class BodyScriptsComponent : AbstractExtension
    {
        public override object ProcessData(ref Scope scope)
        {
            return "<script src=\"/body.js\"></script>";
        }
    }
}