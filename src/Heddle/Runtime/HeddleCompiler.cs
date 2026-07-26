using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Extensions;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Language.Expressions;
using Heddle.Runtime.Expressions;
using Heddle.Runtime.Parameters;
using Heddle.Strings;
using Heddle.Strings.Core;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Heddle.Runtime
{
    internal class HeddleCompiler
    {
        public static RuntimeDocument Compile(string document, CompileScope compileScope, ParseContext parseContext,
            ExType chainedType)
        {
            if (compileScope == null)
                throw new ArgumentNullException(nameof(compileScope));

            // Never record imports' foreign offsets in the analyzed document's offset map; bracket their diagnostics for re-anchoring.
            var importOrigin = parseContext.ImportOrigin;
            if (importOrigin == null)
            {
                compileScope.CompileContext.ScopeMap?.Record(
                    parseContext.AbsoluteOffset, document.Length,
                    compileScope.ScopeType, chainedType);
            }

            int importErrorMark = importOrigin != null ? compileScope.CompileErrors.Count : 0;
            int importWarningMark = importOrigin != null ? compileScope.CompileWarnings.Count : 0;
            try
            {
                return CompileBody(document, compileScope, parseContext, chainedType);
            }
            finally
            {
                if (importOrigin != null)
                {
                    StampAppended(compileScope.CompileErrors, importErrorMark, importOrigin);
                    StampAppended(compileScope.CompileWarnings, importWarningMark, importOrigin);
                }
            }
        }

        /// <summary>
        /// Stamps every entry appended to <paramref name="list"/> after <paramref name="mark"/> with
        /// <paramref name="origin"/> when it carries no marker yet (idempotent; a nested import's marker is left in
        /// place — its <see cref="ImportOrigin.Site"/> is re-anchored at the import block, not here).
        /// </summary>
        private static void StampAppended<T>(List<T> list, int mark, ImportOrigin origin)
            where T : HeddleCompileError
        {
            for (int i = mark; i < list.Count; i++)
            {
                if (list[i].ImportOrigin == null)
                    list[i].ImportOrigin = origin;
            }
        }

        private static RuntimeDocument CompileBody(string document, CompileScope compileScope,
            ParseContext parseContext, ExType chainedType)
        {
            string workingDocument = document;
            bool trimDirectiveLines = compileScope.Options.TrimDirectiveLines;
            DocumentShaping.ShiftBySkippedTokens(parseContext);
            // HED4005 scan runs here when coordinates are consistent with exclusion spans.
            ScanBraceMisreads(parseContext, compileScope, workingDocument);
            if (trimDirectiveLines)
                DocumentShaping.TrimHiddenRemnantLines(parseContext, ref workingDocument);
            DocumentShaping.RemoveDefinitions(parseContext, ref workingDocument, trimDirectiveLines);
            DocumentShaping.ReplaceRawOutput(parseContext, ref workingDocument);
            ProcessBranchSets(parseContext, compileScope, ref workingDocument);
            var documentElements = new List<DocumentElement>();
            // HED2004 left scan excises earlier @(…) source spans; they're unresolved interpolation, not HTML literal.
            var htmlLintLeftSpans = new List<BlockPosition>();
            foreach (var extensions in parseContext.OutputChains)
            {
                var element = new DocumentElement(extensions.BlockPosition);
                ExType returnTypeChainedPrevious = chainedType;
                bool hasProducerToRight = false;
                foreach (var item in ((ICollection<OutputItem>) extensions.Chain).Reverse())
                {
                    try
                    {
                        var compiledItem = CompileItem(item, compileScope, extensions.Context,
                            ref returnTypeChainedPrevious);
                        if (compiledItem != null)
                        {
                            MarkChainConsumer(compiledItem, item, hasProducerToRight);
                            element.CallChain.Add(compiledItem);
                        }
                    }
                    catch (Exception e)
                    {
                        compileScope.CompileErrors.Add(new HeddleCompileError
                        {
                            Exception = e,
                            Position = item.Position,
                            Error = $"Error while compiling {item.ExtensionName}"
                        });
                    }

                    hasProducerToRight = true;
                }

                if (returnTypeChainedPrevious == null)
                {
                    DocumentShaping.RemoveEmptyItem(parseContext, extensions.BlockPosition, ref workingDocument,
                        trimDirectiveLines);
                }
                else
                {
                    // Profile and coordinates are consistent here.
                    ScanHtmlContextLint(extensions, htmlLintLeftSpans, compileScope, workingDocument);
                    documentElements.Add(element);
                    htmlLintLeftSpans.Add(extensions.BlockPosition);
                }
            }

            foreach (var extensions in parseContext.DefaultChains)
            {
                var element = new DocumentElement(new BlockPosition(workingDocument.Length, 0));
                ExType returnTypeChainedPrevious = chainedType;
                bool hasProducerToRight = false;
                foreach (var item in ((ICollection<OutputItem>) extensions.Chain).Reverse())
                {
                    try
                    {
                        var compiledItem = CompileItem(item, compileScope, extensions.Context,
                            ref returnTypeChainedPrevious);
                        if (compiledItem != null)
                        {
                            MarkChainConsumer(compiledItem, item, hasProducerToRight);
                            element.CallChain.Add(compiledItem);
                        }
                    }
                    catch (Exception e)
                    {
                        compileScope.CompileErrors.Add(new HeddleCompileError
                        {
                            Exception = e,
                            Position = item.Position,
                            Error = $"Error while compiling {item.ExtensionName}"
                        });
                    }

                    hasProducerToRight = true;
                }

                if (returnTypeChainedPrevious != null)
                {
                    documentElements.Add(element);
                }
            }

            return new RuntimeDocument(workingDocument, documentElements.ToArray(), compileScope);
        }

        private enum OrphanState
        {
            None,
            Open,
            Closed,
            Unknown
        }

        /// <summary>
        /// Compile-time branch-set scan: classifies blocks and runs orphan state machine for HED3001/3002/3003/3004/3005.
        /// Called between ReplaceRawOutput and chain-compile loop where coordinates are consistent.
        /// </summary>
        private static void ProcessBranchSets(ParseContext parseContext, CompileScope compileScope,
            ref string workingDocument)
        {
            DocumentShaping.StripBranchSets(parseContext, ref workingDocument,
                chain => Classify(chain, chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null),
                new BranchSetDiagnostics(compileScope));
        }

        /// <summary>
        /// Runtime-only branch-set observer: emits HED300x diagnostics and orphan state machine over the shared strip machine's event stream.
        /// </summary>
        private sealed class BranchSetDiagnostics : DocumentShaping.IBranchStripObserver
        {
            private readonly CompileScope _compileScope;
            private OrphanState _state = OrphanState.None;

            internal BranchSetDiagnostics(CompileScope compileScope) => _compileScope = compileScope;

            public void OnClassified(OutputChain chain, OutputItem leftmost, DocumentShaping.BranchKind kind)
            {
                if (kind == DocumentShaping.BranchKind.Continuation || kind == DocumentShaping.BranchKind.Terminal)
                    WarnIfMissingScopeChannel(leftmost, _compileScope);
            }

            public void OnGapCollected(OutputChain prev, OutputChain next, OutputItem nextLeftmost,
                BlockPosition gap, string gapText)
            {
                if (!string.IsNullOrWhiteSpace(gapText) && nextLeftmost != null)
                {
                    _compileScope.CompileWarnings.Add(new HeddleCompileWarning
                    {
                        Error = "Text between branch blocks is never rendered.",
                        Fix = "Move it before the '@if', after the last branch, or into a branch body.",
                        Position = nextLeftmost.Position,
                        DiagnosticId = HeddleDiagnosticIds.BranchTextStripped
                    });
                }
            }

            public void OnBlockCompleted(OutputChain chain, OutputItem leftmost, DocumentShaping.BranchKind kind)
            {
                switch (kind)
                {
                    case DocumentShaping.BranchKind.Opener:
                        _state = OrphanState.Open;
                        break;

                    case DocumentShaping.BranchKind.Continuation:
                        if (_state == OrphanState.None || _state == OrphanState.Closed)
                        {
                            _compileScope.CompileWarnings.Add(new HeddleCompileWarning
                            {
                                Error =
                                    $"'@{leftmost.ExtensionName}' is a branch continuation with no preceding opener in this scope — it starts a new set.",
                                Fix =
                                    "Open the set with a branch opener (such as '@if(...)'), or use a standalone opener if an independent condition is intended.",
                                Position = leftmost.Position,
                                DiagnosticId = HeddleDiagnosticIds.ElifWithoutIf
                            });
                        }

                        _state = OrphanState.Open;
                        break;

                    case DocumentShaping.BranchKind.Terminal:
                        if (leftmost != null && !IsEmptyParameter(leftmost))
                        {
                            _compileScope.CompileWarnings.Add(new HeddleCompileWarning
                            {
                                Error = "A branch terminal takes no condition — its parameter is ignored.",
                                Fix = "Use a branch continuation (such as '@elif(...)') for a conditional branch, or remove the parameter.",
                                Position = leftmost.Position,
                                DiagnosticId = HeddleDiagnosticIds.ElseConditionIgnored
                            });
                        }

                        if (_state == OrphanState.None || _state == OrphanState.Closed)
                        {
                            _compileScope.CompileErrors.Add(
                                $"'@{leftmost?.ExtensionName}' is a branch terminal with no matching opener in this scope."
                                    .ToError(leftmost?.Position ?? chain.BlockPosition,
                                        HeddleDiagnosticIds.ElseWithoutIf));
                            // state unchanged — a further orphan @else errors again.
                        }
                        else
                        {
                            _state = OrphanState.Closed;
                        }

                        break;

                    case DocumentShaping.BranchKind.Participant:
                        _state = OrphanState.Unknown;
                        break;

                    default: // Other
                        // Non-branch blocks leave runtime frame intact so following @else can still bind.
                        break;
                }
            }
        }

        private static DocumentShaping.BranchKind Classify(OutputChain chain, OutputItem leftmost)
        {
            if (leftmost == null)
                return DocumentShaping.BranchKind.Other;
            var name = leftmost.ExtensionName;
            if (chain.Context != null && chain.Context.DefenitionExists(name))
                return DocumentShaping.BranchKind.Other;  // Definition shadows extension.

            if (string.IsNullOrEmpty(name) ||
                !TemplateFactory.TryGetExtensionType(name, out var extensionType))
                return DocumentShaping.BranchKind.Other;

            var role = extensionType.GetBranchRole();  // inherit: true
            if (role.HasValue)
                switch (role.Value)
                {
                    case BranchRole.Opener:       return DocumentShaping.BranchKind.Opener;
                    case BranchRole.Continuation: return DocumentShaping.BranchKind.Continuation;
                    case BranchRole.Terminal:     return DocumentShaping.BranchKind.Terminal;
                }

            if (extensionType.IsHaveAttribute<ScopeChannelAttribute>(true))
                return DocumentShaping.BranchKind.Participant;  // Declared role wins over Participant.

            return DocumentShaping.BranchKind.Other;
        }

        private static void WarnIfMissingScopeChannel(OutputItem leftmost, CompileScope compileScope)
        {
            if (leftmost == null)
                return;
            var name = leftmost.ExtensionName;
            if (string.IsNullOrEmpty(name) || !TemplateFactory.TryGetExtensionType(name, out var extensionType))
                return;
            if (extensionType.IsHaveAttribute<ScopeChannelAttribute>(true))
                return;

            compileScope.CompileWarnings.Add(new HeddleCompileWarning
            {
                Error =
                    $"A branch continuation/terminal '@{name}' does not carry [ScopeChannel]; it cannot read the branch state at render time.",
                Fix = "Add [ScopeChannel] to the extension so it can read the branch state.",
                Position = leftmost.Position,
                DiagnosticId = HeddleDiagnosticIds.BranchRoleMissingScopeChannel
            });
        }

        private static bool IsEmptyParameter(OutputItem item)
        {
            var callParameter = item.CallParameter;
            if (!callParameter.IsModelTypeParameter)
                return false; // chain / C# / native expression parameter — non-empty
            return callParameter.ModelParameter == null || callParameter.ModelParameter.Length == 0 ||
                   string.IsNullOrEmpty(callParameter.ModelParameter[0]);
        }

        /// <summary>
        /// Matches Liquid/Jinja style braces around a single ASCII identifier or dotted path.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex BraceMisreadRegex =
            new System.Text.RegularExpressions.Regex(
                @"\{\{[ \t]*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*\}\}",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// HED4005 lint: warns when <c>{{ … }}</c> in text renders literal braces instead of interpolating.
        /// Skips matches inside exclusion spans. Warning-only; never blocks compilation or changes bytes.
        /// </summary>
        private static void ScanBraceMisreads(ParseContext parseContext, CompileScope compileScope,
            string workingDocument)
        {
            static bool Contains(BlockPosition b, int i) => i >= b.StartIndex && i < b.StartIndex + b.Length;

            foreach (System.Text.RegularExpressions.Match m in BraceMisreadRegex.Matches(workingDocument))
            {
                int at = m.Index;
                bool excluded = false;
                foreach (var chain in parseContext.OutputChains)
                {
                    if (Contains(chain.BlockPosition, at)) { excluded = true; break; }
                }

                if (!excluded)
                {
                    foreach (var raw in parseContext.RawOutputItems)
                    {
                        if (Contains(raw.BlockPosition, at)) { excluded = true; break; }
                    }
                }

                if (!excluded)
                {
                    foreach (var definition in parseContext.DefinitionsBlock.Positions)
                    {
                        if (Contains(definition, at)) { excluded = true; break; }
                    }
                }

                if (excluded)
                    continue;

                var path = m.Groups[1].Value;
                compileScope.CompileWarnings.Add(new HeddleCompileWarning
                {
                    Error = $"'{{{{ {path} }}}}' in text renders literal braces — '{{{{ … }}}}' is a subtemplate body, not interpolation, so the value of '{path}' is not printed.",
                    Fix = $"To output the value, use '@({path})'.",
                    Position = new BlockPosition(parseContext.AbsoluteOffset + at, 2),
                    DiagnosticId = HeddleDiagnosticIds.LiquidStyleInterpolationMisread
                });
            }
        }

        private static TemplateChain CompileParameterChain(IEnumerable<OutputItem> items, CompileScope compileContext,
            ParseContext parseContext, ExType returnTypeChainedPrevious)
        {
            TemplateChain result = new TemplateChain();
            bool hasProducerToRight = false;
            foreach (var item in items.Reverse())
            {
                var compiledItem = CompileItem(item, compileContext, parseContext, ref returnTypeChainedPrevious,
                    chainParameter: true);
                if (compiledItem != null)
                {
                    MarkChainConsumer(compiledItem, item, hasProducerToRight);
                    result.Add(compiledItem);
                }

                hasProducerToRight = true;
            }

            return result;
        }

        // Chain consumers with no {{...}} body receive producer output via the chained channel; flagged structurally
        // per chain position to prevent ambient data leaking into lone calls (e.g., @heading():emphasis() pattern).
        private static void MarkChainConsumer(TemplateItem compiledItem, OutputItem item, bool hasProducerToRight)
        {
            if (hasProducerToRight && item.ParameterTemplate == null &&
                compiledItem.Extension is DefinitionBaseExtension definition)
                definition.ReceivesChainedValue = true;
        }

        // Nested producers in parameter chains forward raw output; only the enclosing leaf applies HTML redirect
        // to avoid double-encoding (e.g., @(upper(x)) → @() ← upper(x)).
        private static TemplateItem CompileItem
        (OutputItem extensionItem, CompileScope compileScope, ParseContext parseContext,
            ref ExType returnTypeChainedPrevious, bool chainParameter = false)
        {
            if (compileScope.CompileContext.CompiledItems.TryGetValue(extensionItem, out var result))
            {
                returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
                return result.CompiledItem;
            }

            result = new CompiledElement
                {CompiledItem = new TemplateItem(), ReturnTypeChainedPrevious = returnTypeChainedPrevious};
            compileScope.CompileContext.CompiledItems.Add(extensionItem, result);

            ExType inputModelType = null;
            ExType dataType;
            IExtension extension;
            DefinitionItem definitionItem = null;
            // Ambient fill scope is consulted before parse-context lookup to reach region calls at any callee depth.
            var regionFills = compileScope.CompileContext.RegionFillScope;
            if (regionFills != null && regionFills.TryGet(extensionItem.ExtensionName, out var filledRegion))
            {
                definitionItem = filledRegion;
            }
            else if (parseContext.DefenitionExists(extensionItem.ExtensionName))
            {
                definitionItem = parseContext.GetDefenition(extensionItem.ExtensionName);
            }

            // Named arguments require the native tier and a definition target.
            if (extensionItem.CallParameter.PropArguments != null)
            {
                var firstArgPosition = extensionItem.CallParameter.PropArguments[0].Position;
                if (compileScope.Options.ExpressionMode == ExpressionMode.MemberPathsOnly)
                {
                    compileScope.CompileErrors.Add(
                        "Native expressions are disabled here — TemplateOptions.ExpressionMode is MemberPathsOnly. Set ExpressionMode.Native (default) or FullCSharp to enable them."
                            .ToError(firstArgPosition, HeddleDiagnosticIds.NativeExpressionsDisabled));
                    return null;
                }

                // Non-definition targets compile named arguments only if they declare [Prop] parameters.
                if (definitionItem == null &&
                    !(TemplateFactory.TryGetExtensionType(extensionItem.ExtensionName, out var namedArgTargetType) &&
                      PropLayout.DeclaresExtensionParameters(namedArgTargetType)))
                {
                    compileScope.CompileErrors.Add(
                        $"'{extensionItem.ExtensionName}' declares no named parameters — named arguments can only be passed to a definition that declares props or to an extension that declares [Prop] parameters."
                            .ToError(firstArgPosition, HeddleDiagnosticIds.NamedArgumentsNotSupported));
                    return null;
                }
            }

            // HED4002: by-name call to definition with default output ('-> chain') renders twice
            // (once here, once at document end); exempt synthetic default-chain self-calls.
            if (definitionItem != null && definitionItem.HasDefaultOutput && !extensionItem.IsDefaultChainSelfCall)
            {
                compileScope.CompileContext.CompileWarnings.Add(new HeddleCompileWarning
                {
                    Error =
                        $"Definition '{extensionItem.ExtensionName}' (declared at {definitionItem.Position}) has a default output ('->') and is also called by name — it renders twice.",
                    Fix = "Remove the '->' from the definition, or remove this call.",
                    Position = extensionItem.Position,
                    DiagnosticId = HeddleDiagnosticIds.DefinitionRendersTwice
                });
            }

            // Fallback: definition → extension → registered function (native tier only, off under MemberPathsOnly).
            if (!string.IsNullOrEmpty(extensionItem.ExtensionName) && definitionItem == null &&
                compileScope.Options.ExpressionMode != ExpressionMode.MemberPathsOnly)
            {
                var functionRegistry = compileScope.Options.Functions ?? FunctionRegistry.Default;
                // Shared with emitter's dispatch to ensure consistent precedence (extension beats function).
                var callTarget = CallTargetRules.ResolveCallTarget(extensionItem.ExtensionName,
                    extensionItem.CallParameter, null, null,
                    TemplateFactory.Exists, functionRegistry.Contains);
                bool nameIsExtension = callTarget == CallTargetKind.Extension;
                bool nameInRegistry = functionRegistry.Contains(extensionItem.ExtensionName);
                bool functionCompatibleShape =
                    CallTargetRules.IsFunctionCompatibleShape(extensionItem.CallParameter);

                if (nameIsExtension && nameInRegistry)
                {
                    compileScope.CompileWarnings.Add(new HeddleCompileWarning
                    {
                        Error =
                            $"Registered function '{extensionItem.ExtensionName}' is shadowed by the extension with the same name; standalone calls '@{extensionItem.ExtensionName}(...)' resolve to the extension.",
                        Fix =
                            $"Rename the function, or invoke it inside an expression: '@( {extensionItem.ExtensionName}(...) )'.",
                        Position = extensionItem.Position,
                        DiagnosticId = HeddleDiagnosticIds.FunctionShadowedByExtension
                    });
                }
                else if (!nameIsExtension && nameInRegistry)
                {
                    if (!functionCompatibleShape)
                    {
                        compileScope.CompileErrors.Add(
                            $"Registered function '{extensionItem.ExtensionName}' takes native-expression arguments only — chained extension calls and C# expressions are not supported here. Use the @ C# tier."
                                .ToError(extensionItem.Position,
                                    HeddleDiagnosticIds.FunctionRequiresExpressionArguments));
                        return null;
                    }

                    var functionArguments =
                        BuildFunctionArguments(extensionItem.CallParameter, extensionItem.Position);
                    var callNode = new CallNode(extensionItem.ExtensionName, functionArguments,
                        extensionItem.Position);
                    var functionParameter = NativeExpressionCompiler.Compile(callNode, compileScope,
                        extensionItem.Context ?? parseContext, out dataType);
                    if (functionParameter == null)
                        return null;
                    result.CompiledItem.Parameter = functionParameter;
                    var carrierItem = new OutputItem(string.Empty, extensionItem.Position,
                        extensionItem.ParameterTemplate)
                    {
                        Context = extensionItem.Context
                    };
                    extension = CreateExtension(carrierItem, compileScope,
                        extensionItem.Context ?? parseContext, ref result.ReturnTypeChainedPrevious, null,
                        dataType, null, chainParameter);
                    returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
                    result.CompiledItem.ReturnType = result.ReturnTypeChainedPrevious;
                    result.CompiledItem.Extension = extension;
                    return result.CompiledItem;
                }
                else if (!nameIsExtension && functionCompatibleShape)
                {
                    compileScope.CompileErrors.Add(
                        $"Cannot find extension or registered function '{extensionItem.ExtensionName}'. Register it with TemplateOptions.Functions, or check the name."
                            .ToError(extensionItem.Position, HeddleDiagnosticIds.UnknownFunction));
                    return null;
                }
            }

            if (extensionItem.CallParameter.IsModelTypeParameter)
            {
                if (extensionItem.CallParameter.ModelParameter.Any() &&
                    !string.IsNullOrEmpty(extensionItem.CallParameter.ModelParameter.First()))
                {
                    dataType = CompileModelAccessor(extensionItem, compileScope, definitionItem, result,
                        ref inputModelType);
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
                if (compileScope.Options.ExpressionMode != ExpressionMode.FullCSharp)
                {
                    compileScope.CompileErrors.Add(
                        "C# Code Not allowed here, see TemplateOptions.ExpressionMode Property".ToError(extensionItem
                            .Position));
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
                OptionalValue<object> constantResult =
                    compileScope.CSharpContext.ParseAndGetResultType(compileScope.CompileContext, expressionOptions,
                        out dataType);
                extension = CreateExtension(extensionItem, compileScope, extensionItem.Context ?? parseContext,
                    ref result.ReturnTypeChainedPrevious, null, dataType, definitionItem, chainParameter);

                returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
                result.CompiledItem.ReturnType = result.ReturnTypeChainedPrevious;
                result.CompiledItem.Extension = extension;
                if (!constantResult.HasValue)
                {
                    result.CompiledItem.Parameter =
                        compileScope.CSharpContext.PushCompileExpression(expressionOptions,
                            compileScope.CompileContext);
                    return result.CompiledItem;
                }

                result.CompiledItem.Parameter = new ConstantParameter(constantResult.Value);
                return result.CompiledItem;
            }
            else if (extensionItem.CallParameter.NativeExpression != null)
            {
                if (compileScope.Options.ExpressionMode == ExpressionMode.MemberPathsOnly)
                {
                    compileScope.CompileErrors.Add(
                        "Native expressions are disabled here — TemplateOptions.ExpressionMode is MemberPathsOnly. Set ExpressionMode.Native (default) or FullCSharp to enable them."
                            .ToError(extensionItem.Position, HeddleDiagnosticIds.NativeExpressionsDisabled));
                    return null;
                }

                var nativeParameter = NativeExpressionCompiler.Compile(extensionItem.CallParameter.NativeExpression,
                    compileScope, extensionItem.Context ?? parseContext, out dataType);
                if (nativeParameter == null)
                    return null;
                result.CompiledItem.Parameter = nativeParameter;
            }
            else
            {
                var callParameter = CompileParameterChain(extensionItem.CallParameter.ChainParameter, compileScope,
                    parseContext, returnTypeChainedPrevious);
                dataType = callParameter.RenderType;
                result.CompiledItem.Parameter = new ChainedParameter(callParameter);
                WarnOnRedundantEncoding(extensionItem, callParameter, compileScope);
            }

            extension = CreateExtension(extensionItem, compileScope, extensionItem.Context ?? parseContext,
                ref result.ReturnTypeChainedPrevious, inputModelType, dataType, definitionItem, chainParameter);
            returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
            result.CompiledItem.ReturnType = result.ReturnTypeChainedPrevious;
            result.CompiledItem.Extension = extension;
            return result.CompiledItem;
        }

        private static ExType CompileModelAccessor(OutputItem extensionItem, CompileScope compileContext,
            DefinitionItem definitionItem,
            CompiledElement result, ref ExType inputType)
        {
            var scopeType = extensionItem.CallParameter.RootReference
                ? compileContext.CompileContext.RootScopeType
                : compileContext.CompileContext.ScopeType;

            // Body prop read wins over model on first segment; resolves before dynamic check to keep props statically typed.
            if (!extensionItem.CallParameter.RootReference)
            {
                var propParameter = TryCompilePropRead(extensionItem.CallParameter.ModelParameter, scopeType,
                    compileContext, extensionItem.Position, out var propType);
                if (propParameter != null)
                {
                    result.CompiledItem.Parameter = propParameter;
                    inputType = propType;
                    return propType;
                }
            }

            if (scopeType.IsDynamic || definitionItem != null && definitionItem.ModelType == ExType.Dynamic.ToString())
            {
                if (extensionItem.CallParameter.RootReference)
                {
                    result.CompiledItem.Parameter =
                        new RootDynamicParameter(extensionItem.CallParameter.ModelParameter);
                }
                else
                {
                    result.CompiledItem.Parameter = new DynamicParameter(extensionItem.CallParameter.ModelParameter);
                }

                return ExType.Dynamic;
            }

            var modelParameters = extensionItem.CallParameter.ModelParameter;
            var resolution = MemberPathResolver.TryResolve(scopeType, modelParameters);
            if (resolution.Kind == MemberPathResolutionKind.Failed)
            {
                compileContext.CompileContext.CompileErrors.Add(
                    resolution.FailureMessage.ToError(extensionItem.Position, HeddleDiagnosticIds.PropertyNotFound));
                return null;
            }

            if (resolution.Kind == MemberPathResolutionKind.DynamicHop)
            {
                if (extensionItem.CallParameter.RootReference)
                {
                    result.CompiledItem.Parameter = new DynamicParameter(modelParameters.Skip(resolution.Index),
                        new RootModelParameter(resolution.Properties));
                }
                else
                {
                    result.CompiledItem.Parameter = new DynamicParameter(modelParameters.Skip(resolution.Index),
                        new ModelParameter(resolution.Properties));
                }

                return ExType.Dynamic;
            }

            inputType = resolution.ResultType;
            if (extensionItem.CallParameter.RootReference)
            {
                result.CompiledItem.Parameter = new RootModelParameter(resolution.Properties);
            }
            else
            {
                result.CompiledItem.Parameter = new ModelParameter(resolution.Properties);
            }

            return inputType;
        }

        /// <summary>
        /// Returns <see cref="PropsSlotParameter"/> if first segment is a prop; <c>null</c> to fall through to model resolution.
        /// </summary>
        private static IRuntimeParameter TryCompilePropRead(string[] segments, ExType scopeType,
            CompileScope compileScope, BlockPosition position, out ExType resultType)
        {
            resultType = null;
            var layout = compileScope.CompileContext.ActivePropLayout;
            if (layout == null || segments == null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
                return null;
            if (!layout.TryGet(segments[0], out var slot))
                return null;

            PropLayout.WarnIfShadowsMember(compileScope, scopeType, slot.Name, position);

            if (segments.Length == 1)
            {
                resultType = slot.Type;
                return new PropsSlotParameter(slot.Index);
            }

            var rest = segments.Skip(1).ToArray();
            var resolution = MemberPathResolver.TryResolve(slot.Type, rest);
            if (resolution.Kind == MemberPathResolutionKind.Failed)
            {
                compileScope.CompileErrors.Add(
                    resolution.FailureMessage.ToError(position, HeddleDiagnosticIds.PropertyNotFound));
                resultType = slot.Type;
                return new PropsSlotParameter(slot.Index);
            }

            if (resolution.Kind == MemberPathResolutionKind.DynamicHop)
            {
                resultType = ExType.Dynamic;
                Func<object, object> prefix = resolution.Properties.Count == 0
                    ? null
                    : ModelParameter.GetPropertyChainAccessor(resolution.Properties).Compile();
                return new PropsSlotParameter(slot.Index, prefix);
            }

            resultType = resolution.ResultType;
            var hops = ModelParameter.GetPropertyChainAccessor(resolution.Properties).Compile();
            return new PropsSlotParameter(slot.Index, hops);
        }

        private static List<ExprNode> BuildFunctionArguments(CallParameter callParameter, BlockPosition position)
        {
            if (callParameter.NativeExpression != null)
                return new List<ExprNode> { callParameter.NativeExpression };

            var segments = callParameter.ModelParameter;
            if (segments == null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
                return new List<ExprNode>();
            return new List<ExprNode> { new PathNode(callParameter.RootReference, segments, null, position) };
        }

        private static CallSite<Func<CallSite, object, object>> CreateBinder(string model,
            CSharpArgumentInfo[] csharpArgumentInfoArray)
        {
            return
                CallSite<Func<CallSite, object, object>>.Create(Binder.GetMember(CSharpBinderFlags.None, model,
                    typeof(IRuntimeParameter),
                    csharpArgumentInfoArray));
        }

        /// <summary>
        /// Emits HED2003 when a bodiless unnamed <c>@(...)</c> under Html profile has an <c>[EncodeOutput]</c> producer in the chain.
        /// </summary>
        private static void WarnOnRedundantEncoding(OutputItem extensionItem, TemplateChain callParameter,
            CompileScope compileScope)
        {
            if (extensionItem.ExtensionName.Length != 0)
                return;
            if (!string.IsNullOrEmpty(extensionItem.ParameterTemplate))
                return;
            if (compileScope.CompileContext.OutputProfile != OutputProfile.Html)
                return;

            var items = callParameter.ItemsToExecute;
            if (items.Count == 0)
                return;
            var producer = items[items.Count - 1];
            // Unwrap parameter-declaring producer to check inner extension's [EncodeOutput] (carrier-transparency).
            var producerExtension = (producer.Extension as ExtensionParameterCarrier)?.Inner ?? producer.Extension;
            if (producerExtension == null ||
                !producerExtension.GetType().IsHaveAttribute<EncodeOutputAttribute>(true))
                return;

            var producerItem = extensionItem.CallParameter.ChainParameter[0];
            compileScope.CompileWarnings.Add(new HeddleCompileWarning
            {
                Error =
                    $"'{producerItem.ExtensionName}()' output feeds the unnamed @(...) output, which already HTML-encodes under the Html profile — the value is encoded twice.",
                Fix = $"Remove '{producerItem.ExtensionName}()', or output the trusted value through @raw(...).",
                Position = producerItem.Position,
                DiagnosticId = HeddleDiagnosticIds.RedundantEncodingExtension
            });
        }

        /// <summary>Classification of bare <c>@(value)</c> block position for HED2004 lint.</summary>
        private enum HtmlContext
        {
            None,
            Attribute,
            Script,
            Url
        }

        /// <summary>Attributes carrying URL values for HED2004 classification.</summary>
        private static readonly HashSet<string> UrlAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "href", "src", "action", "formaction", "cite", "poster", "background", "manifest",
            "data", "longdesc", "usemap", "srcset"
        };

        /// <summary>
        /// HED2004 warning-only lint for bare <c>@(value)</c> blocks under Html profile; classifies surrounding HTML position.
        /// </summary>
        private static void ScanHtmlContextLint(OutputChain chain, List<BlockPosition> leftSpans,
            CompileScope compileScope, string workingDocument)
        {
            if (compileScope.CompileContext.OutputProfile != OutputProfile.Html)
                return; // the explicit Html profile is the sole gate; cheapest check first.

            var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
            if (leftmost == null)
                return;
            // Only bodiless unnamed carriers warn; named encoders and bodied calls never do.
            if (leftmost.ExtensionName.Length != 0 || !string.IsNullOrEmpty(leftmost.ParameterTemplate))
                return;

            var context = ClassifyHtmlContext(workingDocument, chain.BlockPosition.StartIndex, leftSpans);
            if (context == HtmlContext.None)
                return;

            string error, fix;
            switch (context)
            {
                case HtmlContext.Script:
                    error = "A bare '@(...)' output is inside a <script> block under the Html profile; HTML element-text encoding is wrong for a JavaScript context.";
                    fix = "Use '@js(...)' for the JavaScript-string context, or '@raw(...)' if the value is trusted.";
                    break;
                case HtmlContext.Url:
                    error = "A bare '@(...)' output is in a URL component under the Html profile; element-text encoding does not percent-encode it.";
                    fix = "Use '@url(...)' for the URL-component context, or '@raw(...)' if the value is trusted.";
                    break;
                default:
                    error = "A bare '@(...)' output is inside an HTML tag under the Html profile (attribute value or an unquoted/name position); element-text encoding is insufficient there.";
                    fix = "Use '@attr(...)' for the attribute context, or '@raw(...)' if the value is trusted.";
                    break;
            }

            compileScope.CompileWarnings.Add(new HeddleCompileWarning
            {
                Error = error,
                Fix = fix,
                Position = leftmost.Position, // original-source coordinates for reporting.
                DiagnosticId = HeddleDiagnosticIds.MissingContextEncoder
            });
        }

        /// <summary>
        /// Left-only literal heuristic (earlier blocks excised): Step 1 check <c>&lt;script&gt;</c> containment.
        /// Step 2 find nearest tag boundary. Step 3 detect quote parity and URL attribute.
        /// </summary>
        private static HtmlContext ClassifyHtmlContext(string workingDocument, int blockStart,
            List<BlockPosition> leftSpans)
        {
            if (blockStart < 0 || blockStart > workingDocument.Length)
                return HtmlContext.None; // bounds discipline — a shifted position overshot; never dereference.

            var left = BuildLiteralLeft(workingDocument, blockStart, leftSpans);

            // Step 1: <script>-element containment.
            int open = -1;
            for (int m = 0; (m = left.IndexOf("<script", m, StringComparison.OrdinalIgnoreCase)) >= 0; m++)
            {
                int after = m + 7;
                if (after >= left.Length)
                    continue;  // Tag name interrupted.
                char c = left[after];
                if (c != '/' && c != '>' && !char.IsWhiteSpace(c))
                    continue;  // Not a script tag (e.g., <script-loader>).
                if (!HasUnquotedGreaterThan(left, after))
                    continue;  // Start tag not yet closed.
                open = m;
            }

            if (open >= 0)
            {
                int close = -1;
                for (int m = 0; (m = left.IndexOf("</script", m, StringComparison.OrdinalIgnoreCase)) >= 0; m++)
                {
                    int after = m + 8;
                    if (after < left.Length)
                    {
                        char c = left[after];
                        if (c != '/' && c != '>' && !char.IsWhiteSpace(c))
                            continue; // </scriptx> is not an end tag; EOF is a valid boundary.
                    }

                    close = m;
                }

                if (close < 0 || close < open)
                    return HtmlContext.Script;
            }

            // Step 2: nearest tag boundary (unquoted '>' only counts as tag close).
            int tagStart = left.LastIndexOf('<');
            if (tagStart < 0 || HasUnquotedGreaterThan(left, tagStart + 1))
                return HtmlContext.None; // element text — the default element-text encoder is correct.

            // Step 3: classify by quote parity and attribute name.
            string tagText = left.Substring(tagStart);
            int doubleQuotes = CountChar(tagText, '"');
            int singleQuotes = CountChar(tagText, '\'');
            int quote;
            if (doubleQuotes % 2 == 1)
                quote = tagText.LastIndexOf('"');
            else if (singleQuotes % 2 == 1)
                quote = tagText.LastIndexOf('\'');
            else
                return HtmlContext.Attribute; // unquoted position or between attributes — generic in-tag signal.

            string valueSoFar = tagText.Substring(quote + 1);
            int i = quote - 1;
            while (i >= 0 && char.IsWhiteSpace(tagText[i]))
                i--;
            if (i < 0 || tagText[i] != '=')
                return HtmlContext.Attribute;
            i--;
            while (i >= 0 && char.IsWhiteSpace(tagText[i]))
                i--;
            int nameEnd = i;
            while (i >= 0 && IsAttributeNameChar(tagText[i]))
                i--;
            if (i == nameEnd)
                return HtmlContext.Attribute; // no identifier run — not a recognizable attribute value.

            string attributeName = tagText.Substring(i + 1, nameEnd - i);
            bool componentSignal = valueSoFar.IndexOf('?') >= 0 || valueSoFar.IndexOf('&') >= 0 ||
                                   valueSoFar.IndexOf('=') >= 0 ||
                                   (valueSoFar.Length > 0 && valueSoFar[valueSoFar.Length - 1] == '/');
            return UrlAttributes.Contains(attributeName) && componentSignal
                ? HtmlContext.Url
                : HtmlContext.Attribute;
        }

        /// <summary>
        /// Left text of <paramref name="blockStart"/> with earlier producing blocks' source spans excised.
        /// </summary>
        private static string BuildLiteralLeft(string workingDocument, int blockStart, List<BlockPosition> leftSpans)
        {
            if (leftSpans == null || leftSpans.Count == 0)
                return workingDocument.Substring(0, blockStart);

            var builder = new StringBuilder(blockStart);
            int position = 0;
            foreach (var span in leftSpans)
            {
                int start = span.StartIndex;
                int end = span.StartIndex + span.Length;
                if (start >= blockStart)
                    break;
                if (start < position)
                    continue; // defensive — spans are ascending and non-overlapping by construction.
                if (end > blockStart)
                    end = blockStart;
                builder.Append(workingDocument, position, start - position);
                position = end;
            }

            if (position < blockStart)
                builder.Append(workingDocument, position, blockStart - position);
            return builder.ToString();
        }

        /// <summary>
        /// Returns true if an unquoted <c>&gt;</c> exists from <paramref name="from"/> onward.
        /// </summary>
        private static bool HasUnquotedGreaterThan(string text, int from)
        {
            char quote = '\0';
            for (int i = from; i < text.Length; i++)
            {
                char c = text[i];
                if (quote != '\0')
                {
                    if (c == quote)
                        quote = '\0';
                }
                else if (c == '"' || c == '\'')
                {
                    quote = c;
                }
                else if (c == '>')
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountChar(string text, char c)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == c)
                    count++;
            }

            return count;
        }

        private static bool IsAttributeNameChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
            c == ':' || c == '_' || c == '-';

        /// <summary>
        /// Resolves carrier name: Html profile redirects bodiless unnamed carriers to EmptyHtmlExtension.
        /// Records UnnamedOutputCompiled for HED2002.
        /// </summary>
        private static string UnnamedCarrierName(OutputItem item, CompileContext context)
        {
            bool hasBody = !string.IsNullOrEmpty(item.ParameterTemplate);
            if (!hasBody)
                context.UnnamedOutputCompiled = true;
            // Shared with emitter's AllocateEmptyExtension to ensure consistent carrier selection.
            OutputProfileRules.ResolveUnnamedCarrier(context.OutputProfile, hasBody, out var kind, out _);
            return OutputProfileRules.CarrierRegistryName(kind);
        }

        private static IExtension CreateExtension(OutputItem extensionItem, CompileScope compileScope,
            ParseContext parseContext,
            ref ExType returnTypeChainedPrevious, ExType inputModelType, ExType dataType,
            DefinitionItem definition, bool chainParameter = false)
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
                    if (acceptType != typeof(object))
                        dataType = acceptType;
                }

                CheckTypes(dataType, definition.Position, compileScope, acceptType);

                var layout = ResolveLayoutCached(definition, compileScope);
                var slotType = ResolveSlotType(definition, compileScope);
                def.SlotMode = slotType != null;

                if (extensionItem.CallParameter.PropArguments != null && layout.Count == 0)
                {
                    compileScope.CompileErrors.Add(
                        $"Definition '{definition.Name}' declares no props. Fix: add a prop list to the definition header: '<{definition.Name}(name: type)>'."
                            .ToError(extensionItem.CallParameter.PropArguments[0].Position,
                                HeddleDiagnosticIds.DefinitionHasNoProps));
                }
                else if (layout.Count > 0)
                {
                    def.PropsBinder = BindProps(layout, $"definition '{definition.Name}'", extensionItem,
                        compileScope, parseContext);
                }

                // Region bodies inherit ambient fill scope; non-regions build call-scoped scope from matched candidates.
                var compileContext = compileScope.CompileContext;
                var ambientFills = compileContext.RegionFillScope;
                RegionFillScope bodyFills;
                if (definition.IsRegion)
                {
                    bool resolvedFromFillScope = ambientFills != null &&
                                                 ambientFills.TryGet(definition.Name, out var fromScope) &&
                                                 ReferenceEquals(fromScope, definition);
                    bodyFills = resolvedFromFillScope
                        ? ambientFills.WithRebind(definition.Name, definition.BaseDefinition)
                        : ambientFills;
                }
                else
                {
                    bodyFills = BuildRegionFillScope(definition, extensionItem, compileScope);
                }

                // Caller content compiles with slot type (if slot mode) or dataType; enclosing layout stays active.
                var callerModelType = slotType ?? dataType;
                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate,
                    callerModelType, returnTypeChainedPrevious, compileScope, parseContext, extensionItem);

                // Definition body compiles under own layout/slot (save/restore); regions inherit enclosing component's layout.
                var savedLayout = compileContext.ActivePropLayout;
                var savedSlot = compileContext.SlotParameterType;
                var savedFills = compileContext.RegionFillScope;
                compileContext.ActivePropLayout = definition.IsRegion ? savedLayout : layout;
                compileContext.SlotParameterType = slotType;
                compileContext.RegionFillScope = bodyFills;
                def.DefinitionParameterTemplate = CompileFromDefenition(definition, compileScope, out acceptType);
                returnTypeChainedPrevious = InitializeTemplate(def.DefinitionParameterTemplate,
                    definition.ParameterTemplate,
                    dataType,
                    returnTypeChainedPrevious, compileScope, definition.Context, extensionItem);
                compileContext.ActivePropLayout = savedLayout;
                compileContext.SlotParameterType = savedSlot;
                compileContext.RegionFillScope = savedFills;
            }
            else
            {
                var carrierName = extensionItem.ExtensionName.Length == 0 && !chainParameter
                    ? UnnamedCarrierName(extensionItem, compileScope.CompileContext)
                    : extensionItem.ExtensionName;
                extension = TemplateFactory.Create(carrierName, extensionItem.Position, parseContext,
                    compileScope.CompileContext);
                if (extension == null)
                    return null;
                Type templateType = extension.GetType();

                var chainedTypeAttributes = templateType.GetAttributes<ChainedTypeAttribute>(true);

                if (returnTypeChainedPrevious != null)
                {
                    CheckTypes(returnTypeChainedPrevious, extensionItem.Position, compileScope,
                        chainedTypeAttributes.Select(a => (ExType) a.DataType).ToArray());
                }

                var dataTypeAttributes =
                    templateType.GetAttributes<DataTypeAttribute>(true);
                if (inputModelType != null)
                {
                    dataType = inputModelType;
                }

                CheckTypes(dataType, extensionItem.Position, compileScope,
                    dataTypeAttributes.Select(a => (ExType) a.DataType).ToArray());

                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate, dataType,
                    returnTypeChainedPrevious, compileScope, parseContext, extensionItem);

                // Wrap parameter-declaring extensions after InitializeTemplate so render-type attributes stay on inner extension (carrier-transparency).
                if (PropLayout.DeclaresExtensionParameters(templateType))
                {
                    var ownerDisplay = $"extension '{extensionItem.ExtensionName}'";
                    var extLayout = ResolveExtensionLayoutCached(templateType, compileScope, ownerDisplay,
                        extensionItem.Position);
                    if (extLayout.Count > 0)
                    {
                        var binder = BindProps(extLayout, ownerDisplay, extensionItem, compileScope, parseContext);
                        var names = new string[extLayout.Count];
                        foreach (var slot in extLayout.Slots)
                            names[slot.Index] = slot.Name;
                        extension = new ExtensionParameterCarrier(extension, binder,
                            new ExtensionParameterMap(names));
                    }
                }
            }

            return extension;
        }

        /// <summary>
        /// Resolves and caches the [Prop] layout of parameter-declaring extensions; declaration-side diagnostics (HED5007/5008/5009/5010/5015) emitted once per type.
        /// </summary>
        private static PropLayout ResolveExtensionLayoutCached(Type extensionType, CompileScope compileScope,
            string ownerDisplay, BlockPosition ownerCallPosition)
        {
            var cache = compileScope.CompileContext.ResolvedPropLayouts;
            var key = "ext!" + extensionType.AssemblyQualifiedName;
            if (!cache.TryGetValue(key, out var layout))
            {
                layout = PropLayout.ResolveFromExtension(extensionType, compileScope, ownerDisplay,
                    ownerCallPosition);
                cache[key] = layout;
            }

            return layout;
        }

        /// <summary>
        /// Resolves and caches the prop layout of a definition, keyed by stable identity for sharing across parser-isolated contexts.
        /// </summary>
        private static PropLayout ResolveLayoutCached(DefinitionItem definition, CompileScope compileScope)
        {
            var cache = compileScope.CompileContext.ResolvedPropLayouts;
            var key = definition.Name + "@" + definition.Position;
            if (!cache.TryGetValue(key, out var layout))
            {
                layout = PropLayout.Resolve(definition, compileScope);
                cache[key] = layout;
            }

            return layout;
        }

        /// <summary>
        /// Resolves and caches the named-region table of a definition, keyed by the same stable identity as props.
        /// </summary>
        private static RegionLayout ResolveRegionLayoutCached(DefinitionItem definition, CompileScope compileScope)
        {
            var cache = compileScope.CompileContext.ResolvedRegionLayouts;
            var key = definition.Name + "@" + definition.Position;
            if (!cache.TryGetValue(key, out var layout))
            {
                layout = RegionLayout.Resolve(definition, compileScope);
                cache[key] = layout;
            }

            return layout;
        }

        /// <summary>
        /// Matches caller content's region fill candidates against callee's region table; PUBLIC matches materialize fills,
        /// PRIVATE matches raise HED5019; unmatched candidates keep their parse-emitted errors.
        /// </summary>
        private static RegionFillScope BuildRegionFillScope(DefinitionItem definition, OutputItem extensionItem,
            CompileScope compileScope)
        {
            var callerContext = extensionItem.Context;
            if (callerContext == null)
                return null;
            var candidates = callerContext.RegionFillCandidates;
            if (candidates == null || candidates.Count == 0)
                return null;

            Dictionary<string, DefinitionItem> fills = null;
            RegionLayout layout = null;

            // Four-step matching rule is shared (RegionFillResolver); layout resolved lazily per candidate.
            RegionFillResolver.Resolve(candidates, callerContext.OriginIdentity,
                (string name, out bool isPublic) =>
                {
                    layout = layout ?? ResolveRegionLayoutCached(definition, compileScope);
                    if (!layout.TryGet(name, out var slot))
                    {
                        isPublic = false;
                        return false;
                    }

                    isPublic = slot.IsPublic;
                    return true;
                },
                definition,
                (candidate, verdict, materialized) =>
                {
                    switch (verdict)
                    {
                        case RegionFillVerdict.Dangling:
                        case RegionFillVerdict.DefaultMissing:
                            // Genuinely dangling (or declared but not stored) — the parse-emitted error stays.
                            break;

                        case RegionFillVerdict.Private:
                            RetractCandidateError(candidate, compileScope);
                            if (!candidate.PrivateOverrideReported)
                            {
                                candidate.PrivateOverrideReported = true;
                                compileScope.CompileErrors.Add(
                                    $"Region '{candidate.Name}' of definition '{definition.Name}' is private and cannot be overridden from a call site. Mark it public with '<:{candidate.Name}>' in the definition, or remove this override."
                                        .ToError(candidate.Position, HeddleDiagnosticIds.RegionNotPublic));
                            }

                            break;

                        default: // Matched
                            RetractCandidateError(candidate, compileScope);
                            fills = fills ?? new Dictionary<string, DefinitionItem>(StringComparer.Ordinal);
                            fills[candidate.Name] = materialized;
                            break;
                    }
                });

            return fills == null ? null : new RegionFillScope(fills);
        }

        /// <summary>
        /// Removes candidate's base-not-found error from both CompileErrors and ParseContext.Errors (LSP de-dups over the latter).
        /// </summary>
        private static void RetractCandidateError(RegionFillCandidate candidate, CompileScope compileScope)
        {
            compileScope.CompileErrors.Remove(candidate.Error);
            candidate.Origin.Errors.Remove(candidate.Error);
        }

        /// <summary>
        /// Resolves slot parameter type from first declared <c>out::</c> down base chain; HED5010 if unresolvable.
        /// </summary>
        private static ExType ResolveSlotType(DefinitionItem definition, CompileScope compileScope)
        {
            var slotName = SlotRules.SlotTypeName(definition);
            if (slotName == null)
                return null;

            try
            {
                var resolved = ReflectionHelper.ResolveType(slotName, compileScope.CSharpContext.Namespaces);
                if (resolved != null)
                    return new ExType(resolved);
            }
            catch (InvalidOperationException)
            {
            }

            compileScope.CompileContext.CompileErrors.Add(
                $"Cannot resolve type '{slotName}' for the slot parameter of definition '{definition.Name}'."
                    .ToError(definition.Position, HeddleDiagnosticIds.UnresolvedPropType));
            return null;
        }

        /// <summary>
        /// Binds named arguments: builds frozen prototype and dynamic slot plan, emitting HED5001/5003/5004 per argument and HED5002 for unbound required slots.
        /// </summary>
        private static PropsBinder BindProps(PropLayout layout, string ownerDisplay, OutputItem extensionItem,
            CompileScope compileScope, ParseContext parseContext)
        {
            var prototype = new object[layout.Count];
            var bound = new bool[layout.Count];
            foreach (var slot in layout.Slots)
            {
                if (slot.HasDefault)
                    prototype[slot.Index] = slot.DefaultBoxed;
            }

            var plan = new List<PropsBinder.DynamicSlot>();
            var args = extensionItem.CallParameter.PropArguments;
            if (args != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var arg in args)
                {
                    if (!seen.Add(arg.Name))
                    {
                        compileScope.CompileErrors.Add(
                            $"Prop '{arg.Name}' is passed more than once."
                                .ToError(arg.Position, HeddleDiagnosticIds.DuplicatePropArgument));
                        continue;
                    }

                    if (!layout.TryGet(arg.Name, out var slot))
                    {
                        var declared = string.Join(", ", layout.Slots.Select(s => s.Name));
                        compileScope.CompileErrors.Add(
                            $"Unknown prop '{arg.Name}' on {ownerDisplay}. Declared props: {declared}."
                                .ToError(arg.Position, HeddleDiagnosticIds.UnknownProp));
                        continue;
                    }

                    var param = NativeExpressionCompiler.Compile(arg.Value, compileScope, parseContext, out var argType);
                    if (param == null)
                    {
                        // Error already recorded; mark bound to prevent spurious HED5002.
                        bound[slot.Index] = true;
                        continue;
                    }

                    bool isNullConstant = param is ConstantParameter nullConst && nullConst.Value == null;
                    if (isNullConstant)
                    {
                        if (slot.Type.Type.IsValueType && Nullable.GetUnderlyingType(slot.Type.Type) == null)
                        {
                            compileScope.CompileErrors.Add(
                                PropTypeMismatchMessage(ownerDisplay, slot, argType)
                                    .ToError(arg.Position, HeddleDiagnosticIds.PropTypeMismatch));
                            continue;
                        }

                        prototype[slot.Index] = null;
                        bound[slot.Index] = true;
                        continue;
                    }

                    if (!PropConversion.CanConvert(argType, slot.Type, allowBoxToObject: true))
                    {
                        compileScope.CompileErrors.Add(
                            PropTypeMismatchMessage(ownerDisplay, slot, argType)
                                .ToError(arg.Position, HeddleDiagnosticIds.PropTypeMismatch));
                        continue;
                    }

                    if (param is ConstantParameter constant)
                    {
                        prototype[slot.Index] = PropConversion.ConvertValue(constant.Value, argType.Type, slot.Type.Type);
                    }
                    else
                    {
                        plan.Add(new PropsBinder.DynamicSlot(slot.Index, param,
                            BuildNumericConvert(argType.Type, slot.Type.Type)));
                    }

                    bound[slot.Index] = true;
                }
            }

            foreach (var slot in layout.Slots)
            {
                if (!slot.HasDefault && !bound[slot.Index])
                {
                    compileScope.CompileErrors.Add(
                        $"Missing required prop '{slot.Name}' ({slot.Type.Type}) on the call to {ownerDisplay}. Pass '{slot.Name}: …' or declare a default."
                            .ToError(extensionItem.Position, HeddleDiagnosticIds.MissingRequiredProp));
                }
            }

            return new PropsBinder(prototype, plan.ToArray());
        }

        private static string PropTypeMismatchMessage(string ownerDisplay, PropSlot slot, ExType argType)
        {
            var argName = argType == null ? "unknown" : argType.IsDynamic ? "dynamic" : argType.Type.ToString();
            return
                $"Prop '{slot.Name}' of {ownerDisplay} expects {slot.Type.Type}, but the argument type is {argName}.";
        }

        /// <summary>
        /// Builds boxed-value converter for numeric widening; <c>null</c> when no runtime conversion needed.
        /// </summary>
        private static Func<object, object> BuildNumericConvert(Type argType, Type propType)
        {
            var argUnderlying = Nullable.GetUnderlyingType(argType) ?? argType;
            var propUnderlying = Nullable.GetUnderlyingType(propType) ?? propType;
            if (argUnderlying != propUnderlying && NumericPromotion.IsImplicitNumeric(argUnderlying, propUnderlying))
                return raw => raw == null ? null : Convert.ChangeType(raw, propUnderlying, CultureInfo.InvariantCulture);
            return null;
        }

        private static DefinitionBaseExtension CompileFromDefenition(DefinitionItem definition,
            CompileScope compileScope, out Type acceptType)
        {
            WalkValidateDefinitionType(definition, compileScope);
            var result = new DefinitionBaseExtension {Position = definition.Position};
            try
            {
                acceptType =
                    ReflectionHelper.ResolveType(definition.ModelType, compileScope.CSharpContext.Namespaces) ??
                    typeof(object);
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
                var currentType =
                    ReflectionHelper.ResolveType(definition.ModelType, context.CSharpContext.Namespaces) ??
                    typeof(object);

                var definitionBase = definition.BaseDefinition;
                while (definitionBase != null)
                {
                    var baseType =
                        ReflectionHelper.ResolveType(definitionBase.ModelType, context.CSharpContext.Namespaces) ??
                        typeof(object);
                    if (!baseType.IsType(currentType))
                    {
                        context.CompileContext.CompileErrors.Add(
                            $"The new definition type <{currentType}> isn't assignable to base <{baseType}>.".ToError(
                                definition.Position));
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
            CompileScope compileScope, ParseContext parseContext, OutputItem sourceItem = null)
        {
            modelType ??= typeof(object);
            chainedType ??= typeof(object);
            // Shared with emitter's DerivedRenderTypeLiteral to ensure consistent render type evaluation.
            var extensionType = extension.GetType();
            RenderType directRender = RenderTypeRules.Derive(
                extensionType.IsHaveAttribute<EncodeOutputAttribute>(true),
                extensionType.IsHaveAttribute<NotEncodeAttribute>(true));
            extension.SetUpRenderType(directRender);
            var initContext = new InitContext(parameterFastString, compileScope, parseContext)
            {
                SourceItem = sourceItem
            };
            return extension.InitStart(initContext, modelType, chainedType, compileScope.ScopeType);
        }

        private static void CheckTypes(ExType returnType, BlockPosition extensionPosition, CompileScope compileScope,
            params ExType[] dataTypes)
        {
            returnType ??= typeof(object);
            if (!returnType.IsDynamic)
                returnType = returnType.Type.UnwrapNullable();
            if (dataTypes.Any() && dataTypes.All(dataType =>
            {
                dataType ??= typeof(object);
                if (dataType.IsDynamic || returnType.IsDynamic)
                    return false;
                return !dataType.Type.IsType(returnType.Type);
            }))
            {
                compileScope.CompileContext.CompileErrors.Add
                (string.Format
                (CultureInfo.InvariantCulture, "Return Type is {0} but any of [{1}] expected.",
                    returnType.Type.FullName,
                    string.Join(", ", dataTypes.Select(t => t.Type.FullName)))
                    .ToError(extensionPosition, HeddleDiagnosticIds.ReturnTypeMismatch));
            }
        }
    }
}