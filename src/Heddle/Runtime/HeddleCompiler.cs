using System;
using System.Diagnostics.CodeAnalysis;
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
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime.Expressions;
using Heddle.Runtime.Parameters;
using Heddle.Strings;
using Heddle.Strings.Core;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Heddle.Runtime
{
    internal partial class HeddleCompiler
    {
        public static RuntimeDocument Compile(string document, CompileScope compileScope, ParseContext parseContext,
            ExType chainedType)
        {
            return Compile(document, compileScope, parseContext, chainedType, null);
        }

        /// <summary>Overload carrying the pre-parse source for the form record: <paramref name="document"/>
        /// is the parser's clean text (whitespace-normalized), while <paramref name="rawText"/> is the exact
        /// text the build parsed. The loader re-parses the raw text, so items re-parse to the recorded
        /// positions only when the record carries the true source. Null falls back to
        /// <paramref name="document"/> (nested bodies, fragments and children already pass raw text).</summary>
        internal static RuntimeDocument Compile(string document, CompileScope compileScope,
            ParseContext parseContext, ExType chainedType, string rawText)
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
                return CompileBody(document, compileScope, parseContext, chainedType, rawText);
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
            ParseContext parseContext, ExType chainedType, string rawText)
        {
            var bodyRecord = compileScope.CompileContext.FormRecord;
            if (bodyRecord == null)
                return CompileBodyInner(document, compileScope, parseContext, chainedType, rawText);
            bodyRecord.EnterBody();
            try
            {
                return CompileBodyInner(document, compileScope, parseContext, chainedType, rawText);
            }
            finally
            {
                bodyRecord.ExitBody();
            }
        }

        private static RuntimeDocument CompileBodyInner(string document, CompileScope compileScope,
            ParseContext parseContext, ExType chainedType, string rawText)
        {
            string workingDocument = document;
            bool trimDirectiveLines = compileScope.Options.TrimDirectiveLines;
            DocumentShaping.ShiftBySkippedTokens(parseContext);
            // HED4005 scan runs here when coordinates are consistent with exclusion spans.
            OutputLints.ScanBraceMisreads(parseContext, compileScope.CompileWarnings, workingDocument);
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
                    catch (Heddle.Precompiled.PrecompiledStrictLoadException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        compileScope.CompileErrors.Add(CompileItemFault(item, e));
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
                    OutputLints.ScanHtmlContextLint(extensions, htmlLintLeftSpans, compileScope.CompileWarnings,
                        workingDocument,
                        compileScope.CompileContext.OutputProfile == OutputProfile.Html);
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
                    catch (Heddle.Precompiled.PrecompiledStrictLoadException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        compileScope.CompileErrors.Add(CompileItemFault(item, e));
                    }

                    hasProducerToRight = true;
                }

                if (returnTypeChainedPrevious != null)
                {
                    documentElements.Add(element);
                }
            }

            var record = compileScope.CompileContext.FormRecord;
            FormDocument formDocument = null;
            if (record != null)
                formDocument = record.BeginDocument(parseContext, rawText ?? document, workingDocument,
                    documentElements);
            var runtime = new RuntimeDocument(workingDocument, documentElements.ToArray(), compileScope);
            if (record != null)
                record.EndDocument(formDocument, runtime);
            return runtime;
        }

        /// <summary>
        /// The last resort for a call that failed to compile for a reason no rule anticipated. It carries an id like
        /// every other compile error, so a host can classify it, and it says what failed and why: without the
        /// exception's own message the text was "Error while compiling " for an unnamed call, which told a reader
        /// nothing at all.
        /// </summary>
        private static HeddleCompileError CompileItemFault(OutputItem item, Exception exception)
        {
            var subject = string.IsNullOrEmpty(item.ExtensionName)
                ? "an expression"
                : $"'@{item.ExtensionName}'";
            return new HeddleCompileError
            {
                Exception = exception,
                Position = item.Position,
                DiagnosticId = HeddleDiagnosticIds.CompilationFailed,
                Error = $"Compiling {subject} failed: {exception.Message}"
            };
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
                new BranchSetLint(compileScope.CompileWarnings, compileScope.CompileErrors, HasScopeChannel));
        }

        private static bool HasScopeChannel(string name) =>
            TemplateFactory.TryGetExtensionType(name, out var extensionType) &&
            extensionType.IsHaveAttribute<ScopeChannelAttribute>(true);

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
            var record = compileScope.CompileContext.FormRecord;
            if (record == null)
                return CompileItemInner(extensionItem, compileScope, parseContext,
                    ref returnTypeChainedPrevious, chainParameter);
            record.BeginItem(extensionItem, extensionItem.Context ?? parseContext);
            record.PushItem(extensionItem);
            try
            {
                var compiled = CompileItemInner(extensionItem, compileScope, parseContext,
                    ref returnTypeChainedPrevious, chainParameter);
                if (compiled != null)
                    record.EndItem(extensionItem, compiled);
                return compiled;
            }
            finally
            {
                record.PopItem();
            }
        }

        private static TemplateItem CompileItemInner
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

            // Precompiled refusal: the build refused this item and recorded its source. Serve the
            // fragment instead of resolving the hook's missing name — the name is still unbound here,
            // so the definition/extension/function arms below would fault it again. Nothing is armed
            // on the dynamic tier, where the slot and the ambient are both null.
            var refusalCursor = compileScope.FormCursor ?? FormCursor.Current;
            string refusalText;
            if (refusalCursor != null && refusalCursor.TryGetRefusal(extensionItem.Position,
                extensionItem.ParameterTemplate, out refusalText) && refusalText != null)
            {
                var fragment = CompileRefusalFragment(extensionItem, compileScope, refusalCursor,
                    refusalText);
                if (fragment == null)
                    return null;
                // The loader does not statically type refusal output (the recorded nominal would
                // need a load to resolve): like any deferred site it stays dynamically typed, which
                // also keeps the element. The marker's own ReturnType stays null (unknown).
                result.CompiledItem = fragment;
                result.ReturnTypeChainedPrevious = DeferredResult.Deferred;
                returnTypeChainedPrevious = DeferredResult.Deferred;
                return fragment;
            }

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
                compileScope.CompileContext.CompileWarnings.Add(
                    CompileWarningFactory.DefinitionRendersTwice(extensionItem.ExtensionName,
                        definitionItem.Position, extensionItem.Position));
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
                    compileScope.CompileWarnings.Add(
                        CompileWarningFactory.FunctionShadowedByExtension(extensionItem.ExtensionName,
                            extensionItem.Position));
                }
                else if (!nameIsExtension && (nameInRegistry ||
                    compileScope.CompileContext.DeferUnboundFunctions))
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
                        extensionItem.Context ?? parseContext, out dataType, returnTypeChainedPrevious);
                    if (functionParameter == null)
                        return null;
                    result.CompiledItem.Parameter = functionParameter;
                    var record = compileScope.CompileContext.FormRecord;
                    if (record != null)
                    {
                        record.AttachParam(extensionItem, functionParameter);
                        record.AttachFunctionRow(callNode);
                    }

                    // No extension exists yet on the function path (the carrier is created
                    // below); the tail call checks the carrier once created.
                    MaybeAttachRefusal(extensionItem, compileScope, dataType, null, result);

                    var carrierItem = new OutputItem(string.Empty, extensionItem.Position,
                        extensionItem.ParameterTemplate)
                    {
                        Context = extensionItem.Context
                    };
                    extension = CreateExtension(carrierItem, compileScope,
                        extensionItem.Context ?? parseContext, ref result.ReturnTypeChainedPrevious, null,
                        dataType, null, chainParameter, out var carrierName);
                    // A bound function forwards its return value through the carrier, whose
                    // body-derived type says nothing about it: report the bound return type (the
                    // deferred case above does the same with the deferred marker).
                    if (DeferredResult.IsDeferred(dataType))
                        result.ReturnTypeChainedPrevious = DeferredResult.Deferred;
                    else
                        result.ReturnTypeChainedPrevious = dataType;
                    if (record != null && extension != null)
                        record.SetItemExtension(extensionItem, record.GetOrAddExtension(carrierName,
                            UnwrapExtension(extension), PropLayout.Fingerprint(UnwrapExtension(extension))));
                    returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
                    result.CompiledItem.ReturnType = result.ReturnTypeChainedPrevious;
                    result.CompiledItem.Extension = extension;
                    return result.CompiledItem;
                }
                else if (!nameIsExtension && functionCompatibleShape)
                {
                    compileScope.CompileErrors.Add(
                        Expressions.FunctionCallMessages.UnknownFunction(extensionItem.ExtensionName)
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
                    var emptyRecord = compileScope.CompileContext.FormRecord;
                    if (emptyRecord != null)
                        emptyRecord.SetItemPayload(extensionItem, FormNone.Instance);
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
                    Position = extensionItem.Position,
                    DeferUnboundFunctions = compileScope.CompileContext.DeferUnboundFunctions
                };
                OptionalValue<object> constantResult =
                    compileScope.CSharpContext.ParseAndGetResultType(compileScope.CompileContext, expressionOptions,
                        out dataType);
                extension = CreateExtension(extensionItem, compileScope, extensionItem.Context ?? parseContext,
                    ref result.ReturnTypeChainedPrevious, null, dataType, definitionItem, chainParameter,
                    out var csharpName);
                var csharpRecord = compileScope.CompileContext.FormRecord;
                if (csharpRecord != null && extension != null)
                    csharpRecord.SetItemExtension(extensionItem, csharpRecord.GetOrAddExtension(csharpName,
                        UnwrapExtension(extension), PropLayout.Fingerprint(UnwrapExtension(extension))));

                returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
                result.CompiledItem.ReturnType = result.ReturnTypeChainedPrevious;
                result.CompiledItem.Extension = extension;
                if (!constantResult.HasValue)
                {
                    result.CompiledItem.Parameter =
                        compileScope.CSharpContext.PushCompileExpression(expressionOptions,
                            compileScope.CompileContext);
                    if (csharpRecord != null)
                    {
                        csharpRecord.SetItemPayload(extensionItem, new FormCSharpUse(csharpRecord.AppendCSharp(
                            extensionItem.CallParameter.CSharpExpression,
                            new List<string>(compileScope.CSharpContext.Namespaces),
                            compileScope.ScopeType, chainedType, compileScope.RootScopeType,
                            extensionItem.Position)));
                    }

                    return result.CompiledItem;
                }

                result.CompiledItem.Parameter = new ConstantParameter(constantResult.Value);
                if (csharpRecord != null)
                    csharpRecord.SetItemPayload(extensionItem, new FormConst(constantResult.Value));
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
                    compileScope, extensionItem.Context ?? parseContext, out dataType, returnTypeChainedPrevious);
                if (nativeParameter == null)
                    return null;
                result.CompiledItem.Parameter = nativeParameter;
                var nativeRecord = compileScope.CompileContext.FormRecord;
                if (nativeRecord != null)
                    nativeRecord.AttachParam(extensionItem, nativeParameter);
            }
            else
            {
                var callParameter = CompileParameterChain(extensionItem.CallParameter.ChainParameter, compileScope,
                    parseContext, returnTypeChainedPrevious);
                dataType = callParameter.RenderType;
                result.CompiledItem.Parameter = new ChainedParameter(callParameter);
                var chainRecord = compileScope.CompileContext.FormRecord;
                if (chainRecord != null)
                    chainRecord.SetItemPayload(extensionItem, chainRecord.BuildChain(callParameter));
                WarnOnRedundantEncoding(extensionItem, callParameter, compileScope);
            }

            extension = CreateExtension(extensionItem, compileScope, extensionItem.Context ?? parseContext,
                ref result.ReturnTypeChainedPrevious, inputModelType, dataType, definitionItem, chainParameter,
                out var tailName);
            if (DeferredResult.IsDeferred(dataType))
                result.ReturnTypeChainedPrevious = DeferredResult.Deferred;
            if (extension != null)
                MaybeAttachRefusal(extensionItem, compileScope, dataType, extension, result);
            var tailRecord = compileScope.CompileContext.FormRecord;
            if (tailRecord != null && extension != null)
                tailRecord.SetItemExtension(extensionItem, tailRecord.GetOrAddExtension(tailName,
                    UnwrapExtension(extension), PropLayout.Fingerprint(UnwrapExtension(extension))));
            returnTypeChainedPrevious = result.ReturnTypeChainedPrevious;
            result.CompiledItem.ReturnType = result.ReturnTypeChainedPrevious;
            result.CompiledItem.Extension = extension;
            return result.CompiledItem;
        }

        /// <summary>Compiles a recorded refusal source as an independent unit and wraps it in the
        /// marker extension. The fragment owns its recompile: the scope shares the materialization's
        /// options, output profile and C# context (same language, same usings) but owns fresh error,
        /// scope and item caches, and bodies compile from live text under a bypassing cursor frame —
        /// the recorded bodies carry the build's typings (possibly deferred), which a bound recompile
        /// must not inherit. Faults are re-anchored to outer coordinates — shifted home for a true
        /// slice, pointed at the refused call site for a synthesized source — and routed to the outer
        /// errors; a faulted fragment drops the item exactly like any hook failure. The cursor frame
        /// is always exited.</summary>
        private static TemplateItem CompileRefusalFragment(OutputItem extensionItem, CompileScope compileScope,
            FormCursor cursor, string sourceText)
        {
            var baseContext = compileScope.CompileContext;
            var fragmentContext = new CompileContext(baseContext.Options, compileScope.ScopeType);
            fragmentContext.OutputProfile = baseContext.OutputProfile;
            var fragmentScope = new CompileScope(fragmentContext, compileScope.CSharpContext);
            fragmentScope.FormCursor = cursor;
            int sliceOffset = cursor.EnterRefusalFragment(extensionItem.Position,
                extensionItem.ParameterTemplate, sourceText);
            bool translated = sliceOffset >= 0;
            try
            {
                var fragmentParse = DocumentParser.Parse(sourceText, fragmentContext,
                    out var fragmentClean);
                var fragment = Compile(fragmentClean, fragmentScope, fragmentParse, null);
                fragmentScope.Compile();
                if (fragmentScope.CompileErrors.Count != 0)
                {
                    foreach (var error in fragmentScope.CompileErrors)
                    {
                        if (translated)
                            error.Position = new BlockPosition(
                                sliceOffset + error.Position.StartIndex, error.Position.Length);
                        else
                            error.Position = extensionItem.Position;
                        error.LinePosition = null;
                        compileScope.CompileErrors.Add(error);
                    }

                    return null;
                }

                if (fragment == null)
                    return null;
                return new TemplateItem
                {
                    Extension = new RefusalFragmentExtension(fragment, extensionItem.Position),
                    Parameter = new Runtime.Parameters.CompiledParameter
                        {ParameterImplementation = (model, chained, root) => model},
                    Position = extensionItem.Position
                };
            }
            finally
            {
                cursor.ExitBody();
            }
        }

        /// <summary>Refusal rules evaluated after the hook ran: <b>(a)</b> the bound extension type
        /// declares <c>[PrecompileUnsupported]</c> — any use refuses with the author's reason; <b>(c)</b> a
        /// bodied or chained consumer whose data parameter carries a deferred (unbindable) call, because its
        /// hook typed from the call's unknown result. Either swaps the payload for the refusal marker (the
        /// refusal fragment owns the recompile at load); bodiless, unchained consumers keep the deferred
        /// site instead. Class (b) arrives pre-noted by the hook that detected the order dependence.</summary>
        private static void MaybeAttachRefusal(OutputItem extensionItem, CompileScope compileScope,
            ExType dataType, IExtension extension, CompiledElement result)
        {
            var record = compileScope.CompileContext.FormRecord;
            if (record == null)
                return;
            int? index = null;
            if (extension != null)
            {
                // UnwrapExtension already returns the type; a further GetType() would query
                // RuntimeType itself and never see the hook's declaration.
                var liveType = UnwrapExtension(extension);
                var optOut = liveType == null ? null : (PrecompileUnsupportedAttribute)
                    Attribute.GetCustomAttribute(liveType, typeof(PrecompileUnsupportedAttribute), true);
                if (optOut != null)
                    index = record.NoteRefusal(extensionItem, PrecompiledRefusalClass.UnsupportedExtension,
                        string.IsNullOrEmpty(optOut.Reason) ? liveType.Name : optOut.Reason,
                        compileScope.ScopeType, compileScope.ScopeType, compileScope.RootScopeType,
                        new List<string>(compileScope.Namespaces));
            }

            if (index == null && DeferredResult.IsDeferred(dataType) &&
                (!string.IsNullOrEmpty(extensionItem.ParameterTemplate) || extensionItem.IsChainedConsumer))
            {
                var names = record.TakeDeferredNames(extensionItem);
                index = record.NoteRefusal(extensionItem, PrecompiledRefusalClass.UnbindableCallTyping,
                    names.Count != 0 ? names[0] : extensionItem.ExtensionName,
                    compileScope.ScopeType, compileScope.ScopeType, compileScope.RootScopeType,
                    new List<string>(compileScope.Namespaces));
            }

            if (index == null)
                index = record.FindRefusal(extensionItem);
            if (index != null && index.Value >= 0)
                record.SetItemPayload(extensionItem, new FormRefusal(index.Value));
        }

        /// <summary>Builds a member-path parameter from the generated site table when the load carries
        /// one that serves this chain, else compiles the chain as before. A record-backed site the
        /// table leaves unserved throws under strict load; unmatched text always rebuilds from data.</summary>
        private static IRuntimeParameter ResolveMemberParameter(CompileScope compileContext, ExType scopeType,
            string[] segments, bool rootReference, List<(Type Type, PropertyInfo Property)> properties)
        {
            var state = compileContext != null ? compileContext.SiteTableState : null;
            Delegate site;
            int ordinal;
            string kind;
            if (Heddle.Precompiled.SiteTableState.TryResolveAccessor(state, segments, rootReference, scopeType,
                HopTriples(properties), out site, out ordinal, out kind) && site != null)
            {
                var accessor = (Func<object, object>)site;
                return rootReference
                    ? (IRuntimeParameter)new RootModelParameter(accessor)
                    : new ModelParameter(accessor);
            }

            if (state != null)
                state.ThrowIfStrictUnserved(kind, ordinal);
            return rootReference
                ? (IRuntimeParameter)new RootModelParameter(properties)
                : new ModelParameter(properties);
        }

        private static List<(Type Declaring, string Name, Type Member)> HopTriples(
            List<(Type Type, PropertyInfo Property)> properties)
        {
            var triples = new List<(Type Declaring, string Name, Type Member)>(properties.Count);
            foreach (var hop in properties)
                triples.Add((hop.Type, hop.Property.Name, hop.Property.PropertyType));
            return triples;
        }

        private static ExType CompileModelAccessor(OutputItem extensionItem, CompileScope compileContext,
            DefinitionItem definitionItem,
            CompiledElement result, ref ExType inputType)
        {
            var scopeType = extensionItem.CallParameter.RootReference
                ? compileContext.CompileContext.RootScopeType
                : compileContext.CompileContext.ScopeType;
            var record = compileContext.CompileContext.FormRecord;

            // Body prop read wins over model on first segment; resolves before dynamic check to keep props statically typed.
            if (!extensionItem.CallParameter.RootReference)
            {
                var propParameter = TryCompilePropRead(extensionItem.CallParameter.ModelParameter, scopeType,
                    compileContext, extensionItem.Position, out var propType, extensionItem);
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

                if (record != null)
                {
                    record.SetItemPayload(extensionItem, new FormDynamicRef(record.RecordDynamicMember(
                        scopeType, extensionItem.CallParameter.ModelParameter,
                        extensionItem.CallParameter.RootReference,
                        new List<(Type Declaring, string Name, Type Member)>()),
                        extensionItem.CallParameter.ModelParameter));
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
                var prefix = HopTriples(resolution.Properties);
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

                if (record != null)
                {
                    record.SetItemPayload(extensionItem, new FormDynamicRef(record.RecordDynamicMember(
                        scopeType, modelParameters, extensionItem.CallParameter.RootReference, prefix),
                        modelParameters));
                }

                return ExType.Dynamic;
            }

            inputType = resolution.ResultType;
            result.CompiledItem.Parameter = ResolveMemberParameter(compileContext, scopeType,
                extensionItem.CallParameter.ModelParameter, extensionItem.CallParameter.RootReference,
                resolution.Properties);

            if (record != null)
            {
                record.SetItemPayload(extensionItem, new FormMemberRef(record.GetOrAddMember(scopeType,
                    modelParameters, extensionItem.CallParameter.RootReference,
                    HopTriples(resolution.Properties))));
            }

            return inputType;
        }

        /// <summary>
        /// Returns <see cref="PropsSlotParameter"/> if first segment is a prop; <c>null</c> to fall through to model resolution.
        /// </summary>
        private static IRuntimeParameter TryCompilePropRead(string[] segments, ExType scopeType,
            CompileScope compileScope, BlockPosition position, out ExType resultType, OutputItem extensionItem)
        {
            resultType = null;
            var layout = compileScope.CompileContext.ActivePropLayout;
            if (layout == null || segments == null || segments.Length == 0 || string.IsNullOrEmpty(segments[0]))
                return null;
            if (!layout.TryGet(segments[0], out var slot))
                return null;

            PropLayout.WarnIfShadowsMember(compileScope, scopeType, slot.Name, position);
            var record = compileScope.CompileContext.FormRecord;

            if (segments.Length == 1)
            {
                resultType = slot.Type;
                if (record != null)
                    record.SetItemPayload(extensionItem, new FormPropsSlot(slot.Index,
                        new List<(Type Declaring, string Name, Type Member)>(), new string[0], false));
                return new PropsSlotParameter(slot.Index);
            }

            var rest = segments.Skip(1).ToArray();
            var resolution = MemberPathResolver.TryResolve(slot.Type, rest);
            if (resolution.Kind == MemberPathResolutionKind.Failed)
            {
                compileScope.CompileErrors.Add(
                    resolution.FailureMessage.ToError(position, HeddleDiagnosticIds.PropertyNotFound));
                resultType = slot.Type;
                if (record != null)
                    record.SetItemPayload(extensionItem, new FormPropsSlot(slot.Index,
                        new List<(Type Declaring, string Name, Type Member)>(), new string[0], false));
                return new PropsSlotParameter(slot.Index);
            }

            if (resolution.Kind == MemberPathResolutionKind.DynamicHop)
            {
                resultType = ExType.Dynamic;
                Func<object, object> prefix = resolution.Properties.Count == 0
                    ? null
                    : ModelParameter.GetPropertyChainAccessor(resolution.Properties).Compile();
                if (record != null)
                    record.SetItemPayload(extensionItem, new FormPropsSlot(slot.Index,
                        new List<(Type Declaring, string Name, Type Member)>(), new string[0], false));
                return new PropsSlotParameter(slot.Index, prefix);
            }

            resultType = resolution.ResultType;
            var hops = ModelParameter.GetPropertyChainAccessor(resolution.Properties).Compile();
            if (record != null)
                record.SetItemPayload(extensionItem, new FormPropsSlot(slot.Index,
                    HopTriples(resolution.Properties), rest, false));
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

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "P3-R9: the dynamic tier is outside the AOT claim (spec, deferred: AOT of the dynamic tier); reached only for dynamic scopes, which the printer declines and strict load refuses.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "P3-R9: the dynamic tier is outside the AOT claim (spec, deferred: AOT of the dynamic tier); reached only for dynamic scopes, which the printer declines and strict load refuses.")]
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
            compileScope.CompileWarnings.Add(
                CompileWarningFactory.RedundantEncodingExtension(producerItem.ExtensionName, producerItem.Position));
        }

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

        private static Type UnwrapExtension(IExtension extension) =>
            (extension as ExtensionParameterCarrier)?.Inner.GetType() ?? extension.GetType();

        private static IExtension CreateExtension(OutputItem extensionItem, CompileScope compileScope,
            ParseContext parseContext,
            ref ExType returnTypeChainedPrevious, ExType inputModelType, ExType dataType,
            DefinitionItem definition, bool chainParameter, out string resolvedName)
        {
            IExtension extension;
            resolvedName = null;
            var record = compileScope.CompileContext.FormRecord;
            var incomingChained = returnTypeChainedPrevious;
            if (definition != null)
            {
                var def = CompileFromDefenition(definition, compileScope, out var acceptType);
                extension = def;
                resolvedName = definition.Name;
                if (record != null)
                    record.RecordDefinition(definition, acceptType);
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
                if (record != null)
                {
                    record.AttachLayout(definition, layout, slotType);
                    var regionLayout = ResolveRegionLayoutCached(definition, compileScope);
                    var regionNames = new List<string>(regionLayout.Slots.Count);
                    foreach (var regionSlot in regionLayout.Slots)
                        regionNames.Add(regionSlot.Name);
                    record.AttachRegions(definition, regionNames);
                }

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
                        compileScope, parseContext, incomingChained);
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
                int callerMark = record != null ? record.DocCount : 0;
                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate,
                    callerModelType, returnTypeChainedPrevious, compileScope, parseContext, extensionItem);
                if (record != null)
                {
                    int callerDocument = record.DocCount > callerMark
                        ? record.DocCount - 1
                        : record.AppendSynthesizedDocument(extensionItem.Context ?? parseContext);
                    record.SetDefLink(extensionItem, FormRecord.DefinitionKey(definition), callerDocument,
                        def.SlotMode);
                }

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
                resolvedName = carrierName;
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
                        var binder = BindProps(extLayout, ownerDisplay, extensionItem, compileScope, parseContext,
                            incomingChained);
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
            CompileScope compileScope, ParseContext parseContext, ExType chainedType)
        {
            var prototype = new object[layout.Count];
            var bound = new bool[layout.Count];
            foreach (var slot in layout.Slots)
            {
                if (slot.HasDefault)
                    prototype[slot.Index] = slot.DefaultBoxed;
            }

            var record = compileScope.CompileContext.FormRecord;
            var plan = new List<PropsBinder.DynamicSlot>();
            List<FormDynSlot> formSlots = null;
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

                    var param = NativeExpressionCompiler.Compile(arg.Value, compileScope, parseContext, out var argType,
                        chainedType);
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
                        if (record != null)
                        {
                            if (formSlots == null)
                                formSlots = new List<FormDynSlot>();
                            formSlots.Add(new FormDynSlot(slot.Index, param, slot.Type));
                        }
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

            var binder = new PropsBinder(prototype, plan.ToArray());
            if (record != null)
            {
                record.RecordProps(extensionItem, (object[])prototype.Clone(),
                    formSlots ?? new List<FormDynSlot>());
            }

            return binder;
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
            if (DeferredResult.IsDeferred(returnType))
                return;
            if (!returnType.IsDynamic)
                returnType = returnType.Type.UnwrapNullable();
            if (dataTypes.Any() && dataTypes.All(dataType =>
            {
                dataType ??= typeof(object);
                if (DeferredResult.IsDeferred(dataType))
                    return false;
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