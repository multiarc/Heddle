using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Helpers;
using Heddle.Language;
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

    /// <summary>
    /// Template factory, initializes and creates all templates
    /// </summary>
    public static class TemplateFactory
    {
        private static readonly Dictionary<string, Type> Heddle = new Dictionary<string, Type>();

        static TemplateFactory()
        {
            AddExtensions(ObtainExtensions());
        }

        private static IEnumerable<ExtensionType> ObtainExtensions()
        {
            foreach (var baseExtension in LoadBaseExtensions())
            {
                yield return baseExtension;
            }
            foreach (var assembly in AssemblyHelper.GetAssemblies())
            {
                var exportAttributes = assembly.GetCustomAttributes<ExportExtensionsAttribute>();
                foreach (var exportAttribute in exportAttributes)
                {
                    if (exportAttribute != null)
                    {
                        if (exportAttribute.All)
                        {
                            foreach (var extension in LoadAddExtensionsFromAssembly(assembly))
                            {
                                yield return extension;
                            }
                            break;
                        }

                        foreach (var extension in LoadExtensions(exportAttribute.Extensions))
                        {
                            yield return extension;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads all templates are in assembly
        /// </summary>
        /// <param name="assembly"></param>
        public static IEnumerable<ExtensionType> LoadAddExtensionsFromAssembly(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            return LoadExtensions(assembly);
        }

        public static void AddExtensions(IEnumerable<ExtensionType> toAdd)
        {
            if (toAdd == null) throw new ArgumentNullException(nameof(toAdd));
            //extensions marked as replacements comes last
            foreach (var type in toAdd.OrderBy(ext => ext.Replace))
            {
                if (type.Type == null || type.Name == null )
                    throw new ArgumentException();

                if (Heddle.ContainsKey(type.Name))
                {
                    if (type.Replace)
                    {
                        Heddle[type.Name] = type.Type;
                    }
                    else if (Heddle[type.Name].IsAssignableFrom(type.Type))
                    {
                        Heddle[type.Name] = type.Type;
                    }
                    else
                    {
                        throw new TemplateOverrideException(
                            $"Cannot override <{type.Name}> Extension, <{type.Type}> is not inherited from <{Heddle[type.Name]}>");
                    }
                }
                else
                {
                    Heddle.Add(type.Name, type.Type);
                }
            }
        }

        /// <summary>
        /// Creates extension by it's name and adds parameter string if it's present
        /// </summary>
        /// <param name="templateName">Extension name <see cref="ExtensionNameAttribute"/></param>
        /// <param name="absoluteTextPosition">Extension usage position in source text</param>
        /// <param name="context">Parser context, used to get defenitions list</param>
        /// <returns>ITemplate compatible object <see cref="IExtension"/></returns>
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
                compileContext.CompileErrors.Add($"Cannot find extension <{templateName}>".ToError(absoluteTextPosition));
                return null;
            }
            catch (ArgumentException e)
            {
                compileContext.CompileErrors.Add(e.ToError(absoluteTextPosition));
                return null;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Loads all base templates
        /// </summary>
        /// <returns>List of all template types</returns>
        private static IEnumerable<ExtensionType> LoadBaseExtensions ()
        {
            return LoadExtensions(typeof(TemplateFactory).GetTypeInfo().Assembly);
        }

        /// <summary>
        /// Loads all templates in Assembly
        /// </summary>
        /// <param name="assembly">Assembly to get from</param>
        /// <returns>List of all template types</returns>
        internal static IEnumerable<ExtensionType> LoadExtensions (Assembly assembly)
        {
            return LoadExtensions(assembly.GetTypes());
        }

        internal static IEnumerable<ExtensionType> LoadExtensions(IEnumerable<Type> extensions)
        {
            var types =
                extensions.Where(t => t.IsImplement<IExtension>() && t.IsHaveAttribute<ExtensionNameAttribute>(true))
                    .OrderBy
                        (t => t.GetAttributes<DataTypeAttribute>(true).Any(p => p.DataType.GetTypeInfo().IsInterface))
                    .ThenBy
                    (t => t.GetAttributes<ChainedTypeAttribute>(true)
                        .Any(p => p.DataType.GetTypeInfo().IsInterface));
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
        /// Creates Template Instance
        /// </summary>
        /// <param name="templateType">Type of template <see cref="Type"/></param>
        /// <returns></returns>
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