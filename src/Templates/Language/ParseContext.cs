using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Microsoft.CodeAnalysis;
using Templates.Collections;
using Templates.Exceptions;
using Templates.Strings.Core;

namespace Templates.Language {
    public class ParseContext {
        private readonly int _offset;

        internal bool DefenitionsOnly { get; set; }

        private readonly bool _inDefintionContext;

        private static Dictionary<ParseContext, ParseContext> _isolatedSet;

        private static HashSet<ParseContext> _isolatedList;

        private static readonly object LockObject = new object();

        internal ParseContext(ParseContext parentContext = null, int offset = 0) {
            _inDefintionContext = (parentContext?.InDefinition ?? false) || (parentContext?._inDefintionContext ?? false);
            _offset = offset;
            DefinitionsBlock = new DefinitionBlock(parentContext?.DefinitionsBlock);
            OutputChains = new SmartList<OutputChain>();
            RawOutputItems = new SmartList<RawOutputItem>();
            CommentTokens = new SmartList<BlockPosition>();
        }

        private static ParseContext IsolateContextFrom(ParseContext context) {
            var newContext = new ParseContext(context, context._offset) { DefenitionsOnly = context.DefenitionsOnly };
            newContext.DefinitionsBlock.Positions.AddRange(context.DefinitionsBlock.Positions);
            newContext.RawOutputItems.AddRange(context.RawOutputItems);
            newContext.CommentTokens.AddRange(context.CommentTokens);
            return newContext;
        }

        public ParseContext IsolateContextWithTree(string definitionName = null)
        {
            lock (LockObject)
            {
                _isolatedSet = new Dictionary<ParseContext, ParseContext>();
                _isolatedList = new HashSet<ParseContext>();
                var result = IsolateContext(definitionName);
                _isolatedSet = null;
                _isolatedList = null;
                return result;
            }
        }

        internal ParseContext IsolateContext(string definitionName = null)
        {
            var result = IsolateContextFrom(this);
            //Check if this context already was isolated from other one in the call tree
            if (_isolatedList.Contains(this))
            {
                return this;
            }
            //Prevent from stack overflow if child items could have the same context or subsequent contexts
            if (!_isolatedSet.ContainsKey(this))
            {
                _isolatedSet.Add(this, result);
                _isolatedList.Add(result);
            }
            //Return already isolated context instance
            else
            {
                return _isolatedSet[this];
            }
            if (definitionName != null)
            {
                if (DefinitionsBlock.Definitions.ContainsKey(definitionName))
                {
                    var item = new DefinitionItem(DefinitionsBlock.Definitions[definitionName]);

                    var newContest = item.Context.IsolateContext(definitionName);

                    item.Context = newContest;
                    if (item.BaseDefinition != null)
                    {
                        item.BaseDefinition.Context = item.BaseDefinition.Context.IsolateContext(definitionName);
                    }
                    result.DefinitionsBlock.Definitions[definitionName] = item;
                }
            }
            else
            {
                foreach (var definition in DefinitionsBlock.Definitions)
                {
                    var item = new DefinitionItem(definition.Value);
                    item.Context = item.Context.IsolateContext();
                    if (item.BaseDefinition != null)
                    {
                        item.BaseDefinition.Context = item.BaseDefinition.Context.IsolateContext();
                    }
                    result.DefinitionsBlock.Definitions[definition.Key] = item;
                }
            }
            result.OutputChains.AddRange(OutputChains.Select(chain => new OutputChain(chain, result, definitionName)));
            return result;
        }

        internal DefinitionItem CreateDefinition(TtlParser.DefContext context) {
            if (context == null)
                throw new ArgumentNullException("context");
            var inherited = context.inherited_def();
            var simple = context.simple_def();
            if (simple != null) {
                var ttl = simple.subtemplate()?.ttl();
                if (ttl?.Start?.InputStream == null)
                    return null;
                if (simple.DEF_ID(0) == null)
                    throw new TemplateParseException("The Definition should have the Name", GetBlockPosition(context));
                string parameterTemplate = ttl.Start.InputStream.GetText(new Interval(ttl.Start.StartIndex, ttl.Stop.StopIndex));
                return new DefinitionItem(
                    simple.DEF_ID(0).GetText(), parameterTemplate,
                    null,
                    modelType: simple.DEF_ID(1)?.GetText())
                {
                    Position =GetBlockPosition(context)
                };
            }
            if (inherited != null) {
                var ttl = inherited.subtemplate()?.ttl();
                if (ttl?.Start?.InputStream == null)
                    return null;
                if (inherited.DEF_ID(0) == null)
                    throw new TemplateParseException("The Definition should have the Name", GetBlockPosition(context));
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

        public DefinitionItem GetDefenition(string baseName) {
            if (baseName.IsNullOrEmpty())
                return null;
            if (DefenitionExists(baseName))
                return DefinitionsBlock.Definitions[baseName];
            return null;
        }

        internal BlockPosition GetBlockPosition(ParserRuleContext context) {
            if (context == null)
                throw new ArgumentNullException("context");
            if (context.Start == null || context.Stop == null)
                throw new ArgumentException();
            return new BlockPosition(context.Start.StartIndex - _offset,
                context.Stop.StopIndex - context.Start.StartIndex + 1);
        }

        internal OutputChain CreateOutputChain(TtlParser.OutblockContext context) {
            if (context == null)
                throw new ArgumentNullException("context");
            var result = new OutputChain(InDefintionContext ? this : IsolateContextWithTree())
            {
                Chain = CreateChain(context.chain()?.call()),
                BlockPosition = GetBlockPosition(context)
            };
            var ttl = context.subtemplate()?.ttl();
            if (ttl != null) {
                result.Chain.First().ParameterTemplate = ttl.Start.InputStream.GetText(new Interval(ttl.Start.StartIndex, ttl.Stop.StopIndex));
            }
            return result;
        }

        internal RawOutputItem CreateRawOutputItem(TtlParser.RawContext context) {
            if (context == null)
                throw new ArgumentNullException("context");
            var raw = context.RAW();
            if (raw == null)
                throw new TemplateParseException("Raw block is strangely null", GetBlockPosition(context));
            var text = raw.GetText();
            if (text.Length < 4)
                throw new TemplateParseException("Raw block is wrongly formatted", GetBlockPosition(context));
            return new RawOutputItem
            {
                BlockPosition =
                    new BlockPosition(raw.Symbol.StartIndex - _offset, raw.Symbol.StopIndex - raw.Symbol.StartIndex + 1),
                Text = text.Substring(2, text.Length - 4)
            };
        }

        public bool DefenitionExists(string name) {
            return DefinitionsBlock.Definitions.ContainsKey(name ?? string.Empty);
        }


        internal DefinitionItem CurrentDefenition { get; set; }

        internal OutputChain CurrentChain { get; set; }

        public SmartList<OutputChain> OutputChains { get; private set; }

        public SmartList<RawOutputItem> RawOutputItems { get; private set; }

        public SmartList<BlockPosition> CommentTokens { get; private set; }

        internal bool InDefinition { get; set; }
        = false;

        public DefinitionBlock DefinitionsBlock { get; set; }

        internal bool InDefintionContext => _inDefintionContext;

        public int Offset => _offset;

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

        private OutputItem CreateItem(TtlParser.CallContext context) {
            if (context == null)
                throw new ArgumentNullException("context");
            var namedCall = context.named_call();
            var unnamedCall = context.unnamed_call();
            if (namedCall != null) {
                string itemName = namedCall.OUT_ID(0)?.GetText();
                return new OutputItem(itemName)
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
                return new OutputItem(string.Empty)
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