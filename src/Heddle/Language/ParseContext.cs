using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Language.Expressions;
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
            RegionFillCandidates = parentContext?.RegionFillCandidates ?? new List<RegionFillCandidate>();
            ImportOrigin = parentContext?.ImportOrigin;
        }

        /// <summary>
        /// The absolute (document-space) UTF-16 offset this context's tokens are keyed from — the phase 6 D2
        /// span key for the scope map. Equal to the private <c>_offset</c>.
        /// </summary>
        internal int AbsoluteOffset => _offset;

        /// <summary>
        /// The phase 6 D25 import-provenance marker: non-null while this context (or an ancestor) is the parse
        /// of an imported/partial file. Inherited by child contexts through the ctor; <c>null</c> outside imports
        /// and always <c>null</c> unless <see cref="ProvideLanguageFeatures"/> is on. Diagnostics compiled under a
        /// context carrying this marker are re-anchored to the import site by the LSP facade.
        /// </summary>
        internal ImportOrigin ImportOrigin { get; set; }

        /// <summary>
        /// Phase 7 D5: the stable identity of a caller-content context across isolation copies. Each
        /// <c>EnterSubtemplate</c> creates one fresh root context per body; every isolation copy of it (parse-time
        /// <c>IsolateContextWithTree</c> or per-call-site <c>OutputItem</c> isolation) maps back to that root, so
        /// a <see cref="RegionFillCandidate"/> captured while parsing the body matches its call site's
        /// <c>extensionItem.Context</c> by identity regardless of how many times the context was copied.
        /// </summary>
        internal ParseContext IsolationOrigin { get; private set; }

        /// <summary>See <see cref="IsolationOrigin"/>; <c>this</c> for a context that is not an isolation copy.</summary>
        internal ParseContext OriginIdentity => IsolationOrigin ?? this;

        /// <summary>
        /// Phase 7 D5: the root-shared region-fill candidate list (initialized the same shared-up-the-chain way
        /// as <see cref="Errors"/>), so a candidate captured in any sub-context is reachable from the compiler's
        /// call-site fill step without any sub-context sweep.
        /// </summary>
        internal List<RegionFillCandidate> RegionFillCandidates { get; }

        /// <summary>
        /// Phase 7 D2/D3: the regions declared directly in this context (a component body), in declaration order,
        /// appended on the <c>EnterDef</c> store-success path only. Transferred to the enclosing component's
        /// <see cref="DefinitionItem.Regions"/> when its body context is attached at <c>ExitSubtemplate</c>.
        /// Per-context (never shared up the chain).
        /// </summary>
        internal List<RegionDeclaration> DeclaredRegions { get; } = new List<RegionDeclaration>();

        private static ParseContext IsolateContextFrom(ParseContext context)
        {
            var newContext = new ParseContext(context, context._offset);// { DefenitionsOnly = context.DefenitionsOnly };
            newContext.IsolationOrigin = context.OriginIdentity;
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

        internal DefinitionItem CreateDefinition(HeddleParser.DefContext context, out OutputChain chain) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            // Phase 7 D1/D2: <:name> / <:name :: Type> — a public region declaration. The leading DELIM is a
            // direct child of def only in the region alternative (the <child:base> DELIM lives inside def_base).
            if (context.DELIM() != null)
                return CreateRegionDefinition(context, out chain);
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
                var (noBaseProps, noBaseSlot) = ParseDefProps(context.def_props(), definitionName);
                return new DefinitionItem(
                    definitionName, parameterTemplate,
                    null,
                    modelType: defType?.ID().GetText().Trim())
                {
                    Position = GetBlockPosition(context),
                    HasDefaultOutput = chain != null,
                    PropDeclarations = noBaseProps,
                    SlotTypeName = noBaseSlot,
                    // Phase 7 D2: a plain <name> parsed inside a definition body is a private region.
                    IsRegion = InDefintionContext
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
                HeddleCompileError baseNotFound = null;
                if (baseDefenition == null)
                {
                    // Phase 7 D5 (emit-then-retract): the base-not-found error keeps its exact text/position and
                    // is emitted at parse into the shared Errors list, exactly as today. The error object is
                    // captured below on the fill candidate so a compile-time public-region match can retract the
                    // identical instance from BOTH downstream lists; a genuinely dangling <x:x> retracts nothing.
                    baseNotFound = $"Base definition {baseName} couldn't be found".ToError(GetAbsoluteBlockPosition(context));
                    Errors.Add(baseNotFound);
                }
                AddToken(defBase.ID(), HeddleTokenType.Id);
                AddToken(context.DEF_ENDNAME(), HeddleTokenType.DefEndName);
                var chainContext = context.default_chain()?.chain();
                var definitionName = definitionId.GetText().Trim();
                if (baseDefenition?.Name == definitionName && chainContext != null)
                {
                    Errors.Add(
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
                var (baseProps, baseSlot) = ParseDefProps(context.def_props(), definitionName);
                var item = new DefinitionItem(
                    definitionName, parameterTemplate,
                    baseDefenition,
                    modelType: modelType?.GetText()?.Trim() ?? baseDefenition?.ModelType)
                {
                    Position = GetBlockPosition(context),
                    HasDefaultOutput = chain != null,
                    PropDeclarations = baseProps,
                    SlotTypeName = baseSlot
                };

                // Phase 7 D5/D12: only an unresolved-base <x:x> (same name both sides) becomes a region-fill
                // candidate. It is flagged so the listener never registers it into any DefinitionsBlock, and the
                // captured error object makes the compile-time retract possible. A <x:y> with an unresolved base
                // keeps today's plain error and is never a candidate.
                if (baseNotFound != null && string.Equals(definitionName, baseName, StringComparison.Ordinal))
                {
                    item.IsFillCandidate = true;
                    RegionFillCandidates.Add(new RegionFillCandidate(
                        definitionName, item,
                        modelType?.GetText()?.Trim(),
                        GetAbsoluteBlockPosition(context),
                        baseNotFound,
                        OriginIdentity));
                }

                return item;
            }
        }

        /// <summary>
        /// Phase 7 D1/D2: builds the <see cref="DefinitionItem"/> for a public region declaration
        /// (<c>&lt;:name&gt;</c> / <c>&lt;:name :: Type&gt;</c>). A region carries no prop list, no base, and no
        /// default output chain; its model type is the in-header <c>def_region_type</c> or <c>object</c> when
        /// omitted. A <c>&lt;:name&gt;</c> outside any definition body is a positioned id-less parse error (F6).
        /// </summary>
        private DefinitionItem CreateRegionDefinition(HeddleParser.DefContext context, out OutputChain chain)
        {
            chain = null;
            AddToken(context.DEF_STARTNAME(), HeddleTokenType.DefStartName);
            AddToken(context.DELIM(), HeddleTokenType.Delim);
            var subTemplate = context.subtemplate();
            if (subTemplate == null)
                return null;
            var definitionId = context.ID();
            if (definitionId == null)
                throw new TemplateParseException("The Definition should have the Name".ToError(GetAbsoluteBlockPosition(context)));
            AddToken(definitionId, HeddleTokenType.Id);
            var regionType = context.def_region_type();
            AddToken(regionType?.DEF_TYPE(), HeddleTokenType.DefType);
            AddToken(regionType?.ID(), HeddleTokenType.Id);
            AddToken(context.DEF_ENDNAME(), HeddleTokenType.DefEndName);
            AddToken(subTemplate.SUB_START(), HeddleTokenType.SubStart);
            AddToken(subTemplate.SUB_CLOSE(), HeddleTokenType.SubClose);
            var definitionName = definitionId.GetText().Trim();

            if (!InDefintionContext)
            {
                Errors.Add(
                    $"A public region '<:{definitionName}>' can only be declared inside a definition body."
                        .ToError(GetAbsoluteBlockPosition(context)));
            }

            var parameterTemplate = subTemplate.heddle()?.GetText();
            return new DefinitionItem(
                definitionName, parameterTemplate,
                null,
                modelType: regionType?.ID()?.GetText().Trim())
            {
                Position = GetBlockPosition(context),
                IsRegion = true,
                IsPublicRegion = true
            };
        }

        /// <summary>
        /// Parses a definition header's prop list (phase 5). Builds the <see cref="PropDeclaration"/> list and
        /// the slot type name, emitting the parse-time header diagnostics HED5015/HED5016/HED5017/HED5007 and
        /// the editor tokens (D18). Base props are not flattened here — inheritance flattening is a compile-time
        /// concern (the layout resolver).
        /// </summary>
        private (IReadOnlyList<PropDeclaration> props, string slotTypeName) ParseDefProps(
            HeddleParser.Def_propsContext defProps, string definitionName)
        {
            if (defProps == null)
                return (Array.Empty<PropDeclaration>(), null);

            AddToken(defProps.OUT_PARAMSTART(), HeddleTokenType.OutParamStart);
            AddToken(defProps.OUT_PARAMEND(), HeddleTokenType.OutParamEnd);
            foreach (var comma in defProps.COMMA())
                AddToken(comma, HeddleTokenType.Operator);

            var props = new List<PropDeclaration>();
            string slotTypeName = null;
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            bool slotSeen = false;

            foreach (var itemCtx in defProps.def_prop_item())
            {
                var slotCtx = itemCtx.def_slot();
                if (slotCtx != null)
                {
                    var slotIds = slotCtx.ID();
                    var idName = slotIds[0].GetText().Trim();
                    var typeName = slotIds[1].GetText().Trim();
                    AddToken(slotIds[0], HeddleTokenType.Id);
                    AddToken(slotCtx.DEF_TYPE(), HeddleTokenType.DefType);
                    AddToken(slotIds[1], HeddleTokenType.Id);
                    if (!string.Equals(idName, "out", StringComparison.Ordinal))
                    {
                        Errors.Add(
                            "Expected 'out' before '::' — the slot parameter is declared as 'out:: Type'."
                                .ToError(GetAbsoluteBlockPosition(slotCtx), HeddleDiagnosticIds.InvalidSlotDeclaration));
                        continue;
                    }

                    if (slotSeen)
                    {
                        Errors.Add(
                            $"Definition '{definitionName}' declares more than one slot parameter — only one 'out::' declaration is allowed."
                                .ToError(GetAbsoluteBlockPosition(slotCtx),
                                    HeddleDiagnosticIds.MultipleSlotDeclarations));
                        continue;
                    }

                    slotSeen = true;
                    slotTypeName = typeName;
                    continue;
                }

                var propCtx = itemCtx.def_prop();
                if (propCtx == null)
                    continue;

                var propIds = propCtx.ID();
                var name = propIds[0].GetText().Trim();
                var propTypeName = propIds[1].GetText().Trim();
                AddToken(propIds[0], HeddleTokenType.Id);
                AddToken(propCtx.DELIM(), HeddleTokenType.Delim);
                AddToken(propIds[1], HeddleTokenType.Id);

                bool hasDefault = false;
                object defaultValue = null;
                var defaultCtx = propCtx.def_prop_default();
                if (defaultCtx != null)
                {
                    AddToken(defaultCtx.ASSIGN(), HeddleTokenType.Operator);
                    hasDefault = true;
                    defaultValue = ExpressionAstBuilder.DecodeDefaultLiteral(defaultCtx.def_literal(), this, out _);
                }

                if (string.Equals(name, "out", StringComparison.Ordinal) ||
                    string.Equals(name, "this", StringComparison.Ordinal))
                {
                    var fix = string.Equals(name, "out", StringComparison.Ordinal)
                        ? " Declare the slot parameter with 'out:: Type'."
                        : string.Empty;
                    Errors.Add(
                        $"'{name}' is reserved and cannot be used as a prop name.{fix}"
                            .ToError(GetAbsoluteBlockPosition(propCtx), HeddleDiagnosticIds.ReservedPropName));
                    continue;
                }

                if (!seenNames.Add(name))
                {
                    Errors.Add(
                        $"Prop '{name}' is declared more than once on definition '{definitionName}'."
                            .ToError(GetAbsoluteBlockPosition(propCtx), HeddleDiagnosticIds.DuplicatePropDeclaration));
                    continue;
                }

                props.Add(new PropDeclaration(name, propTypeName, hasDefault, defaultValue, GetBlockPosition(propCtx)));
            }

            return (props.Count == 0 ? (IReadOnlyList<PropDeclaration>) Array.Empty<PropDeclaration>() : props,
                slotTypeName);
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

            // Phase 2 (post-2.0) WI1 — the '@@' literal-@ escape: the lexer re-types the two-char '@@' as
            // RAW (AT_ESCAPE/SUB_AT_ESCAPE); it maps to a single literal '@' collapsed by ReplaceRawOutput.
            if (text == "@@")
            {
                return new RawOutputItem
                {
                    BlockPosition = GetBlockPosition(context),
                    Text = "@"
                };
            }

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
                    {
                        item.IsChainedConsumer = true;
                        result.Add(item);
                    }
                }
            }

            if (result.Count == 0)
                return null;
            return result;
        }

        private readonly struct Alt5Info
        {
            public Alt5Info(bool isAlt5, List<NamedArgument> propArguments, string[] model, bool rootRef,
                ExprNode native)
            {
                IsAlt5 = isAlt5;
                PropArguments = propArguments;
                Model = model;
                RootRef = rootRef;
                Native = native;
            }

            public bool IsAlt5 { get; }
            public List<NamedArgument> PropArguments { get; }
            public string[] Model { get; }
            public bool RootRef { get; }
            public ExprNode Native { get; }
        }

        /// <summary>
        /// Reads the 5th <c>call</c> alternative (named arguments, phase 5 D4). Classifies the optional leading
        /// positional <c>expr</c>: a pure <see cref="PathNode"/> (no target) maps to the member-path shape (bit
        /// identical to alternative 2); a <see cref="ThisNode"/> maps to the empty model parameter; anything
        /// else becomes a native expression. Returns <see cref="Alt5Info.IsAlt5"/> false when the call carries no
        /// named arguments (the four existing alternatives are unchanged).
        /// </summary>
        private Alt5Info ReadAlt5(HeddleParser.CallContext context)
        {
            var namedArgs = context.named_argument();
            if (namedArgs == null || namedArgs.Length == 0)
                return new Alt5Info(false, null, null, false, null);

            string[] model = Array.Empty<string>();
            bool rootRef = false;
            ExprNode native = null;
            var positional = context.expr();
            if (positional != null)
            {
                var node = ExpressionAstBuilder.Build(positional, this);
                if (node is PathNode path && path.Target == null)
                {
                    model = path.Segments.ToArray();
                    rootRef = path.RootRef;
                }
                else if (node is ThisNode)
                {
                    // explicit current-model passthrough → empty model parameter
                }
                else
                {
                    native = node;
                }
            }

            var list = new List<NamedArgument>(namedArgs.Length);
            foreach (var na in namedArgs)
            {
                AddToken(na.ID(), HeddleTokenType.Id);
                AddToken(na.DELIM(), HeddleTokenType.Delim);
                var value = ExpressionAstBuilder.Build(na.expr(), this);
                list.Add(new NamedArgument(na.ID().GetText().Trim(), value, GetAbsoluteBlockPosition(na)));
            }

            return new Alt5Info(true, list, model, rootRef, native);
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
                var nativeExpression = ExpressionAstBuilder.Build(context.native_expression(), this);
                var alt5 = ReadAlt5(context);
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
                }
                return new OutputItem(itemName, GetAbsoluteBlockPosition(context))
                {
                    CallParameter =
                    {
                        ModelParameter = alt5.IsAlt5 ? alt5.Model : members.Select(n => n.GetText().Trim()).ToArray(),
                        RootReference = alt5.IsAlt5 ? alt5.RootRef : memberExpr?.ROOT_REF() != null,
                        ChainParameter = CreateChain(context.chain()?.call()),
                        CSharpExpression = csharpExpression?.GetText(),
                        NativeExpression = alt5.IsAlt5 ? alt5.Native : nativeExpression,
                        PropArguments = alt5.PropArguments
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
                var nativeExpression = ExpressionAstBuilder.Build(context.native_expression(), this);
                var alt5 = ReadAlt5(context);
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
                }
                return new OutputItem(callNameOverride ?? string.Empty, GetAbsoluteBlockPosition(context))
                {
                    IsDefaultChainSelfCall = callNameOverride != null,
                    CallParameter =
                    {
                        ModelParameter = alt5.IsAlt5 ? alt5.Model : members.Select(n => n.GetText().Trim()).ToArray(),
                        RootReference = alt5.IsAlt5 ? alt5.RootRef : memberExpr?.ROOT_REF() != null,
                        ChainParameter = CreateChain(context.chain()?.call()),
                        CSharpExpression = csharpExpression?.GetText(),
                        NativeExpression = alt5.IsAlt5 ? alt5.Native : nativeExpression,
                        PropArguments = alt5.PropArguments
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