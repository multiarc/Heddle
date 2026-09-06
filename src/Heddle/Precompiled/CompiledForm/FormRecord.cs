using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Heddle.Data;
using Heddle.Language;
using Heddle.Language.Expressions;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Heddle.Runtime.Parameters;
using Heddle.Strings.Core;

namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>A recorded item parameter. Only one shape is set; the converter maps each shape to the
    /// artifact's parameter vocabulary and refuses shapes the artifact cannot encode.</summary>
    internal abstract class FormParam
    {
    }

    internal sealed class FormNone : FormParam
    {
        internal static readonly FormNone Instance = new FormNone();

        private FormNone()
        {
        }
    }

    internal sealed class FormConst : FormParam
    {
        internal FormConst(object value)
        {
            Value = value;
        }

        internal object Value { get; }
    }

    internal sealed class FormMemberRef : FormParam
    {
        internal FormMemberRef(int memberIndex)
        {
            MemberIndex = memberIndex;
        }

        internal int MemberIndex { get; }
    }

    internal sealed class FormDynamicRef : FormParam
    {
        internal FormDynamicRef(int memberIndex, string[] segments)
        {
            MemberIndex = memberIndex;
            Segments = segments;
        }

        internal int MemberIndex { get; }

        internal string[] Segments { get; }
    }

    internal sealed class FormChainData : FormParam
    {
        internal FormChainData(List<FormItem> items)
        {
            Items = items;
        }

        internal List<FormItem> Items { get; }
    }

    internal sealed class FormExprUse : FormParam
    {
        internal FormExprUse(int expressionIndex)
        {
            ExpressionIndex = expressionIndex;
        }

        internal int ExpressionIndex { get; }
    }

    internal sealed class FormCSharpUse : FormParam
    {
        internal FormCSharpUse(int siteIndex)
        {
            SiteIndex = siteIndex;
        }

        internal int SiteIndex { get; }
    }

    internal sealed class FormPropsSlot : FormParam
    {
        internal FormPropsSlot(int slotIndex, List<(Type Declaring, string Name, Type Member)> prefix,
            string[] rest, bool dynamicRest)
        {
            SlotIndex = slotIndex;
            Prefix = prefix;
            Rest = rest;
            DynamicRest = dynamicRest;
        }

        internal int SlotIndex { get; }

        internal List<(Type Declaring, string Name, Type Member)> Prefix { get; }

        internal string[] Rest { get; }

        internal bool DynamicRest { get; }
    }

    internal sealed class FormRefusal : FormParam
    {
        internal FormRefusal(int refusalIndex)
        {
            RefusalIndex = refusalIndex;
        }

        internal int RefusalIndex { get; }
    }

    internal sealed class FormRefusalData
    {
        internal OutputItem Item;
        internal PrecompiledRefusalClass Class;
        internal string Detail;
        internal BlockPosition Position;
        internal ExType ModelType;
        internal ExType ChainedType;
        internal ExType RootType;
        internal List<string> Namespaces;
        internal int Depth;
        internal string SourceText;
    }

    internal sealed class FormBody
    {
        internal FormBody(string rawText, string shapedText, ExType dataType, ExType chainedType, int documentRef)
        {
            RawText = rawText;
            ShapedText = shapedText;
            DataType = dataType;
            ChainedType = chainedType;
            DocumentRef = documentRef;
        }

        internal string RawText { get; }

        internal string ShapedText { get; }

        internal ExType DataType { get; }

        internal ExType ChainedType { get; }

        internal int DocumentRef { get; }
    }

    internal sealed class FormDynSlot
    {
        internal FormDynSlot(int slotIndex, IRuntimeParameter parameter, ExType targetType)
        {
            SlotIndex = slotIndex;
            Parameter = parameter;
            TargetType = targetType;
        }

        internal int SlotIndex { get; }

        internal IRuntimeParameter Parameter { get; }

        internal ExType TargetType { get; }
    }

    internal sealed class FormProps
    {
        internal FormProps(object[] prototype, List<FormDynSlot> slots)
        {
            Prototype = prototype;
            Slots = slots;
        }

        internal object[] Prototype { get; }

        internal List<FormDynSlot> Slots { get; }
    }

    internal sealed class FormDefLink
    {
        internal FormDefLink(string definitionKey, int callerDocument, bool slotMode)
        {
            DefinitionKey = definitionKey;
            CallerDocument = callerDocument;
            SlotMode = slotMode;
        }

        internal string DefinitionKey { get; }

        internal int CallerDocument { get; }

        internal bool SlotMode { get; }
    }

    internal sealed class FormItem
    {
        internal FormItem(BlockPosition position, string parameterTemplate)
        {
            Position = position;
            ParameterTemplate = parameterTemplate;
        }

        internal BlockPosition Position { get; }

        internal string ParameterTemplate { get; }

        internal List<string> VisibleDefKeys = new List<string>();

        internal int ExtensionRef = -1;

        internal ExType ReturnType;

        internal FormParam Payload;

        internal FormBody Body;

        internal FormProps Props;

        internal FormDefLink DefLink;
    }

    internal sealed class FormElement
    {
        internal FormElement(BlockPosition position, List<TemplateItem> items)
        {
            Position = position;
            Items = items;
        }

        internal BlockPosition Position { get; }

        internal List<TemplateItem> Items { get; }
    }

    internal sealed class FormParseFacts
    {
        internal FormParseFacts(int offset, bool inDefinitionContext, List<string> visibleDefinitions)
        {
            Offset = offset;
            InDefinitionContext = inDefinitionContext;
            VisibleDefinitions = visibleDefinitions;
        }

        internal int Offset { get; }

        internal bool InDefinitionContext { get; }

        internal List<string> VisibleDefinitions { get; }
    }

    internal sealed class FormDocument
    {
        internal FormDocument(string shapedText, FormParseFacts facts, List<FormElement> elements)
        {
            ShapedText = shapedText;
            Facts = facts;
            Elements = elements;
        }

        internal string ShapedText { get; }

        internal bool NeedsLocals;

        internal FormParseFacts Facts { get; }

        internal List<FormElement> Elements { get; }
    }

    internal sealed class FormExprTree
    {
        internal FormExprTree(ExprNode tree, ExType modelType, ExType chainedType, ExType rootType,
            bool usesProps)
        {
            Tree = tree;
            ModelType = modelType;
            ChainedType = chainedType;
            RootType = rootType;
            UsesProps = usesProps;
        }

        internal ExprNode Tree { get; }

        internal ExType ModelType { get; }

        internal ExType ChainedType { get; }

        internal ExType RootType { get; }

        internal bool UsesProps { get; }

        internal bool Deferred;
    }

    internal sealed class FormCSharp
    {
        internal FormCSharp(string source, List<string> usings, ExType modelType, ExType chainedType,
            ExType rootType, BlockPosition position)
        {
            Source = source;
            Usings = usings;
            ModelType = modelType;
            ChainedType = chainedType;
            RootType = rootType;
            Position = position;
        }

        internal string Source { get; }

        internal List<string> Usings { get; }

        internal ExType ModelType { get; }

        internal ExType ChainedType { get; }

        internal ExType RootType { get; }

        internal BlockPosition Position { get; }
    }

    internal sealed class FormExtension
    {
        internal FormExtension(string name, Type type, string fingerprint)
        {
            Name = name;
            Type = type;
            Fingerprint = fingerprint;
        }

        internal string Name { get; }

        internal Type Type { get; }

        internal string Fingerprint { get; }
    }

    internal sealed class FormFunction
    {
        internal FormFunction(string name, Type targetType, int overloadCount)
        {
            Name = name;
            TargetType = targetType;
            OverloadCount = overloadCount;
        }

        internal string Name { get; }

        internal Type TargetType { get; }

        internal int OverloadCount { get; }
    }

    internal sealed class FormFunctionChoice
    {
        internal FormFunctionChoice(string name, Type targetType, int overloadCount)
        {
            Name = name;
            TargetType = targetType;
            OverloadCount = overloadCount;
        }

        internal string Name { get; }

        internal Type TargetType { get; }

        internal int OverloadCount { get; }
    }

    internal sealed class FormMemberRow
    {
        internal FormMemberRow(ExType startType, string[] segments, bool rootRef,
            List<(Type Declaring, string Name, Type Member)> hops)
        {
            StartType = startType;
            Segments = segments;
            RootRef = rootRef;
            Hops = hops;
        }

        internal ExType StartType { get; }

        internal string[] Segments { get; }

        internal bool RootRef { get; }

        internal List<(Type Declaring, string Name, Type Member)> Hops { get; }
    }

    internal sealed class FormPropDecl
    {
        internal FormPropDecl(string name, int index, ExType type, bool hasDefault, object defaultBoxed)
        {
            Name = name;
            Index = index;
            Type = type;
            HasDefault = hasDefault;
            DefaultBoxed = defaultBoxed;
        }

        internal string Name { get; }

        internal int Index { get; }

        internal ExType Type { get; }

        internal bool HasDefault { get; }

        internal object DefaultBoxed { get; }
    }

    internal sealed class FormDefinition
    {
        internal FormDefinition(string key, string name, string baseName, string spelling, Type resolvedType,
            BlockPosition position, string parameterTemplate)
        {
            Key = key;
            Name = name;
            BaseName = baseName;
            Spelling = spelling;
            ResolvedType = resolvedType;
            Position = position;
            ParameterTemplate = parameterTemplate;
        }

        internal string Key { get; }

        internal string Name { get; }

        internal string BaseName { get; }

        internal string Spelling { get; }

        internal Type ResolvedType { get; }

        internal BlockPosition Position { get; }

        internal string ParameterTemplate { get; }

        internal List<FormPropDecl> PropDecls;

        internal ExType SlotType;

        internal List<string> RegionNames;
    }

    /// <summary>The compile-time form record: facts the runtime objects do not expose, gathered while a
    /// text compile runs with recording enabled. One instance is shared across a compile's child contexts;
    /// every hook costs a single null check when recording is off, because callers check for a null
    /// record before touching this type.</summary>
    internal sealed class FormRecord
    {
        private readonly Dictionary<OutputItem, FormItem> _items = new Dictionary<OutputItem, FormItem>();
        private readonly Dictionary<TemplateItem, FormItem> _byTemplate =
            new Dictionary<TemplateItem, FormItem>();
        private readonly Dictionary<IRuntimeParameter, FormParam> _params =
            new Dictionary<IRuntimeParameter, FormParam>();
        private readonly Dictionary<CallNode, FormFunctionChoice> _calls =
            new Dictionary<CallNode, FormFunctionChoice>();
        private readonly Dictionary<RuntimeDocument, int> _documentsByInstance =
            new Dictionary<RuntimeDocument, int>();
        private readonly Dictionary<string, FormDefinition> _definitions =
            new Dictionary<string, FormDefinition>(StringComparer.Ordinal);
        private readonly Stack<OutputItem> _itemStack = new Stack<OutputItem>();
        private readonly Dictionary<OutputItem, List<string>> _deferredCalls =
            new Dictionary<OutputItem, List<string>>();
        private readonly Dictionary<OutputItem, List<OutputItem>> _itemChildren =
            new Dictionary<OutputItem, List<OutputItem>>();
        private readonly List<FormRefusalData> _refusals = new List<FormRefusalData>();
        private readonly List<FormExtension> _extensions = new List<FormExtension>();
        private readonly List<FormFunction> _functions = new List<FormFunction>();
        private readonly List<FormMemberRow> _members = new List<FormMemberRow>();
        private readonly List<FormExprTree> _trees = new List<FormExprTree>();
        private readonly List<FormCSharp> _csharp = new List<FormCSharp>();
        private readonly List<FormDocument> _documents = new List<FormDocument>();
        private int _depth;
        private int _rootDocument = -1;

        internal static string DefinitionKey(DefinitionItem definition) =>
            definition.Name + "@" + definition.Position;

        internal int DocCount => _documents.Count;

        internal int RootDocument => _rootDocument;

        internal FormItem BeginItem(OutputItem item, ParseContext context)
        {
            FormItem form;
            if (!_items.TryGetValue(item, out form))
            {
                form = new FormItem(item.Position, item.ParameterTemplate);
                foreach (var key in context.DefinitionsBlock.Definitions.Keys)
                    form.VisibleDefKeys.Add(key);
                _items.Add(item, form);
            }

            return form;
        }

        internal void EndItem(OutputItem item, TemplateItem compiled)
        {
            FormItem form;
            if (!_items.TryGetValue(item, out form))
                return;
            form.ReturnType = compiled.ReturnType;
            if (compiled != null)
                _byTemplate[compiled] = form;
            if (form.Payload == null && compiled.Parameter != null)
                _params.TryGetValue(compiled.Parameter, out form.Payload);
        }

        internal void AttachParam(OutputItem item, IRuntimeParameter parameter)
        {
            if (parameter == null)
                return;
            FormItem form;
            if (!_items.TryGetValue(item, out form) || form.Payload != null)
                return;
            _params.TryGetValue(parameter, out form.Payload);
        }

        internal void RecordExpressionParameter(IRuntimeParameter parameter, ExprNode tree, ExType modelType,
            ExType chainedType, ExType rootType, bool usesProps)
        {
            if (parameter == null || tree == null)
                return;
            _trees.Add(new FormExprTree(tree, modelType, chainedType, rootType, usesProps));
            _params[parameter] = new FormExprUse(_trees.Count - 1);
        }

        internal void RecordConstantParameter(IRuntimeParameter parameter, object value)
        {
            if (parameter == null)
                return;
            _params[parameter] = new FormConst(value);
        }

        internal void RecordNoneParameter(IRuntimeParameter parameter)
        {
            if (parameter == null)
                return;
            _params[parameter] = FormNone.Instance;
        }

        internal void RecordFunctionChoice(CallNode call, Type targetType, int overloadCount)
        {
            if (call == null)
                return;
            _calls[call] = new FormFunctionChoice(call.Name, targetType, overloadCount);
        }

        internal void RecordDeferredExpression(IRuntimeParameter parameter, ExprNode tree, ExType modelType,
            ExType chainedType, ExType rootType)
        {
            if (parameter == null || tree == null)
                return;
            var recorded = new FormExprTree(tree, modelType, chainedType, rootType, false);
            recorded.Deferred = true;
            _trees.Add(recorded);
            _params[parameter] = new FormExprUse(_trees.Count - 1);
        }

        internal void PushItem(OutputItem item)
        {
            if (_itemStack.Count != 0)
            {
                var parent = _itemStack.Peek();
                List<OutputItem> children;
                if (!_itemChildren.TryGetValue(parent, out children))
                {
                    children = new List<OutputItem>();
                    _itemChildren.Add(parent, children);
                }

                if (!children.Contains(item))
                    children.Add(item);
            }

            _itemStack.Push(item);
        }

        internal void PopItem()
        {
            if (_itemStack.Count != 0)
                _itemStack.Pop();
        }

        internal void NoteDeferredCall(CallNode call)
        {
            if (call == null || _itemStack.Count == 0)
                return;
            var item = _itemStack.Peek();
            List<string> names;
            if (!_deferredCalls.TryGetValue(item, out names))
            {
                names = new List<string>();
                _deferredCalls.Add(item, names);
            }

            if (!names.Contains(call.Name))
                names.Add(call.Name);
        }

        internal List<string> TakeDeferredNames(OutputItem item)
        {
            var collected = new List<string>();
            CollectDeferredNames(item, collected);
            return collected;
        }

        private void CollectDeferredNames(OutputItem item, List<string> collected)
        {
            if (item == null)
                return;
            List<string> names;
            if (_deferredCalls.TryGetValue(item, out names))
            {
                _deferredCalls.Remove(item);
                foreach (var name in names)
                    if (!collected.Contains(name))
                        collected.Add(name);
            }

            List<OutputItem> children;
            if (_itemChildren.TryGetValue(item, out children))
                foreach (var child in children)
                    CollectDeferredNames(child, collected);
        }

        internal int NoteRefusal(OutputItem item, PrecompiledRefusalClass refusalClass, string detail,
            ExType modelType, ExType chainedType, ExType rootType, List<string> namespaces)
        {
            if (item == null)
                return -1;
            var data = new FormRefusalData
            {
                Item = item,
                Class = refusalClass,
                Detail = detail ?? string.Empty,
                Position = item.Position,
                ModelType = modelType,
                ChainedType = chainedType,
                RootType = rootType,
                Namespaces = namespaces ?? new List<string>(),
                Depth = _depth
            };
            _refusals.Add(data);
            return _refusals.Count - 1;
        }

        internal int? FindRefusal(OutputItem item)
        {
            if (item == null)
                return null;
            for (int i = 0; i < _refusals.Count; i++)
                if (ReferenceEquals(_refusals[i].Item, item))
                    return i;
            return null;
        }

        internal void AttachFunctionRow(CallNode call)
        {
            FormFunctionChoice choice;
            if (call == null || !_calls.TryGetValue(call, out choice))
                return;
            GetOrAddFunction(choice.Name, choice.TargetType, choice.OverloadCount);
        }

        internal int GetOrAddExtension(string name, Type type, string fingerprint)
        {
            for (int i = 0; i < _extensions.Count; i++)
                if (_extensions[i].Name == name && _extensions[i].Type == type)
                    return i;
            _extensions.Add(new FormExtension(name, type, fingerprint));
            return _extensions.Count - 1;
        }

        internal void SetItemExtension(OutputItem item, int extensionRef)
        {
            FormItem form;
            if (_items.TryGetValue(item, out form))
                form.ExtensionRef = extensionRef;
        }

        internal void SetItemPayload(OutputItem item, FormParam payload)
        {
            FormItem form;
            if (_items.TryGetValue(item, out form))
                form.Payload = payload;
        }

        internal int GetOrAddFunction(string name, Type targetType, int overloadCount)
        {
            for (int i = 0; i < _functions.Count; i++)
                if (_functions[i].Name == name && _functions[i].TargetType == targetType)
                    return i;
            _functions.Add(new FormFunction(name, targetType, overloadCount));
            return _functions.Count - 1;
        }

        internal int GetOrAddMember(ExType startType, string[] segments, bool rootRef,
            List<(Type Declaring, string Name, Type Member)> hops)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                var existing = _members[i];
                if (existing.RootRef == rootRef && ExTypeEquals(existing.StartType, startType) &&
                    SegmentsEqual(existing.Segments, segments) && HopsEqual(existing.Hops, hops))
                    return i;
            }

            _members.Add(new FormMemberRow(startType, segments, rootRef, hops));
            return _members.Count - 1;
        }

        internal int RecordDynamicMember(ExType startType, string[] segments, bool rootRef,
            List<(Type Declaring, string Name, Type Member)> prefix)
        {
            var hops = new List<(Type Declaring, string Name, Type Member)>(prefix);
            for (int i = prefix.Count; i < segments.Length; i++)
                hops.Add((null, segments[i], null));
            return GetOrAddMember(startType, segments, rootRef, hops);
        }

        private static bool HopsEqual(List<(Type Declaring, string Name, Type Member)> first,
            List<(Type Declaring, string Name, Type Member)> second)
        {
            if (first.Count != second.Count)
                return false;
            for (int i = 0; i < first.Count; i++)
                if (first[i].Declaring != second[i].Declaring || first[i].Name != second[i].Name ||
                    first[i].Member != second[i].Member)
                    return false;
            return true;
        }

        private static bool ExTypeEquals(ExType first, ExType second)
        {
            if (first == null || second == null)
                return first == null && second == null;
            return first.Equals(second);
        }

        private static bool SegmentsEqual(string[] first, string[] second)
        {
            if (first.Length != second.Length)
                return false;
            for (int i = 0; i < first.Length; i++)
                if (first[i] != second[i])
                    return false;
            return true;
        }

        internal bool TryGetExprIndex(IRuntimeParameter parameter, out int index)
        {
            index = -1;
            FormParam payload;
            if (parameter == null || !_params.TryGetValue(parameter, out payload))
                return false;
            var use = payload as FormExprUse;
            if (use == null)
                return false;
            index = use.ExpressionIndex;
            return true;
        }

        internal void EnterBody()
        {
            _depth++;
        }

        internal void ExitBody()
        {
            _depth--;
        }

        internal FormDocument BeginDocument(ParseContext parseContext, string shapedText,
            List<DocumentElement> elements)
        {
            var definitions = new List<string>(parseContext.DefinitionsBlock.Definitions.Keys);
            var facts = new FormParseFacts(parseContext.Offset, parseContext.InDefintionContext, definitions);
            var recorded = new List<FormElement>(elements.Count);
            foreach (var element in elements)
            {
                var items = new List<TemplateItem>(element.CallChain.Count);
                for (int i = 0; i < element.CallChain.Count; i++)
                    items.Add(element.CallChain.ItemsToExecute[i]);
                recorded.Add(new FormElement(element.Position, items));
            }

            return new FormDocument(shapedText, facts, recorded);
        }

        internal int EndDocument(FormDocument document, RuntimeDocument runtime)
        {
            document.NeedsLocals = runtime.NeedsLocals;
            _documents.Add(document);
            int index = _documents.Count - 1;
            _documentsByInstance[runtime] = index;
            if (_depth == 1 && _rootDocument < 0)
                _rootDocument = index;
            ResolveRefusals(document);
            return index;
        }

        private void ResolveRefusals(FormDocument document)
        {
            string shaped = document.ShapedText ?? string.Empty;
            foreach (var refusal in _refusals)
            {
                if (refusal.SourceText != null || refusal.Depth != _depth)
                    continue;
                refusal.SourceText = SliceRefusal(shaped, refusal);
            }
        }

        private string SliceRefusal(string shaped, FormRefusalData refusal)
        {
            string body = null;
            string name = string.Empty;
            if (refusal.Item != null)
            {
                name = refusal.Item.ExtensionName ?? string.Empty;
                FormItem form;
                if (_items.TryGetValue(refusal.Item, out form))
                    body = form.ParameterTemplate;
                int start = refusal.Item.Position.StartIndex;
                int length = refusal.Item.Position.Length;
                if (start >= 0 && length > 0 && start + length <= shaped.Length)
                {
                    string slice = shaped.Substring(start, length);
                    bool covers = !string.IsNullOrEmpty(body) ? slice.Contains(body) :
                        slice.Contains(name);
                    if (covers)
                        return slice;
                    // Item spans cover the call (name plus data); a bodied item's body abuts after
                    // the span, so the slice alone never contains it. Rebuild the full call-site
                    // source from the sliced call plus the recorded body — without the data part the
                    // fragment would not even parse (a bare `@list{{...}}` names no data).
                    if (!string.IsNullOrEmpty(body) && slice.Contains(name))
                        return "@" + slice + "{{" + body + "}}";
                }
            }

            if (!string.IsNullOrEmpty(body))
                return "@" + name + "{{" + body + "}}";
            return "@" + name;
        }

        internal int GetDocIndex(RuntimeDocument runtime)
        {
            int index;
            return runtime != null && _documentsByInstance.TryGetValue(runtime, out index) ? index : -1;
        }

        internal int AppendSynthesizedDocument(ParseContext parseContext)
        {
            var facts = new FormParseFacts(parseContext.Offset, parseContext.InDefintionContext,
                new List<string>(parseContext.DefinitionsBlock.Definitions.Keys));
            var document = new FormDocument(string.Empty, facts, new List<FormElement>());
            _documents.Add(document);
            return _documents.Count - 1;
        }

        internal void RecordBody(OutputItem item, string rawText, string shapedText, ExType dataType,
            ExType chainedType, int documentRef)
        {
            FormItem form;
            if (!_items.TryGetValue(item, out form))
                return;
            form.Body = new FormBody(rawText, shapedText, dataType, chainedType, documentRef);
        }

        internal void RecordDefinition(DefinitionItem definition, Type acceptType)
        {
            var key = DefinitionKey(definition);
            if (_definitions.ContainsKey(key))
                return;
            _definitions.Add(key, new FormDefinition(key, definition.Name,
                definition.BaseDefinition?.Name, definition.ModelType, acceptType, definition.Position,
                definition.ParameterTemplate));
        }

        internal void AttachLayout(DefinitionItem definition, PropLayout layout, ExType slotType)
        {
            FormDefinition form;
            if (!_definitions.TryGetValue(DefinitionKey(definition), out form) || form.PropDecls != null)
                return;
            var decls = new List<FormPropDecl>(layout.Slots.Count);
            foreach (var slot in layout.Slots)
                decls.Add(new FormPropDecl(slot.Name, slot.Index, slot.Type, slot.HasDefault,
                    slot.DefaultBoxed));
            form.PropDecls = decls;
            form.SlotType = slotType;
        }

        internal void AttachRegions(DefinitionItem definition, List<string> regionNames)
        {
            FormDefinition form;
            if (!_definitions.TryGetValue(DefinitionKey(definition), out form) || form.RegionNames != null)
                return;
            form.RegionNames = regionNames;
        }

        internal void SetDefLink(OutputItem item, string definitionKey, int callerDocument, bool slotMode)
        {
            FormItem form;
            if (_items.TryGetValue(item, out form))
                form.DefLink = new FormDefLink(definitionKey, callerDocument, slotMode);
        }

        internal void RecordProps(OutputItem item, object[] prototype, List<FormDynSlot> slots)
        {
            FormItem form;
            if (!_items.TryGetValue(item, out form))
                return;
            form.Props = new FormProps(prototype, slots);
        }

        internal int AppendCSharp(string source, List<string> usings, ExType modelType, ExType chainedType,
            ExType rootType, BlockPosition position)
        {
            _csharp.Add(new FormCSharp(source, usings, modelType, chainedType, rootType, position));
            return _csharp.Count - 1;
        }

        internal bool TryGetItem(OutputItem item, out FormItem form) => _items.TryGetValue(item, out form);

        internal FormChainData BuildChain(TemplateChain chain)
        {
            var items = new List<FormItem>(chain.Count);
            for (int i = 0; i < chain.Count; i++)
            {
                FormItem form;
                if (!_byTemplate.TryGetValue(chain.ItemsToExecute[i], out form) || form == null)
                    throw Refuse("a chain parameter item was not recorded");
                items.Add(form);
            }

            return new FormChainData(items);
        }

        private static InvalidOperationException Refuse(string what) =>
            new InvalidOperationException("Cannot encode the compiled form: " + what + ".");

        internal CompiledArtifact ToArtifact(string engineVersion, string builderVersion, string key,
            string contentHash, string registeredName, string entryPointTypeName, ExType modelType,
            bool modelTypeIsAmbient, bool isDynamic, string profile, string mode, bool trim)
        {
            if (engineVersion == null)
                throw new ArgumentNullException(nameof(engineVersion));
            if (builderVersion == null)
                throw new ArgumentNullException(nameof(builderVersion));
            if (_rootDocument < 0)
                throw Refuse("no root document was recorded");
            var artifact = new CompiledArtifact
            {
                Header = new CompiledHeader
                {
                    EngineVersion = engineVersion,
                    BuilderVersion = builderVersion,
                    ExpressionMode = mode ?? string.Empty,
                    TrimDirectiveLines = trim,
                    DefaultOutputProfile = profile
                }
            };
            foreach (var extension in _extensions)
            {
                artifact.Extensions.Add(new CompiledExtensionRow
                {
                    RegistryName = extension.Name,
                    Type = ToTypeRef(extension.Type),
                    Fingerprint = extension.Fingerprint
                });
            }

            foreach (var function in _functions)
            {
                artifact.Functions.Add(new CompiledFunctionRow
                {
                    Name = function.Name,
                    Target = function.TargetType == null ? null : ToTypeRef(function.TargetType),
                    OverloadCount = function.OverloadCount
                });
            }

            foreach (var member in _members)
                artifact.Members.Add(ConvertMember(member));
            foreach (var tree in _trees)
            {
                artifact.Expressions.Add(new CompiledExpressionTree
                {
                    Root = ToExpression(tree.Tree),
                    ModelType = ToTypeRef(tree.ModelType),
                    ChainedType = ToTypeRef(tree.ChainedType),
                    RootType = ToTypeRef(tree.RootType),
                    ContainsDeferredCall = tree.Deferred
                });
            }

            foreach (var site in _csharp)
            {
                artifact.CSharpSites.Add(new CompiledCSharpSite
                {
                    Source = site.Source,
                    Usings = site.Usings,
                    ModelType = ToTypeRef(site.ModelType),
                    ChainedType = ToTypeRef(site.ChainedType),
                    RootType = ToTypeRef(site.RootType),
                    Position = ToPosition(site.Position)
                });
            }

            ConvertDocuments(artifact);
            ConvertDefinitions(artifact);
            ConvertTemplate(artifact, key, contentHash, registeredName, entryPointTypeName, modelType,
                modelTypeIsAmbient, isDynamic, profile, mode, trim);
            return artifact;
        }

        private CompiledMemberRow ConvertMember(FormMemberRow member)
        {
            var row = new CompiledMemberRow { StartType = ToTypeRef(member.StartType) };
            if (row.StartType == null)
                throw Refuse("a member path has no start type");
            foreach (var segment in member.Segments)
                row.Segments.Add(segment);
            foreach (var hop in member.Hops)
            {
                row.Hops.Add(new CompiledMemberHop
                {
                    DeclaringType = ToTypeRef(hop.Declaring, hop.Name),
                    MemberName = hop.Name,
                    MemberType = ToTypeRef(hop.Member, hop.Name)
                });
            }

            if (row.Hops.Count != row.Segments.Count)
                throw Refuse("a member path's hops do not match its segments");
            return row;
        }

        internal static CompiledTypeRef ToTypeRef(ExType type)
        {
            if (type == null || type.Type == null || DeferredResult.IsDeferred(type))
                return null;
            if (type.IsDynamic)
                return DynamicTypeRef.Instance;
            return ToTypeRef(type.Type);
        }

        private static CompiledTypeRef ToTypeRef(Type type, string member = null)
        {
            if (type == null)
                return null;
            if (type.IsArray)
                return new ArrayTypeRef(ToTypeRef(type.GetElementType(), member), type.GetArrayRank());
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var definition = type.GetGenericTypeDefinition();
                var arguments = type.GetGenericArguments();
                var converted = new CompiledTypeRef[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                    converted[i] = ToTypeRef(arguments[i], member);
                return new GenericTypeRef(ToNamed(definition, member), converted);
            }

            return ToNamed(type, member);
        }

        private static NamedTypeRef ToNamed(Type type, string member)
        {
            if (type.IsGenericParameter || type.IsByRef || type.IsPointer || type.FullName == null)
                throw Refuse("type '" + type + "' has no nominal form" + (member == null ? "" : " at '" + member + "'"));
            string assemblyName;
            try
            {
                assemblyName = type.Assembly.GetName().Name;
            }
            catch (Exception)
            {
                throw Refuse("the assembly of type '" + type.FullName + "' is not readable");
            }

            return new NamedTypeRef(type.FullName, assemblyName, IsFramework(type, assemblyName));
        }

        private static bool IsFramework(Type type, string assemblyName)
        {
            if (assemblyName == "Heddle" || assemblyName == "Heddle.Language" ||
                assemblyName == "Antlr4.Runtime.Standard" ||
                assemblyName.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
                return false;
            string location;
            try
            {
                location = type.Assembly.Location;
            }
            catch (Exception)
            {
                return false;
            }

            if (string.IsNullOrEmpty(location))
                return false;
            try
            {
                if (location.StartsWith(RuntimeEnvironment.GetRuntimeDirectory(),
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch (Exception)
            {
            }

            try
            {
                var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
                if (trusted == null)
                    return false;
                foreach (var candidate in trusted.Split(';'))
                    if (string.Equals(candidate.Trim(), location, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static CompiledLiteral ToLiteral(object value)
        {
            var literal = new CompiledLiteral();
            if (value == null)
            {
                literal.Kind = CompiledLiteralKind.Null;
                return literal;
            }

            var type = value.GetType();
            if (type == typeof(int))
            {
                literal.Kind = CompiledLiteralKind.Int64;
                literal.Int64 = (int)value;
            }
            else if (type == typeof(long))
            {
                literal.Kind = CompiledLiteralKind.Int64;
                literal.Int64 = (long)value;
            }
            else if (type == typeof(short) || type == typeof(sbyte) || type == typeof(byte))
            {
                literal.Kind = CompiledLiteralKind.Int64;
                literal.Int64 = Convert.ToInt64(value);
            }
            else if (type == typeof(uint) || type == typeof(ushort))
            {
                literal.Kind = CompiledLiteralKind.UInt64;
                literal.UInt64 = Convert.ToUInt64(value);
            }
            else if (type == typeof(ulong))
            {
                literal.Kind = CompiledLiteralKind.UInt64;
                literal.UInt64 = (ulong)value;
            }
            else if (type == typeof(float))
            {
                literal.Kind = CompiledLiteralKind.Float;
                literal.Float32 = (float)value;
            }
            else if (type == typeof(double))
            {
                literal.Kind = CompiledLiteralKind.Double;
                literal.Double = (double)value;
            }
            else if (type == typeof(decimal))
            {
                literal.Kind = CompiledLiteralKind.Decimal;
                literal.Decimal = (decimal)value;
            }
            else if (type == typeof(bool))
            {
                literal.Kind = CompiledLiteralKind.Boolean;
                literal.Boolean = (bool)value;
            }
            else if (type == typeof(string))
            {
                literal.Kind = CompiledLiteralKind.String;
                literal.Text = (string)value;
            }
            else if (type == typeof(char))
            {
                literal.Kind = CompiledLiteralKind.Char;
                literal.CharCode = (char)value;
            }
            else
            {
                throw Refuse("a constant of type '" + type + "' has no literal form");
            }

            return literal;
        }

        private static CompiledPosition ToPosition(BlockPosition position) =>
            new CompiledPosition(position.StartIndex, position.Length);

        private static CompiledExprOperator ToOperator(ExprOperator op)
        {
            switch (op)
            {
                case ExprOperator.Add: return CompiledExprOperator.Add;
                case ExprOperator.Subtract: return CompiledExprOperator.Subtract;
                case ExprOperator.Multiply: return CompiledExprOperator.Multiply;
                case ExprOperator.Divide: return CompiledExprOperator.Divide;
                case ExprOperator.Modulo: return CompiledExprOperator.Modulo;
                case ExprOperator.LeftShift: return CompiledExprOperator.LeftShift;
                case ExprOperator.RightShift: return CompiledExprOperator.RightShift;
                case ExprOperator.LessThan: return CompiledExprOperator.LessThan;
                case ExprOperator.LessThanOrEqual: return CompiledExprOperator.LessThanOrEqual;
                case ExprOperator.GreaterThan: return CompiledExprOperator.GreaterThan;
                case ExprOperator.GreaterThanOrEqual: return CompiledExprOperator.GreaterThanOrEqual;
                case ExprOperator.Equal: return CompiledExprOperator.Equal;
                case ExprOperator.NotEqual: return CompiledExprOperator.NotEqual;
                case ExprOperator.And: return CompiledExprOperator.And;
                case ExprOperator.ExclusiveOr: return CompiledExprOperator.ExclusiveOr;
                case ExprOperator.Or: return CompiledExprOperator.Or;
                case ExprOperator.AndAlso: return CompiledExprOperator.AndAlso;
                case ExprOperator.OrElse: return CompiledExprOperator.OrElse;
                case ExprOperator.Coalesce: return CompiledExprOperator.Coalesce;
                case ExprOperator.Not: return CompiledExprOperator.Not;
                case ExprOperator.Negate: return CompiledExprOperator.Negate;
                case ExprOperator.UnaryPlus: return CompiledExprOperator.UnaryPlus;
                case ExprOperator.OnesComplement: return CompiledExprOperator.OnesComplement;
                default: throw Refuse("an expression operator has no stored form");
            }
        }

        private static CompiledExpression ToExpression(ExprNode node)
        {
            if (node == null)
                throw Refuse("an expression node is missing");
            var literal = node as LiteralNode;
            if (literal != null)
            {
                if (literal.LiteralError != null)
                    throw Refuse("an error literal cannot be encoded");
                return new CompiledExpression
                {
                    Kind = CompiledExprKind.Literal,
                    Position = ToPosition(literal.Position),
                    Literal = ToLiteral(literal.Value)
                };
            }

            if (node is ThisNode)
            {
                return new CompiledExpression
                {
                    Kind = CompiledExprKind.This,
                    Position = ToPosition(node.Position)
                };
            }

            var path = node as PathNode;
            if (path != null)
            {
                var converted = new CompiledExpression
                {
                    Kind = CompiledExprKind.Path,
                    Position = ToPosition(path.Position),
                    RootRef = path.RootRef,
                    Target = path.Target == null ? null : ToExpression(path.Target)
                };
                foreach (var segment in path.Segments)
                    converted.Segments.Add(segment);
                return converted;
            }

            var index = node as IndexNode;
            if (index != null)
            {
                var converted = new CompiledExpression
                {
                    Kind = CompiledExprKind.Index,
                    Position = ToPosition(index.Position),
                    Target = ToExpression(index.Target)
                };
                foreach (var argument in index.Arguments)
                    converted.Arguments.Add(ToExpression(argument));
                return converted;
            }

            var call = node as CallNode;
            if (call != null)
            {
                var converted = new CompiledExpression
                {
                    Kind = CompiledExprKind.Call,
                    Position = ToPosition(call.Position),
                    Name = call.Name
                };
                foreach (var argument in call.Arguments)
                    converted.Arguments.Add(ToExpression(argument));
                return converted;
            }

            var unary = node as UnaryNode;
            if (unary != null)
            {
                return new CompiledExpression
                {
                    Kind = CompiledExprKind.Unary,
                    Position = ToPosition(unary.Position),
                    Operator = ToOperator(unary.Operator),
                    Operand = ToExpression(unary.Operand)
                };
            }

            var binary = node as BinaryNode;
            if (binary != null)
            {
                return new CompiledExpression
                {
                    Kind = CompiledExprKind.Binary,
                    Position = ToPosition(binary.Position),
                    Operator = ToOperator(binary.Operator),
                    Left = ToExpression(binary.Left),
                    Right = ToExpression(binary.Right)
                };
            }

            var ternary = node as TernaryNode;
            if (ternary != null)
            {
                return new CompiledExpression
                {
                    Kind = CompiledExprKind.Ternary,
                    Position = ToPosition(ternary.Position),
                    Condition = ToExpression(ternary.Condition),
                    WhenTrue = ToExpression(ternary.WhenTrue),
                    WhenFalse = ToExpression(ternary.WhenFalse)
                };
            }

            var method = node as MethodCallNode;
            if (method != null)
            {
                var converted = new CompiledExpression
                {
                    Kind = CompiledExprKind.MethodCall,
                    Position = ToPosition(method.Position),
                    Target = ToExpression(method.Target),
                    Name = method.Name
                };
                foreach (var argument in method.Arguments)
                    converted.Arguments.Add(ToExpression(argument));
                return converted;
            }

            throw Refuse("an expression node of type '" + node.GetType() + "' has no stored form");
        }

        private void ConvertDocuments(CompiledArtifact artifact)
        {
            foreach (var document in _documents)
            {
                var converted = new CompiledDocument
                {
                    ShapedText = document.ShapedText,
                    NeedsLocals = document.NeedsLocals,
                    ParseFacts = ConvertParseFacts(document.Facts, document)
                };
                int cursor = 0;
                foreach (var element in document.Elements)
                {
                    if (element.Position.StartIndex < cursor)
                        throw Refuse("a document element overlaps its predecessor");
                    if (element.Position.StartIndex > cursor)
                    {
                        if (element.Position.StartIndex > document.ShapedText.Length)
                            throw Refuse("a document element starts past the shaped text");
                        converted.Elements.Add(new CompiledElement
                        {
                            IsChain = false,
                            StaticPiece = document.ShapedText.Substring(cursor,
                                element.Position.StartIndex - cursor)
                        });
                        cursor = element.Position.StartIndex;
                    }

                    var chain = new CompiledChain();
                    foreach (var item in element.Items)
                        chain.Items.Add(ConvertItem(item));
                    converted.Elements.Add(new CompiledElement { IsChain = true, Chain = chain });
                    cursor = element.Position.StartIndex + element.Position.Length;
                    if (cursor > document.ShapedText.Length)
                        throw Refuse("a document element ends past the shaped text");
                }

                if (cursor < document.ShapedText.Length)
                {
                    converted.Elements.Add(new CompiledElement
                    {
                        IsChain = false,
                        StaticPiece = document.ShapedText.Substring(cursor)
                    });
                }

                artifact.Documents.Add(converted);
            }
        }

        private CompiledParseFacts ConvertParseFacts(FormParseFacts facts, FormDocument document)
        {
            var converted = new CompiledParseFacts
            {
                Offset = facts.Offset,
                InDefinitionContext = facts.InDefinitionContext
            };
            var names = new List<string>(facts.VisibleDefinitions);
            foreach (var element in document.Elements)
            {
                foreach (var templateItem in element.Items)
                {
                    FormItem formItem;
                    if (_byTemplate.TryGetValue(templateItem, out formItem) && formItem != null)
                        CollectItemKeys(formItem, names);
                }
            }

            foreach (var name in names)
            {
                int definitionRef;
                if (!TryDefinitionRefByName(name, out definitionRef))
                    continue;
                converted.VisibleDefinitionRefs.Add(definitionRef);
            }

            return converted;
        }

        private static void CollectItemKeys(FormItem item, List<string> names)
        {
            foreach (var key in item.VisibleDefKeys)
                if (!names.Contains(key))
                    names.Add(key);
            var chain = item.Payload as FormChainData;
            if (chain == null)
                return;
            foreach (var nested in chain.Items)
                CollectItemKeys(nested, names);
        }

        /// <summary>Resolves a visible definition name to its recorded index. A name that was visible
        /// but never called has no recorded body and resolves to false: it needs no artifact entry —
        /// nothing references it — while its name stays available through the document's parse facts.
        /// Recording it would compile dead code into the artifact (and surface dead-code errors as build
        /// errors). A called definition is always recorded by the definition path in
        /// <c>HeddleCompiler.CreateExtension</c>, so a false here never hides a live call site.</summary>
        private bool TryDefinitionRefByName(string name, out int index)
        {
            foreach (var definition in _definitions.Values)
                if (definition.Name == name)
                {
                    index = DefinitionIndex(definition.Key);
                    return true;
                }

            index = -1;
            return false;
        }

        private int DefinitionIndex(string key)
        {
            int index = 0;
            foreach (var definition in _definitions.Values)
            {
                if (definition.Key == key)
                    return index;
                index++;
            }

            throw Refuse("a definition reference was not recorded");
        }

        private CompiledItem ConvertItem(TemplateItem item)
        {
            FormItem form;
            if (!_byTemplate.TryGetValue(item, out form) || form == null)
                throw Refuse("a chain item was not recorded");
            return ConvertFormItem(form);
        }

        private CompiledItem ConvertFormItem(FormItem form)
        {
            if (form.ExtensionRef < 0)
                throw Refuse("a chain item at " + form.Position + " has no extension");
            if (form.ReturnType == null)
                throw Refuse("a chain item at " + form.Position + " has no return type");
            var converted = new CompiledItem
            {
                ExtensionRef = form.ExtensionRef,
                Position = ToPosition(form.Position),
                ReturnType = ToTypeRef(form.ReturnType),
                ParameterTemplate = form.ParameterTemplate,
                Parameter = ConvertParam(form)
            };
            if (form.Body != null)
            {
                converted.Body = new CompiledBody
                {
                    RawText = form.Body.RawText,
                    ShapedText = form.Body.ShapedText,
                    DataType = ToTypeRef(form.Body.DataType),
                    ChainedType = ToTypeRef(form.Body.ChainedType)
                };
                if (form.Body.DocumentRef >= 0)
                    converted.Body.CompiledDocumentRef = form.Body.DocumentRef;
            }

            if (form.Props != null)
                converted.Props = ConvertProps(form.Props);
            if (form.DefLink != null)
            {
                converted.Parameter = new CompiledParameter
                {
                    Kind = CompiledParameterKind.DefinitionCall,
                    DefinitionRef = DefinitionIndex(form.DefLink.DefinitionKey),
                    CallerContentRef = EnsureCallerDocument(form.DefLink.CallerDocument),
                    SlotMode = form.DefLink.SlotMode
                };
            }

            return converted;
        }

        private int EnsureCallerDocument(int callerDocument)
        {
            if (callerDocument >= 0)
                return callerDocument;
            throw Refuse("a definition call has no caller-content document");
        }

        private CompiledParameter ConvertParam(FormItem form)
        {
            var payload = form.Payload;
            if (payload == null)
                throw Refuse("a chain item at " + form.Position + " has no parameter");
            if (payload is FormNone)
                return new CompiledParameter { Kind = CompiledParameterKind.None };
            var constant = payload as FormConst;
            if (constant != null)
            {
                return new CompiledParameter
                {
                    Kind = CompiledParameterKind.Constant,
                    Constant = ToLiteral(constant.Value)
                };
            }

            var member = payload as FormMemberRef;
            if (member != null)
            {
                if (member.MemberIndex < 0 || member.MemberIndex >= _members.Count)
                    throw Refuse("a member path reference is out of range");
                var row = _members[member.MemberIndex];
                return new CompiledParameter
                {
                    Kind = row.RootRef ? CompiledParameterKind.RootPath : CompiledParameterKind.ModelPath,
                    MemberRef = member.MemberIndex
                };
            }

            var dynamic = payload as FormDynamicRef;
            if (dynamic != null)
            {
                if (dynamic.MemberIndex < 0 || dynamic.MemberIndex >= _members.Count)
                    throw Refuse("a member path reference is out of range");
                // Root and non-root dynamic paths share one stored form (AC-7's "dynamic path (segments)"):
                // a hop the engine classifies DynamicHop records no identity and binds through the engine's
                // dynamic parameter at load (AC-5), so there is nothing root-specific to store.
                return new CompiledParameter
                {
                    Kind = CompiledParameterKind.DynamicPath,
                    Segments = new List<string>(dynamic.Segments)
                };
            }
            var chain = payload as FormChainData;
            if (chain != null)
            {
                var converted = new CompiledParameter { Kind = CompiledParameterKind.Chain };
                var nested = new CompiledChain();
                foreach (var item in chain.Items)
                    nested.Items.Add(ConvertFormItem(item));
                converted.NestedChain = nested;
                return converted;
            }

            var propsSlot = payload as FormPropsSlot;
            if (propsSlot != null)
            {
                if (propsSlot.SlotIndex < 0)
                    throw Refuse("a prop slot index is negative");
                var convertedSlot = new CompiledParameter
                {
                    Kind = CompiledParameterKind.PropsSlot,
                    SlotIndex = propsSlot.SlotIndex,
                    Segments = new List<string>(propsSlot.Rest),
                    PropDynamicRest = propsSlot.DynamicRest,
                    PropHops = new List<CompiledMemberHop>()
                };
                foreach (var hop in propsSlot.Prefix)
                {
                    convertedSlot.PropHops.Add(new CompiledMemberHop
                    {
                        DeclaringType = ToTypeRef(hop.Declaring, hop.Name),
                        MemberName = hop.Name,
                        MemberType = ToTypeRef(hop.Member, hop.Name)
                    });
                }

                return convertedSlot;
            }

            var expression = payload as FormExprUse;
            if (expression != null)
            {
                return new CompiledParameter
                {
                    Kind = CompiledParameterKind.NativeExpression,
                    ExpressionRef = expression.ExpressionIndex,
                    UsesPropsSlot = _trees[expression.ExpressionIndex].UsesProps
                };
            }

            var csharp = payload as FormCSharpUse;
            if (csharp != null)
            {
                return new CompiledParameter
                {
                    Kind = CompiledParameterKind.CSharpExpression,
                    SiteRef = csharp.SiteIndex
                };
            }

            var refusal = payload as FormRefusal;
            if (refusal != null)
            {
                if (refusal.RefusalIndex < 0 || refusal.RefusalIndex >= _refusals.Count)
                    throw Refuse("a class-(c) refusal at " + form.Position + " names no refusal site");
                var data = _refusals[refusal.RefusalIndex];
                if (data.SourceText == null)
                    throw Refuse("a class-(c) refusal at " + form.Position +
                        " was never resolved against its document");
                return new CompiledParameter
                {
                    Kind = CompiledParameterKind.RefusalSite,
                    Refusal = new CompiledRefusalSource
                    {
                        SourceText = data.SourceText,
                        ModelType = ToTypeRef(data.ModelType),
                        ChainedType = ToTypeRef(data.ChainedType),
                        RootType = ToTypeRef(data.RootType),
                        Namespaces = new List<string>(data.Namespaces),
                        Position = ToPosition(data.Position),
                        Class = data.Class
                    }
                };
            }

            throw Refuse("a chain item parameter at " + form.Position + " has no stored form");
        }

        private CompiledProps ConvertProps(FormProps props)
        {
            var converted = new CompiledProps();
            var dynamicSlots = new HashSet<int>();
            foreach (var slot in props.Slots)
                dynamicSlots.Add(slot.SlotIndex);
            for (int i = 0; i < props.Prototype.Length; i++)
            {
                if (props.Prototype[i] == null && dynamicSlots.Contains(i))
                    converted.FrozenPrototype.Add(null);
                else
                    converted.FrozenPrototype.Add(ToLiteral(props.Prototype[i]));
            }

            foreach (var slot in props.Slots)
            {
                int expressionRef;
                if (!TryGetExprIndex(slot.Parameter, out expressionRef))
                    throw Refuse("a dynamic prop slot has no expression");
                converted.DynamicSlots.Add(new CompiledDynamicSlot
                {
                    SlotIndex = slot.SlotIndex,
                    ExpressionRef = expressionRef,
                    TargetType = ToTypeRef(slot.TargetType)
                });
            }

            return converted;
        }

        private void ConvertDefinitions(CompiledArtifact artifact)
        {
            foreach (var definition in _definitions.Values)
            {
                var converted = new CompiledDefinition
                {
                    Name = definition.Name,
                    BaseName = definition.BaseName,
                    ModelTypeSpelling = definition.Spelling,
                    ModelType = ToTypeRef(definition.ResolvedType),
                    ParameterTemplate = definition.ParameterTemplate,
                    SlotType = definition.SlotType == null ? null : ToTypeRef(definition.SlotType),
                    Position = ToPosition(definition.Position)
                };
                if (definition.PropDecls != null)
                {
                    foreach (var prop in definition.PropDecls)
                    {
                        converted.PropDecls.Add(new CompiledPropDecl
                        {
                            Name = prop.Name,
                            SlotIndex = prop.Index,
                            SlotType = ToTypeRef(prop.Type),
                            DefaultValue = prop.HasDefault ? ToLiteral(prop.DefaultBoxed) : null
                        });
                    }
                }

                if (definition.RegionNames != null)
                    foreach (var region in definition.RegionNames)
                        converted.Regions.Add(region);
                artifact.Definitions.Add(converted);
            }
        }

        private void ConvertTemplate(CompiledArtifact artifact, string key, string contentHash,
            string registeredName, string entryPointTypeName, ExType modelType, bool modelTypeIsAmbient,
            bool isDynamic, string profile, string mode, bool trim)
        {
            var row = new CompiledTemplateRow
            {
                Key = key ?? string.Empty,
                RegisteredName = registeredName,
                ContentHash = contentHash ?? string.Empty,
                ModelType = ToTypeRef(modelType),
                ModelTypeIsAmbient = modelTypeIsAmbient,
                IsDynamic = isDynamic,
                EntryPointTypeName = entryPointTypeName,
                Options = new CompiledOptionsFingerprint
                {
                    Profile = profile,
                    Mode = mode ?? string.Empty,
                    Trim = trim
                },
                RootDocumentRef = _rootDocument,
                SiteCount = 0
            };
            for (int i = 0; i < _extensions.Count; i++)
                row.ExtensionRefs.Add(i);
            for (int i = 0; i < _functions.Count; i++)
                row.FunctionRefs.Add(i);
            int definitionIndex = 0;
            foreach (var definition in _definitions.Values)
            {
                row.DefinitionRefs.Add(definitionIndex);
                definitionIndex++;
            }

            for (int i = 0; i < _refusals.Count; i++)
            {
                var refusal = _refusals[i];
                if (refusal.SourceText == null)
                    throw Refuse("a class-(c) refusal was never resolved against its document");
                row.RefusalSites.Add(new PrecompiledRefusalSite(i, refusal.Class, refusal.Detail,
                    refusal.Position.StartIndex, refusal.Position.Length));
            }

            artifact.Templates.Add(row);
            WalkSites(artifact, row);
            row.SiteCount = artifact.Sites.Count;
        }

        private void WalkSites(CompiledArtifact artifact, CompiledTemplateRow row)
        {
            // First-visit order (AC-6): a self-recursive definition's caller-content document is one of its
            // own ancestors, so the document graph is cyclic and an unguarded walk never terminates. Each
            // document's sites are enumerated once, at first visit.
            WalkDocument(artifact, row.RootDocumentRef, new HashSet<int>());
        }

        private void WalkDocument(CompiledArtifact artifact, int documentRef, HashSet<int> walked)
        {
            if (!walked.Add(documentRef))
                return;
            var document = artifact.Documents[documentRef];
            foreach (var element in document.Elements)
            {
                if (!element.IsChain || element.Chain == null)
                    continue;
                foreach (var item in element.Chain.Items)
                    WalkItem(artifact, item, walked);
            }
        }

        private void WalkItem(CompiledArtifact artifact, CompiledItem item, HashSet<int> walked)
        {
            WalkParameter(artifact, item.Parameter, walked);
            if (item.Body != null && item.Body.CompiledDocumentRef.HasValue)
                WalkDocument(artifact, item.Body.CompiledDocumentRef.Value, walked);
            if (item.Parameter != null && item.Parameter.Kind == CompiledParameterKind.DefinitionCall)
                WalkDocument(artifact, item.Parameter.CallerContentRef, walked);
        }

        private void WalkParameter(CompiledArtifact artifact, CompiledParameter parameter, HashSet<int> walked)
        {
            if (parameter == null)
                return;
            switch (parameter.Kind)
            {
                case CompiledParameterKind.ModelPath:
                case CompiledParameterKind.RootPath:
                    AddSite(artifact, CompiledSiteKind.MemberAccessor, parameter.MemberRef);
                    break;
                case CompiledParameterKind.NativeExpression:
                    AddSite(artifact, CompiledSiteKind.NativeExpression, parameter.ExpressionRef);
                    break;
                case CompiledParameterKind.CSharpExpression:
                    parameter.SiteRef = AddSite(artifact, CompiledSiteKind.EmbeddedCSharp, parameter.SiteRef);
                    break;
                case CompiledParameterKind.Chain:
                    if (parameter.NestedChain != null)
                        foreach (var nested in parameter.NestedChain.Items)
                            WalkItem(artifact, nested, walked);
                    break;
            }
        }

        private static int AddSite(CompiledArtifact artifact, CompiledSiteKind kind, int payloadRef)
        {
            int ordinal = artifact.Sites.Count;
            artifact.Sites.Add(new CompiledSiteRow
            {
                TemplateIndex = 0,
                SiteOrdinal = ordinal,
                Kind = kind,
                PayloadRef = payloadRef
            });
            return ordinal;
        }
    }
}
