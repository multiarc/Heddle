using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// <para>Generator plan phase 2 (D2/D4) — the single implementation of every byte-affecting document-shaping
    /// machine. Both backends drive it: the runtime <c>HeddleCompiler.CompileBody</c> and the build-time
    /// <c>DocumentShaper.Shape</c>. It used to exist twice as hand-maintained copies, which is how the
    /// <see cref="WidenToWholeLine"/> clamp drift shipped; every offset-arithmetic fix now lands here once.</para>
    /// <para><b>Normative pass ordering</b> (D4) — both drivers invoke these in this relative order, and the
    /// lockstep test asserts they still do:</para>
    /// <list type="number">
    /// <item><see cref="ShiftBySkippedTokens"/> — rebase onto the hidden-token-excised clean document.</item>
    /// <item><see cref="TrimHiddenRemnantLines"/> — only when <c>TrimDirectiveLines</c> is on.</item>
    /// <item><see cref="RemoveDefinitions"/> — definition/import block removal.</item>
    /// <item><see cref="ReplaceRawOutput"/> — raw-output splice.</item>
    /// <item><see cref="StripBranchSets"/> — branch-set adjacency strip.</item>
    /// <item><see cref="RemoveEmptyItem"/> — per zero-output chain, in document order.</item>
    /// </list>
    /// <para>Constraints this file lives under: netstandard2.0-clean, no Roslyn types, no <c>unsafe</c>, and no
    /// type outside the already-linked parse model — it is compiled into the generator by the
    /// <c>..\Heddle\Language\**\*.cs</c> glob in <c>Heddle.Generator.csproj</c> with zero csproj edits, so any
    /// violation is a red generator build rather than a latent defect. That is also why the string operations
    /// below are the safe pair (D3) rather than <c>ExStringBuilder</c>'s <c>unsafe</c> equivalents, which cannot
    /// be linked; the runtime's other <c>ExStringBuilder</c> consumers are untouched.</para>
    /// </summary>
    internal static class DocumentShaping
    {
        // ---- Safe string operations (D3): semantically equal to ExStringBuilder.ApplyRemove/Replace. ----

        /// <summary>Removes <paramref name="element"/> from <paramref name="source"/> and returns the removed
        /// length (the shift <c>seed</c> every rebasing loop applies).</summary>
        internal static int ApplyRemove(BlockPosition element, ref string source)
        {
            source = source.Remove(element.StartIndex, element.Length);
            return element.Length;
        }

        /// <summary>Splices <paramref name="replacement"/> over <c>[start, start+length)</c> of
        /// <paramref name="source"/>.</summary>
        internal static string Replace(int start, int length, string replacement, string source)
            => source.Substring(0, start) + replacement + source.Substring(start + length);

        // ---- The whole-line trim predicate. ----

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
        internal static BlockPosition WidenToWholeLine(BlockPosition block, string document)
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

        // ---- The five position-rebasing passes. ----

        /// <summary>Pass 1 — rebases the three offset-keyed lists off the hidden (comment / <c>@\</c>) tokens the
        /// lexer excised, using the three-way classification: a block that <em>encloses</em> a skipped token keeps
        /// its start and loses the token's length; a block wholly after moves back by it; a block wholly before is
        /// untouched and terminates the reverse loop.</summary>
        internal static void ShiftBySkippedTokens(ParseContext context)
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

        /// <summary>
        /// Pass 2 (phase 4 D6/D8 step 2) — removes comment-only remnant lines when trimming is on. A whole-line
        /// comment leaves a bare terminator in the working document (the lexer excised only the hidden comment
        /// token). Each <see cref="ParseContext.SkippedTokens"/> entry is mapped to its clean-document position
        /// (original start minus the summed lengths of prior hidden tokens — the list is in document order),
        /// then a zero-length probe there is run through <see cref="WidenToWholeLine"/>: it widens only when
        /// the remnant line is whitespace-only, which removes comment-only lines and is a no-op for every
        /// <c>@\</c> remnant (their lines retain content by construction). Processed in reverse document order,
        /// skipping positions inside an already-removed span (multiple comments on one line remove it once).
        /// </summary>
        internal static void TrimHiddenRemnantLines(ParseContext context, ref string workingDocument)
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
                int seed = ApplyRemove(widened, ref workingDocument);
                ShiftListsAfter(context, widened, seed);
            }
        }

        /// <summary>
        /// Shifts <see cref="ParseContext.OutputChains"/>, <see cref="DefinitionBlock.Positions"/>, and
        /// <see cref="ParseContext.RawOutputItems"/> to account for the removed span, using the same three-way
        /// classification <see cref="ShiftBySkippedTokens"/> applies: a block that <em>encloses</em> the removed
        /// span keeps its start but loses <paramref name="seed"/> from its length; a block wholly after the span
        /// moves back by <paramref name="seed"/>; a block wholly before is untouched. Used by
        /// <see cref="TrimHiddenRemnantLines"/>. Missing the enclosing case let a whole-line comment removed from
        /// inside a definition block leave that block's length overstated, so <see cref="RemoveDefinitions"/> then
        /// over-removed into the following text (cross-file/imported bodies made this visible as offset drift).
        /// </summary>
        internal static void ShiftListsAfter(ParseContext context, BlockPosition removed, int seed)
        {
            int startToSkip = removed.StartIndex;
            int endToSkip = removed.StartIndex + removed.Length - 1;
            foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
            {
                var start = chain.BlockPosition.StartIndex;
                var end = start + chain.BlockPosition.Length - 1;
                if (start <= startToSkip && end >= endToSkip)
                    chain.BlockPosition = new BlockPosition(start, chain.BlockPosition.Length - seed);
                else if (end > startToSkip)
                    chain.BlockPosition = new BlockPosition(start - seed, chain.BlockPosition.Length);
                else
                    break;
            }

            for (int index = context.DefinitionsBlock.Positions.Count - 1; index >= 0; index--)
            {
                var position = context.DefinitionsBlock.Positions[index];
                var start = position.StartIndex;
                var end = start + position.Length - 1;
                if (start <= startToSkip && end >= endToSkip)
                    context.DefinitionsBlock.Positions[index] = new BlockPosition(start, position.Length - seed);
                else if (end > startToSkip)
                    context.DefinitionsBlock.Positions[index] = new BlockPosition(start - seed, position.Length);
                else
                    break;
            }

            foreach (var raw in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
            {
                var start = raw.BlockPosition.StartIndex;
                var end = start + raw.BlockPosition.Length - 1;
                if (end > startToSkip)
                    raw.BlockPosition = new BlockPosition(start - seed, raw.BlockPosition.Length);
                else
                    break;
            }
        }

        /// <summary>Pass 3 — removes every definition/import block. D8 step 3: each span is widened to its whole
        /// line first when trimming is on; the shift loops compare against the <em>original</em> block boundaries
        /// (the widening only extends into whitespace, so no chain/raw sits between them and the shift set is the
        /// same — only the removed length grows).</summary>
        internal static void RemoveDefinitions(ParseContext context, ref string workingDocument,
            bool trimDirectiveLines)
        {
            foreach (var definitionBlock in ((ICollection<BlockPosition>) context.DefinitionsBlock.Positions).Reverse())
            {
                var removal = trimDirectiveLines ? WidenToWholeLine(definitionBlock, workingDocument) : definitionBlock;
                int seed = ApplyRemove(removal, ref workingDocument);
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

        /// <summary>Pass 4 — splices every raw-output block's text over its source span, shifting the chains
        /// strictly to its right by the length delta.</summary>
        internal static void ReplaceRawOutput(ParseContext context, ref string workingDocument)
        {
            foreach (var rawOut in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
            {
                workingDocument = Replace(rawOut.BlockPosition.StartIndex, rawOut.BlockPosition.Length,
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

        /// <summary>Pass 6 — removes one zero-output chain. D8 step 6: widen a whole-line zero-output chain to
        /// swallow its line when trimming is on. The widening only ever extends into surrounding
        /// whitespace/newline, so the "chains after this block" predicate (unchanged, against the
        /// <em>original</em> position) still selects exactly the positions needing the shift; only the shift
        /// amount grows to the widened length.</summary>
        internal static void RemoveEmptyItem(ParseContext context, BlockPosition blockPosition,
            ref string workingDocument, bool trimDirectiveLines)
        {
            var removal = trimDirectiveLines ? WidenToWholeLine(blockPosition, workingDocument) : blockPosition;
            int seed = ApplyRemove(removal, ref workingDocument);
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

        // ---- Pass 5: the branch-set adjacency strip machine. ----

        /// <summary>
        /// The branch classification both backends map into (D5). Defined once here so the runtime cannot gain a
        /// kind the generator silently misses — the <c>Participant</c>/<c>Other</c> collapse that used to be
        /// "benign only because both disarm" is now a checked shape. Deliberately independent of either
        /// <c>BranchRole</c> enum, so this file needs no attribute or Roslyn types.
        /// </summary>
        internal enum BranchKind
        {
            Other,
            Opener,
            Continuation,
            Terminal,
            Participant
        }

        /// <summary>
        /// The strip machine's event stream, consumed by the runtime to re-host its HED3001–HED3005 diagnostics
        /// and its orphan state machine without duplicating the machine they observe (D5). The generator passes
        /// no observer. Three events rather than the plan's two, because the runtime's diagnostic <em>order</em>
        /// within one block is HED3005 → HED3001 (gap) → HED3002/3/4: the scope-channel check runs before gap
        /// collection and the orphan machine after it, so a single "classified" event could not preserve it.
        /// </summary>
        internal interface IBranchStripObserver
        {
            /// <summary>Raised for every chain in document order, immediately after classification and before any
            /// gap for it is collected.</summary>
            void OnClassified(OutputChain chain, OutputItem leftmost, BranchKind kind);

            /// <summary>Raised for every gap the machine actually collects, with the gap's text.</summary>
            void OnGapCollected(OutputChain prev, OutputChain next, OutputItem nextLeftmost, BlockPosition gap,
                string gapText);

            /// <summary>Raised for every chain in document order, after its gap (if any) was collected and the
            /// strip state advanced.</summary>
            void OnBlockCompleted(OutputChain chain, OutputItem leftmost, BranchKind kind);
        }

        /// <summary>
        /// <para>Pass 5 — the adjacency strip: a document-ordered state machine over
        /// <see cref="ParseContext.OutputChains"/> that swallows the text between the blocks of one branch set.
        /// An <c>Opener</c> arms; a <c>Continuation</c> collects the gap <c>[prevEnd, nextStart)</c> and re-arms;
        /// a <c>Terminal</c> collects and disarms; a <c>Participant</c> and anything else disarm. Collected gaps
        /// are applied right-to-left.</para>
        /// <para>Classification stays per-side behind <paramref name="classify"/> (the runtime resolves through
        /// <c>TemplateFactory</c> + <c>[BranchRole]</c>/<c>[ScopeChannel]</c>; the generator through its
        /// Roslyn-backed binder), including the R8 definition-first guard.</para>
        /// </summary>
        internal static void StripBranchSets(ParseContext parseContext, ref string workingDocument,
            Func<OutputChain, BranchKind> classify, IBranchStripObserver observer = null)
        {
            var chains = parseContext.OutputChains;
            if (chains == null || chains.Count == 0)
                return;

            OutputChain stripPrev = null;
            List<BlockPosition> gaps = null;

            foreach (var chain in chains)
            {
                var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
                var kind = classify(chain);
                observer?.OnClassified(chain, leftmost, kind);

                switch (kind)
                {
                    case BranchKind.Opener:
                        stripPrev = chain;
                        break;

                    case BranchKind.Continuation:
                        if (stripPrev != null)
                            CollectGap(stripPrev, chain, leftmost, workingDocument, ref gaps, observer);
                        stripPrev = chain;
                        break;

                    case BranchKind.Terminal:
                        if (stripPrev != null)
                            CollectGap(stripPrev, chain, leftmost, workingDocument, ref gaps, observer);
                        stripPrev = null;
                        break;

                    case BranchKind.Participant:
                        stripPrev = null;
                        break;

                    default: // Other
                        stripPrev = null;
                        // a non-branch block ends stripping adjacency but leaves the runtime frame intact, so a
                        // following branch terminal still binds to the open set (the observer's concern).
                        break;
                }

                observer?.OnBlockCompleted(chain, leftmost, kind);
            }

            ApplyGaps(parseContext, gaps, ref workingDocument);
        }

        private static void CollectGap(OutputChain prev, OutputChain next, OutputItem nextLeftmost,
            string workingDocument, ref List<BlockPosition> gaps, IBranchStripObserver observer)
        {
            int gapStart = prev.BlockPosition.StartIndex + prev.BlockPosition.Length;
            int gapLength = next.BlockPosition.StartIndex - gapStart;
            if (gapLength <= 0)
                return; // zero-length imported blocks make non-positive gaps possible
            if (gapStart < 0 || gapStart + gapLength > workingDocument.Length)
                return;

            var gap = new BlockPosition(gapStart, gapLength);
            observer?.OnGapCollected(prev, next, nextLeftmost, gap,
                workingDocument.Substring(gapStart, gapLength));

            gaps ??= new List<BlockPosition>();
            gaps.Add(gap);
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
                int seed = ApplyRemove(gap, ref workingDocument);
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

        // ---- Piece slicing (D6). ----

        /// <summary>
        /// <para>The static-piece segmentation walk, shared by <c>RuntimeDocument.GetDocumentPieces</c> and the
        /// emitter's body walk — <em>the</em> byte-parity contract: the emitter's <c>P0..Pn</c> constants must
        /// equal the runtime's piece strings exactly.</para>
        /// <para>Walks <paramref name="elements"/> in document order; emits the literal
        /// <c>document[offset .. StartIndex)</c> through <paramref name="onPiece"/> only when
        /// <c>StartIndex &gt; offset</c>; calls <paramref name="onElement"/> for every element; advances past the
        /// element; emits the trailing literal after the loop. <paramref name="onElement"/> returns <c>false</c>
        /// to abandon the walk (the emitter's mid-walk degrade), which this method reports by returning
        /// <c>false</c> — no exception, per the phase's intentional-refusal posture.</para>
        /// </summary>
        internal static bool SlicePieces<T>(IEnumerable<T> elements, Func<T, BlockPosition> position,
            string document, Action<string> onPiece, Func<T, bool> onElement)
        {
            int offset = 0;
            foreach (var element in elements)
            {
                var pos = position(element);
                if (pos.StartIndex > offset)
                    onPiece(document.Substring(offset, pos.StartIndex - offset));

                if (!onElement(element))
                    return false;

                offset = pos.StartIndex + pos.Length;
            }

            if (document.Length > offset)
                onPiece(document.Substring(offset));

            return true;
        }
    }
}
