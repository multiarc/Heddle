using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Heddle.Native;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Owns the collectible model <see cref="ModelAssemblyContext"/> and its engine registration.
    /// Load registers the assemblies with <see cref="AssemblyHelper"/> so engine type resolution can see their
    /// types; unload clears that registration (dropping the engine's static references) then <c>Unload()</c>s the
    /// context, retaining only a <see cref="WeakReference"/> for the collection check.
    /// </summary>
    internal sealed class ModelAssemblyManager
    {
        private ModelAssemblyContext _context;

        /// <summary>A weak reference to the last-unloaded context — used by the reload/collection test.</summary>
        internal WeakReference LastUnloaded { get; private set; }

        private readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);

        internal IReadOnlyList<Assembly> Load(IReadOnlyList<string> paths, Action<string> log)
        {
            if (paths == null || paths.Count == 0)
                return Array.Empty<Assembly>();

            string loading = null;
            var context = new ModelAssemblyContext(paths, (requested, engine) =>
            {
                // Said once per workspace assembly and version, not on every reload.
                string said = loading + "|" + requested.FullName;
                if (log == null || !_reported.Add(said))
                    return;
                log("Heddle: workspace assembly '" + loading + "' was built against " + requested.Name + " " +
                    requested.Version + "; the language server runs " + engine.GetName().Version +
                    " and uses its own engine for it, so completions and diagnostics follow the server's version.");
            });
            var assemblies = new List<Assembly>();
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;
                if (IsEngineImage(path, out string engineNote))
                {
                    // Said once per path, not on every reload.
                    if (log != null && _reported.Add("engine|" + path))
                        log(engineNote);
                    continue;
                }

                Assembly assembly;
                try
                {
                    loading = Path.GetFileNameWithoutExtension(path);
                    assembly = context.LoadFromBytes(Path.GetFullPath(path));
                }
                catch (Exception e)
                {
                    // Unreadable/invalid assemblies degrade to typeless.
                    log?.Invoke("Heddle: workspace assembly '" + path + "' could not be loaded, so its types are unknown here: " +
                        WorkspaceReflection.Describe(e));
                    continue;
                }

                // References bind lazily; binding the engine ones now attributes a version difference to the
                // assembly that carries it. Only those: any other reference is the workspace's own business, and
                // one that is missing costs the members that need it, not the assembly.
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    if (!ModelAssemblyContext.IsEngine(reference.Name))
                        continue;
                    try
                    {
                        context.LoadFromAssemblyName(reference);
                    }
                    catch (Exception e) when (WorkspaceReflection.IsLoadFault(e))
                    {
                        log?.Invoke("Heddle: reference '" + reference.Name + "' of workspace assembly '" + loading +
                            "' could not be bound: " + WorkspaceReflection.Describe(e));
                    }
                }

                assemblies.Add(assembly);
            }

            if (assemblies.Count > 0)
                AssemblyHelper.RegisterModelAssemblies(assemblies);
            _context = context;
            return assemblies;
        }

        /// <summary>Whether a configured path is one of the engine's own assemblies. Those are never loaded from a
        /// workspace: a second engine beside the server's makes the workspace's exports and sandbox attributes
        /// belong to an engine nothing asks. The server's copy stands in, and the note says which version that is.</summary>
        private static bool IsEngineImage(string path, out string note)
        {
            note = null;
            AssemblyName name;
            try
            {
                name = AssemblyName.GetAssemblyName(Path.GetFullPath(path));
            }
            catch (Exception e) when (e is IOException || e is BadImageFormatException || e is UnauthorizedAccessException ||
                e is ArgumentException)
            {
                return false;
            }

            if (!ModelAssemblyContext.IsEngine(name.Name))
                return false;
            note = "Heddle: '" + path + "' is the engine's own assembly " + name.Name + " " + name.Version +
                " and is not loaded from the workspace; the language server uses its own copy (" +
                typeof(Heddle.HeddleTemplate).Assembly.GetName().Version + "). It can be left out of 'assemblies'.";
            return true;
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
