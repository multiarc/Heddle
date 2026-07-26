using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private static readonly Dictionary<string, Type> Heddle = new Dictionary<string, Type>();

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
                if (!ExportScanned.Add(assembly))
                    return;
            }

            AddExtensions(ExportedExtensions(assembly));
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
            foreach (var type in toAdd.OrderBy(ext => ext.Replace))
            {
                if (type.Type == null || type.Name == null )
                    throw new ArgumentException();

                bool hasIncumbent = Heddle.TryGetValue(type.Name, out var incumbent);
                var verdict = ExtensionRegistrationRules.Resolve(hasIncumbent, type.Replace,
                    hasIncumbent && incumbent.IsAssignableFrom(type.Type));

                switch (verdict)
                {
                    case ExtensionRegistrationVerdict.Register:
                        Heddle.Add(type.Name, type.Type);
                        break;
                    case ExtensionRegistrationVerdict.Replace:
                        Heddle[type.Name] = type.Type;
                        break;
                    default:
                        // Resolve never returns KeepIncumbent in this context.
                        throw new TemplateOverrideException(
                            $"Cannot override <{type.Name}> Extension, <{type.Type}> is not inherited from <{incumbent}>");
                }
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
                var extensionType = Heddle[templateName];
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
            return name != null && Heddle.ContainsKey(name);
        }

        /// <summary>
        /// Snapshot of the registered extension names — one entry per <c>[ExtensionName]</c> alias (feeds LSP
        /// extension-name completion). Ordinal, case-sensitive; includes the unnamed
        /// <see cref="Heddle.Extensions.EmptyExtension"/> alias (<c>""</c>), which completion filters out.
        /// </summary>
        internal static IReadOnlyCollection<string> RegisteredNames()
        {
            lock (Heddle)
            {
                return new List<string>(Heddle.Keys);
            }
        }

        /// <summary>Ordinal registry lookup for the branch-set scan's participant classification (compile-time only).</summary>
        internal static bool TryGetExtensionType(string name, out Type type)
        {
            if (name != null)
                return Heddle.TryGetValue(name, out type);
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
        internal static IEnumerable<ExtensionType> LoadExtensions (Assembly assembly)
        {
            return LoadExtensions(assembly.GetTypes());
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