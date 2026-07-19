using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Strings.Core;

namespace Heddle.Language {
    internal sealed class HeddleMainListener: HeddleParserBaseListener {
        private readonly ParserSettings _settings;
        private readonly Stack<ParseContext> _parserContextStack;

        public ParseContext CurrentParseContext => _parserContextStack.Peek();

        public HeddleMainListener(ParseContext context, ParserSettings settings) {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (context == null) throw new ArgumentNullException(nameof(context));
            _parserContextStack = new Stack<ParseContext>();
            _parserContextStack.Push(context);
        }

        public override void EnterDefinition(HeddleParser.DefinitionContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.InDefinition = true;
            CurrentParseContext.DefinitionsBlock.AddNewBlockPosition(CurrentParseContext.GetBlockPosition(context));
            if (CurrentParseContext.ProvideLanguageFeatures)
            {
                CurrentParseContext.AddToken(context.DEF_START(), HeddleTokenType.DefStart);
            }
        }

        public override void ExitDefinition(HeddleParser.DefinitionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.InDefinition = false;
            if (CurrentParseContext.ProvideLanguageFeatures)
            {
                CurrentParseContext.AddToken(context.DEF_CLOSE(), HeddleTokenType.DefClose);
            }
        }

        public override void EnterDef(HeddleParser.DefContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.CurrentDefenition = CurrentParseContext.CreateDefinition(context, out var chain);
            if (CurrentParseContext.CurrentDefenition == null)
            {
                CurrentParseContext.Errors.Add(
                    "Cannot create definition".ToError(CurrentParseContext.GetBlockPosition(context)));
                return;
            }
            // Phase 7 D5: a region-fill candidate (<x:x> with an unresolved base) is captured at parse and never
            // registered — it must not self-shadow the region default a self-call resolves to, and its error is
            // already emitted (emit-then-retract). CurrentDefenition stays non-null so ExitSubtemplate can attach
            // the override body's context to the candidate item.
            if (CurrentParseContext.CurrentDefenition.IsFillCandidate)
            {
                return;
            }
            if (CurrentParseContext.CurrentDefenition.BaseDefinition == null)
            {
                if (CurrentParseContext.DefinitionsBlock.Definitions.ContainsKey(CurrentParseContext.CurrentDefenition.Name))
                {
                    // Phase 7 D10 (HED5020): upgrade the id-less duplicate error only when BOTH the stored entry
                    // and the incoming declaration are public regions. A public region colliding with a private or
                    // document-scope <name> keeps the id-less message (F6).
                    var stored = CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name];
                    if (stored.IsPublicRegion && CurrentParseContext.CurrentDefenition.IsPublicRegion)
                    {
                        CurrentParseContext.Errors.Add(
                            $"Definition '{EnclosingDefinitionName()}' declares more than one public region named '{CurrentParseContext.CurrentDefenition.Name}'."
                                .ToError(CurrentParseContext.GetBlockPosition(context),
                                    HeddleDiagnosticIds.DuplicateRegionDeclaration));
                        return;
                    }
                    CurrentParseContext.Errors.Add(
                        $"The definition <{CurrentParseContext.CurrentDefenition.Name}> with the same name already exists".ToError(CurrentParseContext.GetBlockPosition(context)));
                    return;
                }
                CurrentParseContext.DefinitionsBlock.Definitions.Add(CurrentParseContext.CurrentDefenition.Name,
                    CurrentParseContext.CurrentDefenition);
                RecordRegionDeclaration(CurrentParseContext.CurrentDefenition);
            }
            if (chain != null)
            {
                CurrentParseContext.DefaultChains.Add(chain);
            }
        }

        /// <summary>Phase 7 D3 (append ordering): records a region declaration into the declaring context's
        /// <see cref="ParseContext.DeclaredRegions"/> on the store-success path only, so a rejected duplicate
        /// never lands in the enclosing component's <see cref="DefinitionItem.Regions"/>.</summary>
        private void RecordRegionDeclaration(DefinitionItem definition)
        {
            if (!definition.IsRegion)
                return;
            CurrentParseContext.DeclaredRegions.Add(new RegionDeclaration(
                definition.Name, definition.ModelType, definition.IsPublicRegion, definition.Position));
        }

        /// <summary>The name of the enclosing definition being parsed (the component a region belongs to) —
        /// the nearest lower stack context with an in-flight definition; <c>"?"</c> when none.</summary>
        private string EnclosingDefinitionName()
        {
            bool first = true;
            foreach (var context in _parserContextStack)
            {
                if (first) { first = false; continue; }
                if (context.CurrentDefenition != null)
                    return context.CurrentDefenition.Name;
            }

            return "?";
        }

        public override void ExitDef(HeddleParser.DefContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (CurrentParseContext.CurrentDefenition.BaseDefinition != null)
            {
                if (!CurrentParseContext.DefinitionsBlock.Definitions.ContainsKey(CurrentParseContext.CurrentDefenition.Name))
                {
                    CurrentParseContext.DefinitionsBlock.Definitions.Add(CurrentParseContext.CurrentDefenition.Name,
                        CurrentParseContext.CurrentDefenition);
                    RecordRegionDeclaration(CurrentParseContext.CurrentDefenition);
                }
                else
                {
                    var definition =
                        CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name];
                    if (CurrentParseContext.InDefintionContext)
                    {
                        if (definition != CurrentParseContext.CurrentDefenition.BaseDefinition)
                        {
                            CurrentParseContext.Errors.Add(
                                $"The definition <{CurrentParseContext.CurrentDefenition.Name}> with the same name already exists".ToError(
                                    CurrentParseContext.GetBlockPosition(context)));
                            return;
                        }
                        // Phase 7 (WI2): a within-component <region:region> replace preserves region-ness — the
                        // replaced entry's visibility carries onto the replacing layer (the model type already
                        // inherits via CreateDefinition's `modelType ?? baseDefenition?.ModelType`).
                        if (definition.IsRegion)
                        {
                            CurrentParseContext.CurrentDefenition.IsRegion = true;
                            CurrentParseContext.CurrentDefenition.IsPublicRegion = definition.IsPublicRegion;
                        }
                        CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name] =
                            CurrentParseContext.CurrentDefenition;
                    }
                    else
                    {
                        if (definition != CurrentParseContext.CurrentDefenition.BaseDefinition)
                        {
                            CurrentParseContext.Errors.Add(
                                $"The definition <{CurrentParseContext.CurrentDefenition.Name}> with the same name already exists".ToError(
                                    CurrentParseContext.GetBlockPosition(context)));
                            return;
                        }
                        CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name].OverrideWith(
                            CurrentParseContext.CurrentDefenition);
                    }
                }
            }
            CurrentParseContext.CurrentDefenition = null;
        }

        public override void EnterOutblock(HeddleParser.OutblockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.CurrentChain = CurrentParseContext.CreateOutputChain(context);
        }

        public override void ExitOutblock(HeddleParser.OutblockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.OutputChains.Add(CurrentParseContext.CurrentChain);
            CurrentParseContext.CurrentChain = null;
        }

        public override void EnterRaw(HeddleParser.RawContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.RawOutputItems.Add(CurrentParseContext.CreateRawOutputItem(context));
        }

        public override void EnterSubtemplate(HeddleParser.SubtemplateContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var heddleCtx = context.heddle();
            if (heddleCtx?.Stop == null || heddleCtx.Start == null)
                throw new ArgumentException();
            if (heddleCtx.Stop.StopIndex - heddleCtx.Start.StartIndex + 1 > 0)
            {
                var currentContext = CurrentParseContext;
                var newContext = new ParseContext(currentContext, heddleCtx.Start.StartIndex);

                var currentOffset = currentContext.Offset;

                //transfer parent skipped tokens inside with offset
                newContext.SkippedTokens.AddRange(currentContext.SkippedTokens
                    .Where(t =>
                    {
                        var startIndex = t.StartIndex + currentOffset;
                        var stopIndex = startIndex + t.Length - 1;

                        return startIndex >= heddleCtx.Start.StartIndex && stopIndex <= heddleCtx.Stop.StopIndex;
                    })
                    .Select(t => new BlockPosition(t.StartIndex + currentOffset - heddleCtx.Start.StartIndex, t.Length)));

                currentContext.AddSubContext(newContext);
                if (!currentContext.InDefintionContext && !currentContext.InDefinition)
                {
                    newContext = newContext.IsolateContextWithTree();
                }
                else {
                    if (currentContext.InDefinition && currentContext.CurrentDefenition != null &&
                        currentContext.CurrentDefenition.FullOverride) {
                        newContext = newContext.IsolateContextWithTree(currentContext.CurrentDefenition.Name);
                    }
                }
                _parserContextStack.Push(newContext);
            }
        }

        public override void ExitSubtemplate(HeddleParser.SubtemplateContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var heddleCtx = context.heddle();
            if (heddleCtx?.Stop == null || heddleCtx.Start == null)
                throw new ArgumentException();
            if (heddleCtx.Stop.StopIndex - heddleCtx.Start.StartIndex + 1 > 0)
            {
                if (_parserContextStack.Count <= 1)
                {
                    throw new TemplateParseException("Subtemplate close block appears too early.".ToError(CurrentParseContext.GetAbsoluteBlockPosition(context)));
                }
                var parserContext = _parserContextStack.Pop();
                if (CurrentParseContext.InDefinition)
                {
                    CurrentParseContext.CurrentDefenition.Context = parserContext;
                    // Phase 7 D3: transfer the body's directly-declared regions (store-success entries only,
                    // declaration order) onto the enclosing component.
                    if (parserContext.DeclaredRegions.Count != 0)
                        CurrentParseContext.CurrentDefenition.Regions = parserContext.DeclaredRegions;
                }
                else
                {
                    var chainItem = CurrentParseContext.CurrentChain.Chain.First();
                    chainItem.Context = parserContext;
                }
            }
        }

        public override void ExitExtension_id(HeddleParser.Extension_idContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.GetText() != "import")
                return;

            // '@import' is removed. Raise the positioned HED4003 removal error at the shared parse layer so the
            // dynamic and precompiled tiers carry the identical diagnostic for every call shape (top-level,
            // chained/consuming, and nested in any subtemplate) — the ParseContext.Errors list is shared by
            // reference down the whole context tree, exactly as the @<< HED4004 detection relies on.
            var call = context.Parent as HeddleParser.CallContext;
            if (call == null)
                return;

            CurrentParseContext.Errors.Add(
                ("'@import' has been removed. Use '@<<{{ path }}' to share definitions and layouts across " +
                 "files, or '@partial(){{ name }}' to embed another template's rendered output inline. " +
                 "See docs/language-reference.md#imports--.")
                .ToError(CurrentParseContext.GetAbsoluteBlockPosition(call),
                    HeddleDiagnosticIds.LegacyImportDirective));
        }

        public override void ExitImport_block(HeddleParser.Import_blockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // A @<< composition import is only well-defined at document scope: it merges the imported file's
            // definitions into the current document and re-bases the imported file's own output chains to the
            // import position. It re-parses the imported file in its own 0-based coordinate space, so every
            // ParseContext.GetBlockPosition in that parse subtracts the *current* context's offset; when that
            // offset is non-zero the result goes negative and BlockPosition's ctor throws a raw ArgumentException.
            // The load-bearing hazard is therefore CurrentParseContext.Offset != 0, not stack depth:
            //   - in-file nesting (@if/@for body, output block, definition body) pushes a sub-context whose offset
            //     is the body's document position (> 0) — caught by the offset test, and by Count > 1.
            // Chained @<< composition re-parses into a context whose offset is inherited from its parent (0 at the
            // document root, staying 0 all the way down the @<< chain), so a legitimate chained compose has
            // Offset == 0 and is left to compose. Collect a positioned diagnostic and skip the import rather than
            // letting the offset defect throw out of the compile. The Count > 1 disjunct is kept as a belt-and-
            // suspenders guard for any nested scope that might present a zero offset.
            if (CurrentParseContext.Offset != 0 || _parserContextStack.Count > 1)
            {
                CurrentParseContext.Errors.Add(
                    ("A '@<<' composition import must appear at the top level of a document; it cannot be nested " +
                     "inside a subtemplate such as an @if/@for body, an output block, or a definition body. Move the " +
                     "import to the top level.")
                    .ToError(CurrentParseContext.GetAbsoluteBlockPosition(context),
                        HeddleDiagnosticIds.ComposeImportNotTopLevel));
                return;
            }

            var path = string.Concat(context.text().Select(t => t.GetText()));
            if (!string.IsNullOrWhiteSpace(path))
            {
                string document = _settings.ReadImport(path);

                // Phase 6 D25 (stamp site 1): mark the imported parse's diagnostics with a shared ImportOrigin so
                // the LSP facade re-anchors them to this @<< site. Flag-gated — production compiles take one bool
                // check and allocate nothing. Post-D4 seam: all front-end diagnostics live on the ParseContext,
                // so the two ParseContext ranges are the only ones stamped (the runtime adapter copies them into
                // the compile context after the whole parse completes).
                bool markProvenance = CurrentParseContext.ProvideLanguageFeatures;
                ImportOrigin origin = null;
                int peMark = 0, pwMark = 0;
                if (markProvenance)
                {
                    origin = new ImportOrigin(_settings.ResolveImportPath(path), CurrentParseContext.GetAbsoluteBlockPosition(context));
                    peMark = CurrentParseContext.Errors.Count;
                    pwMark = CurrentParseContext.Warnings.Count;
                }

                var isolatedContext = CurrentParseContext.IsolateContextWithTree();
                var preImportNames = markProvenance
                    ? new HashSet<string>(isolatedContext.DefinitionsBlock.Definitions.Keys)
                    : null;
                if (markProvenance)
                    isolatedContext.ImportOrigin = origin;
                isolatedContext.OutputChains.Clear();
                // The imported document is parsed in its own coordinate space. The importing document's hidden-token
                // positions (inherited here by IsolateContextWithTree) are in the importing file's coordinates and
                // must not seed the import parse: EnterSubtemplate transfers a context's SkippedTokens into each
                // definition-body sub-context by filtering on the body's span, and an importing-file token whose
                // offset happens to fall inside an imported body's span would be injected into that body, shifting
                // its output chains and corrupting the render. Clear them so only the imported file's own hidden
                // tokens (recorded fresh by the parse below) reach the imported bodies — symmetric to the
                // OutputChains reset above.
                isolatedContext.SkippedTokens.Clear();
                DocumentParser.Parse(document, isolatedContext, _settings/*, true*/);
                CurrentParseContext.DefaultChains.Clear();
                CurrentParseContext.DefaultChains.AddRange(isolatedContext.DefaultChains);
                CurrentParseContext.DefinitionsBlock.Definitions.Clear();
                foreach (var definition in isolatedContext.DefinitionsBlock.Definitions)
                {
                    CurrentParseContext.DefinitionsBlock.Definitions.Add(definition.Key, definition.Value);
                }
                foreach (var isolatedChain in isolatedContext.OutputChains)
                {
                    isolatedChain.BlockPosition = new BlockPosition(CurrentParseContext.GetBlockPosition(context).StartIndex, 0);
                    CurrentParseContext.OutputChains.Add(isolatedChain);
                }

                if (markProvenance)
                {
                    StampImportedRange(CurrentParseContext.Errors, peMark, origin);
                    StampImportedRange(CurrentParseContext.Warnings, pwMark, origin);
                    // Attach the origin to purely-imported definitions so their call-site-compiled bodies
                    // re-anchor via the D2/D25 funnel bracket (stamp site 4). Definitions copied from the
                    // pre-import local set keep their own (null) provenance.
                    foreach (var pair in CurrentParseContext.DefinitionsBlock.Definitions)
                    {
                        if (!preImportNames.Contains(pair.Key))
                            AttachImportOrigin(pair.Value, origin);
                    }
                }
            }
            CurrentParseContext.DefinitionsBlock.AddNewBlockPosition(CurrentParseContext.GetBlockPosition(context));
        }

        /// <summary>
        /// Phase 6 D25: stamps every entry appended after <paramref name="mark"/> with <paramref name="origin"/>.
        /// A null-marker entry (a diagnostic of the imported document itself) takes the shared instance; an entry
        /// already carrying a marker (a nested import's) has only its <see cref="ImportOrigin.Site"/> re-anchored
        /// to this site — the shared instance keeps the deepest <see cref="ImportOrigin.Path"/>.
        /// </summary>
        private static void StampImportedRange<T>(List<T> list, int mark, ImportOrigin origin)
            where T : HeddleCompileError
        {
            for (int i = mark; i < list.Count; i++)
            {
                var existing = list[i].ImportOrigin;
                if (existing == null)
                    list[i].ImportOrigin = origin;
                else if (!ReferenceEquals(existing, origin))
                    existing.Site = origin.Site;
            }
        }

        /// <summary>Phase 6 D25: attaches an import origin to a purely-imported definition's context lineage so
        /// its call-site-compiled body re-anchors (idempotent; recurses base layers).</summary>
        private static void AttachImportOrigin(DefinitionItem definition, ImportOrigin origin)
        {
            for (var d = definition; d != null; d = d.BaseDefinition)
            {
                if (d.Context != null && d.Context.ImportOrigin == null)
                    d.Context.ImportOrigin = origin;
            }
        }
    }
}
