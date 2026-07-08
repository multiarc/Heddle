using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

            // Phase 6 D2/D25: the single body-compile funnel. Record the span→types entry for the scope map
            // (gated by the flag via the ?. on a null map), but never for a body compiled from an imported file —
            // its foreign offsets must not enter the analyzed document's offset-keyed map (the imported file's
            // own analysis owns those spans, D25 site 4). Import-marked compiles instead bracket their appended
            // diagnostics so the facade can re-anchor them to the import site.
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
        /// Phase 6 D25: stamps every entry appended to <paramref name="list"/> after <paramref name="mark"/> with
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
            ShiftBySkippedTokens(parseContext);
            if (trimDirectiveLines)
                TrimHiddenRemnantLines(parseContext, ref workingDocument);
            RemoveDefinitions(parseContext, ref workingDocument, trimDirectiveLines);
            ReplaceRawOutput(parseContext, ref workingDocument);
            ProcessBranchSets(parseContext, compileScope, ref workingDocument);
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
                        if (compiledItem != null)
                            element.CallChain.Add(compiledItem);
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
                }

                if (returnTypeChainedPrevious == null)
                {
                    RemoveEmptyItem(parseContext, extensions.BlockPosition, ref workingDocument, trimDirectiveLines);
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
                        if (compiledItem != null)
                            element.CallChain.Add(compiledItem);
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
                }

                if (returnTypeChainedPrevious != null)
                {
                    documentElements.Add(element);
                }
            }

            return new RuntimeDocument(workingDocument, documentElements.ToArray(), compileScope);
        }

        private enum BranchBlockKind
        {
            Other,
            Opener,
            Continuation,
            Terminal,
            Participant
        }

        private enum OrphanState
        {
            None,
            Open,
            Closed,
            Unknown
        }

        /// <summary>
        /// <para>The phase 3 compile-time branch-set scan (D9/D10): two state machines over
        /// <see cref="ParseContext.OutputChains"/> in document order, run once per compiled body.</para>
        /// <para>The <b>strip machine</b> swallows the text between the blocks of one branch set (adjacency),
        /// warning <c>HED3001</c> when a stripped gap holds non-whitespace. The <b>orphan machine</b> mirrors
        /// the runtime frame — a non-branch block does not clear state — and emits <c>HED3002</c> (orphan
        /// <c>@elif</c>, acts as <c>@if</c>), <c>HED3003</c> (orphan <c>@else</c>, hard error) and
        /// <c>HED3004</c> (<c>@else</c> with an ignored parameter). Called between <see cref="ReplaceRawOutput"/>
        /// and the chain-compile loop, where chain positions and the working document are consistent.
        /// </para>
        /// </summary>
        private static void ProcessBranchSets(ParseContext parseContext, CompileScope compileScope,
            ref string workingDocument)
        {
            var chains = parseContext.OutputChains;
            if (chains == null || chains.Count == 0)
                return;

            OutputChain stripPrev = null;
            List<BlockPosition> gaps = null;
            OrphanState state = OrphanState.None;

            foreach (var chain in chains)
            {
                var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
                var kind = Classify(chain, leftmost);

                switch (kind)
                {
                    case BranchBlockKind.Opener:
                        stripPrev = chain;
                        state = OrphanState.Open;
                        break;

                    case BranchBlockKind.Continuation:
                        if (stripPrev != null)
                            CollectGap(stripPrev, chain, leftmost, compileScope, workingDocument, ref gaps);
                        stripPrev = chain;
                        if (state == OrphanState.None || state == OrphanState.Closed)
                        {
                            compileScope.CompileWarnings.Add(new HeddleCompileWarning
                            {
                                Error =
                                    $"'@{leftmost.ExtensionName}' has no preceding '@if' or '@ifnot' in this scope — it starts a new branch set, behaving like '@if'.",
                                Fix =
                                    "Open the set with '@if(...)' or '@ifnot(...)', or use '@if' directly if a standalone condition is intended.",
                                Position = leftmost.Position,
                                DiagnosticId = HeddleDiagnosticIds.ElifWithoutIf
                            });
                        }

                        state = OrphanState.Open;
                        break;

                    case BranchBlockKind.Terminal:
                        if (stripPrev != null)
                            CollectGap(stripPrev, chain, leftmost, compileScope, workingDocument, ref gaps);
                        stripPrev = null;
                        if (leftmost != null && !IsEmptyParameter(leftmost))
                        {
                            compileScope.CompileWarnings.Add(new HeddleCompileWarning
                            {
                                Error = "'@else' takes no condition — its parameter is ignored.",
                                Fix = "Use '@elif(...)' for a conditional branch, or remove the parameter.",
                                Position = leftmost.Position,
                                DiagnosticId = HeddleDiagnosticIds.ElseConditionIgnored
                            });
                        }

                        if (state == OrphanState.None || state == OrphanState.Closed)
                        {
                            compileScope.CompileErrors.Add(
                                ElseExtension.NoMatchingIfMessage.ToError(
                                    leftmost?.Position ?? chain.BlockPosition, HeddleDiagnosticIds.ElseWithoutIf));
                            // state unchanged — a further orphan @else errors again.
                        }
                        else
                        {
                            state = OrphanState.Closed;
                        }

                        break;

                    case BranchBlockKind.Participant:
                        stripPrev = null;
                        state = OrphanState.Unknown;
                        break;

                    default: // Other
                        stripPrev = null;
                        // state unchanged: a non-branch block ends stripping adjacency but leaves the
                        // runtime frame intact, so a following @else still binds to the open set.
                        break;
                }
            }

            ApplyGaps(parseContext, gaps, ref workingDocument);
        }

        private static BranchBlockKind Classify(OutputChain chain, OutputItem leftmost)
        {
            if (leftmost == null)
                return BranchBlockKind.Other;
            var name = leftmost.ExtensionName;
            if (chain.Context != null && chain.Context.DefenitionExists(name))
                return BranchBlockKind.Other;

            switch (name)
            {
                case "if":
                case "ifnot":
                    return BranchBlockKind.Opener;
                case "elif":
                case "elseif":
                    return BranchBlockKind.Continuation;
                case "else":
                    return BranchBlockKind.Terminal;
            }

            if (!string.IsNullOrEmpty(name) &&
                TemplateFactory.TryGetExtensionType(name, out var extensionType) &&
                extensionType.IsHaveAttribute<ScopeChannelAttribute>(true))
                return BranchBlockKind.Participant;

            return BranchBlockKind.Other;
        }

        private static bool IsEmptyParameter(OutputItem item)
        {
            var callParameter = item.CallParameter;
            if (!callParameter.IsModelTypeParameter)
                return false; // chain / C# / native expression parameter — non-empty
            return callParameter.ModelParameter == null || callParameter.ModelParameter.Length == 0 ||
                   string.IsNullOrEmpty(callParameter.ModelParameter[0]);
        }

        private static void CollectGap(OutputChain prev, OutputChain next, OutputItem nextLeftmost,
            CompileScope compileScope, string workingDocument, ref List<BlockPosition> gaps)
        {
            int gapStart = prev.BlockPosition.StartIndex + prev.BlockPosition.Length;
            int gapLength = next.BlockPosition.StartIndex - gapStart;
            if (gapLength <= 0)
                return; // zero-length imported blocks make non-positive gaps possible
            if (gapStart < 0 || gapStart + gapLength > workingDocument.Length)
                return;

            var gapText = workingDocument.Substring(gapStart, gapLength);
            if (!string.IsNullOrWhiteSpace(gapText) && nextLeftmost != null)
            {
                compileScope.CompileWarnings.Add(new HeddleCompileWarning
                {
                    Error = "Text between branch blocks is never rendered.",
                    Fix = "Move it before the '@if', after the last branch, or into a branch body.",
                    Position = nextLeftmost.Position,
                    DiagnosticId = HeddleDiagnosticIds.BranchTextStripped
                });
            }

            gaps ??= new List<BlockPosition>();
            gaps.Add(new BlockPosition(gapStart, gapLength));
        }

        private static void ApplyGaps(ParseContext parseContext, List<BlockPosition> gaps,
            ref string workingDocument)
        {
            if (gaps == null || gaps.Count == 0)
                return;

            // Apply right-to-left so already-collected (original-coordinate) gaps to the left stay valid;
            // shift every later chain back by the removed length after each removal.
            for (int i = gaps.Count - 1; i >= 0; i--)
            {
                var gap = gaps[i];
                int seed = ExStringBuilder.ApplyRemove(gap, ref workingDocument);
                foreach (var chain in parseContext.OutputChains)
                {
                    if (chain.BlockPosition.StartIndex > gap.StartIndex)
                    {
                        chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                            chain.BlockPosition.Length);
                    }
                }
            }
        }

        private static void RemoveEmptyItem(ParseContext context, BlockPosition blockPosition,
            ref string workingDocument, bool trimDirectiveLines)
        {
            // D8 step 6: widen a whole-line zero-output chain to swallow its line when trimming is on.
            // The widening only ever extends into surrounding whitespace/newline, so the "chains after this
            // block" predicate (unchanged) still selects exactly the positions needing the shift; only the
            // shift amount grows to the widened length.
            var removal = trimDirectiveLines ? WidenToWholeLine(blockPosition, workingDocument) : blockPosition;
            int seed = ExStringBuilder.ApplyRemove(removal, ref workingDocument);
            foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
            {
                if (chain.BlockPosition.StartIndex > blockPosition.StartIndex)
                {
                    chain.BlockPosition =
                        new BlockPosition(chain.BlockPosition.StartIndex - seed, chain.BlockPosition.Length);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// <para>Phase 4 D6 — the whole-line trim predicate, evaluated against the working document at the
        /// moment of removal. A removed span is widened to its whole line iff the block occupies the line by
        /// itself: only spaces/tabs to the left back to a line terminator or document start, and only
        /// spaces/tabs then one line terminator (or EOF) to the right. Both sides must pass. A returned span
        /// equal to the input means "not whole-line — remove exactly as today".</para>
        /// <para>Allocation-free; a plain char loop shared by all TFMs (cross-cutting D7). Handles a
        /// zero-length probe (the remnant-line case) without a special case — the scans meet across the empty
        /// span and the predicate degenerates to "is this line whitespace-only".</para>
        /// </summary>
        private static BlockPosition WidenToWholeLine(BlockPosition block, string document)
        {
            // Defensive clamp: an earlier widened removal on the same line can leave a later block's stored
            // position overshooting the (now shorter) working document. Never dereference past its end.
            int startIndex = block.StartIndex < 0 ? 0
                : (block.StartIndex > document.Length ? document.Length : block.StartIndex);
            int endIndex = block.StartIndex + block.Length;
            if (endIndex > document.Length) endIndex = document.Length;
            if (endIndex < startIndex) endIndex = startIndex;

            int left = startIndex;                             // will become the widened start
            while (left > 0 && (document[left - 1] == ' ' || document[left - 1] == '\t'))
                left--;
            if (left != 0 && document[left - 1] != '\n' && document[left - 1] != '\r')
                return new BlockPosition(startIndex, endIndex - startIndex); // content on the left — unchanged

            int right = endIndex;                              // first index after the block
            while (right < document.Length && (document[right] == ' ' || document[right] == '\t'))
                right++;
            if (right >= document.Length)
                return new BlockPosition(left, right - left);  // EOF is a valid terminator
            if (document[right] == '\r')
            {
                right += right + 1 < document.Length && document[right + 1] == '\n' ? 2 : 1;
                return new BlockPosition(left, right - left);  // CRLF pair or bare CR
            }
            if (document[right] == '\n')
                return new BlockPosition(left, right + 1 - left);
            return new BlockPosition(startIndex, endIndex - startIndex); // content on the right — unchanged
        }

        /// <summary>
        /// Phase 4 D6/D8 step 2 — removes comment-only remnant lines when trimming is on. A whole-line comment
        /// leaves a bare terminator in the working document (the lexer excised only the hidden comment token).
        /// Each <see cref="ParseContext.SkippedTokens"/> entry is mapped to its clean-document position
        /// (original start minus the summed lengths of prior hidden tokens — the list is in document order),
        /// then a zero-length probe there is run through <see cref="WidenToWholeLine"/>: it widens only when
        /// the remnant line is whitespace-only, which removes comment-only lines and is a no-op for every
        /// <c>@\</c> remnant (their lines retain content by construction). Processed in reverse document order,
        /// skipping positions inside an already-removed span (multiple comments on one line remove it once).
        /// </summary>
        private static void TrimHiddenRemnantLines(ParseContext context, ref string workingDocument)
        {
            var skipped = context.SkippedTokens;
            if (skipped == null || skipped.Count == 0)
                return;

            var cleanStarts = new int[skipped.Count];
            int running = 0;
            for (int i = 0; i < skipped.Count; i++)
            {
                cleanStarts[i] = skipped[i].StartIndex - running;
                running += skipped[i].Length;
            }

            int removedStart = int.MaxValue;
            int removedEnd = int.MaxValue;
            for (int i = skipped.Count - 1; i >= 0; i--)
            {
                int start = cleanStarts[i];
                if (start < 0 || start > workingDocument.Length)
                    continue;
                if (start >= removedStart && start < removedEnd)
                    continue;                                  // this line was already removed by a later token

                var widened = WidenToWholeLine(new BlockPosition(start, 0), workingDocument);
                if (widened.Length == 0)
                    continue;                                  // not a whitespace-only remnant line

                removedStart = widened.StartIndex;
                removedEnd = widened.StartIndex + widened.Length;
                int seed = ExStringBuilder.ApplyRemove(widened, ref workingDocument);
                ShiftListsAfter(context, widened, seed);
            }
        }

        /// <summary>
        /// Shifts <see cref="ParseContext.OutputChains"/>, <see cref="DefinitionBlock.Positions"/>, and
        /// <see cref="ParseContext.RawOutputItems"/> back by <paramref name="seed"/> for every entry after the
        /// removed span — the same reverse-loop pattern <see cref="ShiftBySkippedTokens"/> applies to the three
        /// lists, used by <see cref="TrimHiddenRemnantLines"/>.
        /// </summary>
        private static void ShiftListsAfter(ParseContext context, BlockPosition removed, int seed)
        {
            int boundary = removed.StartIndex + removed.Length;
            foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
            {
                if (chain.BlockPosition.StartIndex >= boundary)
                    chain.BlockPosition =
                        new BlockPosition(chain.BlockPosition.StartIndex - seed, chain.BlockPosition.Length);
                else
                    break;
            }

            for (int index = context.DefinitionsBlock.Positions.Count - 1; index >= 0; index--)
            {
                var position = context.DefinitionsBlock.Positions[index];
                if (position.StartIndex >= boundary)
                    context.DefinitionsBlock.Positions[index] =
                        new BlockPosition(position.StartIndex - seed, position.Length);
                else
                    break;
            }

            foreach (var raw in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
            {
                if (raw.BlockPosition.StartIndex >= boundary)
                    raw.BlockPosition = new BlockPosition(raw.BlockPosition.StartIndex - seed, raw.BlockPosition.Length);
                else
                    break;
            }
        }

        private static void ReplaceRawOutput(ParseContext context, ref string workingDocument)
        {
            foreach (var rawOut in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
            {
                workingDocument = ExStringBuilder.Replace(rawOut.BlockPosition.StartIndex, rawOut.BlockPosition.Length,
                    rawOut.Text, workingDocument);
                int seed = rawOut.BlockPosition.Length - rawOut.Text.Length;
                var outputItem = rawOut;
                foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
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
            foreach (var blockPosition in ((ICollection<BlockPosition>) context.SkippedTokens).Reverse())
            {
                var seed = blockPosition.Length;

                var startToSkip = blockPosition.StartIndex;
                var endToSkip = blockPosition.StartIndex + blockPosition.Length - 1;

                foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
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

                foreach (var raw in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
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

        private static void RemoveDefinitions(ParseContext context, ref string workingDocument,
            bool trimDirectiveLines)
        {
            foreach (var definitionBlock in ((ICollection<BlockPosition>) context.DefinitionsBlock.Positions).Reverse())
            {
                // D8 step 3: each definition/import span is widened to its whole line first when trimming is
                // on; the existing shift loops compare against the original block boundaries (the widening
                // only extends into whitespace, so no chain/raw sits between them and the shift set is the
                // same — only the removed length grows).
                var removal = trimDirectiveLines ? WidenToWholeLine(definitionBlock, workingDocument) : definitionBlock;
                int seed = ExStringBuilder.ApplyRemove(removal, ref workingDocument);
                foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
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

                foreach (var raw in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
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
                var compiledItem = CompileItem(item, compileContext, parseContext, ref returnTypeChainedPrevious,
                    chainParameter: true);
                if (compiledItem != null)
                    result.Add(compiledItem);
            }

            return result;
        }

        // chainParameter is true when this item is a *nested* producer inside another item's parenthesized
        // chain (compiled via CompileParameterChain). Such producers forward their value raw to the enclosing
        // carrier — only the enclosing leaf carrier applies the Html redirect (phase 2 D3, as corrected by the
        // amendments ledger: @(upper(x)) compiles as @() <- upper(x), so redirecting the nested function
        // carrier too would double-encode; encoding stays at the emitting leaf per D10).
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
            if (parseContext.DefenitionExists(extensionItem.ExtensionName))
            {
                definitionItem = parseContext.GetDefenition(extensionItem.ExtensionName);
            }

            // Phase 5 (D10 rule 1, D15): named-argument target/mode checks. Named arguments require the native
            // tier (MemberPathsOnly → HED1014) and a definition target (extension/function/unknown → HED5005).
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

                if (definitionItem == null)
                {
                    compileScope.CompileErrors.Add(
                        $"'{extensionItem.ExtensionName}' is not a definition — named arguments can only be passed to definitions that declare props."
                            .ToError(firstArgPosition, HeddleDiagnosticIds.NamedArgumentsNotSupported));
                    return null;
                }
            }

            // HED4002 (phase 4 D4): a by-name call resolving to a definition that carries a default output
            // ('-> chain') renders it twice — once at the call, once via the default chain at document end.
            // The default chain's own synthetic self-call is exempt. Memoization guarantees one warning per
            // distinct call site.
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

            // Standalone name resolution (D11): definition -> extension -> registered function.
            // The registry fallback is part of the native tier, so it is off under MemberPathsOnly.
            if (!string.IsNullOrEmpty(extensionItem.ExtensionName) && definitionItem == null &&
                compileScope.Options.ExpressionMode != ExpressionMode.MemberPathsOnly)
            {
                var functionRegistry = compileScope.Options.Functions ?? FunctionRegistry.Default;
                bool nameIsExtension = TemplateFactory.Exists(extensionItem.ExtensionName);
                bool nameInRegistry = functionRegistry.Contains(extensionItem.ExtensionName);
                bool functionCompatibleShape = extensionItem.CallParameter.ChainParameter == null &&
                                               string.IsNullOrEmpty(extensionItem.CallParameter.CSharpExpression);

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
                if (!compileScope.Options.AllowCSharp)
                {
                    compileScope.CompileErrors.Add(
                        "C# Code Not allowed here, see TemplateOptions.AllowCSharp Property".ToError(extensionItem
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

            // Phase 5 (D9): a body prop read wins over the model on the first segment (never for :: root refs).
            // Resolves before the dynamic check so props stay statically typed even in a :: dynamic definition.
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
        /// Member-tier body prop read (D9). Returns a <see cref="PropsSlotParameter"/> when the first path
        /// segment names a prop in the active layout (single-segment = a direct slot load; multi-hop roots the
        /// phase 1 null-safe chain at the boxed prop value), or <c>null</c> when the segment is not a prop (the
        /// caller falls through to ordinary model resolution).
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
        /// Emits the HED2003 double-encode warning (phase 2 D9): a bodiless unnamed <c>@(...)</c> under the
        /// effective <c>Html</c> profile whose parenthesized chain's final producer (the leftmost call =
        /// the last compiled <see cref="TemplateItem"/>) carries <c>[EncodeOutput]</c> would encode the value
        /// twice. Output is unchanged; the warning points at the redundant producer call.
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
            if (producer.Extension == null ||
                !producer.Extension.GetType().IsHaveAttribute<EncodeOutputAttribute>(true))
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

        /// <summary>
        /// Resolves the registry name for an unnamed <c>@(...)</c> / standalone-function carrier (phase 2 D3).
        /// A bodiless unnamed carrier resolves <c>"html"</c> (<see cref="Heddle.Extensions.EmptyHtmlExtension"/>)
        /// under the <see cref="OutputProfile.Html"/> profile — reusing the proven
        /// <c>[EncodeOutput]</c> pipeline — and stays the raw empty carrier under <c>Text</c>. A bodied
        /// <c>@(X){{…}}</c> is a raw rescoping container and is never redirected. Resolving a bodiless carrier
        /// records <see cref="CompileContext.UnnamedOutputCompiled"/> for the HED2002 directive-position warning.
        /// </summary>
        private static string UnnamedCarrierName(OutputItem item, CompileContext context)
        {
            if (!string.IsNullOrEmpty(item.ParameterTemplate))
                return string.Empty;                       // @(X){{…}} stays the raw rescoping container
            context.UnnamedOutputCompiled = true;          // D13 tracking
            return context.OutputProfile == OutputProfile.Html ? "html" : string.Empty;
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

                // Phase 5: resolve the layout + slot type (cached), bind named arguments, install the binder.
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
                    def.PropsBinder = BindProps(definition, layout, extensionItem, compileScope, parseContext);
                }

                // Caller-content compile: model type is the slot type in slot mode (D11), else the positional
                // model type; the enclosing prop layout/slot type stay active (D12 — caller content is lexical).
                var callerModelType = slotType ?? dataType;
                returnTypeChainedPrevious = InitializeTemplate(extension, extensionItem.ParameterTemplate,
                    callerModelType, returnTypeChainedPrevious, compileScope, parseContext, extensionItem);

                // Def-body compile under this definition's own layout/slot type (D12 save/set/restore).
                var compileContext = compileScope.CompileContext;
                var savedLayout = compileContext.ActivePropLayout;
                var savedSlot = compileContext.SlotParameterType;
                compileContext.ActivePropLayout = layout;
                compileContext.SlotParameterType = slotType;
                def.DefinitionParameterTemplate = CompileFromDefenition(definition, compileScope, out acceptType);
                returnTypeChainedPrevious = InitializeTemplate(def.DefinitionParameterTemplate,
                    definition.ParameterTemplate,
                    dataType,
                    returnTypeChainedPrevious, compileScope, definition.Context, extensionItem);
                compileContext.ActivePropLayout = savedLayout;
                compileContext.SlotParameterType = savedSlot;
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
            }

            return extension;
        }

        /// <summary>
        /// Resolves (and caches, D6) the prop layout of <paramref name="definition"/>. Keyed by a stable
        /// definition identity so two call sites of one definition share the layout instance across the isolated
        /// contexts the parser produces per body.
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

        /// <summary>Resolves the slot parameter type (D3), inheriting the first declared <c>out::</c> down the
        /// base chain; HED5010 (slot form) when unresolvable.</summary>
        private static ExType ResolveSlotType(DefinitionItem definition, CompileScope compileScope)
        {
            string slotName = null;
            for (var d = definition; d != null; d = d.BaseDefinition)
            {
                if (!string.IsNullOrEmpty(d.SlotTypeName))
                {
                    slotName = d.SlotTypeName;
                    break;
                }
            }

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
        /// Binds a call site's named arguments against the layout (D10): builds the frozen prototype (defaults +
        /// converted constant arguments) and the dynamic slot plan, emitting HED5001/HED5003/HED5004 per argument
        /// and HED5002 for any unbound required slot. Argument values are native expressions compiled in the
        /// caller's context (D8 — evaluated against <c>scope.Parent()</c> at bind).
        /// </summary>
        private static PropsBinder BindProps(DefinitionItem definition, PropLayout layout, OutputItem extensionItem,
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
                            $"Unknown prop '{arg.Name}' on definition '{definition.Name}'. Declared props: {declared}."
                                .ToError(arg.Position, HeddleDiagnosticIds.UnknownProp));
                        continue;
                    }

                    var param = NativeExpressionCompiler.Compile(arg.Value, compileScope, parseContext, out var argType);
                    if (param == null)
                    {
                        // The argument expression itself failed (error already recorded); mark bound so a
                        // spurious HED5002 does not pile on.
                        bound[slot.Index] = true;
                        continue;
                    }

                    bool isNullConstant = param is ConstantParameter nullConst && nullConst.Value == null;
                    if (isNullConstant)
                    {
                        if (slot.Type.Type.IsValueType && Nullable.GetUnderlyingType(slot.Type.Type) == null)
                        {
                            compileScope.CompileErrors.Add(
                                PropTypeMismatchMessage(definition, slot, argType)
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
                            PropTypeMismatchMessage(definition, slot, argType)
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
                        $"Missing required prop '{slot.Name}' ({slot.Type.Type}) on the call to definition '{definition.Name}'. Pass '{slot.Name}: …' or declare a default."
                            .ToError(extensionItem.Position, HeddleDiagnosticIds.MissingRequiredProp));
                }
            }

            return new PropsBinder(prototype, plan.ToArray());
        }

        private static string PropTypeMismatchMessage(DefinitionItem definition, PropSlot slot, ExType argType)
        {
            var argName = argType == null ? "unknown" : argType.IsDynamic ? "dynamic" : argType.Type.ToString();
            return
                $"Prop '{slot.Name}' of definition '{definition.Name}' expects {slot.Type.Type}, but the argument type is {argName}.";
        }

        /// <summary>Builds the boxed-value converter for a dynamic argument that widens numerically (the boxed
        /// value's runtime type must become the prop type); <c>null</c> when no runtime conversion is needed.</summary>
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
            RenderType directRender = extension.GetType().IsHaveAttribute<EncodeOutputAttribute>(true)
                ? (extension.GetType().IsHaveAttribute<NotEncodeAttribute>(true) ? RenderType.Raw : RenderType.Encode)
                : RenderType.Raw;
            extension.SetUpRenderType(directRender);
            var initContext = new InitContext(parameterFastString, compileScope, parseContext)
            {
                SourceItem = sourceItem
            };
            return extension.InitStart(initContext, modelType, chainedType, compileScope.ScopeType);
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