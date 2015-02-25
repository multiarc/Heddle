using System.Collections.Generic;
using System.Linq;
using Templates.Strings.Core;

namespace Templates.Language {
    internal class TtlListener : TtlBaseListener
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
                new BlockPosition(context.DefineStart().Symbol.StartIndex, context.GetText().Length));
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

        public override void ExitRawoutput(TtlParser.RawoutputContext context)
        {
            CurrentParseContext.RawOutputItems.Add(CurrentParseContext.CreateRawOutputItem(context));
        }

        public override void EnterSubtemplate(TtlParser.SubtemplateContext context)
        {
            if (context.ttl()?.GetText()?.Length > 0)
            {
                _parserContextStack.Push(new ParseContext(CurrentParseContext,
                    context.TemplateStart().Symbol.StartIndex + 1));
            }
        }

        public override void ExitSubtemplate(TtlParser.SubtemplateContext context)
        {
            if (context.ttl()?.GetText()?.Length > 0)
            {
                var parserContext = _parserContextStack.Pop();
                if (CurrentParseContext.InDefinition)
                {
                    CurrentParseContext.CurrentDefenition.Context = parserContext;
                    CurrentParseContext.CurrentDefenition.OutList.AddRange(parserContext.OutputChains);
                }
                else
                {
                    CurrentParseContext.CurrentChain.Chain.Last().OutList.AddRange(parserContext.OutputChains);
                }
            }
        }
    }
}