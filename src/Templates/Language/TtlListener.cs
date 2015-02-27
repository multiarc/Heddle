using System.Collections.Generic;
using System.Linq;
using Templates.Strings.Core;

namespace Templates.Language {
    internal class TtlListener : TtlParserBaseListener
    {        
        private readonly Stack<ParseContext> _parserContextStack;

        public ParseContext CurrentParseContext => _parserContextStack.Peek();

        public TtlListener()
        {
            _parserContextStack = new Stack<ParseContext>();
            _parserContextStack.Push(new ParseContext());
        }

        public override void EnterDefinition(TtlParser.DefinitionContext context)
        {
            CurrentParseContext.InDefinition = true;
            CurrentParseContext.DefinitionBlock.AddNewBlockPosition(
                new BlockPosition(context.Start.StartIndex, context.Stop.StopIndex - context.Start.StartIndex + 1));
        }

        public override void ExitDefinition(TtlParser.DefinitionContext context)
        {
            CurrentParseContext.InDefinition = false;
        }

        public override void EnterDef(TtlParser.DefContext context)
        {
            CurrentParseContext.CurrentDefenition = CurrentParseContext.CreateDefinition(context);
        }

        public override void ExitDef(TtlParser.DefContext context)
        {
            if (!CurrentParseContext.DefinitionBlock.Definitions.ContainsKey(CurrentParseContext.CurrentDefenition.Name))
            {
                CurrentParseContext.DefinitionBlock.Definitions.Add(CurrentParseContext.CurrentDefenition.Name,
                    CurrentParseContext.CurrentDefenition);
            }
            else
            {
                CurrentParseContext.DefinitionBlock.Definitions[CurrentParseContext.CurrentDefenition.Name] =
                    CurrentParseContext.CurrentDefenition;
            }
            CurrentParseContext.CurrentDefenition = null;
        }

        public override void EnterOutblock(TtlParser.OutblockContext context)
        {
            CurrentParseContext.CurrentChain = CurrentParseContext.CreateOutputChain(context);
        }

        public override void ExitOutblock(TtlParser.OutblockContext context)
        {
            CurrentParseContext.OutputChains.Add(CurrentParseContext.CurrentChain);
            CurrentParseContext.CurrentChain = null;
        }

        public override void ExitTtl(TtlParser.TtlContext context)
        {
            CurrentParseContext.RawOutputItems.AddRange(CurrentParseContext.CreateRawOutputItems(context));
        }

        public override void EnterSubtemplate(TtlParser.SubtemplateContext context)
        {
            if (context.ttl()?.Stop.StopIndex - context.ttl()?.Start.StartIndex + 1 > 0)
            {
                _parserContextStack.Push(new ParseContext(CurrentParseContext,
                    context.ttl().Start.StartIndex));
            }
        }

        public override void ExitSubtemplate(TtlParser.SubtemplateContext context)
        {
            if (context.ttl()?.Stop.StopIndex - context.ttl()?.Start.StartIndex + 1 > 0)
            {
                var parserContext = _parserContextStack.Pop();
                if (CurrentParseContext.InDefinition)
                {
                    CurrentParseContext.CurrentDefenition.Context = parserContext;
                    CurrentParseContext.CurrentDefenition.OutList.AddRange(parserContext.OutputChains);
                }
                else
                {
                    CurrentParseContext.CurrentChain.Context = parserContext;
                    CurrentParseContext.CurrentChain.Chain.First().OutList.AddRange(parserContext.OutputChains);
                }
            }
        }
    }
}