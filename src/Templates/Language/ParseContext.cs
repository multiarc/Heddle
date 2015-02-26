using System.Collections.Generic;
using System.Linq;
using Templates.Collections;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Language {
    public class ParseContext {
        private readonly int _offset;

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
                context.DEF_ID(0).GetText(), context.subtemplate().GetText(),
                GetDefenition(context.DEF_ID(1)?.GetText()),
                modelType: context.DEF_ID(2)?.GetText())
            {
                Position = new BlockPosition(context.Start.StartIndex, context.GetText().Length)
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
                BlockPosition = new BlockPosition(context.Start.StartIndex - _offset, context.GetText().Length)
            };
            result.Chain.Last().ParameterTemplate = context.outtemplate()?.ttl()?.GetText();
            return result;
        }

        internal IEnumerable<RawOutputItem> CreateRawOutputItems(TtlParser.TtlContext context)
        {
            foreach (var raw in context.RAW())
            {
                var text = raw.GetText();
                yield return new RawOutputItem
                {
                    Position = new BlockPosition(raw.Symbol.StartIndex, text.Length),
                    Text = text.Substring(2, text.Length - 4)
                };
            }
        }

        public bool DefenitionExists(string name)
        {
            return DefinitionBlock.Definitions.ContainsKey(name);
        }


        internal DefinitionItem CurrentDefenition { get; set; }

        internal OutputChain CurrentChain { get; set; }

        public SmartList<OutputChain> OutputChains { get; private set; }

        public SmartList<RawOutputItem> RawOutputItems { get; private set; }

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
            return new OutputItem(context.OUT_ID(0)?.GetText())
            {
                CallParameter =
                {
                    ModelParameter = context.OUT_ID(1)?.GetText(),
                    ChainParameter = CreateChain(context.call())
                }
            };
        }

        #endregion
    }
}