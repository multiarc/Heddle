using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Language.Binding;
using Heddle.Runtime.Expressions;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// The function-export half of the workspace scan: probes the retained (default-ALC) assembly handles for
    /// <c>[ExportFunctions]</c> and populates a fresh <see cref="FunctionRegistry"/> via the exact
    /// <c>RegisterFrom</c>/<c>RegisterContainer</c> path — one contract, three readers. Re-run per configuration
    /// (a fresh, not-yet-frozen registry) without rescanning; an invalid export is logged and skipped.
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
                try
                {
                    foreach (var attribute in assembly.GetCustomAttributes<ExportFunctionsAttribute>())
                    {
                        if (attribute?.Containers != null)
                            containers.AddRange(attribute.Containers);
                    }
                }
                catch (Exception e) when (WorkspaceReflection.IsLoadFault(e))
                {
                    log?.Invoke("Function exports of '" + assembly.GetName().Name + "' skipped: " +
                        WorkspaceReflection.Describe(e));
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
                catch (Exception e) when (WorkspaceReflection.IsLoadFault(e))
                {
                    RegisterLoadableMethods(registry, container, log);
                }
            }

            return registry;
        }

        /// <summary>The container again, one method at a time: a method whose signature names a type this
        /// process cannot load costs that export, not its neighbours.</summary>
        private static void RegisterLoadableMethods(FunctionRegistry registry, Type container, Action<string> log)
        {
            MethodInfo[] methods;
            try
            {
                methods = container.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            }
            catch (Exception e) when (WorkspaceReflection.IsLoadFault(e))
            {
                log?.Invoke("Function exports of '" + container.FullName + "' skipped: " +
                    WorkspaceReflection.Describe(e));
                return;
            }

            foreach (var method in methods)
            {
                try
                {
                    if (ExportRules.IsCandidate(FunctionRegistry.DescribeMethod(method)))
                        registry.Register(ExportRules.FunctionName(method.Name), method);
                }
                catch (ArgumentException e)
                {
                    log?.Invoke($"Invalid function export skipped: {e.Message}");
                }
                catch (Exception e) when (WorkspaceReflection.IsLoadFault(e))
                {
                    log?.Invoke("Function export '" + container.FullName + "." + method.Name + "' skipped: " +
                        WorkspaceReflection.Describe(e));
                }
            }
        }
    }
}
