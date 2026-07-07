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
            if (CurrentParseContext.CurrentDefenition.BaseDefinition == null)
            {
                if (CurrentParseContext.DefinitionsBlock.Definitions.ContainsKey(CurrentParseContext.CurrentDefenition.Name))
                {
                    CurrentParseContext.Errors.Add(
                        $"The definition <{CurrentParseContext.CurrentDefenition.Name}> with the same name already exists".ToError(CurrentParseContext.GetBlockPosition(context)));
                    return;
                }
                CurrentParseContext.DefinitionsBlock.Definitions.Add(CurrentParseContext.CurrentDefenition.Name,
                    CurrentParseContext.CurrentDefenition);
            }
            if (chain != null)
            {
                CurrentParseContext.DefaultChains.Add(chain);
            }
        }

        public override void ExitDef(HeddleParser.DefContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (CurrentParseContext.CurrentDefenition.BaseDefinition != null)
            {
                if (!CurrentParseContext.DefinitionsBlock.Definitions.ContainsKey(CurrentParseContext.CurrentDefenition.Name))
                {
                    CurrentParseContext.DefinitionsBlock.Definitions.Add(CurrentParseContext.CurrentDefenition.Name,
                        CurrentParseContext.CurrentDefenition);
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
                }
                else
                {
                    var chainItem = CurrentParseContext.CurrentChain.Chain.First();
                    chainItem.Context = parserContext;
                }
            }
        }

        public override void ExitImport_block(HeddleParser.Import_blockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
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
