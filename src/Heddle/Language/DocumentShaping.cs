using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// Single implementation of all byte-affecting document shaping, shared by runtime compile
    /// and build-time shaper to prevent offset-arithmetic drift. Pass ordering (via
    /// <see cref="ShiftBySkippedTokens"/>, <see cref="TrimHiddenRemnantLines"/>,
    /// <see cref="RemoveDefinitions"/>, <see cref="ReplaceRawOutput"/>, <see cref="StripBranchSets"/>,
    /// <see cref="RemoveEmptyItem"/>) must be preserved. Constraint: netstandard2.0-clean, no Roslyn
    /// types, no <c>unsafe</c> — this file is linked into the generator with zero csproj edits.
    /// </summary>
    internal static class DocumentShaping
    {
        /// <summary>Removes <paramref name="element"/> from <paramref name="source"/>, returning the
        /// removed length used as shift <c>seed</c> in rebasing loops.</summary>
        internal static int ApplyRemove(BlockPosition element, ref string source)
        {
            source = source.Remove(element.StartIndex, element.Length);
            return element.Length;
        }

        /// <summary>Splices <paramref name="replacement"/> at <c>[start, start+length)</c> of
        /// <paramref name="source"/>.</summary>
        internal static string Replace(int start, int length, string replacement, string source)
            => source.Substring(0, start) + replacement + source.Substring(start + length);

        /// <summary>Widens to whole line iff block occupies line by itself (spaces/tabs only to
        /// terminator on both sides). Returns input unchanged if not whole-line. Allocation-free;
        /// handles zero-length probe as whitespace-only line check.</summary>
        internal static BlockPosition WidenToWholeLine(BlockPosition block, string document)
        {
            // Defensive clamp: an earlier widened removal on the same line can leave a later block's stored
            // position overshooting the (now shorter) working document. Never dereference past its end.
            int startIndex = block.StartIndex < 0 ? 0
                : (block.StartIndex > document.Length ? document.Length : block.StartIndex);
            int endIndex = block.StartIndex + block.Length;
            if (endIndex > document.Length) endIndex = document.Length;
            if (endIndex < startIndex) endIndex = startIndex;

            int left = startIndex;
            while (left > 0 && (document[left - 1] == ' ' || document[left - 1] == '\t'))
                left--;
            if (left != 0 && document[left - 1] != '\n' && document[left - 1] != '\r')
                return new BlockPosition(startIndex, endIndex - startIndex);

            int right = endIndex;
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
            return new BlockPosition(startIndex, endIndex - startIndex);
        }

        /// <summary>Pass 1: rebases three offset-keyed lists off hidden tokens excised by the lexer.
        /// Enclosing block loses token length; after block moves back by it; before block untouched.</summary>
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

        /// <summary>Pass 2: removes comment-only remnant lines when trimming is on, using
        /// <see cref="WidenToWholeLine"/> on zero-length probes at skipped token positions.
        /// Reverse document order; skips positions inside already-removed spans.</summary>
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
                    continue; // already removed by a later token in reverse order

                var widened = WidenToWholeLine(new BlockPosition(start, 0), workingDocument);
                if (widened.Length == 0)
                    continue;

                removedStart = widened.StartIndex;
                removedEnd = widened.StartIndex + widened.Length;
                int seed = ApplyRemove(widened, ref workingDocument);
                ShiftListsAfter(context, widened, seed);
            }
        }

        /// <summary>Shifts three offset lists after a removed span: enclosing block loses
        /// <paramref name="seed"/> length; after block moves back by it; before untouched.
        /// Enclosing case is essential — omitting it caused offset drift on comment removal inside definition blocks.</summary>
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

        /// <summary>Pass 3: removes every definition/import block. When trimming is on, widens to
        /// whole line before removal; shift loops always compare against original boundaries.</summary>
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

        /// <summary>Pass 4: splices raw-output text over source spans, shifting chains right by length delta.</summary>
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

        /// <summary>Pass 6: removes one zero-output chain. When trimming is on, widens to whole
        /// line; shift loop predicates unchanged against original position.</summary>
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

        /// <summary>Branch classification shared by both backends; defined once here to prevent
        /// silent mismatches. Independent of <c>BranchRole</c> to keep this file Roslyn-free.</summary>
        internal enum BranchKind
        {
            Other,
            Opener,
            Continuation,
            Terminal,
            Participant
        }

        /// <summary>Event stream for the strip machine. Three events preserve diagnostic ordering:
        /// scope-channel check (OnClassified), gap collection (OnGapCollected), orphan machine (OnBlockCompleted).</summary>
        internal interface IBranchStripObserver
        {
            /// <summary>Raised for every chain after classification, before gap collection.</summary>
            void OnClassified(OutputChain chain, OutputItem leftmost, BranchKind kind);

            /// <summary>Raised for every collected gap, with the gap text.</summary>
            void OnGapCollected(OutputChain prev, OutputChain next, OutputItem nextLeftmost, BlockPosition gap,
                string gapText);

            /// <summary>Raised for every chain after gap collection and state advance.</summary>
            void OnBlockCompleted(OutputChain chain, OutputItem leftmost, BranchKind kind);
        }

        /// <summary>Pass 5: document-ordered state machine over output chains that swallows text
        /// between branch-set blocks. Opener arms; Continuation collects gap and re-arms; Terminal
        /// collects and disarms. Classification via <paramref name="classify"/>; gaps applied right-to-left.</summary>
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
                        // Non-branch block disarms stripping but preserves runtime frame for
                        // a following branch terminal to bind to the open set.
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

            // Right-to-left: keeps left-side gaps valid; shifts later chains after each removal.
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

        /// <summary>Byte-parity static-piece segmentation walk shared by runtime and emitter.
        /// Walks elements in document order, emitting literals between them. <paramref name="onElement"/>
        /// returns false to abandon walk (mid-walk degrade); method propagates this as false return.</summary>
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
