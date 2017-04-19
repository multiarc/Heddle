using Templates.Core;
using Templates.Data;

namespace Templates.Performance.TestSuite.Extensions
{
    public class BodyScriptsComponent : AbstractExtension
    {
        public override object ProcessData(ref Scope scope)
        {
            return "<script src=\"/body.js\"></script>";
        }
    }
}