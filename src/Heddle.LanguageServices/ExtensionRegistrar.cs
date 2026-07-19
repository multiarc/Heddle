using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Heddle.Attributes;
using Heddle.Exceptions;
using Heddle.Runtime;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// The one-shot workspace export scan (phase 6 D23): byte-loads the configured assemblies into the
    /// <b>default</b> (non-collectible) ALC, reads assembly-level <c>[ExportExtensions]</c> and registers the
    /// extensions into the real <see cref="TemplateFactory"/> — the same two branches the engine's own
    /// <c>ObtainExtensions</c> runs. Runs exactly once per process; the retained <see cref="Assembly"/> handles
    /// also feed the D24 function-export probe. Staleness is documented: a post-scan change requires a restart.
    /// </summary>
    internal static class ExtensionRegistrar
    {
        private static readonly object Gate = new object();
        private static bool _scanned;
        private static bool _resolverInstalled;
        private static List<Assembly> _handles = new List<Assembly>();
        private static readonly List<string> ProbeDirectories = new List<string>();

        /// <summary>True once the process-global scan has run.</summary>
        internal static bool HasScanned
        {
            get { lock (Gate) return _scanned; }
        }

        /// <summary>The assembly handles retained from the one-shot scan (input of the D24 function probe).</summary>
        internal static IReadOnlyList<Assembly> RetainedHandles
        {
            get { lock (Gate) return _handles.ToArray(); }
        }

        internal static IReadOnlyList<Assembly> ScanOnce(IReadOnlyList<string> paths, Action<string> log)
        {
            lock (Gate)
            {
                if (_scanned)
                {
                    if (paths != null && paths.Count > 0)
                        log?.Invoke("Workspace assemblies changed after the one-shot scan — restart the server to rescan.");
                    return _handles.ToArray();
                }

                if (paths == null || paths.Count == 0)
                    return Array.Empty<Assembly>();

                InstallResolver(paths);
                var loaded = new List<Assembly>();
                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        log?.Invoke($"Configured assembly not found: {path}");
                        continue;
                    }

                    try
                    {
                        var bytes = File.ReadAllBytes(Path.GetFullPath(path));
                        var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(bytes));
                        loaded.Add(assembly);
                        RegisterExtensions(assembly, log);
                    }
                    catch (Exception e)
                    {
                        log?.Invoke($"Failed to scan '{path}': {e.Message}");
                    }
                }

                _handles = loaded;
                _scanned = true;
                return loaded.ToArray();
            }
        }

        private static void RegisterExtensions(Assembly assembly, Action<string> log)
        {
            var toAdd = new List<ExtensionType>();
            foreach (var attribute in assembly.GetCustomAttributes<ExportExtensionsAttribute>())
            {
                if (attribute == null)
                    continue;
                var extensions = attribute.All
                    ? TemplateFactory.LoadExtensions(assembly)
                    : TemplateFactory.LoadExtensions(attribute.Extensions);
                toAdd.AddRange(extensions);
                if (attribute.All)
                    break;
            }

            if (toAdd.Count == 0)
                return;

            try
            {
                TemplateFactory.AddExtensions(toAdd);
            }
            catch (TemplateOverrideException e)
            {
                log?.Invoke($"Extension override rejected: {e.Message}");
            }
        }

        private static void InstallResolver(IReadOnlyList<string> paths)
        {
            foreach (var path in paths.Where(p => !string.IsNullOrEmpty(p)))
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (dir != null && !ProbeDirectories.Contains(dir))
                    ProbeDirectories.Add(dir);
            }

            if (_resolverInstalled)
                return;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                foreach (var dir in ProbeDirectories)
                {
                    var candidate = Path.Combine(dir, name.Name + ".dll");
                    if (File.Exists(candidate))
                    {
                        try
                        {
                            return context.LoadFromStream(new MemoryStream(File.ReadAllBytes(candidate)));
                        }
                        catch
                        {
                            // unresolvable dependency surfaces later as a logged instantiation failure
                        }
                    }
                }

                return null;
            };
            _resolverInstalled = true;
        }

        /// <summary>Test hook: clears the one-shot latch so a fresh corpus can be scanned. Cannot un-register the
        /// process-global <see cref="TemplateFactory"/> entries (there is no engine unregister API — D23).</summary>
        internal static void ResetForTests()
        {
            lock (Gate)
            {
                _scanned = false;
                _handles = new List<Assembly>();
            }
        }
    }
}
