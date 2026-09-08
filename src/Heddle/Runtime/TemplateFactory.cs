using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Extensions;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Language.Binding;
using Heddle.Strings.Core;

using Heddle.Native;

#if NETSTANDARD2_0
// The trim/AOT attributes ship in-box only on modern targets. This file is the one place that
// needs them on netstandard2.0, so the polyfill lives here rather than in a new file. The linker
// matches the attribute by namespace and name, which is why the shape mirrors the framework type.
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct |
        AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property |
        AttributeTargets.Event, AllowMultiple = true)]
    internal sealed class DynamicDependencyAttribute : Attribute
    {
        public DynamicDependencyAttribute(string memberSignature, Type type)
        {
        }

        public DynamicDependencyAttribute(
            DynamicallyAccessedMemberTypes memberTypes, Type type)
        {
        }

        public DynamicDependencyAttribute(
            DynamicallyAccessedMemberTypes memberTypes, string typeName, string assemblyName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.GenericParameter | AttributeTargets.Parameter |
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field |
        AttributeTargets.ReturnValue, AllowMultiple = false, Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
        {
        }
    }

    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicParameterlessConstructor = 1,
        PublicConstructors = 2,
        NonPublicConstructors = 4,
        PublicMethods = 8,
        NonPublicMethods = 16,
        PublicFields = 32,
        NonPublicFields = 64,
        PublicNestedTypes = 128,
        NonPublicNestedTypes = 256,
        PublicProperties = 512,
        NonPublicProperties = 1024,
        PublicEvents = 2048,
        NonPublicEvents = 4096,
        Interfaces = 8192,
        All = -1,
    }
}
#endif

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
        /// <see cref="ExtensionRegistrationRules"/> — the same rule the build host applies at build time.
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
#pragma warning disable IL2072 // P3-R9: the registry is fed by LoadExtensions, whose types the DynamicDependency roots preserve.
                var resultExtension = CreateExtension(extensionType);
#pragma warning restore IL2072
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
        // P3-R9 rooting story: LoadExtensions enumerates the engine assembly, which trimming would
        // otherwise empty. One root per built-in extension type keeps CreateExtension's
        // Activator.CreateInstance working after trimming; host extensions are rooted by the
        // typeof in [ExportExtensions]/[ExportFunctions]/[HeddleModelAssembly].
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(AttrExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(DateExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(ElifExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(ElseExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(EmptyExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(EmptyHtmlExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(ForIndexExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(GuidExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(IfExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(IfNotExtension))]
        // The archived import tombstone is still enumerated by LoadExtensions, so it needs the same
        // root — but the type is obsolete-as-error, so the string overload names it without a typeof.
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, "Heddle.Extensions.ImportExtension", "Heddle")]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(IntegerExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(JsExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(ListExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(ModelExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(MoneyExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(OutExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(ParamExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(PartialExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(ProfileExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(StringExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(SwapExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(TimeExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(UrlExtension))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(UsingExtension))]
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
#pragma warning disable IL2026 // P3-R9: the enumeration is the discovery the DynamicDependency roots on LoadBaseExtensions preserve.
                types = assembly.GetTypes();
#pragma warning restore IL2026
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            return LoadExtensions(types);
        }

        internal static IEnumerable<ExtensionType> LoadExtensions(IEnumerable<Type> extensions)
        {
            // OrderingKey decides the incumbent candidate; both discovery and the build sort by this call.
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
        private static IExtension CreateExtension (
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type templateType)
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