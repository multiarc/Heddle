using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Templates.Attributes;
using Templates.Collections;
using Templates.Core;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Runtime.Parameters;
using Templates.Strings;
using Templates.Strings.Core;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Templates.Runtime
{
    internal class TtlCompiler
    {
        public static RuntimeDocument Compile(string document, CompileContext compileContext, ParseContext parseContext, ExType chainedType)
        {
            if (compileContext == null)
                throw new ArgumentNullException(nameof(compileContext));
            string workingDocument = document;
            RemoveComments(parseContext, ref workingDocument);
            RemoveDefinitions(parseContext, ref workingDocument);
            ReplaceRawOutput(parseContext, ref workingDocument);
            var documentElements = new SmartList<DocumentElement>();
            foreach (var extensions in parseContext.OutputChains)
            {
                var element = new DocumentElement(extensions.BlockPosition);
                ExType returnTypeChainedPrevious = chainedType;
                foreach (var item in extensions.Chain.Reverse())
                {
                    var compiledItem = CompileItem(item, compileContext, extensions.Context,
                        ref returnTypeChainedPrevious);
                    element.CallChain.Add(compiledItem);
                }
                if (returnTypeChainedPrevious == null)
                {
                    RemoveEmptyItem(parseContext, extensions.BlockPosition, ref workingDocument);
                }
                else
                {
                    documentElements.Add(element);
                }
            }
            return new RuntimeDocument(workingDocument, documentElements.ToArray(), compileContext);
        }

        private static void RemoveEmptyItem(ParseContext context, BlockPosition blockPosition,
            ref string workingDocument)
        {
            int seed = ExStringBuilder.ApplyRemove(blockPosition, ref workingDocument);
            foreach (var chain in context.OutputChains.Reverse())
            {
                if (chain.BlockPosition.StartIndex > blockPosition.StartIndex)
                    chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                        chain.BlockPosition.Length);
                else
                {
                    break;
                }
            }
        }

        private static void ReplaceRawOutput(ParseContext context, ref string workingDocument)
        {
            foreach (var rawOut in context.RawOutputItems.Reverse())
            {
                workingDocument = ExStringBuilder.Replace(rawOut.BlockPosition.StartIndex, rawOut.BlockPosition.Length,
                    rawOut.Text, workingDocument);
                int seed = rawOut.BlockPosition.Length - rawOut.Text.Length;
                var outputItem = rawOut;
                foreach (var chain in context.OutputChains.Reverse())
                {
                    if (chain.BlockPosition.StartIndex > outputItem.BlockPosition.StartIndex)
                        chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                            chain.BlockPosition.Length);
                    else
                    {
                        break;
                    }
                }
            }
        }

        private static void RemoveComments(ParseContext context, ref string workingDocument)
        {
            foreach (var blockPosition in context.CommentTokens.Reverse())
            {
                int seed = ExStringBuilder.ApplyRemove(blockPosition, ref workingDocument);
                foreach (var chain in context.OutputChains.Reverse())
                {
                    if (chain.BlockPosition.StartIndex < blockPosition.StartIndex &&
                        chain.BlockPosition.StartIndex + chain.BlockPosition.Length >
                        blockPosition.StartIndex + blockPosition.Length)
                    {
                        chain.BlockPosition =
                            new BlockPosition(chain.BlockPosition.StartIndex - seed + blockPosition.Length,
                                chain.BlockPosition.Length - blockPosition.Length);
                    }
                    else if (chain.BlockPosition.StartIndex + chain.BlockPosition.Length > blockPosition.StartIndex)
                    {
                        chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                            chain.BlockPosition.Length);
                    }
                    else
                    {
                        break;
                    }
                }
                for (int index = context.DefinitionsBlock.Positions.Length - 1; index >= 0; index--)
                {
                    var position = context.DefinitionsBlock.Positions[index];
                    if (position.StartIndex < blockPosition.StartIndex &&
                        position.StartIndex + position.Length >
                        blockPosition.StartIndex + blockPosition.Length)
                    {
                        context.DefinitionsBlock.Positions[index] =
                            new BlockPosition(position.StartIndex - seed + blockPosition.Length,
                                position.Length - blockPosition.Length);
                    }
                    else if (position.StartIndex + position.Length > blockPosition.StartIndex)
                    {
                        context.DefinitionsBlock.Positions[index] = new BlockPosition(position.StartIndex - seed,
                            position.Length);
                    }
                    else
                    {
                        break;
                    }
                }
                foreach (var raw in context.RawOutputItems.Reverse())
                {
                    if (raw.BlockPosition.StartIndex + raw.BlockPosition.Length > blockPosition.StartIndex)
                    {
                        raw.BlockPosition = new BlockPosition(raw.BlockPosition.StartIndex - seed,
                            raw.BlockPosition.Length);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private static void RemoveDefinitions(ParseContext context, ref string workingDocument)
        {
            foreach (var blockPosition in context.DefinitionsBlock.Positions.Reverse())
            {
                int seed = ExStringBuilder.ApplyRemove(blockPosition, ref workingDocument);
                foreach (var chain in context.OutputChains.Reverse())
                {
                    if (chain.BlockPosition.StartIndex > blockPosition.StartIndex)
                        chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                            chain.BlockPosition.Length);
                    else
                    {
                        break;
                    }
                }
                foreach (var raw in context.RawOutputItems.Reverse())
                {
                    if (raw.BlockPosition.StartIndex + raw.BlockPosition.Length > blockPosition.StartIndex)
                    {
                        raw.BlockPosition = new BlockPosition(raw.BlockPosition.StartIndex - seed,
                            raw.BlockPosition.Length);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private static TemplateChain CompileParameterChain(IEnumerable<OutputItem> items, CompileContext compileContext,
            ParseContext parseContext)
        {
            TemplateChain result = new TemplateChain();
            ExType returnTypeChainedPrevious = null;
            foreach (var item in items.Reverse())
            {
                var compiledItem = CompileItem(item, compileContext, parseContext, ref returnTypeChainedPrevious);
                result.Add(compiledItem);
            }
            return result;
        }

        private static TemplateItem CompileItem
            (OutputItem extensionItem, CompileContext compileContext, ParseContext parseContext,
                ref ExType returnTypeChainedPrevious)
        {
            PropertyInfo data = null;
            ExType dataType = null;
            IRuntimeParameter parameter;
            IExtension extension;
            DefinitionItem definitionItem = null;
            if (parseContext.DefenitionExists(extensionItem.ExtensionName))
            {
                definitionItem = parseContext.GetDefenition(extensionItem.ExtensionName);
            }
            if (extensionItem.CallParameter.IsModelTypeParameter)
            {
                if (!string.IsNullOrEmpty(extensionItem.CallParameter.ModelParameter))
                {
                    if (compileContext.ModelType.IsDynamic ||
                        definitionItem != null && definitionItem.ModelType == "dynamic")
                    {
                        dataType = ExType.Dynamic;
                        CSharpArgumentInfo[] csharpArgumentInfoArray = new CSharpArgumentInfo[1];
                        csharpArgumentInfoArray[0] = CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null);
                        var callSite =
                            CallSite<Func<CallSite, object, object>>.Create(
                                Binder.GetMember(CSharpBinderFlags.None,
                                    extensionItem.CallParameter.ModelParameter, typeof (IRuntimeParameter),
                                    csharpArgumentInfoArray));
                        parameter = new DynamicParameter(callSite);
                    }
                    else
                    {
                        PropertyInfo dataProperty =
                            compileContext.ModelType.Type.GetProperty(extensionItem.CallParameter.ModelParameter,
                                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic |
                                BindingFlags.Public);
                        if (dataProperty != null && dataProperty.CanRead &&
                            !dataProperty.IsHaveAttribute<HiddenAttribute>() &&
                            (dataProperty.GetGetMethod(true).IsAssembly || dataProperty.GetGetMethod(true).IsPublic))
                            data = dataProperty;
                        else
                        {
                            throw new TemplateCompileException
                                (string.Format(CultureInfo.InvariantCulture, "Property {0} no found in Type [{1}]",
                                    extensionItem.CallParameter.ModelParameter, compileContext.ModelType).ToError());
                        }
                        parameter = new ModelParameter(data.ToPropertyGate());
                    }
                }
                else
                {
                    if (definitionItem != null && definitionItem.ModelType == "dynamic")
                    {
                        dataType = ExType.Dynamic;
                    }
                    else
                    {
                        dataType = compileContext.ModelType;
                    }
                    parameter = new EmptyParameter();
                }
            }
            else if (!string.IsNullOrEmpty(extensionItem.CallParameter.CSharpExpression))
            {
                if (!compileContext.Options.AllowCSharp)
                    throw new TemplateCompileException(
                        "C# Code Not allowed here, see TemplateOptions.AllowCSharp Property".ToError());
                var chainedType = returnTypeChainedPrevious ?? ExType.Dynamic;
                var expressionOptions = new ExpressionOptions
                {
                    ChainedType = chainedType,
                    Expression = extensionItem.CallParameter.CSharpExpression,
                    ExtensionName = extensionItem.ExtensionName
                };
                object constantResult;
                dataType = compileContext.ParseAndGetResultType(expressionOptions, out constantResult);
                extension = CreateExtension(extensionItem, compileContext, extensionItem.Context ?? parseContext,
                    ref returnTypeChainedPrevious, null, dataType,  definitionItem);
                if (constantResult == null)
                {
                    return new TemplateItem(returnTypeChainedPrevious, extension)
                    {
                        Parameter = compileContext.PushCompileExpression(expressionOptions)
                    };
                }
                return new TemplateItem(returnTypeChainedPrevious, extension)
                {
                    Parameter = new ConstantParameter(constantResult)
                };
            }
            else
            {
                var callParameter = CompileParameterChain(extensionItem.CallParameter.ChainParameter, compileContext,
                    parseContext);
                dataType = callParameter.RenderType;
                parameter = new ChainedParameter(callParameter);
            }
            extension = CreateExtension(extensionItem, compileContext, extensionItem.Context ?? parseContext,
                ref returnTypeChainedPrevious, data, dataType, definitionItem);
            return new TemplateItem(returnTypeChainedPrevious, extension)
            {
                Parameter = parameter
            };
        }

        private static IExtension CreateExtension(OutputItem extensionItem, CompileContext compileContext,
            ParseContext parseContext,
            ref ExType returnTypeChainedPrevious, PropertyInfo data, ExType dataType, 
            DefinitionItem definition)
        {
            IExtension extension;
            if (definition != null)
            {
                Type acceptType;
                var def = CompileFromDefenition(definition, compileContext, out acceptType);
                extension = def;
                if (data != null)
                {
                    dataType = data.PropertyType;
                    CheckTypes(data, acceptType);
                }
                else
                {
                    if (acceptType != typeof(object))
                        dataType = acceptType;

                    CheckTypes(dataType, acceptType);
                }
                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate, dataType,
                    returnTypeChainedPrevious, compileContext, parseContext);

                def.DefenitionTemplate = CompileFromDefenition(definition, compileContext, out acceptType);
                returnTypeChainedPrevious = InitializeTemplate(def.DefenitionTemplate, definition.ParameterTemplate,
                    dataType,
                    returnTypeChainedPrevious, compileContext, definition.Context);
            }
            else
            {
                extension = TemplateFactory.Create(extensionItem.ExtensionName, parseContext);
                Type templateType = extension.GetType();

                var chainedTypeAttributes = templateType.GetAttributes<ChainedTypeAttribute>(true);

                if (returnTypeChainedPrevious != null)
                {
                    CheckTypes(returnTypeChainedPrevious, chainedTypeAttributes.Select(a => (ExType)a.DataType).ToArray());
                }

                var dataTypeAttributes =
                    templateType.GetAttributes<DataTypeAttribute>(true);
                if (data != null)
                {
                    dataType = data.PropertyType;
                    CheckTypes(data, dataTypeAttributes.Select(a => a.DataType).ToArray());
                }
                else
                {
                    CheckTypes(dataType, dataTypeAttributes.Select(a => (ExType) a.DataType).ToArray());
                }

                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate, dataType,
                    returnTypeChainedPrevious, compileContext, parseContext);
            }
            return extension;
        }

        private static DefenitionBaseExtension CompileFromDefenition(DefinitionItem definition,
            CompileContext compileContext, out Type acceptType)
        {
            WalkValidateDefinitionType(definition, compileContext);
            var result = new DefenitionBaseExtension();
            acceptType = ReflectionHelper.ResolveType(definition.ModelType, compileContext.Namespaces.ToArray()) ??
                         typeof (object);
            return result;
        }

        private static void WalkValidateDefinitionType(DefinitionItem definition, CompileContext context)
        {
            var currentType = ReflectionHelper.ResolveType(definition.ModelType, context.Namespaces.ToArray()) ??
                              typeof (object);
            var definitionBase = definition.BaseDefinition;
            while (definitionBase != null)
            {
                var baseType = ReflectionHelper.ResolveType(definitionBase.ModelType, context.Namespaces.ToArray()) ??
                               typeof (object);
                if (!baseType.IsType(currentType))
                {
                    throw new TemplateDefinitionTypeException(definition.Position,
                        "The new definition type isn't assignable to base.");
                }

                definitionBase = definitionBase.BaseDefinition;
            }
        }


        private static ExType InitializeTemplate
            (IExtension extension, string parameterFastString, ExType modelType, ExType chainedType,
                CompileContext context, ParseContext parseContext)
        {
            modelType = modelType ?? typeof (object);
            chainedType = chainedType ?? typeof (object);
            RenderType directRender = extension.GetType().IsHaveAttribute<EncodeOutputAttribute>(true)
                ? (extension.GetType().IsHaveAttribute<NotEncodeAttribute>(true) ? RenderType.Raw : RenderType.Encode)
                : RenderType.Raw;
            extension.SetUpRenderType(directRender);
            return extension.InitStart(new InitContext(parameterFastString, context, parseContext), modelType, chainedType);
        }

        private static void CheckTypes(PropertyInfo property, params Type[] dataTypes)
        {
            if (property != null && dataTypes.Any() && dataTypes.All(type => !(type ?? typeof (object)).IsType(property.PropertyType)))
            {
                throw new TemplateCompileException
                    (string.Format
                        (CultureInfo.InvariantCulture, "Property {0} have Type {1} but any of [{2}] expected.",
                            property.Name,
                            property.PropertyType.FullName,
                            string.Join(", ", dataTypes.Select(t => t.FullName))).ToError());
            }
        }

        private static void CheckTypes(ExType returnType, params ExType[] dataTypes)
        {
            returnType = returnType ?? typeof (object);
            if (dataTypes.Any() && dataTypes.All(dataType =>
            {
                dataType = dataType ?? typeof (object);
                if (dataType.IsDynamic || returnType.IsDynamic)
                    return false;
                return !dataType.Type.IsType(returnType.Type);
            }))
            {
                throw new TemplateCompileException
                    (string.Format
                        (CultureInfo.InvariantCulture, "Return Type is {0} but any of [{1}] expected.",
                            returnType.Type.FullName,
                            string.Join(", ", dataTypes.Select(t => t.Type.FullName))).ToError());
            }
        }
    }
}