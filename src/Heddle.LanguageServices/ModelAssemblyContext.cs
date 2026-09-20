using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Collectible <see cref="AssemblyLoadContext"/> for the workspace model assemblies.
    /// Dependency probing runs through <see cref="AssemblyDependencyResolver"/> when a <c>.deps.json</c> sits next
    /// to the assembly, else same-directory probing. Model assemblies are byte-loaded so the source files stay
    /// rebuildable for the watch loop.
    /// <para>The engine is never loaded here. A build output directory has a copy of it beside the models, and
    /// loading that copy gives the workspace an engine of its own: its <c>[Hidden]</c>, its extension base
    /// class and its export attributes are then types the server's engine does not recognize, and the sandbox
    /// attributes silently stop meaning anything. Engine references resolve to the server's engine instead —
    /// whatever version the workspace was built against, which is reported, once, rather than acted on.</para>
    /// </summary>
    internal sealed class ModelAssemblyContext : AssemblyLoadContext
    {
        private readonly List<AssemblyDependencyResolver> _resolvers = new List<AssemblyDependencyResolver>();
        private readonly List<string> _directories = new List<string>();
        private readonly Action<AssemblyName, Assembly> _engineVersionDiffers;

        /// <summary>The assemblies the server and a workspace have to share for attribute and base-type
        /// identity to hold: the engine, and what its public surface exposes.</summary>
        private static readonly Assembly[] Engine =
        {
            typeof(Heddle.HeddleTemplate).Assembly,
            typeof(Heddle.Language.HeddleParser).Assembly,
            typeof(Antlr4.Runtime.Parser).Assembly
        };

        /// <summary>Whether the simple name is one of the assemblies this context serves from the server.</summary>
        internal static bool IsEngine(string simpleName)
        {
            foreach (var engine in Engine)
            {
                if (string.Equals(engine.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal ModelAssemblyContext(IEnumerable<string> paths,
            Action<AssemblyName, Assembly> engineVersionDiffers = null)
            : base("HeddleModelAssemblies", isCollectible: true)
        {
            _engineVersionDiffers = engineVersionDiffers;
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
            foreach (var engine in Engine)
            {
                var name = engine.GetName();
                if (!string.Equals(name.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (assemblyName.Version != null && assemblyName.Version != name.Version)
                    _engineVersionDiffers?.Invoke(assemblyName, engine);
                return engine;
            }

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
