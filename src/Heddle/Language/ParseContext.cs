using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Heddle.Language {
    public class ParseContext {
        private readonly int _offset;

        //internal bool DefenitionsOnly { get; set; }

        private readonly bool _inDefintionContext;

        private readonly List<HeddleToken> _tokens = new List<HeddleToken>();

        private readonly List<ParseContext> _subContexts = new List<ParseContext>();

        public IEnumerable<HeddleToken> Tokens => _tokens;
        
        public IEnumerable<ParseContext> SubContexts => _subContexts;

        internal ParseContext(ParseContext parentContext = null, int offset = 0, bool provideLanguageFeatures = false) {
            _inDefintionContext = (parentContext?.InDefinition ?? false) || (parentContext?._inDefintionContext ?? false);
            ProvideLanguageFeatures = provideLanguageFeatures || (parentContext?.ProvideLanguageFeatures ?? false);
            _offset = offset;
            DefinitionsBlock = new DefinitionBlock(parentContext?.DefinitionsBlock);
            OutputChains = new List<OutputChain>();
            RawOutputItems = new List<RawOutputItem>();
            SkippedTokens = new List<BlockPosition>();
            DefaultChains = new List<OutputChain>();
            Errors = parentContext?.Errors ?? new List<HeddleCompileError>();
            Warnings = parentContext?.Warnings ?? new List<HeddleCompileWarning>();
        }

        private static ParseContext IsolateContextFrom(ParseContext context)
        {
            var newContext = new ParseContext(context, context._offset);// { DefenitionsOnly = context.DefenitionsOnly };
            newContext.DefinitionsBlock.Positions.AddRange(context.DefinitionsBlock.Positions);
            newContext.RawOutputItems.AddRange(context.RawOutputItems);
            newContext.SkippedTokens.AddRange(context.SkippedTokens);
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
                //DefinitionItem definition;
                //if (DefinitionsBlock.Definitions.TryGetValue(definitionName, out definition) && definition.BaseDefinition != null)
                if (DefinitionsBlock.Definitions.ContainsKey(definitionName))
                {
                    var item = new DefinitionItem(DefinitionsBlock.Definitions[definitionName]);

                    var newContext = item.Context.IsolateContext(definitionName);

                    item.Context = newContext;
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
            result.DefaultChains.AddRange(DefaultChains.Select(chain => new OutputChain(chain, result, definitionName)));
            return result;
        }

        internal HeddleCompileWarning CreateWarning(string message, string fix, int startIndex, int stopIndex)
        {
            return new HeddleCompileWarning
            {
                Error = message,
                Position = new BlockPosition(startIndex, stopIndex - startIndex + 1),
                Fix = fix
            };
        }

        internal HeddleCompileError CreateError(RecognitionException exception, string message, IToken token = null)
        {
            HeddleCompileError error;
            if (token != null)
            {
                error = message.ToError(GetAbsoluteBlockPosition(token));
                error.Exception = exception;
                return error;
            }
            error = message.ToError(default(BlockPosition));
            error.Exception = exception;
            return error;
        }

        internal void AddSubContext(ParseContext context)
        {
            _subContexts.Add(context);
        }

        internal DefinitionItem CreateDefinition(HeddleParser.DefContext context, CompileContext compileContext, out OutputChain chain) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var defBase = context.def_base();
            var defType = context.def_type();
            if (defBase == null) {
                AddToken(context.DEF_STARTNAME(), HeddleTokenType.DefStartName);
                var subTemplate = context.subtemplate();
                if (subTemplate == null)
                {
                    chain = null;
                    return null;
                }

                var parameterTemplate = subTemplate.heddle()?.GetText();
                var definitionId = context.ID();
                if (definitionId == null)
                    throw new TemplateParseException("The Definition should have the Name".ToError(GetAbsoluteBlockPosition(context)));
                AddToken(definitionId, HeddleTokenType.Id);
                AddToken(context.DEF_ENDNAME(), HeddleTokenType.DefEndName);
                var definitionName = definitionId.GetText().Trim();
                var defOutChain = context.default_chain();
                if (defOutChain != null)
                {
                    AddToken(defOutChain.DEF_OUT(), HeddleTokenType.DefOutputOnEnd);
                }
                chain = CreateOutputChain(defOutChain?.chain(), definitionName);
                AddToken(subTemplate.SUB_START(), HeddleTokenType.SubStart);
                AddToken(subTemplate.SUB_CLOSE(), HeddleTokenType.SubClose);
                return new DefinitionItem(
                    definitionName, parameterTemplate,
                    null,
                    modelType: defType?.ID().GetText().Trim())
                {
                    Position = GetBlockPosition(context)
                };
            } else {
                AddToken(context.DEF_STARTNAME(), HeddleTokenType.DefStartName);
                var subTemplate = context.subtemplate();
                var heddleCtx = subTemplate?.heddle();
                if (heddleCtx?.Start?.InputStream == null)
                {
                    chain = null;
                    return null;
                }
                var definitionId = context.ID();
                if (definitionId == null)
                    throw new TemplateParseException("The Definition should have the Name".ToError(GetAbsoluteBlockPosition(context)));
                AddToken(definitionId, HeddleTokenType.Id);
                var parameterTemplate = subTemplate.heddle().GetText();
                var baseName = defBase.ID()?.GetText().Trim();
                var baseDefenition = GetDefenition(baseName);
                if (baseDefenition == null)
                {
                    compileContext.CompileErrors.Add($"Base definition {baseName} couldn't be found".ToError(GetAbsoluteBlockPosition(context)));
                }
                AddToken(defBase.ID(), HeddleTokenType.Id);
                AddToken(context.DEF_ENDNAME(), HeddleTokenType.DefEndName);
                var chainContext = context.default_chain()?.chain();
                var definitionName = definitionId.GetText().Trim();
                if (baseDefenition?.Name == definitionName && chainContext != null)
                {
                    compileContext.CompileErrors.Add(
                        "Default output call chain can't be used in fully overriden definitions".ToError(
                            GetAbsoluteBlockPosition(chainContext)));
                }
                var defOutChain = context.default_chain();
                if (defOutChain != null)
                {
                    AddToken(defOutChain.DEF_OUT(), HeddleTokenType.DefOutputOnEnd);
                }
                var modelType = defType?.ID();
                AddToken(context.def_type()?.DEF_TYPE(), HeddleTokenType.DefType);
                AddToken(modelType, HeddleTokenType.Id);
                AddToken(defBase.DELIM(), HeddleTokenType.Delim);
                AddToken(subTemplate.SUB_START(), HeddleTokenType.SubStart);
                AddToken(subTemplate.SUB_CLOSE(), HeddleTokenType.SubClose);
                chain = CreateOutputChain(defOutChain?.chain(), definitionName);
                return new DefinitionItem(
                    definitionName, parameterTemplate,
                    baseDefenition,
                    modelType: modelType?.GetText()?.Trim() ?? baseDefenition?.ModelType)
                {
                    Position = GetBlockPosition(context)
                };
            }
        }

        public DefinitionItem GetDefenition(string baseName) {
            if (string.IsNullOrEmpty(baseName))
                return null;
            if (DefenitionExists(baseName))
                return DefinitionsBlock.Definitions[baseName];
            return null;
        }

        internal BlockPosition GetAbsoluteBlockPosition(IToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));
            return new BlockPosition(token);
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

        internal OutputChain CreateOutputChain(HeddleParser.OutblockContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            AddToken(context.OUT(), HeddleTokenType.Out);
            var delims = context.chain()?.DELIM();
            if (delims != null)
            {
                foreach (var delim in delims)
                {
                    AddToken(delim, HeddleTokenType.Delim);
                }
            }

            var result = new OutputChain(InDefintionContext ? this : IsolateContextWithTree())
            {
                Chain = CreateChain(context.chain()?.call()),
                BlockPosition = GetBlockPosition(context)
            };
            var subTemplate = context.subtemplate();
            AddToken(subTemplate?.SUB_START(), HeddleTokenType.SubStart);
            AddToken(subTemplate?.SUB_CLOSE(), HeddleTokenType.SubClose);
            result.Chain.First().ParameterTemplate = subTemplate?.heddle().GetText();

            return result;
        }

        internal OutputChain CreateOutputChain(HeddleParser.ChainContext chain, string firstCallOverride)
        {
            if (chain == null)
                return null;
            var delims = chain.DELIM();
            if (delims != null)
            {
                foreach (var delim in delims)
                {
                    AddToken(delim, HeddleTokenType.Delim);
                }
            }
            var result = new OutputChain(this)
            {
                Chain = CreateChain(chain.call(), firstCallOverride),
                BlockPosition = GetBlockPosition(chain)
            };
            return result;
        }

        internal RawOutputItem CreateRawOutputItem(HeddleParser.RawContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var raw = context.RAW();
            if (raw == null)
                throw new TemplateParseException("Raw block is strangely null".ToError(GetAbsoluteBlockPosition(context)));
            var text = raw.GetText();

            var oneLineStyle = text.StartsWith("@:");
            
            if (oneLineStyle && text.Length < 2 || !oneLineStyle && text.Length < 4)
                throw new TemplateParseException("Raw block is wrongly formatted".ToError(GetAbsoluteBlockPosition(context)));
            return new RawOutputItem
            {
                BlockPosition = GetBlockPosition(context),
                Text = text.Substring(2, text.Length - (oneLineStyle ? 2 : 4))
            };
        }

        public bool DefenitionExists(string name) {
            return DefinitionsBlock.Definitions.ContainsKey(name ?? string.Empty);
        }

        internal DefinitionItem CurrentDefenition { get; set; }

        internal OutputChain CurrentChain { get; set; }

        public List<OutputChain> DefaultChains { get; }

        public List<HeddleCompileError> Errors { get; }

        public List<HeddleCompileWarning> Warnings { get; }

        public List<OutputChain> OutputChains { get; }

        public List<RawOutputItem> RawOutputItems { get; }

        public List<BlockPosition> SkippedTokens { get; }

        internal bool InDefinition { get; set; }

        public DefinitionBlock DefinitionsBlock { get; set; }

        internal bool InDefintionContext => _inDefintionContext;

        internal bool ProvideLanguageFeatures { get; }

        public int Offset => _offset;

        private List<OutputItem> CreateChain(IEnumerable<HeddleParser.CallContext> context,
            string firstCallOverride = null)
        {
            if (context == null)
                return null;
            var result = new List<OutputItem>();
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

            if (result.Count == 0)
                return null;
            return result;
        }

        private OutputItem CreateItem(HeddleParser.CallContext context, string callNameOverride = null) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            var extensionId = context.extension_id();
            var memberExpr = context.member_expression();
            if (extensionId != null) {
                var idTemplate = extensionId.ID();
                AddToken(idTemplate, HeddleTokenType.Id);
                
                string itemName = idTemplate?.GetText().Trim();
                AddToken(context.OUT_PARAMSTART(), HeddleTokenType.OutParamStart);
                
                var members = memberExpr?.ID() ?? [];
                foreach (var member in members)
                {
                    AddToken(member, HeddleTokenType.Id);
                }
                var memberSelectors = memberExpr?.MEMBER_P() ?? [];
                foreach (var selectors in memberSelectors)
                {
                    AddToken(selectors, HeddleTokenType.MemberSelector);
                }
                AddToken(memberExpr?.ROOT_REF(), HeddleTokenType.RootReference);
                AddToken(context.OUT_PARAMEND(), HeddleTokenType.OutParamEnd);
                var delims = context.chain()?.DELIM();
                if (delims != null)
                {
                    foreach (var delim in delims)
                    {
                        AddToken(delim, HeddleTokenType.Delim);
                    }
                }
                var csharpExpression = context.csharp_expression();
                if (csharpExpression != null)
                {
                    AddToken(context.CSHARP_START(), HeddleTokenType.CSharpStart);
                    foreach (var token in csharpExpression.CSHARP_TOKEN())
                    {
                        AddToken(token, HeddleTokenType.CSharpToken);
                    }
                    foreach (var token in csharpExpression.OUT_PARAMEND())
                    {
                        AddToken(token, HeddleTokenType.CSharpToken);
                    }
                }
                return new OutputItem(itemName, GetAbsoluteBlockPosition(context))
                {
                    CallParameter =
                    {
                        ModelParameter = members.Select(n => n.GetText().Trim()).ToArray(),
                        RootReference = memberExpr?.ROOT_REF() != null,
                        ChainParameter = CreateChain(context.chain()?.call()),
                        CSharpExpression = csharpExpression?.GetText()
                    }
                };
            } else {
                AddToken(context.OUT_PARAMSTART(), HeddleTokenType.OutParamStart);
                
                var members = memberExpr?.ID() ?? [];
                foreach (var member in members)
                {
                    AddToken(member, HeddleTokenType.Id);
                }
                var memberSelectors = memberExpr?.MEMBER_P() ?? [];
                foreach (var selectors in memberSelectors)
                {
                    AddToken(selectors, HeddleTokenType.MemberSelector);
                }
                AddToken(memberExpr?.ROOT_REF(), HeddleTokenType.RootReference);
                AddToken(context.OUT_PARAMEND(), HeddleTokenType.OutParamEnd);
                
                var delims = context.chain()?.DELIM();
                if (delims != null)
                {
                    foreach (var delim in delims)
                    {
                        AddToken(delim, HeddleTokenType.Delim);
                    }
                }
                var csharpExpression = context.csharp_expression();
                if (csharpExpression != null)
                {
                    AddToken(context.CSHARP_START(), HeddleTokenType.CSharpStart);
                    foreach (var token in csharpExpression.CSHARP_TOKEN())
                    {
                        AddToken(token, HeddleTokenType.CSharpToken);
                    }
                    foreach (var token in csharpExpression.OUT_PARAMEND())
                    {
                        AddToken(token, HeddleTokenType.CSharpToken);
                    }
                }
                return new OutputItem(callNameOverride ?? string.Empty, GetAbsoluteBlockPosition(context))
                {
                    CallParameter =
                    {
                        ModelParameter = members.Select(n => n.GetText().Trim()).ToArray(),
                        RootReference = memberExpr?.ROOT_REF() != null,
                        ChainParameter = CreateChain(context.chain()?.call()),
                        CSharpExpression = csharpExpression?.GetText()
                    }
                };
            }
        }

        internal void AddToken(ITerminalNode token, HeddleTokenType tokenType)
        {
            if (token != null)
                AddToken(token.Symbol, tokenType);
        }

        internal void AddToken(IToken token, HeddleTokenType tokenType)
        {
            if (ProvideLanguageFeatures && token != null)
            {
                _tokens.Add(new HeddleToken
                {
                    Position = new BlockPosition(token),
                    HeddleTokenType = tokenType
                });
            }
        }

        #region Helpers

        private static Dictionary<ParseContext, ParseContext> _isolatedSet;

        private static HashSet<ParseContext> _isolatedList;

        private static readonly object LockObject = new object();

        #endregion
    }
}