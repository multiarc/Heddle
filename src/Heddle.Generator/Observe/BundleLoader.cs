using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Heddle.Generator.Observe
{
    /// <summary>
    /// Turns the compilation's file-backed references into a loadable <b>bundle</b> — the emitted intermediate
    /// assembly, the consumer's own engine, every extension assembly, and whatever the referenced closure needs to
    /// satisfy them — with an <see cref="AppDomain.AssemblyResolve"/> handler that resolves cross-references
    /// <i>within</i> the bundle while pinning Roslyn and the BCL to the compiler's own copies.
    /// <para><b>Nothing is loaded from where the build wrote it.</b> Every member is first copied to a
    /// content-addressed path under the observe directory and the <i>copy</i> is what gets loaded, so
    /// <c>LoadFrom</c>'s permanent lock never lands on a file the next build overwrites. That dissolves the whole
    /// immutable-root question the earlier probe answered with a package-folder allow-list: a project reference, a
    /// <c>bin</c> path and a package path are all equally safe once what is opened is a copy nothing rewrites. The
    /// content tag is the reference's module version id, which the compiler already has open — so a rebuilt
    /// assembly is a different path, loaded fresh, and the stale one is simply never referenced again.</para>
    /// <para><b>An assembly the process already holds at the same module version is reused, never re-loaded.</b>
    /// Reuse locks nothing new, and it is what keeps one identity per assembly — an observed extension meeting two
    /// engines is a worse failure than not observing at all. The mvid is part of the test, so reuse can never hand
    /// back a differently-built assembly that happens to share a name.</para>
    /// <para><b>There is no sandbox and this class does not imply one.</b> <c>netstandard2.0</c> across a .NET
    /// Framework compiler host and a .NET one leaves neither <c>AssemblyLoadContext</c> nor <c>AppDomain</c>
    /// isolation available, so what the driver bounds is the <i>wait</i>, not the execution.</para>
    /// </summary>
    internal sealed class BundleLoader
    {
        private static readonly object ResolveGate = new object();

        /// <summary>Assembly simple name → an already content-addressed path, accumulated across bundles so the
        /// process-wide resolve handler can satisfy a loaded assembly's dependencies. Only ever gains paths this
        /// loader itself wrote.</summary>
        private static readonly Dictionary<string, string> Resolvable =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Assembly simple name → a reference this process saw declared, and where the copy of it would
        /// go. Separate from <see cref="Resolvable"/> because a declared reference has not been copied or loaded:
        /// it is what the handler falls back to when a bundle member asks for a dependency nothing has needed
        /// yet — the engine's own parser runtime being the first one every bundle asks for.</summary>
        private static readonly Dictionary<string, ReferenceEntry> Declared =
            new Dictionary<string, ReferenceEntry>(StringComparer.OrdinalIgnoreCase);

        private static bool _resolveHooked;

        /// <summary>Every path this process has handed to <c>LoadFrom</c>, for the immutability gate to read back.
        /// </summary>
        private static readonly List<string> LoadedPaths = new List<string>();

        private readonly string _directory;
        private readonly Dictionary<string, ReferenceEntry> _bySimpleName =
            new Dictionary<string, ReferenceEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Assembly> _loaded =
            new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private readonly struct ReferenceEntry
        {
            internal ReferenceEntry(string path, string moduleVersionId, bool alreadyAddressed,
                string directory = null)
            {
                Path = path;
                ModuleVersionId = moduleVersionId;
                AlreadyAddressed = alreadyAddressed;
                Directory = directory;
            }

            internal string Path { get; }

            /// <summary>Where a content-addressed copy of this reference belongs, for the handler to make one when
            /// a dependency is asked for that no bundle ever loaded directly.</summary>
            internal string Directory { get; }

            /// <summary>The reference's module version id as the compiler read it; <c>null</c> when unknown, which
            /// makes the entry uncopyable and unreusable rather than guessed at.</summary>
            internal string ModuleVersionId { get; }

            internal bool AlreadyAddressed { get; }
        }

        internal BundleLoader(string directory)
        {
            _directory = directory;
        }

        /// <summary>Records a reference the bundle may need, under the assembly simple name a dependent will ask
        /// for it by.</summary>
        internal void Declare(string simpleName, string path, string moduleVersionId)
        {
            if (string.IsNullOrEmpty(simpleName) || string.IsNullOrEmpty(path) || _bySimpleName.ContainsKey(simpleName))
                return;
            var entry = new ReferenceEntry(path, moduleVersionId, alreadyAddressed: false, directory: _directory);
            _bySimpleName[simpleName] = entry;
            lock (ResolveGate)
                if (!Declared.ContainsKey(simpleName))
                    Declared[simpleName] = entry;
        }

        /// <summary>Records an assembly already sitting at a content-addressed path — the emitted intermediate
        /// assembly, which was written into the store to begin with and needs no copy.</summary>
        internal void DeclareContentAddressed(string simpleName, string path)
        {
            if (string.IsNullOrEmpty(simpleName) || string.IsNullOrEmpty(path))
                return;
            _bySimpleName[simpleName] = new ReferenceEntry(path, null, alreadyAddressed: true);
        }

        /// <summary>The assembly behind one simple name, from a content-addressed copy or from the identical image
        /// this process already holds. <c>null</c> is always "this bundle cannot supply it", never an error.
        /// </summary>
        internal Assembly TryLoad(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return null;
            if (_loaded.TryGetValue(simpleName, out var cached))
                return cached;

            Assembly assembly = null;
            if (_bySimpleName.TryGetValue(simpleName, out var entry))
            {
                assembly = AlreadyLoaded(simpleName, entry.ModuleVersionId);
                if (assembly == null)
                {
                    var path = entry.AlreadyAddressed
                        ? entry.Path
                        : entry.ModuleVersionId == null
                            ? null
                            : ObserveCache.CopyContentAddressed(entry.Path, _directory, simpleName,
                                entry.ModuleVersionId, out _);
                    assembly = LoadContentAddressed(simpleName, path);
                }
            }

            _loaded[simpleName] = assembly;
            return assembly;
        }

        /// <summary>Installs the process-wide resolve handler, once.</summary>
        internal void Arm()
        {
            lock (ResolveGate)
            {
                if (_resolveHooked)
                    return;
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                _resolveHooked = true;
            }
        }

        private static Assembly AlreadyLoaded(string simpleName, string moduleVersionId)
        {
            if (moduleVersionId == null)
                return null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;
                if (!string.Equals(assembly.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    if (string.Equals(assembly.ManifestModule.ModuleVersionId.ToString("N"), moduleVersionId,
                            StringComparison.OrdinalIgnoreCase))
                        return assembly;
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        private Assembly LoadContentAddressed(string simpleName, string path)
        {
            if (string.IsNullOrEmpty(path) || !ObserveCache.IsComplete(path))
                return null;

            lock (ResolveGate)
            {
                Resolvable[simpleName] = path;
                LoadedPaths.Add(path);
            }

            Arm();
            try
            {
                return Assembly.LoadFrom(path);
            }
            catch (Exception e)
            {
                // A corrupt, native or wrong-bitness image. Refusing to observe is always available and always
                // safe; failing a consumer's build over someone else's package is not — but the reason is kept,
                // because a swallowed load failure is the one thing that makes an unobservable build
                // undiagnosable from the outside.
                LastLoadError = simpleName + ": " + e.Message;
                return null;
            }
        }

        /// <summary>The reason the last load this loader attempted did not produce an assembly, or <c>null</c>.
        /// Carried into the build diagnostic so an unobservable build says what stopped it.</summary>
        internal static string LastLoadError { get; private set; }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string simple;
            try
            {
                simple = new AssemblyName(args.Name).Name;
            }
            catch (Exception)
            {
                return null;
            }

            // Bundle members only. Roslyn, the BCL and everything else the compiler brought with it are never
            // named here, so they resolve the way they always did and stay one identity by not being touched.
            string path;
            ReferenceEntry declared;
            bool known;
            lock (ResolveGate)
            {
                if (!Resolvable.TryGetValue(simple, out path))
                    path = null;
                known = Declared.TryGetValue(simple, out declared);
            }

            if (path == null)
            {
                if (!known)
                    return null;

                // A dependency of something already in the bundle. The image this process already holds wins when
                // it is the same one — a second identity of an assembly the bundle is mid-way through loading is
                // the failure this whole loader exists to avoid.
                var reused = AlreadyLoaded(simple, declared.ModuleVersionId);
                if (reused != null)
                    return SatisfiesVersion(args.Name, reused) ? reused : null;

                if (declared.ModuleVersionId == null)
                    return null;
                path = ObserveCache.CopyContentAddressed(declared.Path, declared.Directory, simple,
                    declared.ModuleVersionId, out _);
                if (path == null)
                    return null;
                lock (ResolveGate)
                {
                    Resolvable[simple] = path;
                    LoadedPaths.Add(path);
                }
            }

            Assembly loaded;
            try
            {
                loaded = Assembly.LoadFrom(path);
            }
            catch (Exception)
            {
                return null;
            }

            return SatisfiesVersion(args.Name, loaded) ? loaded : null;
        }

        /// <summary>
        /// Whether a bundle member may answer a request, by the rule the default load context already applies: a
        /// version <b>behind</b> the request is not a substitute for it.
        /// <para>The handler is process-wide — there is no other kind on <c>netstandard2.0</c> across two compiler
        /// hosts — so it has to be no more permissive than the binder it sits behind. Answering a request the
        /// runtime would have refused does not just mis-resolve the bundle: it changes what an assembly-qualified
        /// <c>@model</c> spelling binds to for everything else in the process, and a template the engine refuses
        /// would start compiling because a generator happened to run first.</para>
        /// </summary>
        private static bool SatisfiesVersion(string requested, Assembly candidate)
        {
            try
            {
                var wanted = new AssemblyName(requested).Version;
                if (wanted == null)
                    return true;
                var have = candidate.GetName().Version;
                return have != null && have >= wanted;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Test seam: every path this process handed to <c>LoadFrom</c>, so the immutability gate can
        /// assert each one is content-addressed rather than take the claim on trust.</summary>
        internal static IReadOnlyList<string> LoadedFromPaths
        {
            get
            {
                lock (ResolveGate)
                    return LoadedPaths.ToArray();
            }
        }

        internal static string FileSimpleName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
