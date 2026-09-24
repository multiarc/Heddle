using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Heddle.Runtime.Expressions;

namespace Heddle.Tool.Compile
{
    /// <summary>Loads one invocation's implementation images into an isolated
    /// <see cref="AssemblyLoadContext"/> whose resolver serves the engine set from the host's own
    /// default context (so engine types unify) and every other name from the image set by simple
    /// name; framework names fall to the host runtime, which is what makes an assembly framework
    /// per the artifact contract's type-identity rule. For the invocation's duration the host also
    /// answers the default context's <c>Resolving</c> event for image simple names (never the engine
    /// set), because the engine's C# tier loads its emitted assembly with <c>Assembly.Load(byte[])</c>
    /// and that assembly's model-type references resolve through the default context.</summary>
    internal sealed class ImageLoadContext : IDisposable
    {
        private static readonly HashSet<string> EngineNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Heddle",
            "Heddle.Language",
            "Antlr4.Runtime.Standard"
        };

        private readonly AssemblyLoadContext _context;
        private readonly Dictionary<string, string> _pathsBySimpleName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Assembly> _images = new List<Assembly>();
        private readonly Dictionary<Assembly, string> _pathsByImage = new Dictionary<Assembly, string>();
        private bool _disposed;

        /// <summary>Set by the process entry point. The build host is one process per invocation, and there an
        /// image is loaded from its path: the runtime then finds the image's satellite resources and native
        /// libraries beside it exactly as it would in the consumer's own process, which code that runs during
        /// the build — an extension's hooks, a static constructor — depends on. A caller that stays alive (a
        /// test run, an embedding tool) gets images loaded from bytes instead, so no file is held open for its
        /// lifetime, and the same two lookups answered from the image's directory by this context.</summary>
        internal static bool ProcessEndsWithTheInvocation { get; set; }

        internal ImageLoadContext()
        {
            // Not collectible. The engine's C# tier emits an ordinary (non-collectible) assembly whose
            // method signatures name the template's model types, and the runtime forbids such an assembly
            // from referencing a collectible one — so from a collectible context a model type could only
            // be reached through a second load of its image, which is a different type. The host is a
            // one-shot process; its images live exactly as long as it does.
            _context = new AssemblyLoadContext("HeddleBuildHost", isCollectible: false);
            _context.Resolving += Resolve;
            _context.ResolvingUnmanagedDll += ResolveNative;
            AssemblyLoadContext.Default.Resolving += ResolveDefault;
        }

        internal IReadOnlyList<Assembly> Images => _images;

        internal Assembly LoadImage(string path)
        {
            string full = Path.GetFullPath(path);
            // An image the host already loaded (its own engine assemblies, or the test assembly
            // in an in-process run) is shared, never reloaded into the build ALC: a second copy
            // splits attribute identity, and export discovery ([ExportFunctions] on a consumer
            // image) silently misses when the copy's attribute type is not the host's.
            string simple;
            byte[] bytes;
            Guid mvid;
            try
            {
                bytes = File.ReadAllBytes(full);
                using (var pe = new System.Reflection.PortableExecutable.PEReader(
                           System.Collections.Immutable.ImmutableArray.Create(bytes)))
                {
                    var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
                    simple = metadata.GetString(metadata.GetAssemblyDefinition().Name);
                    mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is BadImageFormatException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                throw ImageLoadFailure(path, ex.Message);
            }

            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loaded.IsCollectible)
                    continue;
                AssemblyName name;
                try
                {
                    name = loaded.GetName();
                }
                catch (Exception ex) when (ex is IOException || ex is BadImageFormatException)
                {
                    continue;
                }

                // The engine set is shared by name, whatever build of it the project references. Anything else
                // is shared only when it is this very image: a process that compiles more than once (a test
                // run, a long-lived caller) may meet two different images under one name, and the one loaded
                // first is not the one this invocation was given.
                if (string.Equals(name.Name, simple, StringComparison.OrdinalIgnoreCase) &&
                    (IsShared(simple) || loaded.ManifestModule.ModuleVersionId == mvid))
                {
                    if (!_pathsBySimpleName.ContainsKey(simple))
                        _pathsBySimpleName[simple] = full;
                    _images.Add(loaded);
                    return loaded;
                }
            }

            Assembly image;
            try
            {
                image = ProcessEndsWithTheInvocation
                    ? _context.LoadFromAssemblyPath(full)
                    : _context.LoadFromStream(new MemoryStream(bytes));
            }
            catch (Exception ex) when (ex is IOException || ex is BadImageFormatException ||
                ex is UnauthorizedAccessException)
            {
                throw ImageLoadFailure(path, ex.Message);
            }

            simple = image.GetName().Name;
            if (!_pathsBySimpleName.ContainsKey(simple))
                _pathsBySimpleName[simple] = full;
            _images.Add(image);
            _pathsByImage[image] = full;
            return image;
        }

        internal static Exception ImageLoadFailure(string path, string message) =>
            new ImageLoadException(path, message);

        /// <summary>Resolves a model-type spelling over the image set: assembly-qualified first,
        /// then full name, then the engine's own template-spelling resolver under the template's
        /// <paramref name="usings"/> — the one that reads a dotted nested type, a closed generic or an
        /// alias, and the one the runtime will apply to the same directive — then simple name in
        /// response-file order. Returns null when unresolved.</summary>
        internal Type ResolveModelType(string spelling, ICollection<string> usings = null)
        {
            if (string.IsNullOrEmpty(spelling))
                return null;
            Type qualified = Type.GetType(spelling, throwOnError: false);
            if (qualified != null)
                return qualified;
            foreach (var image in _images)
            {
                Type found;
                try
                {
                    found = image.GetType(spelling, throwOnError: false, ignoreCase: false);
                }
                catch (Exception ex) when (ex is IOException || ex is BadImageFormatException)
                {
                    continue;
                }

                if (found != null)
                    return found;
            }

            try
            {
                Type spelled = Heddle.Helpers.ReflectionHelper.ResolveType(spelling, usings);
                if (spelled != null)
                    return spelled;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                // Unresolved or ambiguous under the engine's rules: the simple-name pass below still
                // speaks, and a miss there is the caller's unresolved-model diagnostic.
            }

            foreach (var image in _images)
            {
                Type[] types;
                try
                {
                    types = image.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch (IOException)
                {
                    continue;
                }
                catch (BadImageFormatException)
                {
                    continue;
                }

                if (types == null)
                    continue;
                foreach (var type in types)
                    if (type != null && string.Equals(type.Name, spelling, StringComparison.Ordinal))
                        return type;
            }

            return null;
        }

        private static bool IsShared(string simpleName)
        {
            return simpleName != null && (EngineNames.Contains(simpleName) ||
                simpleName.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal));
        }

        private Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            if (name == null || IsShared(name.Name))
                return null;
            var satellite = ResolveSatellite(context, name);
            if (satellite != null)
                return satellite;
            if (name.Name != null && _pathsBySimpleName.TryGetValue(name.Name, out string path))
            {
                foreach (var image in _images)
                {
                    if (string.Equals(image.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase))
                        return image;
                }

                try
                {
                    return ProcessEndsWithTheInvocation
                        ? context.LoadFromAssemblyPath(path)
                        : context.LoadFromStream(new MemoryStream(File.ReadAllBytes(path)));
                }
                catch (IOException)
                {
                    return null;
                }
                catch (BadImageFormatException)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>An image loaded from bytes has no location, so the runtime has nowhere to look for its
        /// satellites: <c>&lt;culture&gt;/&lt;name&gt;.resources.dll</c> beside the image it belongs to. The
        /// runtime asks culture by culture, parent after child, so one directory per request is the whole rule.</summary>
        private Assembly ResolveSatellite(AssemblyLoadContext context, AssemblyName name)
        {
            const string suffix = ".resources";
            if (name.Name == null || string.IsNullOrEmpty(name.CultureName) ||
                !name.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return null;
            string owner = name.Name.Substring(0, name.Name.Length - suffix.Length);
            if (!_pathsBySimpleName.TryGetValue(owner, out string ownerPath))
                return null;
            string candidate = Path.Combine(Path.GetDirectoryName(ownerPath), name.CultureName, name.Name + ".dll");
            try
            {
                if (!File.Exists(candidate))
                    return null;
                return ProcessEndsWithTheInvocation
                    ? context.LoadFromAssemblyPath(candidate)
                    : context.LoadFromStream(new MemoryStream(File.ReadAllBytes(candidate)));
            }
            catch (Exception ex) when (ex is IOException || ex is BadImageFormatException ||
                ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>A native library an image imports: where the image's <c>.deps.json</c> says, else beside the
        /// image, else under its <c>runtimes/&lt;rid&gt;/native</c> — the places the consumer's own process finds it.</summary>
        private IntPtr ResolveNative(Assembly requesting, string library)
        {
            if (requesting == null || string.IsNullOrEmpty(library) ||
                !_pathsByImage.TryGetValue(requesting, out string imagePath))
                return IntPtr.Zero;
            string directory = Path.GetDirectoryName(imagePath);
            var candidates = new List<string>();
            try
            {
                string declared = new AssemblyDependencyResolver(imagePath).ResolveUnmanagedDllToPath(library);
                if (declared != null)
                    candidates.Add(declared);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is ArgumentException)
            {
                // No usable .deps.json beside the image; the directory probes below still speak.
            }

            string rid = (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            foreach (string root in new[] { directory, Path.Combine(directory, "runtimes", rid, "native") })
            {
                candidates.Add(Path.Combine(root, library));
                candidates.Add(Path.Combine(root, library + ".dll"));
                candidates.Add(Path.Combine(root, "lib" + library + ".so"));
                candidates.Add(Path.Combine(root, "lib" + library + ".dylib"));
            }

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate) &&
                    System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out IntPtr handle))
                    return handle;
            }

            return IntPtr.Zero;
        }

        private Assembly ResolveDefault(AssemblyLoadContext context, AssemblyName name)
        {
            if (name == null || IsShared(name.Name))
                return null;
            // The image this invocation already loaded, never a second load of its file: the engine bound the
            // template's model types from that instance, and an assembly the C# tier emits has to mean the same
            // types by them. Loading the path again here gives the default context a copy of its own, and the
            // emitted method's parameter type is then not the type the engine passes it.
            if (name.Name != null)
            {
                foreach (var image in _images)
                {
                    if (string.Equals(image.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase))
                        return image;
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            AssemblyLoadContext.Default.Resolving -= ResolveDefault;
            // The context's own handlers stay. It is not collectible, so its images outlive this invocation,
            // and a later one over the same image is handed that image back: a satellite or a native library
            // it first asks for then is still asked of this context, which is the one that knows where it is.
        }
    }

    /// <summary>An implementation image that cannot be loaded (build diagnostic HED7036).</summary>
    internal sealed class ImageLoadException : Exception
    {
        internal ImageLoadException(string path, string message)
            : base(message)
        {
            Path = path;
        }

        internal string Path { get; }
    }
}
