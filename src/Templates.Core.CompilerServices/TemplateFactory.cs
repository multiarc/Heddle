using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Templates.Attributes;
using Templates.Core.Extensions;
using Templates.Exceptions;

namespace Templates.Core.CompilerServices {
    //DONE: пустой шаблон
    //DONE: оптимизация производительности получения данных объекта
    //DONE: трекинг изменения файла
    //DONE: автооткат при ошибках
    //DONE: HTML кодирование данных для вывода
    //TODO: 1) логирование ошибок

    /// <summary>
    /// Template factory, initializes and creates all templates
    /// </summary>
    internal static class TemplateFactory {
        private static readonly Dictionary<string, Type> Templates = LoadTemplates();

        #region Public Methods

        /// <summary>
        /// Loads all templates are in assembly
        /// </summary>
        /// <param name="assembly"></param>
        public static void LoadAddTemplatesFromAssembly (Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly");

            Dictionary<string, Type> toAdd = LoadTemplates(assembly);
            foreach (var type in toAdd)
                Templates.Add(type.Key, type.Value);
        }

        /// <summary>
        /// Creates template by it's name and adds parameter string if it's present
        /// </summary>
        /// <param name="templateName">Template name <see cref="NameAttribute"/></param>
        /// <returns>ITemplate compatible object <see cref="IExtension"/></returns>
        public static IExtension Create (string templateName, CompileContext context = null)
        {
            if (templateName == null)
                throw new ArgumentNullException("templateName");

            try {
                Type resultType = Templates[templateName];
                IExtension resultExtension = CreateTemplate(resultType);
                return resultExtension;
            }
            catch (KeyNotFoundException e) {
                if (context != null) {
                    try {
                        Type resultType = context.Extensions[templateName];
                        IExtension resultExtension = CreateTemplate(resultType);
                        return resultExtension;
                    }
                    catch (KeyNotFoundException ex) {
                        throw new ArgumentException("Template unrecognized. <" + templateName + ">", ex);
                    }
                }
                throw new ArgumentException("Template unrecognized. <" + templateName + ">", e);
            }
            catch (ArgumentException e) {
                throw new ArgumentException("Cannot create template", e);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Loads all templates in current Assembly
        /// </summary>
        /// <returns>List of all template types</returns>
        private static Dictionary<string, Type> LoadTemplates ()
        {
            return LoadTemplates(Assembly.GetExecutingAssembly());
        }

        /// <summary>
        /// Loads all templates in Assembly
        /// </summary>
        /// <param name="assembly">Assembly to get from</param>
        /// <returns>List of all template types</returns>
        public static Dictionary<string, Type> LoadTemplates (Assembly assembly)
        {
            List<Type> types =
                assembly.GetTypes().Where(t => t.IsImplement<IExtension>() && t.IsHaveAttribute<NameAttribute>()).OrderBy
                    (t => t.GetAttributes<TypeAttribute>().Any(p => p.DataType.IsInterface)).OrderBy
                    (t => t.GetAttributes<AdditionalTypeAttribute>().Any(p => p.DataType.IsInterface)).ToList();
            var result = new Dictionary<string, Type>();
            foreach (Type type in types) {
                NameAttribute[] names = type.GetAttributes<NameAttribute>();
                foreach (NameAttribute name in names)
                    result.Add(name.Name, type);
            }
            return result;
        }

        /// <summary>
        /// Creates Template Instance
        /// </summary>
        /// <param name="templateType">Type of template <see cref="Type"/></param>
        /// <returns></returns>
        private static IExtension CreateTemplate (Type templateType)
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