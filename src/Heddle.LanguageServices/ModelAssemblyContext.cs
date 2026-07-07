using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Collectible <see cref="AssemblyLoadContext"/> for the workspace model assemblies (phase 6 D14).
    /// Dependency probing runs through <see cref="AssemblyDependencyResolver"/> when a <c>.deps.json</c> sits next
    /// to the assembly, else same-directory probing. Model assemblies are byte-loaded so the source files stay
    /// rebuildable for the watch loop.
    /// </summary>
    internal sealed class ModelAssemblyContext : AssemblyLoadContext
    {
        private readonly List<AssemblyDependencyResolver> _resolvers = new List<AssemblyDependencyResolver>();
        private readonly List<string> _directories = new List<string>();

        internal ModelAssemblyContext(IEnumerable<string> paths)
            : base("HeddleModelAssemblies", isCollectible: true)
        {
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                var full = Path.GetFullPath(path);
                var dir = Path.GetDirectoryName(full);
                if (dir != null && !_directories.Contains(dir))
                    _directories.Add(dir);
                try
                {
                    _resolvers.Add(new AssemblyDependencyResolver(full));
                }
                catch
                {
                    // no .deps.json — same-directory probing below covers it
                }
            }
        }

        internal Assembly LoadFromBytes(string path)
        {
            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            return LoadFromStream(stream);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            foreach (var resolver in _resolvers)
            {
                var resolved = resolver.ResolveAssemblyToPath(assemblyName);
                if (resolved != null && File.Exists(resolved))
                    return LoadFromBytes(resolved);
            }

            foreach (var dir in _directories)
            {
                var candidate = Path.Combine(dir, assemblyName.Name + ".dll");
                if (File.Exists(candidate))
                    return LoadFromBytes(candidate);
            }

            return null; // fall back to the default context
        }
    }
}
