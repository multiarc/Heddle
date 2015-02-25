using System.Collections.Generic;
using System.Linq;
using Templates.Collections;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Language {
    public class ParseContext {
        private readonly int _offset;

        internal ParseContext()
        {
            _offset = 0;
            DefinitionBlock = new DefinitionBlock();
        }

        internal ParseContext(ParseContext previous = null, int offset = 0)
        {
            _offset = offset;
            DefinitionBlock = new DefinitionBlock(previous?.DefinitionBlock);
            OutputChains = new SmartList<OutputChain>();
            RawOutputItems = new SmartList<RawOutputItem>();
        }

        internal DefinitionItem CreateDefinition(TtlParser.DefContext context)
        {
            return new DefinitionItem(
                context.name().ID().GetText(), context.subtemplate().GetText(),
                GetDefenition(context.name().BaseID()?.GetText()),
                modelType: context.typedefenition()?.ID().GetText())
            {
                Position = new BlockPosition(context.DefineNameStart().Symbol.StartIndex, context.GetText().Length)
            };
        }

        public DefinitionItem GetDefenition(string baseName)
        {
            if (baseName.IsNullOrEmpty())
                return null;
            return DefinitionBlock.Definitions[baseName];
        }

        internal OutputChain CreateOutputChain(TtlParser.OutblockContext context)
        {
            var result = new OutputChain(this)
            {
                Chain = CreateChain(context.call()),
                BlockPosition = new BlockPosition(context.Out().Symbol.StartIndex - _offset, context.GetText().Length)
            };
            result.Chain.Last().ParameterTemplate = context.subtemplate()?.ttl()?.GetText();
            return result;
        }

        internal RawOutputItem CreateRawOutputItem(TtlParser.RawoutputContext context)
        {
            return new RawOutputItem
            {
                Position = new BlockPosition(context.RawStart().Symbol.StartIndex, context.GetText().Length),
                Text = context.Anything()?.GetText()
            };
        }

        public bool DefenitionExists(string name)
        {
            return DefinitionBlock.Definitions.ContainsKey(name);
        }


        internal DefinitionItem CurrentDefenition { get; set; }

        internal OutputChain CurrentChain { get; set; }

        public SmartList<OutputChain> OutputChains { get; private set; }

        public SmartList<RawOutputItem> RawOutputItems { get; set; }

        internal bool InDefinition { get; set; } = false;

        public DefinitionBlock DefinitionBlock { get; }

        #region Helper Methods

        private SmartList<OutputItem> CreateChain(IEnumerable<TtlParser.CallContext> context) {
            if (context == null)
                return null;
            var result = new SmartList<OutputItem>();
            result.AddRange(context.Select(CreateItem));
            return result;
        }

        private OutputItem CreateItem(TtlParser.CallContext context)
        {
            return new OutputItem(context.ChainName().GetText())
            {
                CallParameter =
                {
                    ModelParameter = context.ModelName()?.GetText(),
                    ChainParameter = CreateChain(context.call())
                }
            };
        }

        #endregion
    }
}