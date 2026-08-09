using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Heddle.Generator.Probe
{
    /// <summary>
    /// The one place hook probing turns a path into an <see cref="Assembly"/>, and the rule that makes probing
    /// shippable at all.
    /// <para><b>Only immutable roots.</b> <see cref="Assembly.LoadFrom(string)"/> holds the file open for the
    /// lifetime of the process, and the compiler is a persistent server: load a consumer's <c>bin\</c> or
    /// <c>obj\</c> output once and the <i>next</i> build cannot overwrite it. So the loader loads only from a
    /// restore's package folders — <c>$(NuGetPackageFolders)</c>, which NuGet itself writes and which name
    /// content-addressed directories nothing rewrites in place. The same rule disposes of two other problems for
    /// free: a project-to-project reference surfaces as a <c>CompilationReference</c> with no file at all (which is
    /// why the IDE and the CLI would otherwise disagree), and a reference assembly has no method bodies to run.</para>
    /// <para><b>Already-loaded assemblies are reused, never re-loaded.</b> Loading is what locks a file; handing
    /// back one this process already has locks nothing new, and it is also the natural cache for the second and
    /// later compilations in a server's lifetime. In a real build the only assemblies this loader can find already
    /// loaded are ones it loaded itself from an immutable root, so the reuse set is a subset of the load set. It is
    /// wider in the test host, and deliberately: it is what lets the probe be exercised end to end against a
    /// project-referenced engine without ever locking a build output.</para>
    /// <para><b>There is no sandbox, and this class does not pretend otherwise.</b> netstandard2.0 across a .NET
    /// Framework compiler host (Visual Studio) and a .NET one (<c>dotnet build</c>) leaves neither
    /// <c>AssemblyLoadContext</c> nor <c>AppDomain</c> available, so a loaded extension runs with the compiler's own
    /// privileges and cannot be unloaded. What the probe driver adds is a bounded <i>wait</i>, not bounded
    /// execution — see <see cref="ReflectionHookProbe"/>.</para>
    /// <para>RS1035 permits every API used here: <c>Assembly.LoadFrom</c> and <c>Assembly.Load</c> of an
    /// <c>AssemblyName</c> are not on the analyzer ban list, while <c>System.IO.File</c> and
    /// <c>System.Environment</c> are — which is why nothing here tests for a file's existence (a missing file is a
    /// caught exception) and why the package roots arrive as an MSBuild property rather than as an environment
    /// variable.</para>
    /// </summary>
    internal sealed class ProbeAssemblyLoader
    {
        /// <summary>Path segments that mark a build output. A package folder never contains one; a consumer's
        /// output always does. Checked in addition to the root test, because the root test is only as good as the
        /// property that supplied it.</summary>
        private static readonly string[] BuildOutputSegments = { "bin", "obj" };

        private static readonly object ResolveGate = new object();

        /// <summary>Simple name → immutable path, accumulated across loaders so the process-wide
        /// <see cref="AppDomain.AssemblyResolve"/> handler can satisfy a loaded assembly's dependencies. Only ever
        /// gains entries that already passed <see cref="IsImmutableRoot"/>.</summary>
        private static readonly Dictionary<string, string> ResolvablePaths =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static bool _resolveHooked;

        private readonly IReadOnlyList<string> _roots;
        private readonly Dictionary<string, string> _bySimpleName;
        private readonly Dictionary<string, Assembly> _loaded =
            new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        /// <param name="packageFolders">The value of <c>$(NuGetPackageFolders)</c>, already split.</param>
        /// <param name="referencePaths">Every file-backed reference of the compilation, which is both the set the
        /// loader may be asked for and the set its <see cref="AppDomain.AssemblyResolve"/> handler resolves
        /// dependencies from.</param>
        internal ProbeAssemblyLoader(IReadOnlyList<string> packageFolders, IEnumerable<string> referencePaths)
        {
            _roots = packageFolders ?? new string[0];
            _bySimpleName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (referencePaths != null)
            {
                foreach (var path in referencePaths)
                {
                    var name = SimpleName(path);
                    if (name != null && !_bySimpleName.ContainsKey(name))
                        _bySimpleName[name] = path;
                }
            }
        }

        /// <summary>Splits <c>$(NuGetPackageFolders)</c>. NuGet writes it semicolon-separated with a trailing
        /// separator on each entry; both are normalised here so the prefix test has one shape to compare.</summary>
        internal static IReadOnlyList<string> SplitPackageFolders(string raw)
        {
            var roots = new List<string>();
            if (string.IsNullOrEmpty(raw))
                return roots;

            foreach (var part in raw.Split(';'))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0)
                    continue;
                var normalized = Normalize(trimmed);
                if (normalized[normalized.Length - 1] != Path.DirectorySeparatorChar)
                    normalized += Path.DirectorySeparatorChar;
                roots.Add(normalized);
            }

            return roots;
        }

        /// <summary>
        /// The predicate the whole design rests on: may this path be <c>LoadFrom</c>ed?
        /// <list type="number">
        /// <item><description>It must be a rooted path — a relative one names nothing stable.</description></item>
        /// <item><description>No segment may be <c>bin</c> or <c>obj</c>, whatever the roots say.</description></item>
        /// <item><description>It must sit under one of <paramref name="packageFolders"/>.</description></item>
        /// </list>
        /// <para>An empty root list refuses everything, which is the correct answer for a project that was never
        /// restored through NuGet: no roots means no immutable roots, not "anything goes".</para>
        /// <para>The prefix test is case-insensitive because it compares filesystem paths — the same split
        /// <c>TemplateKey.TryMakeRelative</c> already makes between comparing a path and preserving a key.</para>
        /// </summary>
        internal static bool IsImmutableRoot(string path, IReadOnlyList<string> packageFolders)
        {
            if (string.IsNullOrEmpty(path) || packageFolders == null || packageFolders.Count == 0)
                return false;

            string full;
            try
            {
                if (!Path.IsPathRooted(path))
                    return false;
                full = Normalize(Path.GetFullPath(path));
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }

            if (HasBuildOutputSegment(full))
                return false;

            foreach (var root in packageFolders)
            {
                if (root.Length != 0 && full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>The assembly behind one reference path, or <c>null</c> when this loader may not have it.
        /// Never throws: every failure is the extension being unprobeable, which costs a call site its tier.</summary>
        internal Assembly TryLoad(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            if (_loaded.TryGetValue(path, out var cached))
                return cached;

            var assembly = AlreadyLoaded(path) ?? LoadFromImmutableRoot(path);
            _loaded[path] = assembly;
            return assembly;
        }

        /// <summary>The assembly behind one simple name among the compilation's references.</summary>
        internal Assembly TryLoadBySimpleName(string simpleName) =>
            simpleName != null && _bySimpleName.TryGetValue(simpleName, out var path) ? TryLoad(path) : null;

        private Assembly LoadFromImmutableRoot(string path)
        {
            if (!IsImmutableRoot(path, _roots))
                return null;

            Register(path);
            try
            {
                return Assembly.LoadFrom(path);
            }
            catch (Exception)
            {
                // A missing, corrupt, native or wrong-bitness file. Refusing to probe is always available and
                // always safe; failing the consumer's build over someone else's package is not.
                return null;
            }
        }

        /// <summary>An assembly this process already holds, matched by location. Reuse locks nothing, so it is
        /// exempt from the root rule — see the type doc.</summary>
        private static Assembly AlreadyLoaded(string path)
        {
            string full;
            try
            {
                full = Normalize(Path.GetFullPath(path));
            }
            catch (Exception)
            {
                return null;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                string location;
                try
                {
                    location = assembly.Location;
                }
                catch (Exception)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(location) &&
                    string.Equals(Normalize(location), full, StringComparison.OrdinalIgnoreCase))
                    return assembly;
            }

            return null;
        }

        /// <summary>Makes this loader's immutable references resolvable to a dependency of something it loaded, and
        /// installs the process-wide handler once. The handler resolves by <b>simple name</b> and prefers what the
        /// host already has — which is what pins Roslyn (and every framework assembly) to the compiler's own copy
        /// rather than to whatever version a package happens to carry beside it.</summary>
        private void Register(string path)
        {
            lock (ResolveGate)
            {
                foreach (var pair in _bySimpleName)
                    if (!ResolvablePaths.ContainsKey(pair.Key) && IsImmutableRoot(pair.Value, _roots))
                        ResolvablePaths[pair.Key] = pair.Value;

                var self = SimpleName(path);
                if (self != null && !ResolvablePaths.ContainsKey(self))
                    ResolvablePaths[self] = path;

                if (_resolveHooked)
                    return;
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                _resolveHooked = true;
            }
        }

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

            // Host copy first: Roslyn, the BCL and anything else the compiler brought with it must stay one
            // identity, or a probed extension meets two of them.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (!assembly.IsDynamic &&
                    string.Equals(assembly.GetName().Name, simple, StringComparison.OrdinalIgnoreCase))
                    return assembly;

            string path;
            lock (ResolveGate)
            {
                if (!ResolvablePaths.TryGetValue(simple, out path))
                    return null;
            }

            try
            {
                return Assembly.LoadFrom(path);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool HasBuildOutputSegment(string full)
        {
            var parts = full.Split(Path.DirectorySeparatorChar);
            foreach (var part in parts)
                foreach (var banned in BuildOutputSegments)
                    if (string.Equals(part, banned, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }

        private static string Normalize(string path) =>
            path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        private static string SimpleName(string path)
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
