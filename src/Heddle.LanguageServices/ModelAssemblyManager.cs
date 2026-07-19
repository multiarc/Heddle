using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Heddle.Native;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Owns the collectible model <see cref="ModelAssemblyContext"/> and its engine registration (phase 6 D14).
    /// Load registers the assemblies with <see cref="AssemblyHelper"/> so engine type resolution can see their
    /// types; unload clears that registration (dropping the engine's static references) then <c>Unload()</c>s the
    /// context, retaining only a <see cref="WeakReference"/> for the collection check.
    /// </summary>
    internal sealed class ModelAssemblyManager
    {
        private ModelAssemblyContext _context;

        /// <summary>A weak reference to the last-unloaded context — used by the reload/collection test.</summary>
        internal WeakReference LastUnloaded { get; private set; }

        internal IReadOnlyList<Assembly> Load(IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0)
                return Array.Empty<Assembly>();

            var context = new ModelAssemblyContext(paths);
            var assemblies = new List<Assembly>();
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;
                try
                {
                    assemblies.Add(context.LoadFromBytes(Path.GetFullPath(path)));
                }
                catch
                {
                    // an unreadable/invalid model assembly degrades to typeless for that file (D20 log-level)
                }
            }

            if (assemblies.Count > 0)
                AssemblyHelper.RegisterModelAssemblies(assemblies);
            _context = context;
            return assemblies;
        }

        internal void Unload()
        {
            if (_context == null)
                return;
            AssemblyHelper.UnregisterModelAssemblies();
            LastUnloaded = new WeakReference(_context);
            _context.Unload();
            _context = null;
        }
    }
}
