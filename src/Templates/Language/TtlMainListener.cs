using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Templates.Data;
using Templates.Exceptions;
using Templates.Runtime;
using Templates.Strings.Core;

namespace Templates.Language {
    internal sealed class TtlMainListener: TtlParserBaseListener {
        private readonly CompileContext _compileContext;
        private readonly Stack<ParseContext> _parserContextStack;

        public ParseContext CurrentParseContext => _parserContextStack.Peek();

        public TtlMainListener(ParseContext context, CompileContext compileContext) {
            _compileContext = compileContext;
            if (context == null) throw new ArgumentNullException(nameof(context));
            _parserContextStack = new Stack<ParseContext>();
            _parserContextStack.Push(context);
        }

        public override void EnterDefinition(TtlParser.DefinitionContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.InDefinition = true;
            CurrentParseContext.DefinitionsBlock.AddNewBlockPosition(CurrentParseContext.GetBlockPosition(context));
            if (CurrentParseContext.ProvideLanguageFeatures)
            {
                CurrentParseContext.AddToken(context.DEF_START(), TtlTokenType.DefStart);
            }
        }

        public override void ExitDefinition(TtlParser.DefinitionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.InDefinition = false;
            if (CurrentParseContext.ProvideLanguageFeatures)
            {
                CurrentParseContext.AddToken(context.DEF_CLOSE(), TtlTokenType.DefClose);
            }
        }

        public override void EnterDef(TtlParser.DefContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.CurrentDefenition = CurrentParseContext.CreateDefinition(context, _compileContext, out var chain);
            if (CurrentParseContext.CurrentDefenition == null)
            {
                _compileContext.CompileErrors.Add(
                    "Cannot create definition".ToError(CurrentParseContext.GetBlockPosition(context)));
                return;
            }
            if (CurrentParseContext.CurrentDefenition.BaseDefinition == null)
            {
                if (CurrentParseContext.DefinitionsBlock.Definitions.ContainsKey(CurrentParseContext.CurrentDefenition.Name))
                {
                    _compileContext.CompileErrors.Add(
                        $"The definition <{CurrentParseContext.CurrentDefenition.Name}> with the same name already exists".ToError(CurrentParseContext.GetBlockPosition(context)));
                    return;
                }
                CurrentParseContext.DefinitionsBlock.Definitions.Add(CurrentParseContext.CurrentDefenition.Name,
                    CurrentParseContext.CurrentDefenition);
            }
            if (chain != null)
            {
                CurrentParseContext.DefaultChains.Add(chain);
            }
        }

        public override void ExitDef(TtlParser.DefContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (CurrentParseContext.CurrentDefenition.BaseDefinition != null)
            {
                if (!CurrentParseContext.DefinitionsBlock.Definitions.ContainsKey(CurrentParseContext.CurrentDefenition.Name))
                {
                    CurrentParseContext.DefinitionsBlock.Definitions.Add(CurrentParseContext.CurrentDefenition.Name,
                        CurrentParseContext.CurrentDefenition);
                }
                else
                {
                    var definition =
                        CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name];
                    if (CurrentParseContext.InDefintionContext)
                    {
                        if (definition != CurrentParseContext.CurrentDefenition.BaseDefinition)
                        {
                            _compileContext.CompileErrors.Add(
                                $"The definition <{CurrentParseContext.CurrentDefenition.Name}> with the same name already exists".ToError(
                                    CurrentParseContext.GetBlockPosition(context)));
                            return;
                        }
                        CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name] =
                            CurrentParseContext.CurrentDefenition;
                    }
                    else
                    {
                        if (definition != CurrentParseContext.CurrentDefenition.BaseDefinition)
                        {
                            _compileContext.CompileErrors.Add(
                                $"The definition <{CurrentParseContext.CurrentDefenition.Name}> with the same name already exists".ToError(
                                    CurrentParseContext.GetBlockPosition(context)));
                            return;
                        }
                        CurrentParseContext.DefinitionsBlock.Definitions[CurrentParseContext.CurrentDefenition.Name].OverrideWith(
                            CurrentParseContext.CurrentDefenition);
                    }
                }
            }
            CurrentParseContext.CurrentDefenition = null;
        }

        public override void EnterOutblock(TtlParser.OutblockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            //if (!CurrentParseContext.DefenitionsOnly)
                CurrentParseContext.CurrentChain = CurrentParseContext.CreateOutputChain(context);
        }

        public override void ExitOutblock(TtlParser.OutblockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            //if (!CurrentParseContext.DefenitionsOnly) {
                CurrentParseContext.OutputChains.Add(CurrentParseContext.CurrentChain);
                CurrentParseContext.CurrentChain = null;
            //}
        }

        public override void EnterRaw(TtlParser.RawContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            CurrentParseContext.RawOutputItems.Add(CurrentParseContext.CreateRawOutputItem(context));
        }

        public override void EnterSubtemplate(TtlParser.SubtemplateContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var ttl = context.ttl();
            if (ttl?.Stop == null || ttl.Start == null)
                throw new ArgumentException();
            if (ttl.Stop.StopIndex - ttl.Start.StartIndex + 1 > 0)
            {
                var currentContext = CurrentParseContext;
                var newContext = new ParseContext(currentContext, ttl.Start.StartIndex);

                var currentOffset = currentContext.Offset;
                
                //transfer parent skipped tokens inside with offset
                newContext.SkippedTokens.AddRange(currentContext.SkippedTokens
                    .Where(t =>
                    {
                        var startIndex = t.StartIndex + currentOffset;
                        var stopIndex = startIndex + t.Length - 1;

                        return startIndex >= ttl.Start.StartIndex && stopIndex <= ttl.Stop.StopIndex;
                    })
                    .Select(t => new BlockPosition(t.StartIndex + currentOffset - ttl.Start.StartIndex, t.Length)));
                
                currentContext.AddSubContext(newContext);
                if (!currentContext.InDefintionContext && !currentContext.InDefinition)
                {
                    newContext = newContext.IsolateContextWithTree();
                }
                else {
                    if (currentContext.InDefinition && currentContext.CurrentDefenition != null &&
                        currentContext.CurrentDefenition.FullOverride) {
                        newContext = newContext.IsolateContextWithTree(currentContext.CurrentDefenition.Name);
                    }
                }
                _parserContextStack.Push(newContext);
            }
        }

        public override void ExitSubtemplate(TtlParser.SubtemplateContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var ttl = context.ttl();
            if (ttl?.Stop == null || ttl.Start == null)
                throw new ArgumentException();
            if (ttl.Stop.StopIndex - ttl.Start.StartIndex + 1 > 0)
            {
                if (_parserContextStack.Count <= 1)
                {
                    throw new TemplateParseException("Subtemplate close block appears too early.".ToError(CurrentParseContext.GetAbsoluteBlockPosition(context)));
                }
                var parserContext = _parserContextStack.Pop();
                if (CurrentParseContext.InDefinition)
                {
                    CurrentParseContext.CurrentDefenition.Context = parserContext;
                }
                else
                {
                    //if (!CurrentParseContext.DefenitionsOnly)
                    //{
                        var chainItem = CurrentParseContext.CurrentChain.Chain.First();
                        chainItem.Context = parserContext;
                    //}
                }
            }
        }

        public override void ExitImport_block(TtlParser.Import_blockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var path = string.Concat(context.text().Select(t => t.GetText()));
            if (!string.IsNullOrWhiteSpace(path))
            {
                using var file = File.OpenText(Path.Combine(_compileContext.Options.RootPath, path));
                string document = file.ReadToEnd();
                var isolatedContext = CurrentParseContext.IsolateContextWithTree();
                isolatedContext.OutputChains.Clear();
                DocumentParser.Parse(document, isolatedContext, _compileContext/*, true*/);
                CurrentParseContext.DefaultChains.Clear();
                CurrentParseContext.DefaultChains.AddRange(isolatedContext.DefaultChains);
                CurrentParseContext.DefinitionsBlock.Definitions.Clear();
                foreach (var definition in isolatedContext.DefinitionsBlock.Definitions)
                {
                    CurrentParseContext.DefinitionsBlock.Definitions.Add(definition.Key, definition.Value);
                }
                foreach (var isolatedChain in isolatedContext.OutputChains)
                {
                    isolatedChain.BlockPosition = new BlockPosition(CurrentParseContext.GetBlockPosition(context).StartIndex, 0);
                    CurrentParseContext.OutputChains.Add(isolatedChain);
                }
            }
            CurrentParseContext.DefinitionsBlock.AddNewBlockPosition(CurrentParseContext.GetBlockPosition(context));
        }
    }
}