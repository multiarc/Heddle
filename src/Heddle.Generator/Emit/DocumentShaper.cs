using System.Collections.Generic;
using Heddle.Attributes;
using Heddle.Language;
using Heddle.Strings.Core;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// The emitter-side driver over the shared document-shaping core (<see cref="DocumentShaping"/>) — skipped-token
    /// alignment, remnant-line trimming, definition/import removal, raw-output splice, branch-set adjacency strip,
    /// and zero-output-chain removal — run against the parsed <see cref="ParseContext"/> so the emitter derives the
    /// exact same static-piece boundaries the runtime <c>RuntimeDocument</c> derives. The shared pass bodies live in
    /// <c>Heddle.Language.DocumentShaping</c>, linked into this project by the <c>..\Heddle\Language\**\*.cs</c> glob;
    /// what remains here is generator representation only — the <see cref="Element"/>/<see cref="Result"/> surface,
    /// the pass sequence, and the classification adapters. The HED300x branch-set diagnostics stay in the runtime
    /// compiler (it passes the shared machine an observer; the generator passes none), not in the generator.
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
            System.Func<OutputChain, bool> isZeroOutput, System.Func<string, bool> isDefinition = null,
            System.Func<string, BranchRole?> roleOf = null,
            System.Func<string, bool> hasScopeChannel = null)
        {
            var workingDocument = cleanDocument;

            // The normative pass order (DocumentShaping's header contract), driven identically on both tiers.
            DocumentShaping.ShiftBySkippedTokens(parseContext);
            if (trimDirectiveLines)
                DocumentShaping.TrimHiddenRemnantLines(parseContext, ref workingDocument);
            DocumentShaping.RemoveDefinitions(parseContext, ref workingDocument, trimDirectiveLines);
            DocumentShaping.ReplaceRawOutput(parseContext, ref workingDocument);
            DocumentShaping.StripBranchSets(parseContext, ref workingDocument,
                ClassifierFor(isDefinition ?? (_ => false), roleOf ?? (_ => null),
                    hasScopeChannel ?? (_ => false)));

            var elements = new List<Element>();
            foreach (var chain in parseContext.OutputChains)
            {
                var blockPosition = chain.BlockPosition;
                if (isZeroOutput(chain))
                    DocumentShaping.RemoveEmptyItem(parseContext, blockPosition, ref workingDocument,
                        trimDirectiveLines);
                else
                    elements.Add(new Element(chain, blockPosition));
            }

            foreach (var chain in parseContext.DefaultChains)
            {
                // The generator models an empty default chain as a zero-length element with an empty call chain at
                // document end, which renders nothing but participates in element-list identity and strategy selection.
                if (!isZeroOutput(chain))
                    elements.Add(new Element(chain, new BlockPosition(workingDocument.Length, 0)));
            }

            return new Result(workingDocument, elements);
        }

        /// <summary>Maps the generator's Roslyn-backed binder answers onto the shared
        /// <see cref="DocumentShaping.BranchKind"/> — the same rule <c>HeddleCompiler.Classify</c> applies against
        /// reflection: the R8 definition-first guard, then the branch role, then <c>[ScopeChannel]</c> as
        /// <c>Participant</c> (role wins over Participant, R10).</summary>
        private static System.Func<OutputChain, DocumentShaping.BranchKind> ClassifierFor(
            System.Func<string, bool> isDefinition, System.Func<string, BranchRole?> roleOf,
            System.Func<string, bool> hasScopeChannel)
            => chain =>
            {
                var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
                if (leftmost == null)
                    return DocumentShaping.BranchKind.Other;
                var name = leftmost.ExtensionName;
                if (isDefinition(name))
                    return DocumentShaping.BranchKind.Other;   // definition-first guard
                switch (roleOf(name))
                {
                    case BranchRole.Opener:       return DocumentShaping.BranchKind.Opener;
                    case BranchRole.Continuation: return DocumentShaping.BranchKind.Continuation;
                    case BranchRole.Terminal:     return DocumentShaping.BranchKind.Terminal;
                }

                return hasScopeChannel(name)
                    ? DocumentShaping.BranchKind.Participant   // byte-equal to Other today (both disarm)
                    : DocumentShaping.BranchKind.Other;
            };
    }
}
