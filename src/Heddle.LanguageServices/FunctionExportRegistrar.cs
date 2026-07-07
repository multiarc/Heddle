using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Runtime.Expressions;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// The D24 function-export half of the scan: probes the retained (default-ALC) assembly handles for
    /// <c>[ExportFunctions]</c> and populates a fresh <see cref="FunctionRegistry"/> via the exact
    /// <c>RegisterFrom</c>/<c>RegisterContainer</c> path — one contract, three readers. Re-run per configuration
    /// (a fresh, not-yet-frozen registry) without rescanning; an invalid export is logged and skipped (D20).
    /// </summary>
    internal static class FunctionExportRegistrar
    {
        /// <summary>Builds a workspace registry from the retained handles, or null when no handle exports
        /// functions (null = <see cref="FunctionRegistry.Default"/> — bare-host parity).</summary>
        internal static FunctionRegistry BuildRegistry(IReadOnlyList<Assembly> handles, Action<string> log)
        {
            if (handles == null || handles.Count == 0)
                return null;

            var containers = new List<Type>();
            foreach (var assembly in handles)
            {
                foreach (var attribute in assembly.GetCustomAttributes<ExportFunctionsAttribute>())
                {
                    if (attribute?.Containers != null)
                        containers.AddRange(attribute.Containers);
                }
            }

            if (containers.Count == 0)
                return null;

            var registry = new FunctionRegistry();
            foreach (var container in containers)
            {
                try
                {
                    registry.RegisterContainer(container);
                }
                catch (ArgumentException e)
                {
                    log?.Invoke($"Invalid function export skipped: {e.Message}");
                }
            }

            return registry;
        }
    }
}
