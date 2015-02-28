using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Templates.Attributes;
using Templates.Collections;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templates.Runtime
{
    public class TtlCompiler
    {
        public static RuntimeDocument Compile(string document, CompileContext compileContext, ParseContext parseContext)
        {
            string workingDocument = document;
            int seed = 0;
            RemoveComments(parseContext, ref seed, ref workingDocument);
            RemoveDefinitions(parseContext, ref seed, ref workingDocument);
            ReplaceRawOutput(parseContext, ref seed, ref workingDocument);
            if (compileContext == null)
                throw new ArgumentNullException("compileContext");
            var documentElements = new SmartList<DocumentElement>();
            foreach (var extensions in parseContext.OutputChains)
            {
                var element = new DocumentElement(extensions.BlockPosition);
                Type returnTypeChainedPrevious = null;
                foreach (var item in extensions.Chain.Reverse())
                {
                    var compiledItem = CompileItem(item, compileContext, extensions.Context,
                        ref returnTypeChainedPrevious);
                    element.CallChain.Add(compiledItem);
                }
                documentElements.Add(element);
            }
            compileContext.Compile();
            return new RuntimeDocument(workingDocument, documentElements.ToArray(), compileContext.ModelType);
        }

        private static void ReplaceRawOutput(ParseContext context, ref int seed, ref string workingDocument)
        {
            foreach (var rawOut in context.RawOutputItems)
            {
                workingDocument = ExStringBuilder.Replace(rawOut.Position.StartIndex, rawOut.Position.Length,
                    rawOut.Text, workingDocument);
                seed += rawOut.Position.Length - rawOut.Text.Length;
                var outputItem = rawOut;
                var itemsToUpdate =
                    context.OutputChains.Where(c => c.BlockPosition.StartIndex > outputItem.Position.StartIndex);
                foreach (var chain in itemsToUpdate)
                {
                    chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                        chain.BlockPosition.Length);
                }
            }
        }

        private static void RemoveComments(ParseContext context, ref int seed, ref string workingDocument) {
            foreach (var blockPosition in context.CommentTokens) {
                seed += ExStringBuilder.ApplyRemove(blockPosition, ref workingDocument);
                var position = blockPosition;
                var itemsToUpdate = context.OutputChains.Where(c => c.BlockPosition.StartIndex > position.StartIndex);
                foreach (var chain in itemsToUpdate) {
                    chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                        chain.BlockPosition.Length);
                }
            }
        }

        private static void RemoveDefinitions(ParseContext context, ref int seed, ref string workingDocument)
        {
            foreach (var blockPosition in context.DefinitionBlock.Positions)
            {
                seed += ExStringBuilder.ApplyRemove(blockPosition, ref workingDocument);
                var position = blockPosition;
                var itemsToUpdate = context.OutputChains.Where(c => c.BlockPosition.StartIndex > position.StartIndex);
                foreach (var chain in itemsToUpdate)
                {
                    chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                        chain.BlockPosition.Length);
                }
            }
        }

        private static TemplateChain CompileParameterChain(IEnumerable<OutputItem> items, CompileContext compileContext, ParseContext parseContext) {
            TemplateChain result = new TemplateChain();
            Type returnTypeChainedPrevious = null;
            foreach (var item in items.Reverse()) {
                var compiledItem = CompileItem(item, compileContext, parseContext, ref returnTypeChainedPrevious);
                result.Add(compiledItem);
            }
            return result;
        }

        private static TemplateItem CompileItem
            (OutputItem extensionItem, CompileContext compileContext, ParseContext parseContext,
                ref Type returnTypeChainedPrevious)
        {
            PropertyInfo data = null;
            Type dataType = null;
            TemplateChain callParameter = null;
            if (extensionItem.CallParameter.IsModelTypeParameter)
            {
                if (!string.IsNullOrEmpty(extensionItem.CallParameter.ModelParameter) &&
                    extensionItem.CallParameter.ModelParameter != "null")
                {
                    PropertyInfo dataProperty =
                        compileContext.ModelType.GetProperty(extensionItem.CallParameter.ModelParameter);
                    if (dataProperty != null && dataProperty.CanRead && !dataProperty.IsHaveAttribute<HiddenAttribute>())
                        data = dataProperty;
                    else
                    {
                        throw new TemplateCompileException
                            (string.Format(CultureInfo.InvariantCulture, "Property {0} no found in Type [{1}]",
                                extensionItem.CallParameter.ModelParameter, compileContext.ModelType));
                    }
                }
                if (extensionItem.CallParameter.ModelParameter != "null")
                {
                    dataType = data?.PropertyType ?? compileContext.ModelType;
                }
            }
            else
            {
                callParameter = CompileParameterChain(extensionItem.CallParameter.ChainParameter, compileContext, parseContext);
                dataType = callParameter.RenderType;
            }
            IExtension extension;
            if (parseContext.DefenitionExists(extensionItem.ExtensionName))
            {
                DefinitionItem definition = parseContext.GetDefenition(extensionItem.ExtensionName);
                Type acceptType;
                extension = CompileFromDefenition(definition, compileContext, out acceptType);
                if (data != null)
                    CheckTypes(data, acceptType);
                else
                    CheckTypes(dataType, acceptType);
                returnTypeChainedPrevious = InitializeTemplate(extension, definition.ParameterTemplate, dataType,
                    returnTypeChainedPrevious, compileContext, definition.Context);
            }
            else
            {
                extension = TemplateFactory.Create(extensionItem.ExtensionName, parseContext);
                Type templateType = extension.GetType();

                ChainedTypeAttribute chainedTypeAttribute =
                    templateType.GetAttributes<ChainedTypeAttribute>(true).FirstOrDefault();

                if (returnTypeChainedPrevious != null)
                    CheckTypes(returnTypeChainedPrevious, chainedTypeAttribute?.DataType);

                DataTypeAttribute dataTypeAttribute = templateType.GetAttributes<DataTypeAttribute>(true).FirstOrDefault();
                if (data != null)
                    CheckTypes(data, dataTypeAttribute?.DataType);
                else
                    CheckTypes(dataType, dataTypeAttribute?.DataType);

                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate, dataType,
                    returnTypeChainedPrevious, compileContext, parseContext);
            }

            return new TemplateItem(returnTypeChainedPrevious, extension)
            {
                Parameter = new RuntimeCallParameter(GatesCache.GetPropertyGate(data), callParameter)
            };
        }

        private static IExtension CompileFromDefenition(DefinitionItem definition, CompileContext compileContext, out Type acceptType)
        {
            WalkValidateDefinitionType(definition, compileContext);
            IExtension result = new DefenitionBaseExtension();
            acceptType = ReflectionHelper.ResolveType(definition.ModelType, compileContext.Namespaces.ToArray()) ?? typeof(object);
            return result;
        }

        private static void WalkValidateDefinitionType(DefinitionItem definition, CompileContext context)
        {
            var currentType = ReflectionHelper.ResolveType(definition.ModelType, context.Namespaces.ToArray()) ?? typeof(object);
            var definitionBase = definition.BaseDefinition;
            while (definitionBase != null)
            {
                var baseType = ReflectionHelper.ResolveType(definitionBase.ModelType, context.Namespaces.ToArray()) ?? typeof(object);
                if (!baseType.IsType(currentType))
                {
                    throw new TemplateDefinitionTypeException(definition.Position, "The new definition type isn't assignable to base.");
                }

                definitionBase = definitionBase.BaseDefinition;
            }
        }


        private static Type InitializeTemplate
            (IExtension extension, string parameterFastString, Type modelType, Type chainedType, CompileContext context, ParseContext parseContext) {
            try
            {
                modelType = modelType ?? typeof (object);
                chainedType = chainedType ?? typeof (object);
                RenderType directRender = extension.GetType().IsHaveAttribute<EncodeOutputAttribute>(true)
                    ? (extension.GetType().IsHaveAttribute<NotEncodeAttribute>(false) ? RenderType.Raw : RenderType.Encode)
                    : RenderType.Raw;
                extension.SetUpRenderType(directRender);
                return extension.InitStart(parameterFastString, modelType, chainedType, context, parseContext);
            }
            catch (Exception e) {
                throw new TemplateCreateException(string.Format(CultureInfo.InvariantCulture, "Unable to initialize Extension {0}", extension), e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extension"></param>
        /// <param name="property"></param>
        /// <param name="dataTypeAttribute"></param>
        private static void CheckTypes(PropertyInfo property, Type dataType) {
            dataType = dataType ?? typeof(object);

            if (property != null) {
                if (!dataType.IsType(property.PropertyType)) {
                    throw new TemplateCompileException
                        (string.Format
                            (CultureInfo.InvariantCulture, "Property {0} have Type {1} but {2} expected.", property.Name,
                                property.PropertyType.FullName,
                                dataType.FullName));
                }
            }
        }

        private static void CheckTypes(Type returnType, Type dataType) {
            dataType = dataType ?? typeof(object);

            if (returnType != null) {
                if (!dataType.IsType(returnType)) {
                    throw new TemplateCompileException
                        (string.Format
                            (CultureInfo.InvariantCulture, "Return Type is {0} but {1} expected.", returnType.FullName,
                                dataType.FullName));
                }
            }
        }
    }
}