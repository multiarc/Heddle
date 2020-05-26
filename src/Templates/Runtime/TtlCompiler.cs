using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
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
        public static RuntimeDocument Compile(string document, CompileScope compileScope, ParseContext parseContext, ExType chainedType)
        {
            if (compileScope == null)
                throw new ArgumentNullException(nameof(compileScope));
            string workingDocument = document;
            ShiftBySkippedTokens(parseContext);
            RemoveDefinitions(parseContext, ref workingDocument);
            ReplaceRawOutput(parseContext, ref workingDocument);
            var documentElements = new List<DocumentElement>();
            foreach (var extensions in parseContext.OutputChains)
            {
                var element = new DocumentElement(extensions.BlockPosition);
                ExType returnTypeChainedPrevious = chainedType;
                foreach (var item in ((ICollection<OutputItem>) extensions.Chain).Reverse())
                {
                    try
                    {
                        var compiledItem = CompileItem(item, compileScope, extensions.Context,
                            ref returnTypeChainedPrevious);
                        element.CallChain.Add(compiledItem);
                    }
                    catch (Exception e)
                    {
                        compileScope.CompileErrors.Add(new TtlCompileError
                        {
                            Exception = e,
                            Position = item.Position,
                            Error = $"Error while compiling {item.ExtensionName}"
                        });
                    }
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
            foreach (var extensions in parseContext.DefaultChains)
            {
                var element = new DocumentElement(new BlockPosition(workingDocument.Length, 0));
                ExType returnTypeChainedPrevious = chainedType;
                foreach (var item in ((ICollection<OutputItem>) extensions.Chain).Reverse())
                {
                    try
                    {
                        var compiledItem = CompileItem(item, compileScope, extensions.Context,
                            ref returnTypeChainedPrevious);
                        element.CallChain.Add(compiledItem);
                    }
                    catch (Exception e)
                    {
                        compileScope.CompileErrors.Add(new TtlCompileError
                        {
                            Exception = e,
                            Position = item.Position,
                            Error = $"Error while compiling {item.ExtensionName}"
                        });
                    }
                }
                if (returnTypeChainedPrevious != null)
                {
                    documentElements.Add(element);
                }
            }
            return new RuntimeDocument(workingDocument, documentElements.ToArray(), compileScope);
        }

        private static void RemoveEmptyItem(ParseContext context, BlockPosition blockPosition,
            ref string workingDocument)
        {
            int seed = ExStringBuilder.ApplyRemove(blockPosition, ref workingDocument);
            foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
            {
                if (chain.BlockPosition.StartIndex > blockPosition.StartIndex)
                {
                    chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed, chain.BlockPosition.Length);
                }
                else
                {
                    break;
                }
            }
        }

        private static void ReplaceRawOutput(ParseContext context, ref string workingDocument)
        {
            foreach (var rawOut in ((ICollection<RawOutputItem>)context.RawOutputItems).Reverse())
            {
                workingDocument = ExStringBuilder.Replace(rawOut.BlockPosition.StartIndex, rawOut.BlockPosition.Length,
                    rawOut.Text, workingDocument);
                int seed = rawOut.BlockPosition.Length - rawOut.Text.Length;
                var outputItem = rawOut;
                foreach (var chain in ((ICollection<OutputChain>)context.OutputChains).Reverse())
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

        private static void ShiftBySkippedTokens(ParseContext context)
        {
            foreach (var blockPosition in ((ICollection<BlockPosition>)context.SkippedTokens).Reverse())
            {
                var seed = blockPosition.Length;
                
                var startToSkip = blockPosition.StartIndex;
                var endToSkip = blockPosition.StartIndex + blockPosition.Length - 1;
                
                foreach (var chain in ((ICollection<OutputChain>)context.OutputChains).Reverse())
                {
                    var chainBlockStart = chain.BlockPosition.StartIndex;
                    var chainBlockEnd = chain.BlockPosition.StartIndex + chain.BlockPosition.Length - 1;

                    if (chainBlockStart <= startToSkip && chainBlockEnd >= endToSkip)
                    {
                        chain.BlockPosition = new BlockPosition(chainBlockStart,
                            chain.BlockPosition.Length - seed);
                    }
                    else if (chainBlockEnd > startToSkip)
                    {
                        chain.BlockPosition = new BlockPosition(chainBlockStart - seed,
                            chain.BlockPosition.Length);
                    }
                    else
                    {
                        break;
                    }
                }
                for (int index = context.DefinitionsBlock.Positions.Count - 1; index >= 0; index--)
                {
                    var position = context.DefinitionsBlock.Positions[index];
                    
                    var definitionBlockStart = position.StartIndex;
                    var definitionBlockEnd = position.StartIndex + position.Length - 1;
                    
                    if (definitionBlockStart <= startToSkip && definitionBlockEnd >= endToSkip)
                    {
                        context.DefinitionsBlock.Positions[index] =
                            new BlockPosition(definitionBlockStart,
                                position.Length - seed);
                    }
                    else if (definitionBlockEnd > startToSkip)
                    {
                        context.DefinitionsBlock.Positions[index] = new BlockPosition(definitionBlockStart - seed,
                            position.Length);
                    }
                    else
                    {
                        break;
                    }
                }
                foreach (var raw in ((ICollection<RawOutputItem>)context.RawOutputItems).Reverse())
                {
                    var rawBlockStart = raw.BlockPosition.StartIndex;
                    var rawBlockEnd = raw.BlockPosition.StartIndex + raw.BlockPosition.Length - 1;
                    
                    if (rawBlockEnd > blockPosition.StartIndex)
                    {
                        raw.BlockPosition = new BlockPosition(rawBlockStart - seed,
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
            foreach (var definitionBlock in ((ICollection<BlockPosition>)context.DefinitionsBlock.Positions).Reverse())
            {
                int seed = ExStringBuilder.ApplyRemove(definitionBlock, ref workingDocument);
                foreach (var chain in ((ICollection<OutputChain>)context.OutputChains).Reverse())
                {
                    if (chain.BlockPosition.StartIndex >= definitionBlock.StartIndex + definitionBlock.Length)
                    {
                        chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                            chain.BlockPosition.Length);
                    }
                    else
                    {
                        break;
                    }
                }
                foreach (var raw in ((ICollection<RawOutputItem>)context.RawOutputItems).Reverse())
                {
                    if (raw.BlockPosition.StartIndex >= definitionBlock.StartIndex + definitionBlock.Length)
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

        private static TemplateChain CompileParameterChain(IEnumerable<OutputItem> items, CompileScope compileContext,
            ParseContext parseContext, ExType returnTypeChainedPrevious)
        {
            TemplateChain result = new TemplateChain();
            foreach (var item in items.Reverse())
            {
                var compiledItem = CompileItem(item, compileContext, parseContext, ref returnTypeChainedPrevious);
                result.Add(compiledItem);
            }
            return result;
        }

        private static TemplateItem CompileItem
            (OutputItem extensionItem, CompileScope compileScope, ParseContext parseContext,
                ref ExType returnTypeChainedPrevious)
        {
            if (compileScope.CompileContext.CompiledItems.TryGetValue(extensionItem, out var result))
            {
                returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
                return result.CompiledItem;
            }
            result = new CompiledElement {CompiledItem = new TemplateItem(), ReturnTypeChainedPrevious = returnTypeChainedPrevious};
            compileScope.CompileContext.CompiledItems.Add(extensionItem, result);

            ExType inputModelType = null;
            ExType dataType;
            IExtension extension;
            DefinitionItem definitionItem = null;
            if (parseContext.DefenitionExists(extensionItem.ExtensionName))
            {
                definitionItem = parseContext.GetDefenition(extensionItem.ExtensionName);
            }
            if (extensionItem.CallParameter.IsModelTypeParameter)
            {
                if (extensionItem.CallParameter.ModelParameter.Any() &&
                    !string.IsNullOrEmpty(extensionItem.CallParameter.ModelParameter.First()))
                {
                    dataType = CompileModelAccessor(extensionItem, compileScope, definitionItem, result, ref inputModelType);
                }
                else
                {
                    if (definitionItem != null && definitionItem.ModelType == "dynamic")
                    {
                        dataType = ExType.Dynamic;
                    }
                    else
                    {
                        dataType = compileScope.ScopeType;
                    }
                    result.CompiledItem.Parameter = new EmptyParameter();
                }
            }
            else if (!string.IsNullOrEmpty(extensionItem.CallParameter.CSharpExpression))
            {
                if (!compileScope.Options.AllowCSharp)
                {
                    compileScope.CompileErrors.Add(
                        "C# Code Not allowed here, see TemplateOptions.AllowCSharp Property".ToError(extensionItem.Position));
                    return null;
                }
                var chainedType = returnTypeChainedPrevious ?? ExType.Dynamic;
                var expressionOptions = new ExpressionOptions
                {
                    ChainedType = chainedType,
                    Expression = extensionItem.CallParameter.CSharpExpression,
                    ExtensionName = extensionItem.ExtensionName,
                    Position = extensionItem.Position
                };
                OptionalValue<object> constantResult = compileScope.CSharpContext.ParseAndGetResultType(compileScope.CompileContext, expressionOptions, out dataType);
                extension = CreateExtension(extensionItem, compileScope, extensionItem.Context ?? parseContext,
                    ref result.ReturnTypeChainedPrevious, null, dataType, definitionItem);

                returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
                result.CompiledItem.ReturnType = result.ReturnTypeChainedPrevious;
                result.CompiledItem.Extension = extension;
                if (!constantResult.HasValue)
                {
                    result.CompiledItem.Parameter = compileScope.CSharpContext.PushCompileExpression(expressionOptions, compileScope.CompileContext);
                    return result.CompiledItem;
                }
                result.CompiledItem.Parameter = new ConstantParameter(constantResult.Value);
                return result.CompiledItem;
            }
            else
            {
                var callParameter = CompileParameterChain(extensionItem.CallParameter.ChainParameter, compileScope,
                    parseContext, returnTypeChainedPrevious);
                dataType = callParameter.RenderType;
                result.CompiledItem.Parameter = new ChainedParameter(callParameter);
            }
            extension = CreateExtension(extensionItem, compileScope, extensionItem.Context ?? parseContext,
                ref result.ReturnTypeChainedPrevious, inputModelType, dataType, definitionItem);
            returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
            result.CompiledItem.ReturnType = result.ReturnTypeChainedPrevious;
            result.CompiledItem.Extension = extension;
            return result.CompiledItem;
        }

        private static ExType CompileModelAccessor(OutputItem extensionItem, CompileScope compileContext, DefinitionItem definitionItem,
            CompiledElement result, ref ExType inputType)
        {
            ExType scopeType = extensionItem.CallParameter.RootReference ? compileContext.CompileContext.RootScopeType : compileContext.CompileContext.ScopeType;
            if (scopeType.IsDynamic ||
                definitionItem != null && definitionItem.ModelType == ExType.Dynamic.ToString())
            {
                if (extensionItem.CallParameter.RootReference)
                {
                    result.CompiledItem.Parameter = new RootDynamicParameter(extensionItem.CallParameter.ModelParameter);
                }
                else
                {
                    result.CompiledItem.Parameter = new DynamicParameter(extensionItem.CallParameter.ModelParameter);
                }
                return ExType.Dynamic;
            }
            var modelParameters = extensionItem.CallParameter.ModelParameter;
            var dataProperties = new KeyValuePair<Type, PropertyInfo>[modelParameters.Length];
            Type currentType = scopeType.Type;
            for (int i = 0; i < modelParameters.Length; i++)
            {
                var dataProperty = currentType.GetProperty(modelParameters[i],
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic |
                    BindingFlags.Public);
                if (dataProperty == null || !dataProperty.CanRead || dataProperty.IsHaveAttribute<HiddenAttribute>() ||
                    (!dataProperty.GetGetMethod(true).IsAssembly && !dataProperty.GetGetMethod(true).IsPublic))
                {
                    compileContext.CompileContext.CompileErrors.Add(
                        $"Property {extensionItem.CallParameter.ModelParameter[i]} not found in Type [{currentType}]"
                            .ToError(extensionItem.Position));
                    return null;
                }
                dataProperties[i] = new KeyValuePair<Type, PropertyInfo>(currentType, dataProperty);
                currentType = dataProperty.PropertyType;
            }
            inputType = dataProperties.Last().Value.PropertyType;
            if (extensionItem.CallParameter.RootReference)
            {
                result.CompiledItem.Parameter = new RootModelParameter(dataProperties);
            }
            else
            {
                result.CompiledItem.Parameter = new ModelParameter(dataProperties);
            }
            return inputType;
        }

        private static CallSite<Func<CallSite, object, object>> CreateBinder(string model, CSharpArgumentInfo[] csharpArgumentInfoArray)
        {
            return
                CallSite<Func<CallSite, object, object>>.Create(Binder.GetMember(CSharpBinderFlags.None, model, typeof (IRuntimeParameter),
                    csharpArgumentInfoArray));
        }

        private static IExtension CreateExtension(OutputItem extensionItem, CompileScope compileScope,
            ParseContext parseContext,
            ref ExType returnTypeChainedPrevious, ExType inputModelType, ExType dataType, 
            DefinitionItem definition)
        {
            IExtension extension;
            if (definition != null)
            {
                var def = CompileFromDefenition(definition, compileScope, out var acceptType);
                extension = def;
                if (inputModelType != null)
                {
                    dataType = inputModelType;
                }
                else
                {
                    if (acceptType != typeof (object))
                        dataType = acceptType;
                }
                CheckTypes(dataType, definition.Position, compileScope, acceptType);
                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate, dataType,
                    returnTypeChainedPrevious, compileScope, parseContext);

                def.DefinitionParameterTemplate = CompileFromDefenition(definition, compileScope, out acceptType);
                returnTypeChainedPrevious = InitializeTemplate(def.DefinitionParameterTemplate, definition.ParameterTemplate,
                    dataType,
                    returnTypeChainedPrevious, compileScope, definition.Context);
            }
            else
            {
                extension = TemplateFactory.Create(extensionItem.ExtensionName, extensionItem.Position, parseContext, compileScope.CompileContext);
                if (extension == null)
                    return null;
                Type templateType = extension.GetType();

                var chainedTypeAttributes = templateType.GetAttributes<ChainedTypeAttribute>(true);

                if (returnTypeChainedPrevious != null)
                {
                    CheckTypes(returnTypeChainedPrevious, extensionItem.Position, compileScope, chainedTypeAttributes.Select(a => (ExType)a.DataType).ToArray());
                }

                var dataTypeAttributes =
                    templateType.GetAttributes<DataTypeAttribute>(true);
                if (inputModelType != null)
                {
                    dataType = inputModelType;
                }
                CheckTypes(dataType, extensionItem.Position, compileScope, dataTypeAttributes.Select(a => (ExType)a.DataType).ToArray());

                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate, dataType,
                    returnTypeChainedPrevious, compileScope, parseContext);
            }
            return extension;
        }

        private static DefinitionBaseExtension CompileFromDefenition(DefinitionItem definition,
            CompileScope compileScope, out Type acceptType)
        {
            WalkValidateDefinitionType(definition, compileScope);
            var result = new DefinitionBaseExtension {Position = definition.Position};
            try
            {
                acceptType = ReflectionHelper.ResolveType(definition.ModelType, compileScope.CSharpContext.Namespaces) ??
                             typeof (object);
            }
            catch (InvalidOperationException e)
            {
                compileScope.CompileContext.CompileErrors.Add(e.ToError(definition.Position));
                acceptType = typeof(object);
            }
            return result;
        }

        private static void WalkValidateDefinitionType(DefinitionItem definition, CompileScope context)
        {
            try
            {
                var currentType = ReflectionHelper.ResolveType(definition.ModelType, context.CSharpContext.Namespaces) ??
                                  typeof (object);

                var definitionBase = definition.BaseDefinition;
                while (definitionBase != null)
                {
                    var baseType = ReflectionHelper.ResolveType(definitionBase.ModelType, context.CSharpContext.Namespaces) ??
                                   typeof (object);
                    if (!baseType.IsType(currentType))
                    {
                        context.CompileContext.CompileErrors.Add(
                            $"The new definition type <{currentType}> isn't assignable to base <{baseType}>.".ToError(definition.Position));
                    }

                    definitionBase = definitionBase.BaseDefinition;
                }
            }
            catch (InvalidOperationException e)
            {
                context.CompileContext.CompileErrors.Add(e.ToError(definition.Position));
            }
        }


        private static ExType InitializeTemplate
            (IExtension extension, string parameterFastString, ExType modelType, ExType chainedType,
                CompileScope compileScope, ParseContext parseContext)
        {
            modelType ??= typeof (object);
            chainedType ??= typeof (object);
            RenderType directRender = extension.GetType().IsHaveAttribute<EncodeOutputAttribute>(true)
                ? (extension.GetType().IsHaveAttribute<NotEncodeAttribute>(true) ? RenderType.Raw : RenderType.Encode)
                : RenderType.Raw;
            extension.SetUpRenderType(directRender);
            return extension.InitStart(new InitContext(parameterFastString, compileScope, parseContext), modelType, chainedType, compileScope.ScopeType);
        }

        //private static void CheckTypes(PropertyInfo property, BlockPosition extensionPosition, CompileContext context, params Type[] dataTypes)
        //{
        //    if (property != null && dataTypes.Any() && dataTypes.All(type => !(type ?? typeof (object)).IsType(property.PropertyType)))
        //    {
        //        context.CompileErrors.Add
        //            (string.Format
        //                (CultureInfo.InvariantCulture, "Property {0} have Type {1} but any of [{2}] expected.",
        //                    property.Name,
        //                    property.PropertyType.FullName,
        //                    string.Join(", ", dataTypes.Select(t => t.FullName))).ToError(extensionPosition));
        //    }
        //}

        private static void CheckTypes(ExType returnType, BlockPosition extensionPosition, CompileScope compileScope, params ExType[] dataTypes)
        {
            returnType ??= typeof (object);
            if (!returnType.IsDynamic)
                returnType = returnType.Type.UnwrapNullable();
            if (dataTypes.Any() && dataTypes.All(dataType =>
            {
                dataType ??= typeof (object);
                if (dataType.IsDynamic || returnType.IsDynamic)
                    return false;
                return !dataType.Type.IsType(returnType.Type);
            }))
            {
                compileScope.CompileContext.CompileErrors.Add
                    (string.Format
                        (CultureInfo.InvariantCulture, "Return Type is {0} but any of [{1}] expected.",
                            returnType.Type.FullName,
                            string.Join(", ", dataTypes.Select(t => t.Type.FullName))).ToError(extensionPosition));
            }
        }
    }
}