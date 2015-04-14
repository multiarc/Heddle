using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Antlr4.Runtime.Tree;
using Templates.Data;
using Templates.Exceptions;
using Templates.Strings.Core;

namespace Templates.Language {
    internal sealed class TtlMainListener: TtlParserBaseListener {
        private readonly Stack<ParseContext> _parserContextStack;

        public ParseContext CurrentParseContext => _parserContextStack.Peek();

        public TtlMainListener(ParseContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            _parserContextStack = new Stack<ParseContext>();
            _parserContextStack.Push(context);
        }

        public override void EnterDefinition(TtlParser.DefinitionContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.InDefinition = true;
            CurrentParseContext.DefinitionsBlock.AddNewBlockPosition(CurrentParseContext.GetBlockPosition(context));
        }

        public override void ExitDefinition(TtlParser.DefinitionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.InDefinition = false;
        }

        public override void EnterDef(TtlParser.DefContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            OutputChain chain;
            CurrentParseContext.CurrentDefenition = CurrentParseContext.CreateDefinition(context, out chain);
            if (chain != null)
            {
                CurrentParseContext.DefaultChains.Add(chain);
            }
        }

        public override void ExitDef(TtlParser.DefContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!CurrentParseContext.DefinitionsBlock.Definitions.ContainsKey(CurrentParseContext.CurrentDefenition.Name)) {
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
                        throw new TemplateParseException("The definition with the same name already exists".ToError(CurrentParseContext.GetBlockPosition(context)));
                    CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name] = CurrentParseContext.CurrentDefenition;
                }
                else
                {
                    if (definition != CurrentParseContext.CurrentDefenition.BaseDefinition)
                        throw new TemplateParseException("The definition with the same name already exists".ToError(CurrentParseContext.GetBlockPosition(context)));
                    CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name].OverrideWith(CurrentParseContext.CurrentDefenition);
                }
            }
            CurrentParseContext.CurrentDefenition = null;
        }

        public override void EnterOutblock(TtlParser.OutblockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!CurrentParseContext.DefenitionsOnly)
                CurrentParseContext.CurrentChain = CurrentParseContext.CreateOutputChain(context);
        }

        public override void ExitOutblock(TtlParser.OutblockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!CurrentParseContext.DefenitionsOnly) {
                CurrentParseContext.OutputChains.Add(CurrentParseContext.CurrentChain);
                CurrentParseContext.CurrentChain = null;
            }
        }

        public override void EnterRaw(TtlParser.RawContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.RawOutputItems.Add(CurrentParseContext.CreateRawOutputItem(context));
        }

        public override void EnterComment(TtlParser.CommentContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.CommentTokens.Add(CurrentParseContext.GetBlockPosition(context));
        }

        public override void EnterSubtemplate(TtlParser.SubtemplateContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var ttl = context.ttl();
            if (ttl?.Stop == null || ttl.Start == null)
                throw new ArgumentException();
            if (ttl.Stop?.StopIndex - ttl.Start?.StartIndex + 1 > 0)
            {
                var currentContext = CurrentParseContext;
                var newContext = new ParseContext(currentContext, ttl.Start.StartIndex);
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

        public override void ExitSubtemplate(TtlParser.SubtemplateContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var ttl = context.ttl();
            if (ttl?.Stop == null || ttl.Start == null)
                throw new ArgumentException();
            if (ttl.Stop.StopIndex - ttl.Start.StartIndex + 1 > 0)
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
                    if (!CurrentParseContext.DefenitionsOnly)
                    {
                        var chainItem = CurrentParseContext.CurrentChain.Chain.First();
                        chainItem.Context = parserContext;
                    }
                }
            }
        }

        public override void ExitTtl(TtlParser.TtlContext context)
        {
            if (CurrentParseContext.DefaultChains.Length > 0)
            {
                foreach (var chain in CurrentParseContext.DefaultChains)
                {
                    chain.BlockPosition = new BlockPosition(context.Stop.StopIndex + 1, 0);
                }
                CurrentParseContext.OutputChains.AddRange(CurrentParseContext.DefaultChains);
                CurrentParseContext.DefaultChains.Clear();
            }
        }
    }
}