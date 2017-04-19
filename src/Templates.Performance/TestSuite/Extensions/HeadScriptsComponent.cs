using Templates.Core;
using Templates.Data;

namespace Templates.Performance.TestSuite.Extensions
{
    public class HeadScriptsComponent : AbstractExtension
    {
        public override object ProcessData(ref Scope scope)
        {
            return "<script src=\"/head.js\"></script>";
        }
    }
}