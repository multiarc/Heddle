using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Language.Binding;
using Heddle.Strings.Core;

using Heddle.Native;

namespace Heddle.Runtime {
    public struct ExtensionType
    {
        public ExtensionType(string name, Type type, bool replace)
        {
            Name = name;
            Type = type;
            Replace = replace;
        }

        public string Name { get; set; }
        public Type Type { get; set; }
        public bool Replace { get; set; }
    }

    /// <summary>Discovery, registration, and instantiation of template extensions at compile time.</summary>
    public static class TemplateFactory
    {
        /// <summary>
        /// The name → extension-type registry, published copy-on-write. Reads are lock-free and always see one whole
        /// registration; a write builds a copy under <see cref="RegistrationLock"/> and publishes it in one assignment.
        /// The old shape mutated a shared dictionary while unlocked readers enumerated it, so a host registering an
        /// assembly while another thread compiled could make a valid template draw a phantom diagnostic.
        /// </summary>
        private static Dictionary<string, Type> _registry = new Dictionary<string, Type>(StringComparer.Ordinal);

        private static readonly object RegistrationLock = new object();

        private static readonly HashSet<Assembly> ExportScanned = new HashSet<Assembly>();

        static TemplateFactory()
        {
            AddExtensions(LoadBaseExtensions());
        }

        /// <summary>
        /// Offers one assembly's assembly-level <c>[ExportExtensions]</c> to the registry. Only assemblies a host
        /// registers are scanned — an assembly that merely happens to be loaded never takes an extension name.
        /// Idempotent per assembly.
        /// </summary>
        internal static void RegisterExportedExtensions(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            lock (ExportScanned)
            {
                if (ExportScanned.Contains(assembly))
                    return;
            }

            AddExtensions(ExportedExtensions(assembly));

            // Marked only after the registration succeeds. Marking first made a failed Register unrepeatable: the
            // host caught the exception, called Register again, and got a silent no-op.
            lock (ExportScanned)
            {
                ExportScanned.Add(assembly);
            }
        }

        private static IEnumerable<ExtensionType> ExportedExtensions(Assembly assembly)
        {
            foreach (var exportAttribute in assembly.GetCustomAttributes<ExportExtensionsAttribute>())
            {
                if (exportAttribute == null)
                    continue;

                if (exportAttribute.All)
                {
                    foreach (var extension in LoadAddExtensionsFromAssembly(assembly))
                    {
                        yield return extension;
                    }
                    yield break;
                }

                foreach (var extension in LoadExtensions(exportAttribute.Extensions))
                {
                    yield return extension;
                }
            }
        }

        /// <summary>
        /// Loads and registers all extensions from an assembly.
        /// </summary>
        /// <param name="assembly">Assembly to load extensions from.</param>
        public static IEnumerable<ExtensionType> LoadAddExtensionsFromAssembly(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            return LoadExtensions(assembly);
        }

        /// <summary>
        /// Registers extension types, resolving name collisions through the shared
        /// <see cref="ExtensionRegistrationRules"/> — the same rule the source generator applies at build time.
        /// </summary>
        public static void AddExtensions(IEnumerable<ExtensionType> toAdd)
        {
            if (toAdd == null) throw new ArgumentNullException(nameof(toAdd));

            lock (RegistrationLock)
            {
                // Built on a copy and published at the end, so a rejected registration leaves the live registry
                // untouched rather than half-applied.
                var next = new Dictionary<string, Type>(_registry, StringComparer.Ordinal);
                foreach (var type in toAdd.OrderBy(ext => ext.Replace))
                {
                    if (type.Type == null || type.Name == null )
                        throw new ArgumentException();

                    bool hasIncumbent = next.TryGetValue(type.Name, out var incumbent);
                    var verdict = ExtensionRegistrationRules.Resolve(hasIncumbent, type.Replace,
                        hasIncumbent && incumbent.IsAssignableFrom(type.Type));

                    switch (verdict)
                    {
                        case ExtensionRegistrationVerdict.Register:
                            next.Add(type.Name, type.Type);
                            break;
                        case ExtensionRegistrationVerdict.Replace:
                            next[type.Name] = type.Type;
                            break;
                        default:
                            // Resolve never returns KeepIncumbent in this context.
                            throw new TemplateOverrideException(
                                $"Cannot override <{type.Name}> Extension, <{type.Type}> is not inherited from <{incumbent}>");
                    }
                }

                Volatile.Write(ref _registry, next);
            }
        }

        /// <summary>Creates an extension instance by name, with position and error collection.</summary>
        /// <param name="templateName">Extension name from <see cref="ExtensionNameAttribute"/></param>
        /// <param name="absoluteTextPosition">Usage position in the source text</param>
        /// <param name="context">Parser context for definition resolution</param>
        /// <returns>An <see cref="IExtension"/> instance, or null on error</returns>
        public static IExtension Create(string templateName, BlockPosition absoluteTextPosition, ParseContext context, CompileContext compileContext)
        {
            if (templateName == null)
                throw new ArgumentNullException(nameof(templateName));
            try
            {
                var extensionType = Volatile.Read(ref _registry)[templateName];
                var resultExtension = CreateExtension(extensionType);
                resultExtension.Position = absoluteTextPosition;
                return resultExtension;
            }
            catch (KeyNotFoundException)
            {
                // HED0002: extension not found. Narrower than HED1001 (neither extension nor function).
                compileContext.CompileErrors.Add($"Cannot find extension <{templateName}>"
                    .ToError(absoluteTextPosition, Data.HeddleDiagnosticIds.ExtensionNotFound));
                return null;
            }
            catch (ArgumentException e)
            {
                compileContext.CompileErrors.Add(e.ToError(absoluteTextPosition));
                return null;
            }
        }

        /// <summary>True when an extension with this exact name is registered.</summary>
        internal static bool Exists(string name)
        {
            return name != null && Volatile.Read(ref _registry).ContainsKey(name);
        }

        /// <summary>
        /// Snapshot of the registered extension names — one entry per <c>[ExtensionName]</c> alias (feeds LSP
        /// extension-name completion). Ordinal, case-sensitive; includes the unnamed
        /// <see cref="Heddle.Extensions.EmptyExtension"/> alias (<c>""</c>), which completion filters out.
        /// </summary>
        internal static IReadOnlyCollection<string> RegisteredNames()
        {
            return new List<string>(Volatile.Read(ref _registry).Keys);
        }

        /// <summary>Ordinal registry lookup for the branch-set scan's participant classification (compile-time only).</summary>
        internal static bool TryGetExtensionType(string name, out Type type)
        {
            if (name != null)
                return Volatile.Read(ref _registry).TryGetValue(name, out type);
            type = null;
            return false;
        }

        #region Helper Methods

        /// <summary>
        /// Loads all built-in extensions from this assembly.
        /// </summary>
        /// <returns>All discovered extensions.</returns>
        private static IEnumerable<ExtensionType> LoadBaseExtensions ()
        {
            return LoadExtensions(typeof(TemplateFactory).GetTypeInfo().Assembly);
        }

        /// <summary>
        /// Loads all extensions from an assembly.
        /// </summary>
        /// <param name="assembly">Assembly to load extensions from.</param>
        /// <returns>All discovered extensions.</returns>
        /// <summary>
        /// Every extension candidate in an assembly. A type that cannot be loaded is skipped rather than aborting the
        /// registration: `[ExportExtensions]` in its parameterless form reaches every type in the assembly, and one
        /// unresolvable reference — a plugin built against a version the host does not have — would otherwise throw
        /// <see cref="ReflectionTypeLoadException"/> out of the host's startup call.
        /// </summary>
        internal static IEnumerable<ExtensionType> LoadExtensions (Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            return LoadExtensions(types);
        }

        internal static IEnumerable<ExtensionType> LoadExtensions(IEnumerable<Type> extensions)
        {
            // OrderingKey decides the incumbent candidate; both discovery and generator sort by this call.
            var types =
                extensions.Where(t => t.IsImplement<IExtension>() && t.IsHaveAttribute<ExtensionNameAttribute>(true))
                    .OrderBy(t => ExtensionRegistrationRules.OrderingKey(
                        t.GetAttributes<DataTypeAttribute>(true).Any(p => p.DataType.GetTypeInfo().IsInterface),
                        t.GetAttributes<ChainedTypeAttribute>(true).Any(p => p.DataType.GetTypeInfo().IsInterface)));
            foreach (var type in types)
            {
                var extensionNames = type.GetAttributes<ExtensionNameAttribute>(true);
                var replace = type.IsHaveAttribute<ExtensionReplaceAttribute>();
                foreach (var result in extensionNames.Select(name => new ExtensionType(name.Name, type, replace)))
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Instantiates an extension type.
        /// </summary>
        /// <param name="templateType">Extension type to instantiate.</param>
        /// <returns>A new instance of the extension.</returns>
        private static IExtension CreateExtension (Type templateType)
        {
            try {
                return (IExtension) Activator.CreateInstance(templateType);
            }
            catch (Exception e) {
                throw new TemplateCreateException($"Unable to create Type {templateType} ({e.Message})", e);
            }
        }

        #endregion
    }
}