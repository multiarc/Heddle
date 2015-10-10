using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Templates.Collections;
using Templates.Data;
using Templates.Exceptions;
using Templates.Strings.Core;

namespace Templates.Language {
    public class ParseContext {
        private readonly int _offset;

        internal bool DefenitionsOnly { get; set; }

        private readonly bool _inDefintionContext;

        internal ParseContext(ParseContext parentContext = null, int offset = 0) {
            _inDefintionContext = (parentContext?.InDefinition ?? false) || (parentContext?._inDefintionContext ?? false);
            _offset = offset;
            DefinitionsBlock = new DefinitionBlock(parentContext?.DefinitionsBlock);
            OutputChains = new SmartList<OutputChain>();
            RawOutputItems = new SmartList<RawOutputItem>();
            CommentTokens = new SmartList<BlockPosition>();
            DefaultChains = new SmartList<OutputChain>();
            Errors = parentContext?.Errors ?? new SmartList<TtlCompileError>();
            Warnings = parentContext?.Warnings ?? new SmartList<TtlCompileWarning>();
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
            //Prevent from stack overflow if child items could have the same context or same subsequent contexts
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

        internal TtlCompileWarning CreateWarning(string message, string fix, int startIndex, int stopIndex)
        {
            return new TtlCompileWarning
            {
                Error = message,
                Position = new BlockPosition(startIndex, stopIndex - startIndex + 1),
                Fix = fix
            };
        }

        internal TtlCompileError CreateError(RecognitionException exception, string message, IToken token = null)
        {
            TtlCompileError error;
            if (token != null)
            {
                error = message.ToError(GetAbsoluteBlockPosition(token));
                error.Exception = exception;
                return error;
            }
            error = message.ToError();
            error.Exception = exception;
            return error;
        }

        internal DefinitionItem CreateDefinition(TtlParser.DefContext context, out OutputChain chain) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var inherited = context.inherited_def();
            var simple = context.simple_def();
            if (simple != null) {
                var ttl = simple.subtemplate()?.ttl();
                if (ttl?.Start?.InputStream == null)
                {
                    chain = null;
                    return null;
                }
                var definition = simple.ID(0);
                if (definition == null)
                    throw new TemplateParseException("The Definition should have the Name".ToError(GetAbsoluteBlockPosition(simple)));
                string parameterTemplate = ttl.Start.InputStream.GetText(new Interval(ttl.Start.StartIndex, ttl.Stop.StopIndex));
                var definitionName = definition.GetText();
                chain = CreateOutputChain(simple.default_chain()?.chain(), definitionName);
                return new DefinitionItem(
                    definitionName, parameterTemplate,
                    null,
                    modelType: simple.ID(1)?.GetText())
                {
                    Position =GetBlockPosition(context)
                };
            }
            if (inherited != null) {
                var ttl = inherited.subtemplate()?.ttl();
                if (ttl?.Start?.InputStream == null)
                {
                    chain = null;
                    return null;
                }
                var definition = inherited.ID(0);
                if (definition == null)
                    throw new TemplateParseException("The Definition should have the Name".ToError(GetAbsoluteBlockPosition(inherited)));
                string parameterTemplate = ttl.Start.InputStream.GetText(new Interval(ttl.Start.StartIndex, ttl.Stop.StopIndex));
                var baseName = inherited.ID(1)?.GetText();
                var baseDefenition = GetDefenition(baseName);
                if (baseDefenition == null)
                {
                    throw new TemplateCompileException($"Base definition {baseName} couldn't be found".ToError(GetAbsoluteBlockPosition(inherited)));
                }
                var chainContext = inherited.default_chain()?.chain();
                var definitionName = definition.GetText();
                if (baseDefenition.Name == definitionName && chainContext != null)
                {
                    throw new TemplateCompileException(
                        "Default output call chain can't be used in fully overriden definitions".ToError(
                            GetAbsoluteBlockPosition(chainContext)));
                }
                chain = CreateOutputChain(inherited.default_chain()?.chain(), definitionName);
                return new DefinitionItem(
                    definitionName, parameterTemplate,
                    baseDefenition,
                    modelType: inherited.ID(2)?.GetText() ?? baseDefenition.ModelType)
                {
                    Position = GetBlockPosition(context)
                };
            }
            chain = null;
            return null;
        }

        public DefinitionItem GetDefenition(string baseName) {
            if (baseName.IsNullOrEmpty())
                return null;
            if (DefenitionExists(baseName))
                return DefinitionsBlock.Definitions[baseName];
            return null;
        }

        internal BlockPosition GetAbsoluteBlockPosition(IToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));
            return new BlockPosition(token.StartIndex, token.StopIndex - token.StartIndex + 1);
        }

        internal BlockPosition GetBlockPosition(IToken token) {
            if (token == null)
                throw new ArgumentNullException(nameof(token));
            return new BlockPosition(token.StartIndex - _offset, token.StopIndex - token.StartIndex + 1);
        }

        internal BlockPosition GetBlockPosition(ParserRuleContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.Start == null || context.Stop == null)
                throw new ArgumentException();
            return new BlockPosition(context.Start.StartIndex - _offset,
                context.Stop.StopIndex - context.Start.StartIndex + 1);
        }

        internal BlockPosition GetAbsoluteBlockPosition(ParserRuleContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.Start == null || context.Stop == null)
                throw new ArgumentException();
            return new BlockPosition(context.Start.StartIndex,
                context.Stop.StopIndex - context.Start.StartIndex + 1);
        }

        internal OutputChain CreateOutputChain(TtlParser.OutblockContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
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

        internal OutputChain CreateOutputChain(TtlParser.ChainContext context, string firstCallOverride)
        {
            if (context == null)
                return null;
            var result = new OutputChain(this)
            {
                Chain = CreateChain(context.call(), firstCallOverride),
                BlockPosition = GetBlockPosition(context)
            };
            return result;
        }

        internal RawOutputItem CreateRawOutputItem(TtlParser.RawContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var raw = context.RAW();
            if (raw == null)
                throw new TemplateParseException("Raw block is strangely null".ToError(GetAbsoluteBlockPosition(context)));
            var text = raw.GetText();
            if (text.Length < 4)
                throw new TemplateParseException("Raw block is wrongly formatted".ToError(GetAbsoluteBlockPosition(context)));
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

        internal SmartList<OutputChain> DefaultChains { get; set; }

        public SmartList<TtlCompileError> Errors { get; }

        public SmartList<TtlCompileWarning> Warnings { get; }

        public SmartList<OutputChain> OutputChains { get; }

        public SmartList<RawOutputItem> RawOutputItems { get; }

        public SmartList<BlockPosition> CommentTokens { get; }

        internal bool InDefinition { get; set; }
        = false;

        public DefinitionBlock DefinitionsBlock { get; set; }

        internal bool InDefintionContext => _inDefintionContext;

        public int Offset => _offset;

        private SmartList<OutputItem> CreateChain(IEnumerable<TtlParser.CallContext> context,
            string firstCallOverride = null)
        {
            if (context == null)
                return null;
            var result = new SmartList<OutputItem>();
            bool first = true;
            foreach (var callContext in context)
            {
                if (first)
                {
                    first = false;
                    var item = CreateItem(callContext, firstCallOverride);
                    if (item != null)
                        result.Add(item);
                }
                else
                {
                    var item = CreateItem(callContext);
                    if (item != null)
                        result.Add(item);
                }
            }

            if (result.Length == 0)
                return null;
            return result;
        }

        private OutputItem CreateItem(TtlParser.CallContext context, string callNameOverride = null) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var namedCall = context.named_call();
            var unnamedCall = context.unnamed_call();
            if (namedCall != null) {
                string itemName = namedCall.ID(0)?.GetText();
                return new OutputItem(itemName)
                {
                    CallParameter =
                    {
                        ModelParameter = namedCall.ID(1)?.GetText(),
                        ChainParameter = CreateChain(namedCall.chain()?.call()),
                        CSharpExpression = namedCall.csharp_expression()?.GetText()
                    }
                };
            }
            if (unnamedCall != null) {
                return new OutputItem(callNameOverride ?? string.Empty)
                {
                    CallParameter =
                    {
                        ModelParameter = unnamedCall.ID()?.GetText(),
                        ChainParameter = CreateChain(unnamedCall.chain()?.call()),
                        CSharpExpression = unnamedCall.csharp_expression()?.GetText()
                    }
                };
            }
            return null;
        }

        #region Helpers

        private static Dictionary<ParseContext, ParseContext> _isolatedSet;

        private static HashSet<ParseContext> _isolatedList;

        private static readonly object LockObject = new object();

        #endregion
    }
}