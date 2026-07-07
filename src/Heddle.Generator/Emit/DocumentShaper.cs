using System.Collections.Generic;
using System.Linq;
using Heddle.Language;
using Heddle.Strings.Core;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// The emitter-side reimplementation (phase 7 D22, WI6a) of the back-end document-shaping machines that live in
    /// <c>HeddleCompiler.CompileBody</c> — skipped-token alignment, remnant-line trimming, definition/import removal,
    /// raw-output splice, and zero-output-chain removal — reproduced against the parsed <see cref="ParseContext"/>
    /// so the emitter derives the exact same static-piece boundaries the runtime <c>RuntimeDocument</c> derives.
    /// The strip/orphan branch machine (<c>ProcessBranchSets</c>) is layered in by <see cref="BranchSetScanner"/>.
    /// </summary>
    internal static class DocumentShaper
    {
        /// <summary>An output chain that survives shaping and renders, with its final working-document position.</summary>
        internal sealed class Element
        {
            public Element(OutputChain chain, BlockPosition position)
            {
                Chain = chain;
                Position = position;
            }

            public OutputChain Chain { get; }
            public BlockPosition Position { get; }
        }

        internal sealed class Result
        {
            public Result(string workingDocument, List<Element> elements)
            {
                WorkingDocument = workingDocument;
                Elements = elements;
            }

            public string WorkingDocument { get; }
            public List<Element> Elements { get; }
        }

        /// <param name="isZeroOutput">Classifies a rendering chain (false) versus a zero-output chain the emitter
        /// removes and widens (true) — the directive/empty-chain equivalent of the runtime's
        /// <c>returnTypeChainedPrevious == null</c> outcome.</param>
        /// <param name="isDefinition">Names that resolve to a definition in this scope — such a leftmost call is
        /// never a branch keyword (matches <c>HeddleCompiler.Classify</c>'s definition-shadowing check).</param>
        public static Result Shape(string cleanDocument, ParseContext parseContext, bool trimDirectiveLines,
            System.Func<OutputChain, bool> isZeroOutput, System.Func<string, bool> isDefinition = null)
        {
            var workingDocument = cleanDocument;

            ShiftBySkippedTokens(parseContext);
            if (trimDirectiveLines)
                TrimHiddenRemnantLines(parseContext, ref workingDocument);
            RemoveDefinitions(parseContext, ref workingDocument, trimDirectiveLines);
            ReplaceRawOutput(parseContext, ref workingDocument);
            StripBranchSets(parseContext, ref workingDocument, isDefinition ?? (_ => false));

            var elements = new List<Element>();
            foreach (var chain in parseContext.OutputChains)
            {
                var blockPosition = chain.BlockPosition;
                if (isZeroOutput(chain))
                    RemoveEmptyItem(parseContext, blockPosition, ref workingDocument, trimDirectiveLines);
                else
                    elements.Add(new Element(chain, blockPosition));
            }

            foreach (var chain in parseContext.DefaultChains)
            {
                if (chain.Chain == null || chain.Chain.Count == 0)
                    continue;
                if (!isZeroOutput(chain))
                    elements.Add(new Element(chain, new BlockPosition(workingDocument.Length, 0)));
            }

            return new Result(workingDocument, elements);
        }

        // ---- Branch strip machine (phase 3 D10 / D22, WI6a) — the byte-affecting half of
        // HeddleCompiler.ProcessBranchSets: adjacency stripping of whitespace between the blocks of one branch set.
        // The HED3001–HED3004 diagnostics are re-emitted by the branch-diagnostics pass, not here.

        private enum BranchKind { Other, Opener, Continuation, Terminal }

        private static BranchKind ClassifyBranch(OutputChain chain, System.Func<string, bool> isDefinition)
        {
            var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
            if (leftmost == null)
                return BranchKind.Other;
            var name = leftmost.ExtensionName;
            if (isDefinition(name))
                return BranchKind.Other;
            switch (name)
            {
                case "if":
                case "ifnot":
                    return BranchKind.Opener;
                case "elif":
                case "elseif":
                    return BranchKind.Continuation;
                case "else":
                    return BranchKind.Terminal;
            }

            return BranchKind.Other;
        }

        private static void StripBranchSets(ParseContext parseContext, ref string workingDocument,
            System.Func<string, bool> isDefinition)
        {
            var chains = parseContext.OutputChains;
            if (chains == null || chains.Count == 0)
                return;

            OutputChain stripPrev = null;
            List<BlockPosition> gaps = null;

            foreach (var chain in chains)
            {
                var kind = ClassifyBranch(chain, isDefinition);
                switch (kind)
                {
                    case BranchKind.Opener:
                        stripPrev = chain;
                        break;
                    case BranchKind.Continuation:
                        if (stripPrev != null)
                            CollectGap(stripPrev, chain, workingDocument, ref gaps);
                        stripPrev = chain;
                        break;
                    case BranchKind.Terminal:
                        if (stripPrev != null)
                            CollectGap(stripPrev, chain, workingDocument, ref gaps);
                        stripPrev = null;
                        break;
                    default:
                        stripPrev = null;
                        break;
                }
            }

            ApplyGaps(parseContext, gaps, ref workingDocument);
        }

        private static void CollectGap(OutputChain prev, OutputChain next, string workingDocument,
            ref List<BlockPosition> gaps)
        {
            int gapStart = prev.BlockPosition.StartIndex + prev.BlockPosition.Length;
            int gapLength = next.BlockPosition.StartIndex - gapStart;
            if (gapLength <= 0)
                return;
            if (gapStart < 0 || gapStart + gapLength > workingDocument.Length)
                return;

            gaps ??= new List<BlockPosition>();
            gaps.Add(new BlockPosition(gapStart, gapLength));
        }

        private static void ApplyGaps(ParseContext parseContext, List<BlockPosition> gaps, ref string workingDocument)
        {
            if (gaps == null || gaps.Count == 0)
                return;

            for (int i = gaps.Count - 1; i >= 0; i--)
            {
                var gap = gaps[i];
                int seed = ApplyRemove(gap, ref workingDocument);
                foreach (var chain in parseContext.OutputChains)
                {
                    if (chain.BlockPosition.StartIndex > gap.StartIndex)
                        chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                            chain.BlockPosition.Length);
                }
            }
        }

        // ---- Ported verbatim from HeddleCompiler (type-free subset). ----

        private static int ApplyRemove(BlockPosition element, ref string source)
        {
            source = source.Remove(element.StartIndex, element.Length);
            return element.Length;
        }

        private static string ReplaceSpan(int start, int length, string replacement, string source)
            => source.Substring(0, start) + replacement + source.Substring(start + length);

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
                        chain.BlockPosition = new BlockPosition(chainBlockStart, chain.BlockPosition.Length - seed);
                    else if (chainBlockEnd > startToSkip)
                        chain.BlockPosition = new BlockPosition(chainBlockStart - seed, chain.BlockPosition.Length);
                    else
                        break;
                }

                for (int index = context.DefinitionsBlock.Positions.Count - 1; index >= 0; index--)
                {
                    var position = context.DefinitionsBlock.Positions[index];
                    var definitionBlockStart = position.StartIndex;
                    var definitionBlockEnd = position.StartIndex + position.Length - 1;

                    if (definitionBlockStart <= startToSkip && definitionBlockEnd >= endToSkip)
                        context.DefinitionsBlock.Positions[index] =
                            new BlockPosition(definitionBlockStart, position.Length - seed);
                    else if (definitionBlockEnd > startToSkip)
                        context.DefinitionsBlock.Positions[index] =
                            new BlockPosition(definitionBlockStart - seed, position.Length);
                    else
                        break;
                }

                foreach (var raw in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
                {
                    var rawBlockEnd = raw.BlockPosition.StartIndex + raw.BlockPosition.Length - 1;
                    if (rawBlockEnd > blockPosition.StartIndex)
                        raw.BlockPosition = new BlockPosition(raw.BlockPosition.StartIndex - seed, raw.BlockPosition.Length);
                    else
                        break;
                }
            }
        }

        private static void RemoveDefinitions(ParseContext context, ref string workingDocument,
            bool trimDirectiveLines)
        {
            foreach (var definitionBlock in ((ICollection<BlockPosition>) context.DefinitionsBlock.Positions).Reverse())
            {
                var removal = trimDirectiveLines ? WidenToWholeLine(definitionBlock, workingDocument) : definitionBlock;
                int seed = ApplyRemove(removal, ref workingDocument);
                foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
                {
                    if (chain.BlockPosition.StartIndex >= definitionBlock.StartIndex + definitionBlock.Length)
                        chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                            chain.BlockPosition.Length);
                    else
                        break;
                }

                foreach (var raw in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
                {
                    if (raw.BlockPosition.StartIndex >= definitionBlock.StartIndex + definitionBlock.Length)
                        raw.BlockPosition = new BlockPosition(raw.BlockPosition.StartIndex - seed, raw.BlockPosition.Length);
                    else
                        break;
                }
            }
        }

        private static void ReplaceRawOutput(ParseContext context, ref string workingDocument)
        {
            foreach (var rawOut in ((ICollection<RawOutputItem>) context.RawOutputItems).Reverse())
            {
                workingDocument = ReplaceSpan(rawOut.BlockPosition.StartIndex, rawOut.BlockPosition.Length,
                    rawOut.Text, workingDocument);
                int seed = rawOut.BlockPosition.Length - rawOut.Text.Length;
                var outputItem = rawOut;
                foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
                {
                    if (chain.BlockPosition.StartIndex > outputItem.BlockPosition.StartIndex)
                        chain.BlockPosition = new BlockPosition(chain.BlockPosition.StartIndex - seed,
                            chain.BlockPosition.Length);
                    else
                        break;
                }
            }
        }

        private static void RemoveEmptyItem(ParseContext context, BlockPosition blockPosition,
            ref string workingDocument, bool trimDirectiveLines)
        {
            var removal = trimDirectiveLines ? WidenToWholeLine(blockPosition, workingDocument) : blockPosition;
            int seed = ApplyRemove(removal, ref workingDocument);
            foreach (var chain in ((ICollection<OutputChain>) context.OutputChains).Reverse())
            {
                if (chain.BlockPosition.StartIndex > blockPosition.StartIndex)
                    chain.BlockPosition =
                        new BlockPosition(chain.BlockPosition.StartIndex - seed, chain.BlockPosition.Length);
                else
                    break;
            }
        }

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
                    continue;

                var widened = WidenToWholeLine(new BlockPosition(start, 0), workingDocument);
                if (widened.Length == 0)
                    continue;

                removedStart = widened.StartIndex;
                removedEnd = widened.StartIndex + widened.Length;
                int seed = ApplyRemove(widened, ref workingDocument);
                ShiftListsAfter(context, widened, seed);
            }
        }

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

        internal static BlockPosition WidenToWholeLine(BlockPosition block, string document)
        {
            int left = block.StartIndex;
            while (left > 0 && (document[left - 1] == ' ' || document[left - 1] == '\t'))
                left--;
            if (left != 0 && document[left - 1] != '\n' && document[left - 1] != '\r')
                return block;

            int right = block.StartIndex + block.Length;
            while (right < document.Length && (document[right] == ' ' || document[right] == '\t'))
                right++;
            if (right == document.Length)
                return new BlockPosition(left, right - left);
            if (document[right] == '\r')
            {
                right += right + 1 < document.Length && document[right + 1] == '\n' ? 2 : 1;
                return new BlockPosition(left, right - left);
            }
            if (document[right] == '\n')
                return new BlockPosition(left, right + 1 - left);
            return block;
        }
    }
}
