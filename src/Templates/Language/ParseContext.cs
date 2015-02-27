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
            CommentTokens = new SmartList<BlockPosition>();
        }

        internal DefinitionItem CreateDefinition(TtlParser.DefContext context)
        {
            return new DefinitionItem(
                context.ID(0).GetText(), context.subtemplate().GetText(),
                GetDefenition(context.ID(1)?.GetText()),
                modelType: context.ID(2)?.GetText())
            {
                Position = new BlockPosition(context.Start.StartIndex, context.Stop.StopIndex - context.Start.StartIndex + 1)
            };
        }

        public DefinitionItem GetDefenition(string baseName)
        {
            if (baseName.IsNullOrEmpty())
                return null;
            if (DefenitionExists(baseName))
                return DefinitionBlock.Definitions[baseName];
            return null;
        }

        internal OutputChain CreateOutputChain(TtlParser.OutblockContext context)
        {
            var result = new OutputChain()
            {
                Chain = CreateChain(context.chain().call()),
                BlockPosition = new BlockPosition(context.Start.StartIndex - _offset, context.Stop.StopIndex - context.Start.StartIndex + 1)
            };
            result.Chain.First().ParameterTemplate = context.subtemplate()?.ttl()?.GetText();
            return result;
        }

        internal IEnumerable<RawOutputItem> CreateRawOutputItems(TtlParser.TtlContext context)
        {
            foreach (var raw in context.RAW())
            {
                var text = raw.GetText();
                yield return new RawOutputItem
                {
                    Position = new BlockPosition(raw.Symbol.StartIndex, raw.Symbol.StopIndex - raw.Symbol.StartIndex + 1),
                    Text = text.Substring(2, text.Length - 4)
                };
            }
        }

        public bool DefenitionExists(string name)
        {
            return DefinitionBlock.Definitions.ContainsKey(name ?? string.Empty);
        }


        internal DefinitionItem CurrentDefenition { get; set; }

        internal OutputChain CurrentChain { get; set; }

        public SmartList<OutputChain> OutputChains { get; private set; }

        public SmartList<RawOutputItem> RawOutputItems { get; private set; }

        public SmartList<BlockPosition> CommentTokens { get; private set; }

        internal bool InDefinition { get; set; } = false;

        public DefinitionBlock DefinitionBlock { get; }

        #region Helper Methods

        private SmartList<OutputItem> CreateChain(IEnumerable<TtlParser.CallContext> context) {
            if (context == null)
                return null;
            var result = new SmartList<OutputItem>();
            result.AddRange(context.Select(CreateItem).Where(item => item != null));
            if (result.Length == 0)
                return null;
            return result;
        }

        private OutputItem CreateItem(TtlParser.CallContext context)
        {
            var namedCall = context.named_call();
            var unnamedCall = context.unnamed_call();
            if (namedCall != null) {
                return new OutputItem(namedCall.OUT_ID(0)?.GetText())
                {
                    CallParameter =
                    {
                        ModelParameter = namedCall.OUT_ID(1)?.GetText(),
                        ChainParameter = CreateChain(namedCall.chain()?.call())
                    }
                };
            }
            if (unnamedCall != null) {
                return new OutputItem(string.Empty)
                {
                    CallParameter =
                    {
                        ModelParameter = unnamedCall.OUT_ID()?.GetText(),
                        ChainParameter = CreateChain(unnamedCall.chain()?.call())
                    }
                };
            }
            return null;
        }

        #endregion
    }
}