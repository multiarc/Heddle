using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Templates.Collections;
using Templates.Data;
using Antlr4.Runtime.Misc;
using Templates.Strings.Core;

namespace Templates.Language {
    public class ParseContext {
        private readonly int _offset;

        internal bool DefenitionsOnly { get; set; } = false;

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
            var inherited = context.inherited_def();
            var simple = context.simple_def();
            if (simple != null)
            {
                var ttl = simple.subtemplate().ttl();
                string parameterTemplate = ttl.Start.InputStream.GetText(new Interval(ttl.Start.StartIndex, ttl.Stop.StopIndex));
                return new DefinitionItem(
                    simple.DEF_ID(0).GetText(), parameterTemplate,
                    null,
                    modelType: simple.DEF_ID(1)?.GetText())
                {
                    Position =GetBlockPosition(context)
                };
            }
            if (inherited != null)
            {
                var ttl = inherited.subtemplate().ttl();
                string parameterTemplate = ttl.Start.InputStream.GetText(new Interval(ttl.Start.StartIndex, ttl.Stop.StopIndex));
                var baseDefenition = GetDefenition(inherited.DEF_ID(1)?.GetText());
                return new DefinitionItem(
                    inherited.DEF_ID(0).GetText(), parameterTemplate,
                    baseDefenition,
                    modelType: inherited.DEF_ID(2)?.GetText() ?? baseDefenition.ModelType)
                {
                    Position = GetBlockPosition(context)
                };
            }
            return null;
        }

        public DefinitionItem GetDefenition(string baseName)
        {
            if (baseName.IsNullOrEmpty())
                return null;
            if (DefenitionExists(baseName))
                return DefinitionBlock.Definitions[baseName];
            return null;
        }

        internal BlockPosition GetBlockPosition(ParserRuleContext context)
        {
            return new BlockPosition(context.Start.StartIndex - _offset,
                context.Stop.StopIndex - context.Start.StartIndex + 1);
        }

        internal OutputChain CreateOutputChain(TtlParser.OutblockContext context)
        {
            var result = new OutputChain(new ParseContext(this, _offset))
            {
                Chain = CreateChain(context.chain().call()),
                BlockPosition = GetBlockPosition(context)
            };
            var ttl = context.subtemplate()?.ttl();
            if (ttl != null) {
                result.Chain.First().ParameterTemplate = ttl.Start.InputStream.GetText(new Interval(ttl.Start.StartIndex, ttl.Stop.StopIndex));
            }
            return result;
        }

        internal RawOutputItem CreateRawOutputItem(TtlParser.RawContext context)
        {
            var raw = context.RAW();

            var text = raw.GetText();
            return new RawOutputItem
            {
                BlockPosition =
                    new BlockPosition(raw.Symbol.StartIndex - _offset, raw.Symbol.StopIndex - raw.Symbol.StartIndex + 1),
                Text = text.Substring(2, text.Length - 4)
            };
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
                return new OutputItem(namedCall.OUT_ID(0)?.GetText(), new ParseContext(this, _offset))
                {
                    CallParameter =
                    {
                        ModelParameter = namedCall.OUT_ID(1)?.GetText(),
                        ChainParameter = CreateChain(namedCall.chain()?.call()),
                        CSharpExpression = namedCall.csharp_expression()?.GetText()
                    }
                };
            }
            if (unnamedCall != null) {
                return new OutputItem(string.Empty, new ParseContext(this, _offset))
                {
                    CallParameter =
                    {
                        ModelParameter = unnamedCall.OUT_ID()?.GetText(),
                        ChainParameter = CreateChain(unnamedCall.chain()?.call()),
                        CSharpExpression = unnamedCall.csharp_expression()?.GetText()
                    }
                };
            }
            return null;
        }

        #endregion
    }
}