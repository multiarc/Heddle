using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Templates.Attributes;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;

namespace Templates.Runtime {
    internal static class EntityCompiler {
        /// <summary>
        /// Returns result of parsed template expression as <see cref="DocumentElement"/> object
        /// </summary>
        /// <exception cref="TemplateProcessingException">Throws upon any parse or data misstype errors</exception>
        /// <returns>Returns object with filled data about template <see cref="DocumentElement"/></returns>
        public static DocumentElement CompileElement
            (IEnumerable<ExtensionItem> extensions, string resultAdditionalDataName, string resultDataName, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            PropertyInfo data = null,
                         additionalData = null;
            if (!String.IsNullOrEmpty(resultDataName)) {
                PropertyInfo dataProperty = context.ModelType.TypeValue.GetProperty(resultDataName);
                if (dataProperty != null && dataProperty.CanRead && !dataProperty.IsHaveAttribute<HiddenAttribute>())
                    data = dataProperty;
                else {
                    throw new TemplateCompileException
                        (String.Format(CultureInfo.InvariantCulture, "Property {0} no found in Type [{1}]", resultDataName, context.ModelType));
                }
            }
            if (!String.IsNullOrEmpty(resultAdditionalDataName)) {
                PropertyInfo dataProperty = context.ModelType.TypeValue.GetProperty(resultAdditionalDataName);
                if (dataProperty != null && dataProperty.CanRead && !dataProperty.IsHaveAttribute<HiddenAttribute>())
                    additionalData = dataProperty;
                else {
                    throw new TemplateCompileException
                        (String.Format(CultureInfo.InvariantCulture, "Property {0} no found in Type [{1}]", resultDataName, context.ModelType));
                }
            }
            Type mainDataType = null,
                 additionalDataType = null;
            if (data != null)
                mainDataType = data.PropertyType;
            if (additionalData != null)
                additionalDataType = additionalData.PropertyType;
            var result = new DocumentElement(data.GetPropertyGate(), additionalData.GetPropertyGate());
            foreach (ExtensionItem extensionItem in extensions) {
                IExtension extension = TemplateFactory.Create(extensionItem.ExtensionName, context);
                var initType = InitializeTemplate(extension, extensionItem.ParameterTemplate, mainDataType, additionalDataType, context);
                Type templateType = extension.GetType();
                TypeAttribute typeAttribute = templateType.GetAttributes<TypeAttribute>().FirstOrDefault();
                AdditionalTypeAttribute additionalTypeAttribute = templateType.GetAttributes<AdditionalTypeAttribute>().FirstOrDefault();

                //Type checking

                CheckTypes(extension, data, typeAttribute);
                CheckTypes(extension, additionalData, additionalTypeAttribute);

                result.TemplateBlock.Add(new TemplateItem(initType, extension));
            }

            return result;
        }

        private static TypeReference InitializeTemplate
            (IExtension extension, string parameterFastString, Type dataType, Type additionalType, CompileContext context)
        {
            try {
                bool directRender = extension.GetType().IsHaveAttribute<DirectRenderAttribute>();
                extension.ParseParameter(parameterFastString, dataType ?? context.ModelType, additionalType ?? context.ModelType, directRender);
                return extension.InitializeInnerTemplate
                    (parameterFastString, dataType ?? context.ModelType, additionalType ?? context.ModelType, context);
            }
            catch (Exception e) {
                throw new TemplateCreateException(string.Format(CultureInfo.InvariantCulture, "Unable to initialize Type {0}", extension), e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extension"></param>
        /// <param name="data"></param>
        /// <param name="typeAttribute"></param>
        private static void CheckTypes (IExtension extension, PropertyInfo data, TypeAttribute typeAttribute)
        {
            if (extension == null)
                throw new ArgumentNullException("extension");

            if (data != null) {
                if (typeAttribute == null) {
                    throw new TemplateCompileException
                        (String.Format(CultureInfo.InvariantCulture, "Template {0} haven't Templates.Core.TypeAttribute", extension.GetType()));
                }
                if (!typeAttribute.DataType.IsType(data.PropertyType)) {
                    throw new TemplateCompileException
                        (String.Format
                             (CultureInfo.InvariantCulture, "Property {0} have Type {1} but {2} expected.", data.Name, data.PropertyType.FullName,
                              typeAttribute.DataType.FullName));
                }
            }
        }
    }
}