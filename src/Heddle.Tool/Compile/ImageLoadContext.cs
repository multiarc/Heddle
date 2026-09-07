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
        private bool _disposed;

        internal ImageLoadContext()
        {
            _context = new AssemblyLoadContext("HeddleBuildHost", isCollectible: true);
            _context.Resolving += Resolve;
            AssemblyLoadContext.Default.Resolving += ResolveDefault;
        }

        internal IReadOnlyList<Assembly> Images => _images;

        internal Assembly LoadImage(string path)
        {
            string full = Path.GetFullPath(path);
            Assembly image;
            try
            {
                image = _context.LoadFromAssemblyPath(full);
            }
            catch (Exception ex) when (ex is IOException || ex is BadImageFormatException ||
                ex is UnauthorizedAccessException)
            {
                throw ImageLoadFailure(path, ex.Message);
            }

            string simple = image.GetName().Name;
            if (!_pathsBySimpleName.ContainsKey(simple))
                _pathsBySimpleName[simple] = full;
            _images.Add(image);
            return image;
        }

        internal static Exception ImageLoadFailure(string path, string message) =>
            new ImageLoadException(path, message);

        /// <summary>Resolves a model-type spelling over the image set: assembly-qualified first,
        /// then full name, then simple name in response-file order. Returns null when unresolved.</summary>
        internal Type ResolveModelType(string spelling)
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

        private Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            if (name == null || EngineNames.Contains(name.Name) ||
                (name.Name != null &&
                    name.Name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)))
                return null;
            if (name.Name != null && _pathsBySimpleName.TryGetValue(name.Name, out string path))
            {
                try
                {
                    return context.LoadFromAssemblyPath(path);
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

        private Assembly ResolveDefault(AssemblyLoadContext context, AssemblyName name)
        {
            if (name == null || EngineNames.Contains(name.Name) ||
                (name.Name != null &&
                    name.Name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)))
                return null;
            if (name.Name != null && _pathsBySimpleName.TryGetValue(name.Name, out string path))
            {
                try
                {
                    return context.LoadFromAssemblyPath(path);
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

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            AssemblyLoadContext.Default.Resolving -= ResolveDefault;
            _context.Resolving -= Resolve;
            _context.Unload();
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
