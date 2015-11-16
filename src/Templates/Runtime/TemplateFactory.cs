using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Templates.Attributes;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
#if DOTNET5_4
using Templates.Native;
#endif

namespace Templates.Runtime {
    //DONE: пустой шаблон
    //DONE: оптимизация производительности получения данных объекта
    //DONE: трекинг изменения файла
    //DONE: автооткат при ошибках
    //DONE: HTML кодирование данных для вывода
    //TODO: 1) логирование ошибок

    /// <summary>
    /// Template factory, initializes and creates all templates
    /// </summary>
    internal static class TemplateFactory
    {
        private static readonly Dictionary<string, Type> Templates = new Dictionary<string, Type>();

        static TemplateFactory()
        {
            AddExtensions(LoadBaseExtensions());
#if !DOTNET5_4
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
#else
            foreach (var assembly in AssemblyHelper.GetAssemblies())
#endif
            {
                var exportAttributes = assembly.GetCustomAttributes<ExportExtensionsAttribute>();
                foreach (var exportAttribute in exportAttributes)
                {
                    if (exportAttribute != null)
                    {
                        if (exportAttribute.All)
                        {
                            LoadAddExtensionsFromAssembly(assembly);
                            break;
                        }
                        AddExtensions(LoadExtensions(exportAttribute.Extensions));
                    }
                }
            }
        }

        /// <summary>
        /// Loads all templates are in assembly
        /// </summary>
        /// <param name="assembly"></param>
        public static void LoadAddExtensionsFromAssembly(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            IEnumerable<KeyValuePair<string, Type>> toAdd = LoadExtensions(assembly);
            AddExtensions(toAdd);
        }

        public static void AddExtensions(IEnumerable<KeyValuePair<string, Type>> toAdd)
        {
            if (toAdd == null) throw new ArgumentNullException(nameof(toAdd));
            foreach (var type in toAdd)
            {
                if (type.Key == null || type.Value == null )
                    throw new ArgumentException();
                if (Templates.ContainsKey(type.Key))
                {
                    if (Templates[type.Key].IsAssignableFrom(type.Value))
                    {
                        Templates[type.Key] = type.Value;
                    }
                    else
                    {
                        throw new TemplateOverrideException(
                            string.Format("Cannot override {0} Extension, {1} is not inherited from {2}", type.Key,
                                type.Value, Templates[type.Key]));
                    }
                }
                else
                {
                    Templates.Add(type.Key, type.Value);
                }
            }
        }

        /// <summary>
        /// Creates template by it's name and adds parameter string if it's present
        /// </summary>
        /// <param name="templateName">Template name <see cref="NameAttribute"/></param>
        /// <param name="context">Parser context, used to get defenitions list</param>
        /// <returns>ITemplate compatible object <see cref="IExtension"/></returns>
        public static IExtension Create (string templateName, ParseContext context)
        {
            if (templateName == null)
                throw new ArgumentNullException(nameof(templateName));
            try {
                Type extensionType = Templates[templateName];
                IExtension resultExtension = CreateExtension(extensionType);
                return resultExtension;
            }
            catch (KeyNotFoundException e) {
                throw new ArgumentException("Template unrecognized. <" + templateName + ">", e);
            }
            catch (ArgumentException e) {
                throw new ArgumentException("Cannot create template", e);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Loads all base templates
        /// </summary>
        /// <returns>List of all template types</returns>
        private static IEnumerable<KeyValuePair<string, Type>> LoadBaseExtensions ()
        {
            return LoadExtensions(typeof(TemplateFactory).GetTypeInfo().Assembly);
        }

        /// <summary>
        /// Loads all templates in Assembly
        /// </summary>
        /// <param name="assembly">Assembly to get from</param>
        /// <returns>List of all template types</returns>
        internal static IEnumerable<KeyValuePair<string, Type>> LoadExtensions (Assembly assembly)
        {
            return LoadExtensions(assembly.GetTypes());
        }

        internal static IEnumerable<KeyValuePair<string, Type>> LoadExtensions(IEnumerable<Type> extensions) {
            List<Type> types =
                extensions.Where(t => t.IsImplement<IExtension>() && t.IsHaveAttribute<NameAttribute>(true)).OrderBy
                    (t => t.GetAttributes<DataTypeAttribute>(true).Any(p => p.DataType.GetTypeInfo().IsInterface)).ThenBy
                    (t => t.GetAttributes<ChainedTypeAttribute>(true).Any(p => p.DataType.GetTypeInfo().IsInterface)).ToList();
            var result = new List<KeyValuePair<string, Type>>();
            foreach (Type type in types) {
                NameAttribute[] names = type.GetAttributes<NameAttribute>(true);
                foreach (NameAttribute name in names)
                    result.Add(new KeyValuePair<string, Type>(name.Name, type));
            }
            return result;
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
                throw new TemplateCreateException(string.Format(CultureInfo.InvariantCulture, "Unable to create Type {0}", templateType), e);
            }
        }

        #endregion
    }
}