using System;
using System.IO;
using System.Linq;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The host's assembly closure for reference building, in <c>TRUSTED_PLATFORM_ASSEMBLIES</c> shape.
    /// TPA is a .NET Core host concept and is NULL on .NET Framework, where the closure is what the AppDomain
    /// has actually loaded — the same fallback <c>OldSchemaManifestFixture</c> proved on the net48 leg. Kept in
    /// the joined-string shape rather than a list so every call site's existing split-and-filter stays intact.
    /// </summary>
    internal static class HostAssemblies
    {
        internal static string TrustedOrLoaded()
        {
            var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (tpa != null)
                return tpa;

            return string.Join(Path.PathSeparator.ToString(),
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .Select(a => a.Location));
        }
    }
}
