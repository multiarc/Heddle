using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Heddle.Data;
using Heddle.Generator.Binding;
using Heddle.Generator.Typing;
using Heddle.Language.Expressions;
using Heddle.Language.Members;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// Maps the native-expression AST (<see cref="ExprNode"/>) to C# 1:1. The native tier is a strict C# subset, so
    /// operators, ternary/coalesce, and literals emit verbatim and the consumer's compiler applies the identical
    /// numeric promotion / lifting / string-concat semantics the runtime reconstructs by hand. The one type-sensitive
    /// spot is member-path null-safety, resolved through <see cref="SymbolTypeResolver"/>. A construct the writer
    /// cannot yet reproduce faithfully returns null, so the emitter degrades the template to the dynamic path.
    /// <para>Operators emit verbatim only where the shared <see cref="NativeOperatorRules"/> table says C# and the
    /// native tier agree. The documented deviations, and every operand whose static facts the estimator cannot pin down,
    /// degrade instead — which stops the consumer's C# compiler from having an opinion about rendered output.</para>
    /// </summary>
    internal sealed class NativeExpressionWriter
    {
        private static readonly Dictionary<string, string> DefaultShims = BuildDefaultShims();
        private static readonly Dictionary<string, int> DefaultOverloadCounts = BuildDefaultOverloadCounts();

        private readonly Dictionary<ExprNode, OperandKind> _estimates = new Dictionary<ExprNode, OperandKind>();
        private readonly Dictionary<CallNode, DefaultFunctionBinder.Binding> _bindings =
            new Dictionary<CallNode, DefaultFunctionBinder.Binding>();

        private readonly Dictionary<CallNode, ExportFunctionBinder.Binding> _exportBindings =
            new Dictionary<CallNode, ExportFunctionBinder.Binding>();

        private readonly Dictionary<CallNode, MergedFunctionBinder.Binding> _mergedBindings =
            new Dictionary<CallNode, MergedFunctionBinder.Binding>();

        private readonly SymbolTypeResolver _resolver;
        private readonly ITypeSymbol _modelType;
        private readonly ITypeSymbol _rootModelType;
        private readonly string _modelLocal;
        private readonly PropLayoutInfo _props;
        private readonly FunctionExportResolver _exports;
        private bool _usedModel;

        /// <summary>Non-zero while writing a function call's arguments — the one place a ref-struct refusal names
        /// the argument sink rather than the operand one.</summary>
        private int _functionArgDepth;
        private readonly HashSet<string> _usedDefaultFunctions = new HashSet<string>();
        private readonly List<(string Name, string Aqn, int OverloadCount)> _usedExports =
            new List<(string, string, int)>();
        private readonly List<(string Name, int OverloadCount)> _usedCollidedBuiltIns =
            new List<(string, int)>();
        private readonly List<(string Name, Heddle.Strings.Core.BlockPosition Position)> _unresolvableFunctions =
            new List<(string, Heddle.Strings.Core.BlockPosition)>();
        private readonly List<SymbolMemberResolver.MemberFailure> _memberFailures =
            new List<SymbolMemberResolver.MemberFailure>();
        private readonly List<(string Name, Heddle.Strings.Core.BlockPosition Position)> _propReads =
            new List<(string, Heddle.Strings.Core.BlockPosition)>();

        private readonly List<(string Name, Heddle.Strings.Core.BlockPosition Position, string Detail,
            string RuntimeDiagnosticId)> _unbindableCalls =
            new List<(string, Heddle.Strings.Core.BlockPosition, string, string)>();

        private readonly List<(string Name, Heddle.Strings.Core.BlockPosition Position, string Display)>
            _unnameableCalls = new List<(string, Heddle.Strings.Core.BlockPosition, string)>();

        private readonly SymbolTypeFacts _typeFacts;

        public NativeExpressionWriter(SymbolTypeResolver resolver, ITypeSymbol modelType, ITypeSymbol rootModelType,
            string modelLocal, FunctionExportResolver exports, SymbolTypeFacts typeFacts,
            Func<string> allocateHopLocal, PropLayoutInfo props = null,
            bool modelDeclaredDynamic = false)
        {
            _resolver = resolver;
            _modelType = modelType;
            // Only an in-file `@model(){{dynamic}}` proves the engine's scope is dynamic: the directive overrides
            // whatever model type a runtime caller supplies, where a merely UNDECLARED model can still be typed by
            // the caller's CompileContext — so only the declared form may forward the engine's HED1004.
            _modelDeclaredDynamic = modelDeclaredDynamic;
            // The template's own @model, whatever body this expression sits in: a definition body keeps its
            // caller's model in _modelType while '::' still roots at the outer template's model, exactly as the
            // engine resolves '::' against RootScopeType rather than the body's scope type.
            _rootModelType = rootModelType;
            _modelLocal = modelLocal;
            _props = props;
            _exports = exports;
            _typeFacts = typeFacts;
            // Shared with the emitter that made this writer, so the names cannot collide with the ones it allocates
            // for paths of its own in the same block.
            _allocateHopLocal = allocateHopLocal;
        }

        private readonly Func<string> _allocateHopLocal;
        private readonly bool _modelDeclaredDynamic;

        public bool UsedModel => _usedModel;

        /// <summary>Default (built-in) function names bound to the <c>PrecompiledFunctions</c> shim in this
        /// expression; the emitter records one manifest <c>FunctionBindings</c> row per name.</summary>
        public IEnumerable<string> UsedDefaultFunctions => _usedDefaultFunctions;

        /// <summary>Discovered <c>[ExportFunctions]</c> names bound directly to their container in this expression:
        /// the function name with its container AQN-sans-version target and that container's overload count.
        /// A name exported by more than one container yields one row <b>per container</b>, matching
        /// the runtime's merged registry — the gauntlet compares each row's count exactly.</summary>
        public IReadOnlyList<(string Name, string Aqn, int OverloadCount)> UsedExports => _usedExports;

        /// <summary>Collided names (built-in ∧ export) called in this expression whose built-in overloads are
        /// still live after exact-signature exports replaced theirs, with that remaining count — the shim-target
        /// manifest row the gauntlet compares exactly against the merged registry. A name whose every built-in
        /// overload was replaced does not appear: the live registry holds no shim-target entries for it either.</summary>
        public IReadOnlyList<(string Name, int OverloadCount)> UsedCollidedBuiltIns => _usedCollidedBuiltIns;

        /// <summary>Function names in this expression resolvable from neither the default table nor any referenced
        /// export; each name with its <c>.heddle</c> position. When non-empty,
        /// the emitter degrades the template to a fallback-marker entry and reports <c>HED7014</c>.</summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position)> UnresolvableFunctions =>
            _unresolvableFunctions;

        /// <summary>Genuine member-path failures in this expression — a property not found on a resolved,
        /// non-dynamic receiver (<c>HED7008</c>). Drained by the emitter and reported at the <c>.heddle</c>
        /// span.</summary>
        public IReadOnlyList<SymbolMemberResolver.MemberFailure> MemberFailures => _memberFailures;

        /// <summary>Every path in this expression that rooted at a body prop, with its <c>.heddle</c> position.
        /// The emitter drains these to ask whether the prop's name also names a model member it hides — the
        /// question the runtime asks at the same point of its own prop-root walk.</summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position)> PropReads => _propReads;

        /// <summary>The model this expression's paths root at — the scope type the shadowing question is asked of.</summary>
        public ITypeSymbol ModelSymbol => _modelType;

        /// <summary>Function calls the <b>shared</b> overload ranker <i>proved</i> illegal — an
        /// ambiguous flat-Pareto front or no applicable overload, over arguments the estimator typed — each with its
        /// <c>.heddle</c> position, the runtime-shaped sentence naming the candidates, and the diagnostic id
        /// HED7025. Drained by the emitter and reported at Error.
        /// <para>Refusals the generator could <b>not</b> prove (an <c>Unknown</c> argument estimate, an unspellable
        /// cast target) never land here: they stay the silent degrade they always were,
        /// because the generator has established nothing about what the runtime will do.</para></summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position, string Detail,
            string RuntimeDiagnosticId)> UnbindableFunctionCalls => _unbindableCalls;

        /// <summary>Exported host functions this compilation may not write a call to, each with its
        /// <c>.heddle</c> position and the signature to name in the message. Drained by the emitter as
        /// <c>HED7030</c>, the same id a model type the consumer's compiler rejects gets — from the consumer's
        /// side it is the same situation.</summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position, string Display)>
            UnnameableFunctionCalls => _unnameableCalls;

        /// <summary>Constant divisions and modulos with a zero divisor the fold refused, each with the
        /// offending operator's position and lexeme. Drained by the emitter and reported as the engine's own
        /// <b>HED1018</b> forwarded, not as a HED7xxx twin: the build tier re-derives a fact the engine also
        /// refuses at compile time, and one fact carries one id on both tiers.</summary>
        public IReadOnlyList<(Heddle.Strings.Core.BlockPosition Position, string Operator)>
            DivisionsByConstantZero => _divisionsByZero;

        private readonly List<(Heddle.Strings.Core.BlockPosition Position, string Operator)> _divisionsByZero =
            new List<(Heddle.Strings.Core.BlockPosition, string)>();

        /// <summary>When a fold rejection is (or contains) a constant division by zero, records the innermost
        /// offending site for the drain. Other rejection causes stay the silent degrade they always were.</summary>
        private void RecordDivisionByConstantZero(ExprNode node)
        {
            if (ConstantFolding.TryFindDivisionByConstantZero(node, out var site))
                _divisionsByZero.Add((site.Position, OperatorLexeme.ForBinary(site.Operator)));
        }

        /// <summary>Refusals whose engine counterpart is PROVEN: the same fact the engine refuses at its own
        /// template compile, recorded with the engine's id and the engine's exact sentence. Drained by the
        /// emitter and forwarded as build errors the way HED1018 is — one fact, one id, on both tiers.
        /// <para>A refusal the writer cannot prove — an Unknown estimate, a runtime-owned verdict, a type
        /// spelling it cannot reproduce — never lands here and stays the silent degrade it always was.</para></summary>
        public IReadOnlyList<(string Id, Heddle.Strings.Core.BlockPosition Position, string Message)>
            EngineRefusals => _engineRefusals;

        private readonly List<(string Id, Heddle.Strings.Core.BlockPosition Position, string Message)>
            _engineRefusals = new List<(string, Heddle.Strings.Core.BlockPosition, string)>();

        /// <summary>Refuses like <see cref="Refuse"/>, additionally recording the engine's id and sentence for
        /// the forward — or only refusing when <paramref name="message"/> is null, i.e. when some type spelling
        /// in the engine's sentence could not be reproduced exactly.</summary>
        private string RefuseAsEngine(string id, Heddle.Strings.Core.BlockPosition position, string message,
            string reason)
        {
            if (message != null)
                _engineRefusals.Add((id, position, message));
            return Refuse(reason);
        }

        private const string TypedModelRequiredMessage =
            "Native expressions require a typed model; declare @model(...) / ':: <Type>' or use the @ C# tier.";

        /// <summary>The FIRST specific refusal this writer hit, or null where nothing refused. The recursion
        /// bottoms out at the offending node before any ancestor propagates the null, so first-wins is
        /// innermost-wins — the construct a template author can actually act on. The emitter prints it inside
        /// HED7031's parenthesis in place of the generic "unsupported native expression".</summary>
        public string RefusalReason { get; private set; }

        private string Refuse(string reason)
        {
            if (RefusalReason == null)
                RefusalReason = reason;
            return null;
        }

        /// <summary>The shared ranker's descriptor for what a function call returns, for callers that have to type a
        /// call-site value without emitting it. <c>Unknown</c> where the ranker refuses or the name is neither a
        /// built-in nor an export — the caller's "cannot say".</summary>
        public OperandKind EstimateCallReturn(CallNode call) => EstimateCall(call);

        /// <summary>
        /// The <b>declared</b> return type of the overload the shared ranker picks for this call, or null where it
        /// picks none and where the name is neither a built-in nor an export.
        /// <para>This is what a caller typing a call-site value needs, and it is not what
        /// <see cref="EstimateCallReturn"/> gives it: the operand descriptor names only the primitives, so every
        /// call returning anything else — <c>range</c>, an export returning a <c>DateTime</c> or a class of the
        /// host's — came back indistinguishable from "the generator cannot say", and the gates that refuse a value
        /// the engine will not take exempted all of them.</para>
        /// </summary>
        public ITypeSymbol CallReturnType(CallNode call, Compilation compilation)
        {
            if (_exports != null && _exports.TryGet(call.Name, out _))
            {
                if (DefaultShims.ContainsKey(call.Name))
                    return BindMergedCall(call)?.ReturnType;
                return BindExportCall(call)?.ReturnType;
            }

            var binding = BindDefaultCall(call);
            return binding == null ? null : compilation.GetTypeByMetadataName(binding.Row.ReturnTypeName);
        }

        public static bool IsDefaultFunction(string name) => DefaultShims.ContainsKey(name);

        public static int DefaultOverloadCount(string name) =>
            DefaultOverloadCounts.TryGetValue(name, out var c) ? c : 0;

        private static Dictionary<string, string> BuildDefaultShims()
        {
            var map = new Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (var row in DefaultFunctionTable.Rows)
                map[row.Name] = row.ShimMethodName;
            return map;
        }

        private static Dictionary<string, int> BuildDefaultOverloadCounts()
        {
            var map = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var row in DefaultFunctionTable.Rows)
                map[row.Name] = map.TryGetValue(row.Name, out var c) ? c + 1 : 1;
            return map;
        }

        /// <summary>
        /// Emits a whole expression — the entry point the emitter uses, as distinct from the recursive
        /// <see cref="Write(ExprNode)"/> it calls for the parts.
        /// <para>Wrapped in <c>unchecked</c>, because the engine's arithmetic is: it builds
        /// <c>Expression.Add</c> and friends, which are the unchecked factories, so an overflow wraps and the
        /// template renders a wrapped number. Bare operators here inherit the <em>consumer's</em> setting instead,
        /// so a host that builds with <c>CheckForOverflowUnderflow</c> got an <c>OverflowException</c> from the
        /// precompiled tier where the dynamic tier rendered a value — the same template, two behaviours, decided by
        /// an MSBuild property in a project the template knows nothing about. A constant overflow is a separate
        /// question and still degrades: the fold reports what C# would reject, and this does not change that.</para>
        /// </summary>
        public string WriteRoot(ExprNode node)
        {
            var written = Write(node);
            return written == null ? null : "unchecked(" + written + ")";
        }

        /// <summary>Emits the expression, or returns null if it uses a construct the writer does not yet support.</summary>
        public string Write(ExprNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return literal.LiteralError != null ? null : LiteralFormatter.Format(literal.Value);
                case ThisNode thisNode:
                    return WriteThis(thisNode);
                case PathNode path:
                    return WritePath(path);
                case UnaryNode unary:
                    return WriteUnary(unary);
                case BinaryNode binary:
                    return WriteBinary(binary);
                case TernaryNode ternary:
                    return WriteTernary(ternary);
                case CallNode call:
                    return WriteCall(call);
                case IndexNode index:
                    return WriteIndex(index);
                case MethodCallNode method:
                    return RefuseAsEngine(HeddleDiagnosticIds.MethodCallNotAvailable, method.Position,
                        "Method calls are not available in native expressions — register a function with TemplateOptions.Functions or use the @ C# tier.",
                        "method-call syntax, which the engine refuses too (HED1003)");
                default:
                    return Refuse("an expression construct the writer has no spelling for");
            }
        }

        /// <summary>
        /// Emits <c>this</c> as an operand: the model local itself, which the enclosing method declares cast to the
        /// model type.
        /// <para>What it denotes is the enclosing body's own model, whatever built that body — a <c>@list</c>
        /// element, a definition's caller model, the template's declared model — because every one of them arrives
        /// here as the model type. A body with no static model has no <c>this</c> either: the engine's compiler
        /// refuses the whole expression with "native expressions require a typed model", so nothing is emitted and
        /// the template meets that same refusal on the tier that renders it.</para>
        /// <para>Distinct from <c>this</c> as a <b>whole</b> call parameter, which the emitter passes through as the
        /// scope's model and which needs no static type at all — that is the empty member path, not an operand.</para>
        /// </summary>
        private string WriteThis(ThisNode node)
        {
            if (_modelType == null)
                return _modelDeclaredDynamic
                    ? RefuseAsEngine(HeddleDiagnosticIds.TypedModelRequired, node.Position,
                        TypedModelRequiredMessage, "'this' in an expression with no static model type")
                    : Refuse("'this' in an expression with no static model type");
            _usedModel = true;
            return _modelLocal;
        }

        private string WriteCall(CallNode call)
        {
            bool isDefault = DefaultShims.TryGetValue(call.Name, out var shim);
            FunctionExportResolver.ExportEntry export = null;
            bool hasExport = _exports != null && _exports.TryGet(call.Name, out export);

            if (!isDefault && !hasExport)
            {
                _unresolvableFunctions.Add((call.Name, call.Position));
                return Refuse("a call to '" + call.Name + "', which is neither a built-in nor an export");
            }

            var args = new string[call.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
            {
                _functionArgDepth++;
                args[i] = Write(call.Arguments[i]);
                _functionArgDepth--;
                if (args[i] == null)
                    return null;
            }

            if (isDefault && hasExport)
                return WriteMergedCall(call, export, args);

            if (hasExport)
            {
                var exportBinding = BindExportCall(call);
                if (exportBinding == null)
                    return Refuse("an export call to '" + call.Name + "' the binder refuses");

                for (int i = 0; i < args.Length; i++)
                    if (exportBinding.ArgumentCasts[i] != null)
                        args[i] = "(" + exportBinding.ArgumentCasts[i] + ")(" + args[i] + ")";

                _exports.TryGet(call.Name, out var entry);
                foreach (var row in entry.ManifestRows)
                    if (!_usedExports.Contains((call.Name, row.Aqn, row.OverloadCount)))
                        _usedExports.Add((call.Name, row.Aqn, row.OverloadCount));

                var argumentList = exportBinding.Expanded
                    ? ExpandedArgumentList(args, exportBinding.Overload.Method.Parameters.Length - 1,
                        exportBinding.ParamsElementTypeName)
                    : string.Join(", ", args);
                return exportBinding.Overload.ContainerGlobalName + "." + exportBinding.Overload.MethodName +
                       "(" + argumentList + ")";
            }

            var binding = BindDefaultCall(call);
            if (binding == null)
                return Refuse("a built-in call to '" + call.Name + "' the binder refuses");

            for (int i = 0; i < args.Length; i++)
            {
                if (binding.ArgumentCasts[i] != null)
                    args[i] = "(" + binding.ArgumentCasts[i] + ")(" + args[i] + ")";
            }

            _usedDefaultFunctions.Add(call.Name);
            return "global::Heddle.Precompiled.PrecompiledFunctions." + shim + "(" + string.Join(", ", args) + ")";
        }

        /// <summary>Emits a call to a collided name — the registry's own merge replayed by
        /// <see cref="MergedFunctionBinder"/> — spelling the winner's ordinary call target. The manifest gets one
        /// row per live target of the name (the shim for surviving built-in overloads, each exporting container),
        /// whichever side this call binds, because the gauntlet checks every live registration of the name.</summary>
        private string WriteMergedCall(CallNode call, FunctionExportResolver.ExportEntry entry, string[] args)
        {
            var binding = BindMergedCall(call);
            if (binding == null)
                return Refuse("a call to '" + call.Name +
                              "' the merged built-in and export candidate set does not bind");

            for (int i = 0; i < args.Length; i++)
                if (binding.ArgumentCasts[i] != null)
                    args[i] = "(" + binding.ArgumentCasts[i] + ")(" + args[i] + ")";

            foreach (var row in entry.ManifestRows)
                if (!_usedExports.Contains((call.Name, row.Aqn, row.OverloadCount)))
                    _usedExports.Add((call.Name, row.Aqn, row.OverloadCount));
            if (binding.RemainingBuiltInOverloads > 0 &&
                !_usedCollidedBuiltIns.Contains((call.Name, binding.RemainingBuiltInOverloads)))
                _usedCollidedBuiltIns.Add((call.Name, binding.RemainingBuiltInOverloads));

            if (binding.IsBuiltIn)
                return "global::Heddle.Precompiled.PrecompiledFunctions." + binding.Row.ShimMethodName +
                       "(" + string.Join(", ", args) + ")";
            var argumentList = binding.Expanded
                ? ExpandedArgumentList(args, binding.Overload.Method.Parameters.Length - 1,
                    binding.ParamsElementTypeName)
                : string.Join(", ", args);
            return binding.Overload.ContainerGlobalName + "." + binding.Overload.MethodName +
                   "(" + argumentList + ")";
        }

        /// <summary>The argument list of a params-expanded bind: the expanded tail is spelled as one explicitly
        /// typed array creation, so the consumer's compiler binds the winner in normal form instead of re-running
        /// its own params expansion — whose betterness rules are not the engine's — over the overload set.</summary>
        private static string ExpandedArgumentList(string[] args, int fixedCount, string elementTypeName)
        {
            var parts = new string[fixedCount + 1];
            for (int i = 0; i < fixedCount; i++)
                parts[i] = args[i];
            parts[fixedCount] = args.Length == fixedCount
                ? "new " + elementTypeName + "[] { }"
                : "new " + elementTypeName + "[] { " +
                  string.Join(", ", args, fixedCount, args.Length - fixedCount) + " }";
            return string.Join(", ", parts);
        }

        /// <summary>Resolves a collided call against the merged built-in + export candidate set; returns null when
        /// the merged ranker refuses or the winning export may not be spelled (recorded as <c>HED7030</c>).</summary>
        private MergedFunctionBinder.Binding BindMergedCall(CallNode call)
        {
            if (_mergedBindings.TryGetValue(call, out var cached))
                return cached;

            MergedFunctionBinder.Binding binding = null;
            if (_typeFacts != null && _exports != null && _exports.TryGet(call.Name, out var entry))
            {
                var argKinds = new OperandKind[call.Arguments.Count];
                var argTypes = new ITypeSymbol[call.Arguments.Count];
                for (int i = 0; i < argKinds.Length; i++)
                {
                    argKinds[i] = Estimate(call.Arguments[i]);
                    argTypes[i] = ArgumentType(call.Arguments[i]);
                }

                binding = MergedFunctionBinder.TryBind(_typeFacts, _resolver, call.Name, entry.Overloads,
                    argKinds, argTypes, out var refusal);
                RecordIfProvenIllegal(call, refusal);

                if (binding != null && !binding.IsBuiltIn && !CanWriteCallTo(binding.Overload, out var display))
                {
                    _unnameableCalls.Add((call.Name, call.Position, display));
                    binding = null;
                }
            }

            _mergedBindings[call] = binding;
            return binding;
        }

        /// <summary>
        /// Emits an index access with the engine's null-receiver semantics: the receiver is evaluated once, and a
        /// null receiver yields <c>default(TResult)</c> without evaluating the index expressions — which is what
        /// C#'s <c>?[</c> does, so the null-conditional form spells it directly, <c>?? default(T)</c> restoring the
        /// non-nullable result type the engine's conditional carries. A value-type receiver reads directly, as the
        /// engine does. Argument casts come pinned from <see cref="ResolveIndex"/> so neither the conversion nor —
        /// for indexers — the overload choice is left to the consumer's compiler.
        /// </summary>
        private string WriteIndex(IndexNode index)
        {
            var target = Write(index.Target);
            if (target == null)
                return null;
            var args = new string[index.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = Write(index.Arguments[i]);
                if (args[i] == null)
                    return null;
            }

            var binding = ResolveIndex(index);
            if (binding.Refusal != null)
                return binding.EngineMessage != null
                    ? RefuseAsEngine(HeddleDiagnosticIds.IndexerNotFound, index.Position, binding.EngineMessage,
                        binding.Refusal)
                    : Refuse(binding.Refusal);

            for (int i = 0; i < args.Length; i++)
            {
                if (binding.ArgumentCasts[i] != null)
                    args[i] = "((" + binding.ArgumentCasts[i] + ")(" + args[i] + "))";
            }

            var joined = string.Join(", ", args);
            if (binding.DirectReceiver)
                return "(" + target + "[" + joined + "])";
            if (SymbolTypeResolver.IsNonNullableValueType(binding.ResultType))
                return "(" + target + "?[" + joined + "] ?? default(" +
                       SymbolTypeResolver.FullyQualified(binding.ResultType) + "))";
            return "(" + target + "?[" + joined + "])";
        }

        /// <summary>What one index access resolved to — or why it never will, in <see cref="Refusal"/>.</summary>
        private sealed class IndexBinding
        {
            public ITypeSymbol ResultType;

            /// <summary>Per-argument cast target, or null where the argument's own type is already exact.</summary>
            public string[] ArgumentCasts;

            /// <summary>A value-type receiver reads directly — the engine skips the null test there too.</summary>
            public bool DirectReceiver;

            public string Refusal;

            /// <summary>The engine's HED1010 sentence, set only on the refusals whose engine counterpart is
            /// proven and whose every type spelling the writer can reproduce; drives the forward.</summary>
            public string EngineMessage;
        }

        private readonly Dictionary<IndexNode, IndexBinding> _indexBindings =
            new Dictionary<IndexNode, IndexBinding>();

        private IndexBinding ResolveIndex(IndexNode index)
        {
            if (_indexBindings.TryGetValue(index, out var cached))
                return cached;
            var binding = ResolveIndexCore(index);
            _indexBindings[index] = binding;
            return binding;
        }

        private static IndexBinding IndexRefusal(string reason, string engineMessage = null) =>
            new IndexBinding { Refusal = reason, EngineMessage = engineMessage };

        /// <summary>The engine's HED1010 sentence for this access — receiver and argument types in the engine's
        /// own spelling — or null when any of them cannot be reproduced exactly.</summary>
        private string IndexerNotFoundMessage(ITypeSymbol receiverType, IndexNode index)
        {
            var receiver = EngineFriendlyName(receiverType);
            if (receiver == null)
                return null;
            var args = new string[index.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = EngineTypeName(index.Arguments[i]);
                if (args[i] == null)
                    return null;
            }

            return "Type " + receiver + " has no accessible indexer that takes (" + string.Join(", ", args) + ").";
        }

        private IndexBinding ResolveIndexCore(IndexNode index)
        {
            var receiverType = ArgumentType(index.Target);
            if (!IsEstablishedType(receiverType))
                return IndexRefusal("an index whose receiver has no established static type");

            if (receiverType is IArrayTypeSymbol array)
                return ResolveArrayIndex(index, array);
            return ResolveIndexerAccess(index, receiverType);
        }

        /// <summary>The array arm, mirroring the engine: any integral index — <c>char</c> included — converts to
        /// <c>int</c> unless it already is one exactly, so a wide index truncates and a nullable index throws at
        /// render on both tiers. A known non-integral index is the engine's own HED1010 refusal.</summary>
        private IndexBinding ResolveArrayIndex(IndexNode index, IArrayTypeSymbol array)
        {
            if (array.Rank != index.Arguments.Count)
                return IndexRefusal("an array index whose argument count does not match the array's rank");
            if (_resolver.ClassifyTypeName(array.ElementType, out _) != SymbolTypeResolver.NameFault.None)
                return IndexRefusal("an array element type generated code cannot name");

            var casts = new string[index.Arguments.Count];
            for (int i = 0; i < casts.Length; i++)
            {
                var kind = Estimate(index.Arguments[i]);
                if (kind.Category == OperandCategory.Unknown)
                    return IndexRefusal("an index whose static type the writer cannot establish");
                if (kind.Category != OperandCategory.Numeric || !NumericTable.IsIntegral(kind.Kind))
                    return IndexRefusal("an index access the engine refuses too (HED1010)",
                        IndexerNotFoundMessage(array, index));
                if (kind.Kind != NumericKind.Int32 || kind.IsNullable)
                    casts[i] = "int";
            }

            return new IndexBinding { ResultType = array.ElementType, ArgumentCasts = casts };
        }

        /// <summary>
        /// The indexer arm. Emitted only when exactly ONE candidate the engine's filter admits matches the
        /// argument arity and types under the shared conversion rank — with several matches the engine takes
        /// whichever reflection happens to enumerate first, which no symbol walk can reproduce. Candidates are
        /// gathered most-derived-first over the base chain and, on an interface receiver, from the interface's own
        /// declarations only, both mirroring what reflection's <c>GetProperties</c> surfaces to the engine.
        /// </summary>
        private IndexBinding ResolveIndexerAccess(IndexNode index, ITypeSymbol receiverType)
        {
            if (_typeFacts?.Compilation == null)
                return IndexRefusal("an index whose static type the writer cannot establish");

            var args = new RankArgument<ITypeSymbol>[index.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
            {
                var kind = Estimate(index.Arguments[i]);
                if (kind.Category == OperandCategory.NullLiteral)
                {
                    args[i] = RankArgument<ITypeSymbol>.Null();
                    continue;
                }

                var type = ArgumentType(index.Arguments[i]) ??
                           ExportFunctionBinder.ToSymbol(_typeFacts.Compilation, kind);
                if (type == null || type.TypeKind == TypeKind.Dynamic || type.TypeKind == TypeKind.Error)
                    return IndexRefusal("an index whose static type the writer cannot establish");
                args[i] = RankArgument<ITypeSymbol>.Of(type);
            }

            var model = new ExportFunctionBinder.SymbolRankModel(_typeFacts);
            IPropertySymbol single = null;
            for (var type = receiverType; type != null; type = type.BaseType)
            {
                foreach (var member in type.GetMembers())
                {
                    if (!(member is IPropertySymbol property) || property.Parameters.Length == 0 ||
                        property.Parameters.Length != args.Length)
                        continue;
                    var facts = SymbolTypeResolver.PropertyFacts(property);
                    if (!MemberVisibility.IsAccessible(facts))
                    {
                        // A [Hidden] indexer is skipped by the engine's filter but stays a perfectly bindable
                        // overload to the consumer's compiler, so no cast pin can keep the two in step.
                        if (facts.HasHidden && MemberVisibility.IsAccessible(
                                new MemberFacts(facts.CanRead, facts.Access, false, facts.IsStatic)))
                            return IndexRefusal("an indexer the engine's [Hidden] filter skips but C# would bind");
                        continue;
                    }

                    bool matches = true;
                    for (int i = 0; i < args.Length && matches; i++)
                        matches = OverloadRank.ConversionRank(model, args[i], property.Parameters[i].Type) >= 0;
                    if (!matches)
                        continue;
                    if (single != null)
                        return IndexRefusal("an indexer choice that depends on reflection order");
                    single = property;
                }
            }

            if (single == null)
                return IndexRefusal("an indexer access the engine refuses too (HED1010)",
                    IndexerNotFoundMessage(receiverType, index));

            if (!_resolver.IsAccessibleFromCompilation(single) ||
                !_resolver.IsAccessibleFromCompilation(single.GetMethod) ||
                SymbolTypeResolver.IsObsoleteError(single) || SymbolTypeResolver.IsObsoleteError(single.GetMethod))
                return IndexRefusal("an indexer generated code cannot name");
            if (_resolver.ClassifyTypeName(single.Type, out _) != SymbolTypeResolver.NameFault.None)
                return IndexRefusal("an indexer whose type generated code cannot name");
            if (SymbolTypeResolver.IsRefLikeOrRestricted(single.Type))
                return IndexRefusal("an indexer returning a ref struct, which an expression operand cannot box");

            var casts = new string[args.Length];
            for (int i = 0; i < casts.Length; i++)
            {
                var parameterType = single.Parameters[i].Type;
                if (!args[i].IsNullLiteral && SymbolEqualityComparer.Default.Equals(args[i].Type, parameterType))
                    continue;
                if (_resolver.ClassifyTypeName(parameterType, out _) != SymbolTypeResolver.NameFault.None)
                    return IndexRefusal("an indexer whose type generated code cannot name");
                casts[i] = SymbolTypeResolver.FullyQualified(parameterType);
            }

            return new IndexBinding
            {
                ResultType = single.Type,
                ArgumentCasts = casts,
                DirectReceiver = receiverType.IsValueType
            };
        }

        /// <summary>
        /// The prop this path is rooted at, or null where it is not rooted at one. A body prop read wins over the
        /// model on a path's first segment — resolution is syntactic, which is what lets both tiers agree on it by
        /// rule rather than by coincidence — and once it does the model is out of the picture entirely: the read
        /// either roots at the prop or the expression degrades, never falls back to the member the prop shadows.
        /// </summary>
        private PropSlotInfo PropRoot(PathNode path)
        {
            if (path.Target != null || path.RootRef || _props == null || path.Segments.Count == 0)
                return null;
            if (!_props.ByName.TryGetValue(path.Segments[0], out var slot) || slot == null)
                return slot;
            _propReads.Add((slot.Name, path.Position));
            return slot;
        }

        /// <summary>Whether the path hops off a target expression of its own. <c>this.</c> does not: the path
        /// roots at the model exactly as a bare one does, because that is what the engine's compiler converts the
        /// target to.
        /// <para>Prop-first resolution needs no exception for a target — a path with one is not a prop read on
        /// either tier, so <c>this.Name</c> reads the model member a prop named <c>Name</c> shadows.</para></summary>
        private static bool HasExpressionTarget(PathNode path) => path.Target != null && !(path.Target is ThisNode);

        private string WritePath(PathNode path)
        {
            if (HasExpressionTarget(path))
                return WriteTargetedPath(path);

            var prop = PropRoot(path);
            if (prop != null)
                return WritePropPath(prop, path);

            if (path.RootRef)
            {
                if (_rootModelType == null)
                    return _modelDeclaredDynamic
                        ? RefuseAsEngine(HeddleDiagnosticIds.TypedModelRequired, path.Position,
                            TypedModelRequiredMessage,
                            "a '::'-rooted path in an expression with no static root model type")
                        : Refuse("a '::'-rooted path in an expression with no static root model type");
                // The cast is parity, not a hazard: the engine converts the root object to RootScopeType at the
                // first hop, so a mismatched root throws the same InvalidCastException on both tiers.
                var rootRead = "((" + SymbolTypeResolver.FullyQualified(_rootModelType) +
                               ")global::Heddle.Precompiled.PrecompiledRuntime.RootModel(in scope))";
                return WriteMemberChain(_rootModelType, path, rootRead);
            }

            if (_modelType == null)
                return _modelDeclaredDynamic
                    ? RefuseAsEngine(HeddleDiagnosticIds.TypedModelRequired, path.Position,
                        TypedModelRequiredMessage,
                        "a model-rooted path in an expression with no static model type")
                    : Refuse("a model-rooted path in an expression with no static model type");

            _usedModel = true;
            return WriteMemberChain(_modelType, path, _modelLocal);
        }

        /// <summary>Resolves the path's segments against <paramref name="startType"/> and writes the null-safe hop
        /// chain off <paramref name="rootExpr"/> (already cast to that type) — the emission the model root and the
        /// <c>::</c> root share.</summary>
        private string WriteMemberChain(ITypeSymbol startType, PathNode path, string rootExpr)
        {
            var resolution = _resolver.ResolvePath(startType, path.Segments);
            // A path crossing a [Dynamic] property: the shared member walk answers DynamicHop on both tiers,
            // and the engine's native tier refuses that answer outright where its member tier would go DLR.
            if (resolution.Kind == SymbolTypeResolver.PathKind.DynamicHop)
                return RefuseAsEngine(HeddleDiagnosticIds.TypedModelRequired, path.Position,
                    TypedModelRequiredMessage, "a path crossing a [Dynamic] property");
            if (resolution.Kind != SymbolTypeResolver.PathKind.Resolved)
            {
                // Unusable is the one refusal with no author-facing fault behind it — the member is there and
                // readable, its type is simply one no generated code could hold a value of — so it degrades in
                // silence rather than reaching a diagnostic that names the member.
                if ((resolution.Kind == SymbolTypeResolver.PathKind.Failed ||
                     resolution.Kind == SymbolTypeResolver.PathKind.Inaccessible) &&
                    resolution.Fault != SymbolTypeResolver.NameFault.Unusable)
                {
                    var idx = resolution.DynamicIndex;
                    var receiver = resolution.Hops.Count == 0
                        ? startType
                        : resolution.Hops[resolution.Hops.Count - 1].Property;
                    if (!TemplateEmitter.IsUntypedReceiver(receiver))
                    {
                        var member = idx >= 0 && idx < path.Segments.Count
                            ? path.Segments[idx]
                            : path.Segments[path.Segments.Count - 1];
                        _memberFailures.Add(new SymbolMemberResolver.MemberFailure(
                            receiver, member,
                            MemberPathWriter.Display(path.Segments), path.Position,
                            resolution.Kind == SymbolTypeResolver.PathKind.Inaccessible));
                    }
                }

                return Refuse("a member path that does not resolve statically");
            }

            // An expression's operands and a function's arguments are boxed, and a ref struct cannot be. The
            // modern-TFM engine refuses the same path at template compile (HED0005); degrading surfaces that
            // positioned id instead of a CS0030 in a .g.cs. Only the rendered sink escapes this — outside the
            // expression tier entirely, where the carrier stringifies the value in place (TemplateEmitter's
            // RefStructUse split).
            if (SymbolTypeResolver.EndsOnRefStruct(resolution))
                return Refuse(_functionArgDepth > 0
                    ? "a member path ending on a ref struct, which a function argument cannot box (CS1503)"
                    : "a member path ending on a ref struct, which an expression operand cannot box (CS0029)");

            return MemberPathWriter.Write(rootExpr, TemplateEmitter.MapHops(resolution), _allocateHopLocal);
        }

        /// <summary>Emits a path hopping off a target expression — a call, an index, a literal — as the written
        /// target cast to its static type, with the shared hop chain rooted there. The engine boxes the target to
        /// <c>object</c> and re-converts at the first hop, so the cast reads the same value; every hop form splices
        /// the root text exactly once, which keeps the engine's evaluate-the-target-once contract.</summary>
        private string WriteTargetedPath(PathNode path)
        {
            var target = Write(path.Target);
            if (target == null)
                return null;

            var targetType = TargetType(path.Target);
            if (!IsEstablishedType(targetType))
                return Refuse("a member path rooted at an expression with no established static type");
            if (SymbolTypeResolver.IsRefLikeOrRestricted(targetType))
                return Refuse("a member path rooted at a ref struct, which an expression operand cannot box");
            if (_resolver.ClassifyTypeName(targetType, out _) != SymbolTypeResolver.NameFault.None)
                return Refuse("a member path rooted at a type generated code cannot name");

            var rootRead = "((" + SymbolTypeResolver.FullyQualified(targetType) + ")(" + target + "))";
            return WriteMemberChain(targetType, path, rootRead);
        }

        /// <summary>The static type a path target roots the member walk at — the engine's <c>target.Type</c>: a
        /// literal's own CLR type, and for every other shape the symbol <see cref="ArgumentType"/> resolves. Null
        /// where nothing is established — a binary or ternary result has an operand descriptor but no symbol
        /// behind it.</summary>
        private ITypeSymbol TargetType(ExprNode target)
        {
            if (target is LiteralNode literal)
                return literal.LiteralError != null || _typeFacts?.Compilation == null
                    ? null
                    : ExportFunctionBinder.ToSymbol(_typeFacts.Compilation, EstimateLiteral(literal.Value));
            return ArgumentType(target);
        }

        private static bool IsEstablishedType(ITypeSymbol type) =>
            type != null && type.TypeKind != TypeKind.Dynamic && type.TypeKind != TypeKind.Error &&
            type.TypeKind != TypeKind.TypeParameter;

        /// <summary>Emits the prop read: the boxed slot cast back to its declared type, then the remaining segments
        /// hopped off that type — the same shape a prop-rooted call-site parameter emits. Null degrades the
        /// expression; it never re-reads the model member the prop shadows.</summary>
        private string WritePropPath(PropSlotInfo slot, PathNode path)
        {
            if (slot.Type == null)
                return Refuse("a prop whose declared type the writer cannot resolve");

            var read = "((" + slot.TypeFq + ")global::Heddle.Precompiled.PrecompiledRuntime.Prop(in scope, " +
                       slot.Index + "))";
            if (path.Segments.Count == 1)
                return read;

            if (slot.Type.TypeKind == TypeKind.Dynamic)
                return Refuse("a path crossing a [Dynamic] prop");

            var resolution = _resolver.ResolvePath(slot.Type, Rest(path));
            if (resolution.Kind == SymbolTypeResolver.PathKind.DynamicHop)
                return RefuseAsEngine(HeddleDiagnosticIds.TypedModelRequired, path.Position,
                    TypedModelRequiredMessage, "a prop-rooted path crossing a [Dynamic] property");
            if (resolution.Kind != SymbolTypeResolver.PathKind.Resolved ||
                SymbolTypeResolver.EndsOnRefStruct(resolution))
                return Refuse("a prop-rooted path that does not resolve statically");

            return MemberPathWriter.Write(read, TemplateEmitter.MapHops(resolution), _allocateHopLocal);
        }

        /// <summary>The static type of a prop-rooted path, or null where the remaining segments do not resolve.</summary>
        private ITypeSymbol PropPathType(PropSlotInfo slot, PathNode path)
        {
            if (slot.Type == null)
                return null;
            if (path.Segments.Count == 1)
                return slot.Type;
            if (slot.Type.TypeKind == TypeKind.Dynamic)
                return null;
            var resolution = _resolver.ResolvePath(slot.Type, Rest(path));
            return resolution.Kind == SymbolTypeResolver.PathKind.Resolved ? resolution.ResultType : null;
        }

        private static IReadOnlyList<string> Rest(PathNode path)
        {
            var rest = new string[path.Segments.Count - 1];
            for (int i = 1; i < path.Segments.Count; i++)
                rest[i - 1] = path.Segments[i];
            return rest;
        }

        private string WriteUnary(UnaryNode node)
        {
            var op = OperatorLexeme.ForUnary(node.Operator);
            if (op == null)
                return null;
            if (ConstantFolding.TryEngineConstantSpelling(node, out var engineConstant))
                return engineConstant;
            var operand = Write(node.Operand);
            if (operand == null)
                return null;
            var verdict = NativeOperatorRules.ClassifyUnary(node.Operator, Estimate(node.Operand));
            if (verdict == OperatorVerdict.NotDefined)
            {
                var operandName = EngineTypeName(node.Operand);
                return RefuseAsEngine(HeddleDiagnosticIds.UnaryOperatorNotDefined, node.Position,
                    operandName == null
                        ? null
                        : "Operator '" + op + "' is not defined for operand type " + operandName + ".",
                    "operator '" + op + "' over an operand kind the shared table does not emit");
            }

            if (verdict != OperatorVerdict.Supported)
                return Refuse("operator '" + op + "' over an operand kind the shared table does not emit");
            return "(" + op + operand + ")";
        }

        private string WriteBinary(BinaryNode node)
        {
            var op = OperatorLexeme.ForBinary(node.Operator);
            if (op == null)
                return null;
            // A constant subtree the two tiers type or value differently is spelled as the ENGINE's folded
            // value, a typed literal — every later promotion and overload choice then starts from the
            // engine's type, so nothing here needs to degrade.
            if (ConstantFolding.TryEngineConstantSpelling(node, out var engineConstant))
                return engineConstant;

            // A constant division by zero is HED1018 on both tiers: the writer records the site and the
            // emitter forwards the engine's id as a build error.
            if ((node.Operator == ExprOperator.Divide || node.Operator == ExprOperator.Modulo) &&
                ConstantFolding.TryFindDivisionByConstantZero(node, out var zeroSite) &&
                ReferenceEquals(zeroSite, node))
            {
                RecordDivisionByConstantZero(node);
                return Refuse("a constant division by a zero divisor, refused on both tiers (HED1018)");
            }

            // A constant arithmetic whose engine evaluation throws at render — the smallest signed value
            // over -1, a decimal overflow — is emitted as an adapter call the consumer's compiler cannot
            // fold, so the render throw stays the engine's instead of C# folding to a different number.
            if (ConstantFolding.TryThrowingArithmeticSpelling(node, out var throwing, out var throwLeft,
                    out var throwRight))
                return "global::Heddle.Precompiled.RuntimeOperators." + throwing +
                       "(" + throwLeft + ", " + throwRight + ")";

            var left = Write(node.Left);
            var right = Write(node.Right);
            if (left == null || right == null)
                return null;
            string coalesceWiderFq = null;
            var coalesceRelation = node.Operator == ExprOperator.Coalesce
                ? RelateOperands(node.Left, node.Right, out coalesceWiderFq)
                : TypeRelation.Unknown;
            var leftKind = Estimate(node.Left);
            var rightKind = Estimate(node.Right);
            var witness = WitnessIfUserPair(node.Operator, node.Left, node.Right, leftKind, rightKind,
                out var adapterLeftFq, out var adapterRightFq, out var adapterResultFq);
            var verdict = NativeOperatorRules.Classify(node.Operator, leftKind, rightKind, coalesceRelation, witness);
            if (verdict == OperatorVerdict.NotDefined)
                return RefuseBinaryAsEngine(node, op, leftKind, rightKind);
            if (verdict != OperatorVerdict.Supported)
                return Refuse("operator '" + op + "' over operand kinds the shared table does not emit");

            // A proven user-defined operator emits through the adapter that replays the engine's factory
            // over the operands' static types — never the operator (or user conversion) C#'s own overload
            // resolution might prefer. Equality routes through the EqualityViaAdapter block below.
            if (witness == OperatorWitness.Bound)
            {
                var adapter = AdapterMethodName(node.Operator);
                if (adapter != null)
                {
                    bool arithmetic = node.Operator == ExprOperator.Add || node.Operator == ExprOperator.Subtract ||
                                      node.Operator == ExprOperator.Multiply ||
                                      node.Operator == ExprOperator.Divide || node.Operator == ExprOperator.Modulo;
                    var typeArguments = arithmetic
                        ? adapterLeftFq + ", " + adapterRightFq + ", " + adapterResultFq
                        : adapterLeftFq + ", " + adapterRightFq;
                    return "global::Heddle.Precompiled.RuntimeOperators." + adapter +
                           "<" + typeArguments + ">(" + left + ", " + right + ")";
                }
            }

            // The engine folds a null-against-null comparison to a constant; spell that constant.
            if (node.Operator == ExprOperator.Equal || node.Operator == ExprOperator.NotEqual)
            {
                if (Estimate(node.Left).Category == OperandCategory.NullLiteral &&
                    Estimate(node.Right).Category == OperandCategory.NullLiteral)
                    return node.Operator == ExprOperator.Equal ? "true" : "false";
            }

            // Mixed/unrelated equality emits through the adapter that replays the engine's own fallback
            // chain over the call site's static types — a user operator where the pair binds one, null-safe
            // object.Equals for the rest. Verbatim C# is either CS0019 or a bare reference comparison here.
            if ((node.Operator == ExprOperator.Equal || node.Operator == ExprOperator.NotEqual) &&
                NativeOperatorRules.EqualityViaAdapter(leftKind, rightKind, witness))
            {
                return "global::Heddle.Precompiled.RuntimeOperators." +
                       (node.Operator == ExprOperator.Equal ? "Equal" : "NotEqual") +
                       "(" + left + ", " + right + ")";
            }

            // String concatenation with an enum or user-typed operand spells the engine's exact BCL call.
            // The engine's EmitStringConcat binds string.Concat(object, object) UNCONDITIONALLY for a string
            // side — never a user-defined '+' or implicit conversion — where verbatim C# would prefer one.
            if (node.Operator == ExprOperator.Add)
            {
                var leftKindForConcat = Estimate(node.Left);
                var rightKindForConcat = Estimate(node.Right);
                bool leftIsString = leftKindForConcat.Category == OperandCategory.String;
                bool rightIsString = rightKindForConcat.Category == OperandCategory.String;
                if (leftIsString != rightIsString)
                {
                    var otherSide = leftIsString ? rightKindForConcat : leftKindForConcat;
                    if (otherSide.Category == OperandCategory.Enum ||
                        otherSide.Category == OperandCategory.Reference ||
                        otherSide.Category == OperandCategory.Other)
                        return "global::System.String.Concat((object)(" + left + "), (object)(" + right + "))";
                }
            }

            if (node.Operator == ExprOperator.LeftShift || node.Operator == ExprOperator.RightShift)
                right = ShiftCountSpelling(right, Estimate(node.Right));

            // A coalesce over numeric operands of DIFFERING kinds unifies in the ENGINE's promotion: the left
            // converts to the promoted type lifted (it is nullable by construction — '??' demands it), the
            // right at its own nullability. Verbatim C# would type some of these pairs differently and refuse
            // others outright (int? ?? uint is CS0019), so the promotion is written down as casts.
            if (node.Operator == ExprOperator.Coalesce)
            {
                // 'null ?? x' is x boxed to object on the engine; the constant-null left arm is never spelled.
                if (leftKind.Category == OperandCategory.NullLiteral)
                    return "((object)(" + right + "))";
                if (leftKind.Category == OperandCategory.Numeric && rightKind.Category == OperandCategory.Numeric &&
                    leftKind.Kind != rightKind.Kind)
                {
                    if (!NumericTable.TryPromote(leftKind.Kind, rightKind.Kind, out var promoted))
                        return null;   // unreachable behind a Supported verdict; kept inert
                    var keyword = KindKeyword(promoted);
                    if (keyword == null)
                        return null;
                    left = "((" + keyword + "?)(" + left + "))";
                    right = "((" + keyword + (rightKind.IsNullable ? "?" : string.Empty) + ")(" + right + "))";
                }

                // Pin the narrower operand to the wider type so C#'s '??' typing — which also consults
                // user-defined conversions the engine never binds — has nothing left to decide.
                if (coalesceRelation == TypeRelation.LeftWidensToRight && coalesceWiderFq != null)
                    left = "((" + coalesceWiderFq + ")(" + left + "))";
                else if (coalesceRelation == TypeRelation.RightWidensToLeft && coalesceWiderFq != null)
                    right = "((" + coalesceWiderFq + ")(" + right + "))";
            }

            return "(" + left + " " + op + " " + right + ")";
        }

        /// <summary>A binary NotDefined verdict, forwarded under the id and sentence the engine's own refusal
        /// site composes: HED1005 for logical, HED1007 for a coalesce with no common type, HED1008 for the
        /// rest. A coalesce over a non-null-assignable left is the engine's HED1006, which is not in the
        /// forwarded set and stays a silent degrade.</summary>
        private string RefuseBinaryAsEngine(BinaryNode node, string op, in OperandKind left, in OperandKind right)
        {
            var reason = "operator '" + op + "' over operand kinds the shared table does not emit";
            if (node.Operator == ExprOperator.AndAlso || node.Operator == ExprOperator.OrElse)
            {
                // The engine tests the left operand first and names the first one that is not exactly bool.
                bool leftIsBareBool = left.Category == OperandCategory.Bool && !left.IsNullable;
                var offenderName = EngineTypeName(leftIsBareBool ? node.Right : node.Left);
                return RefuseAsEngine(HeddleDiagnosticIds.LogicalOperatorRequiresBool, node.Position,
                    offenderName == null
                        ? null
                        : "Operator '" + op + "' requires bool operands, but the operand type is " +
                          offenderName + ".",
                    reason);
            }

            if (node.Operator == ExprOperator.Coalesce)
            {
                if (!left.IsNullAssignable)
                    return Refuse(reason);
                var leftName = EngineTypeName(node.Left);
                var rightName = EngineTypeName(node.Right);
                return RefuseAsEngine(HeddleDiagnosticIds.TernaryArmsNoCommonType, node.Position,
                    leftName == null || rightName == null
                        ? null
                        : "The conditional operator arms have no common type (" + leftName + " vs " +
                          rightName + ").",
                    reason);
            }

            var l = EngineTypeName(node.Left);
            var r = EngineTypeName(node.Right);
            return RefuseAsEngine(HeddleDiagnosticIds.BinaryOperatorNotDefined, node.Position,
                l == null || r == null
                    ? null
                    : "Operator '" + op + "' is not defined for operand types " + l + " and " + r + ".",
                reason);
        }

        /// <summary>C# accepts only an <c>int</c> (or implicitly-int) shift count; the runtime converts ANY
        /// integral count with a truncating <c>Expression.Convert</c>. The same truncation in C# is the
        /// <c>(int)</c> cast — the emission sits inside <c>unchecked</c>, so a wide count truncates to the
        /// same bits the runtime keeps — lifted to <c>(int?)</c> when the count is nullable so C#'s lifted
        /// shift carries the null through exactly as the runtime's lifted tree does. An implicitly-int count
        /// needs no cast even when nullable: C# lifts the implicit conversion with the operator.</summary>
        private static string ShiftCountSpelling(string count, in OperandKind kind)
        {
            if (kind.Category != OperandCategory.Numeric)
                return count;   // unreachable behind a Supported shift verdict; kept inert
            if (kind.Kind == NumericKind.Int32 || NumericTable.IsImplicit(kind.Kind, NumericKind.Int32))
                return count;
            return (kind.IsNullable ? "((int?)" : "((int)") + count + ")";
        }

        private string WriteTernary(TernaryNode node)
        {
            if (ConstantFolding.TryEngineConstantSpelling(node, out var engineConstant))
                return engineConstant;
            var c = Write(node.Condition);
            var t = Write(node.WhenTrue);
            var f = Write(node.WhenFalse);
            if (c == null || t == null || f == null)
                return null;
            var tKind = Estimate(node.WhenTrue);
            var fKind = Estimate(node.WhenFalse);
            var condition = Estimate(node.Condition);
            var armRelation = RelateOperands(node.WhenTrue, node.WhenFalse, out var armWiderFq);
            var verdict = NativeOperatorRules.ClassifyTernary(condition, tKind, fKind, armRelation);
            if (verdict == OperatorVerdict.NotDefined)
            {
                if (condition.Category != OperandCategory.Bool || condition.IsNullable)
                {
                    var conditionName = EngineTypeName(node.Condition);
                    return RefuseAsEngine(HeddleDiagnosticIds.TernaryConditionNotBool, node.Position,
                        conditionName == null
                            ? null
                            : "The conditional operator requires a bool condition, but the condition type is " +
                              conditionName + ".",
                        "conditional arms the shared table does not unify");
                }

                // The engine's ternary sentence names no operand types; the stray space is its exact spelling.
                return RefuseAsEngine(HeddleDiagnosticIds.TernaryArmsNoCommonType, node.Position,
                    "The conditional operator arms have no common type (the two arms ).",
                    "conditional arms the shared table does not unify");
            }

            if (verdict != OperatorVerdict.Supported)
                return Refuse("conditional arms the shared table does not unify");

            // Numeric arms of DIFFERING kinds unify in the ENGINE's promotion, which is written down as an
            // explicit cast on both arms so C#'s own conditional typing never gets a vote — it would pick the
            // same type for most pairs and refuse int-against-uint outright (CS0173) where the engine renders
            // long. Equal kinds are returned untouched by the engine's unifier (a char pair stays char), so
            // they stay verbatim; so do null arms, whose type C# infers from the other arm as the engine does.
            if (tKind.Category == OperandCategory.Numeric && fKind.Category == OperandCategory.Numeric &&
                tKind.Kind != fKind.Kind)
            {
                if (!NumericTable.TryPromote(tKind.Kind, fKind.Kind, out var promoted))
                    return null;   // unreachable behind a Supported verdict; kept inert
                var keyword = KindKeyword(promoted);
                if (keyword == null)
                    return null;
                var unified = keyword + (tKind.IsNullable || fKind.IsNullable ? "?" : string.Empty);
                t = "((" + unified + ")(" + t + "))";
                f = "((" + unified + ")(" + f + "))";
            }

            // Cast the narrower arm to the wider type so C#'s conditional inference — which weighs
            // user-defined conversions and can find the pair ambiguous (CS0172) — never gets a vote.
            if (armRelation == TypeRelation.LeftWidensToRight && armWiderFq != null)
                t = "((" + armWiderFq + ")(" + t + "))";
            else if (armRelation == TypeRelation.RightWidensToLeft && armWiderFq != null)
                f = "((" + armWiderFq + ")(" + f + "))";

            return "(" + c + " ? " + t + " : " + f + ")";
        }

        /// <summary>The C# keyword for a numeric kind — what the unified-arm cast spells.</summary>
        private static string KindKeyword(NumericKind kind)
        {
            switch (kind)
            {
                case NumericKind.SByte: return "sbyte";
                case NumericKind.Byte: return "byte";
                case NumericKind.Int16: return "short";
                case NumericKind.UInt16: return "ushort";
                case NumericKind.Int32: return "int";
                case NumericKind.UInt32: return "uint";
                case NumericKind.Int64: return "long";
                case NumericKind.UInt64: return "ulong";
                case NumericKind.Char: return "char";
                case NumericKind.Single: return "float";
                case NumericKind.Double: return "double";
                case NumericKind.Decimal: return "decimal";
                default: return null;
            }
        }

        /// <summary>
        /// The engine's <c>FriendlyName</c> spelling of the type this operand compiles to on the engine — the
        /// word its refusal sentence prints — or null where the writer cannot reproduce it exactly, in which
        /// case the refusal is not forwarded. The estimator's kinds already mirror the engine's promotions, so
        /// a composite operand's keyword spelling needs no symbol; the non-primitive categories prefer the
        /// resolved symbol and fall back to the kind's type identity where the simple name is unambiguous.
        /// </summary>
        private string EngineTypeName(ExprNode node)
        {
            var kind = Estimate(node);
            switch (kind.Category)
            {
                case OperandCategory.NullLiteral:
                    return "object";   // the engine types the null-literal constant as object
                case OperandCategory.String:
                    return "string";
                case OperandCategory.Bool:
                    return kind.IsNullable ? "bool?" : "bool";
                case OperandCategory.Numeric:
                {
                    var keyword = KindKeyword(kind.Kind);
                    return keyword == null ? null : kind.IsNullable ? keyword + "?" : keyword;
                }
                case OperandCategory.Enum:
                case OperandCategory.Reference:
                case OperandCategory.Other:
                {
                    var symbol = ArgumentType(node);
                    if (symbol != null && IsEstablishedType(symbol))
                        return EngineFriendlyName(symbol);
                    return IdentitySimpleName(kind);
                }
                default:
                    return null;
            }
        }

        /// <summary>Mirrors the engine's <c>FriendlyName(Type)</c> over a symbol: C# keywords for the special
        /// types, the underlying spelling plus <c>?</c> for <c>Nullable&lt;T&gt;</c>, reflection's
        /// <c>Type.Name</c> for the rest.</summary>
        private static string EngineFriendlyName(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Int32: return "int";
                case SpecialType.System_UInt32: return "uint";
                case SpecialType.System_Int64: return "long";
                case SpecialType.System_UInt64: return "ulong";
                case SpecialType.System_Int16: return "short";
                case SpecialType.System_UInt16: return "ushort";
                case SpecialType.System_Byte: return "byte";
                case SpecialType.System_SByte: return "sbyte";
                case SpecialType.System_Double: return "double";
                case SpecialType.System_Single: return "float";
                case SpecialType.System_Decimal: return "decimal";
                case SpecialType.System_String: return "string";
                case SpecialType.System_Boolean: return "bool";
                case SpecialType.System_Char: return "char";
                case SpecialType.System_Object: return "object";
            }

            var underlying = SymbolFacts.Unwrap(type, out var lifted);
            if (lifted)
            {
                var inner = EngineFriendlyName(underlying);
                return inner == null ? null : inner + "?";
            }

            return ClrTypeName(type);
        }

        /// <summary>Reflection's <c>Type.Name</c>, which the engine's <c>FriendlyName</c> falls back to: the
        /// metadata name — arity suffix, no type arguments, no keywords — with CLR array suffixes.</summary>
        private static string ClrTypeName(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                var element = ClrTypeName(array.ElementType);
                if (element == null)
                    return null;
                return element + (array.Rank == 1 ? "[]" : "[" + new string(',', array.Rank - 1) + "]");
            }

            return string.IsNullOrEmpty(type.MetadataName) ? null : type.MetadataName;
        }

        /// <summary>The simple name off a kind's opaque type identity, for composite operands with no symbol
        /// behind them. Only an identity whose last segment is generic-free reproduces reflection's
        /// <c>Type.Name</c>; anything else answers null and the refusal stays a silent degrade.</summary>
        private static string IdentitySimpleName(in OperandKind kind)
        {
            var identity = kind.TypeIdentity;
            if (identity == null || identity.IndexOf('<') >= 0)
                return null;
            var dot = identity.LastIndexOf('.');
            var name = dot < 0 ? identity : identity.Substring(dot + 1);
            if (name.StartsWith("global::", StringComparison.Ordinal))
                name = name.Substring("global::".Length);
            if (name.Length == 0)
                return null;
            return kind.IsNullable ? name + "?" : name;
        }

        /// <summary>The caller-computed <see cref="TypeRelation"/> for a coalesce pair or ternary arms.
        /// Reference pairs accept only implicit reference conversions — the assignability walk the engine's
        /// unifier performs — never user-defined ones. <paramref name="widerTypeFq"/> is the cast target for
        /// the widening relations.</summary>
        private TypeRelation RelateOperands(ExprNode left, ExprNode right, out string widerTypeFq)
        {
            widerTypeFq = null;
            var lk = Estimate(left);
            var rk = Estimate(right);
            if (OperandKind.KnownSameType(lk, rk))
                return TypeRelation.Identical;

            bool leftRef = lk.Category == OperandCategory.Reference || lk.Category == OperandCategory.String;
            bool rightRef = rk.Category == OperandCategory.Reference || rk.Category == OperandCategory.String;
            if (leftRef != rightRef)
                return TypeRelation.None;   // a value type never unifies with a reference type on the engine
            if (!leftRef)
                return OperandKind.KnownDifferentType(lk, rk) ? TypeRelation.None : TypeRelation.Unknown;

            var compilation = _typeFacts?.Compilation;
            if (compilation == null)
                return TypeRelation.Unknown;
            var leftSymbol = ArgumentType(left);
            var rightSymbol = ArgumentType(right);
            if (leftSymbol == null || rightSymbol == null)
                return TypeRelation.Unknown;
            leftSymbol = SymbolFacts.Unwrap(leftSymbol, out _);
            rightSymbol = SymbolFacts.Unwrap(rightSymbol, out _);

            var leftToRight = compilation.ClassifyCommonConversion(leftSymbol, rightSymbol);
            if (leftToRight.IsIdentity)
                return TypeRelation.Identical;
            if (leftToRight.Exists && leftToRight.IsImplicit && leftToRight.IsReference)
            {
                widerTypeFq = SymbolTypeResolver.FullyQualified(rightSymbol);
                return TypeRelation.LeftWidensToRight;
            }

            var rightToLeft = compilation.ClassifyCommonConversion(rightSymbol, leftSymbol);
            if (rightToLeft.Exists && rightToLeft.IsImplicit && rightToLeft.IsReference)
            {
                widerTypeFq = SymbolTypeResolver.FullyQualified(leftSymbol);
                return TypeRelation.RightWidensToLeft;
            }

            return TypeRelation.None;
        }

        /// <summary>
        /// The caller-computed <see cref="OperatorWitness"/> for a pair with a reference/user-struct side.
        /// Bound is claimed only for an operator declared on an operand type itself whose parameters are
        /// exactly the operands' types (the shape the engine's factory certainly binds); Absent only when no
        /// operator by the name exists anywhere in either base chain. Nullable-wrapped operands stay Unknown —
        /// lifted user operators are not modeled. The out spellings feed the adapter emission.
        /// </summary>
        private OperatorWitness BinaryWitness(ExprOperator op, ExprNode leftNode, ExprNode rightNode,
            out string leftFq, out string rightFq, out string resultFq)
        {
            leftFq = rightFq = resultFq = null;
            var name = UserOperatorMetadataName(op);
            if (name == null)
                return OperatorWitness.Unknown;
            var leftSymbol = ArgumentType(leftNode);
            var rightSymbol = ArgumentType(rightNode);
            if (leftSymbol == null || rightSymbol == null)
                return OperatorWitness.Unknown;
            leftSymbol = SymbolFacts.Unwrap(leftSymbol, out var leftLifted);
            rightSymbol = SymbolFacts.Unwrap(rightSymbol, out var rightLifted);
            if (leftLifted || rightLifted)
                return OperatorWitness.Unknown;

            bool any = false;
            IMethodSymbol bound = null;
            foreach (var container in new[] { leftSymbol, rightSymbol })
            {
                for (var type = container; type != null; type = type.BaseType)
                {
                    foreach (var member in type.GetMembers(name))
                    {
                        if (!(member is IMethodSymbol method) || !method.IsStatic ||
                            method.DeclaredAccessibility != Accessibility.Public ||
                            method.Parameters.Length != 2)
                            continue;
                        any = true;
                        if (bound == null &&
                            SymbolEqualityComparer.Default.Equals(type, container) &&
                            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, leftSymbol) &&
                            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, rightSymbol))
                            bound = method;
                    }
                }
            }

            if (bound == null)
                return any ? OperatorWitness.Unknown : OperatorWitness.Absent;

            bool boolRequired = op != ExprOperator.Add && op != ExprOperator.Subtract &&
                                op != ExprOperator.Multiply && op != ExprOperator.Divide &&
                                op != ExprOperator.Modulo;
            if (bound.ReturnsVoid ||
                boolRequired && bound.ReturnType.SpecialType != SpecialType.System_Boolean)
                return OperatorWitness.Unknown;
            if (_resolver.ClassifyTypeName(leftSymbol, out _) != SymbolTypeResolver.NameFault.None ||
                _resolver.ClassifyTypeName(rightSymbol, out _) != SymbolTypeResolver.NameFault.None ||
                _resolver.ClassifyTypeName(bound.ReturnType, out _) != SymbolTypeResolver.NameFault.None)
                return OperatorWitness.Unknown;

            leftFq = SymbolTypeResolver.FullyQualified(leftSymbol);
            rightFq = SymbolTypeResolver.FullyQualified(rightSymbol);
            resultFq = SymbolTypeResolver.FullyQualified(bound.ReturnType);
            return OperatorWitness.Bound;
        }

        private static string UserOperatorMetadataName(ExprOperator op)
        {
            switch (op)
            {
                case ExprOperator.Add: return "op_Addition";
                case ExprOperator.Subtract: return "op_Subtraction";
                case ExprOperator.Multiply: return "op_Multiply";
                case ExprOperator.Divide: return "op_Division";
                case ExprOperator.Modulo: return "op_Modulus";
                case ExprOperator.Equal: return "op_Equality";
                case ExprOperator.NotEqual: return "op_Inequality";
                case ExprOperator.LessThan: return "op_LessThan";
                case ExprOperator.LessThanOrEqual: return "op_LessThanOrEqual";
                case ExprOperator.GreaterThan: return "op_GreaterThan";
                case ExprOperator.GreaterThanOrEqual: return "op_GreaterThanOrEqual";
                default: return null;
            }
        }

        private static string AdapterMethodName(ExprOperator op)
        {
            switch (op)
            {
                case ExprOperator.Add: return "Add";
                case ExprOperator.Subtract: return "Subtract";
                case ExprOperator.Multiply: return "Multiply";
                case ExprOperator.Divide: return "Divide";
                case ExprOperator.Modulo: return "Modulo";
                case ExprOperator.LessThan: return "LessThan";
                case ExprOperator.LessThanOrEqual: return "LessOrEqual";
                case ExprOperator.GreaterThan: return "GreaterThan";
                case ExprOperator.GreaterThanOrEqual: return "GreaterOrEqual";
                default: return null;
            }
        }

        /// <summary>Computes the witness only where the verdict can consume it: an arithmetic, relational or
        /// equality operator over a pair with a reference/user-struct side. String concatenation is excluded —
        /// its Supported verdict never binds a user operator.</summary>
        private OperatorWitness WitnessIfUserPair(ExprOperator op, ExprNode leftNode, ExprNode rightNode,
            in OperandKind left, in OperandKind right, out string leftFq, out string rightFq, out string resultFq)
        {
            leftFq = rightFq = resultFq = null;
            bool userSide = left.Category == OperandCategory.Reference || left.Category == OperandCategory.Other ||
                            right.Category == OperandCategory.Reference || right.Category == OperandCategory.Other;
            if (!userSide || UserOperatorMetadataName(op) == null)
                return OperatorWitness.Unknown;
            if (op == ExprOperator.Add &&
                (left.Category == OperandCategory.String || right.Category == OperandCategory.String))
                return OperatorWitness.Unknown;
            return BinaryWitness(op, leftNode, rightNode, out leftFq, out rightFq, out resultFq);
        }

        #region Operand-kind estimation

        /// <summary>The static-kind estimate for a sub-expression; anything unknown degrades conservatively.</summary>
        private OperandKind Estimate(ExprNode node)
        {
            if (node == null)
                return OperandKind.Unknown;
            if (_estimates.TryGetValue(node, out var cached))
                return cached;
            var estimate = EstimateCore(node);
            _estimates[node] = estimate;
            return estimate;
        }

        private OperandKind EstimateCore(ExprNode node)
        {
            switch (node)
            {
                case LiteralNode literal:
                    return literal.LiteralError != null ? OperandKind.Unknown : EstimateLiteral(literal.Value);
                case ThisNode _:
                    return SymbolFacts.Classify(_modelType);
                case PathNode path:
                    return EstimatePath(path);
                case CallNode call:
                    return EstimateCall(call);
                case UnaryNode unary:
                    return NativeOperatorRules.UnaryResult(unary.Operator, Estimate(unary.Operand));
                case BinaryNode binary:
                {
                    var left = Estimate(binary.Left);
                    var right = Estimate(binary.Right);
                    var relation = binary.Operator == ExprOperator.Coalesce
                        ? RelateOperands(binary.Left, binary.Right, out _)
                        : TypeRelation.Unknown;
                    var witness = WitnessIfUserPair(binary.Operator, binary.Left, binary.Right, left, right,
                        out _, out _, out _);
                    return NativeOperatorRules.BinaryResult(binary.Operator, left, right, relation, witness);
                }
                case TernaryNode ternary:
                {
                    var whenTrue = Estimate(ternary.WhenTrue);
                    var whenFalse = Estimate(ternary.WhenFalse);
                    return NativeOperatorRules.TernaryResult(whenTrue, whenFalse,
                        RelateOperands(ternary.WhenTrue, ternary.WhenFalse, out _));
                }
                case IndexNode index:
                    return SymbolFacts.Classify(ResolveIndex(index).ResultType);
                default:
                    return OperandKind.Unknown;   // MethodCallNode — never emitted here
            }
        }

        private static OperandKind EstimateLiteral(object value)
        {
            switch (value)
            {
                case null: return OperandKind.Null;
                case bool _: return OperandKind.Of(OperandCategory.Bool);
                case string _: return OperandKind.Of(OperandCategory.String);
                default:
                    return OperandKind.Numeric(NumericTable.FromClrType(value.GetType()), false);
            }
        }

        private OperandKind EstimatePath(PathNode path) => SymbolFacts.Classify(PathType(path));

        /// <summary>The resolved static type of a member path, before the shared descriptor is taken of it. Null
        /// where nothing resolves — the same condition <see cref="EstimatePath"/> answers <c>Unknown</c> for.</summary>
        private ITypeSymbol PathType(PathNode path)
        {
            if (HasExpressionTarget(path))
            {
                var targetType = TargetType(path.Target);
                if (!IsEstablishedType(targetType))
                    return null;
                var targeted = _resolver.ResolvePath(targetType, path.Segments);
                return targeted.Kind == SymbolTypeResolver.PathKind.Resolved ? targeted.ResultType : null;
            }

            var prop = PropRoot(path);
            if (prop != null)
                return PropPathType(prop, path);

            var start = path.RootRef ? _rootModelType : _modelType;
            if (start == null)
                return null;
            var resolution = _resolver.ResolvePath(start, path.Segments);
            return resolution.Kind == SymbolTypeResolver.PathKind.Resolved ? resolution.ResultType : null;
        }

        /// <summary>
        /// The static type of an argument as the <b>engine</b> ranks it: <c>NativeExpressionCompiler</c> hands
        /// <c>OverloadRank</c> the compiled expression's own <c>Type</c>. Only the shapes this writer resolves a
        /// symbol for answer — a member path and a call's declared return; everything else is left to the shared
        /// descriptor, which names exactly the types it can name and says nothing about the rest.
        /// <para>Handing the descriptor alone to the export binder made every struct, enum, class and
        /// <c>object</c> argument look like an argument nothing had resolved, and an unresolved argument is what the
        /// sole-candidate shortcut is allowed to bind past. That wrote a call the consumer's compiler answers with
        /// CS1503 where the engine answers <c>HED1012</c>.</para>
        /// </summary>
        private ITypeSymbol ArgumentType(ExprNode node)
        {
            switch (node)
            {
                case ThisNode _:
                    return _modelType;
                case PathNode path:
                    return PathType(path);
                case CallNode call:
                    return _typeFacts?.Compilation == null ? null : CallReturnType(call, _typeFacts.Compilation);
                case IndexNode index:
                    return ResolveIndex(index).ResultType;
                default:
                    return null;
            }
        }

        /// <summary>Built-in and export calls contribute their return type if the ranker accepts them; otherwise unknown.</summary>
        private OperandKind EstimateCall(CallNode call)
        {
            if (_exports != null && _exports.TryGet(call.Name, out _))
            {
                if (DefaultShims.ContainsKey(call.Name))
                {
                    var merged = BindMergedCall(call);
                    if (merged == null)
                        return OperandKind.Unknown;
                    return merged.IsBuiltIn
                        ? DefaultFunctionBinder.ReturnKind(merged.Row)
                        : SymbolFacts.Classify(merged.ReturnType);
                }

                var exportBinding = BindExportCall(call);
                return exportBinding == null
                    ? OperandKind.Unknown
                    : SymbolFacts.Classify(exportBinding.ReturnType);
            }

            var binding = BindDefaultCall(call);
            return binding == null ? OperandKind.Unknown : DefaultFunctionBinder.ReturnKind(binding.Row);
        }

        /// <summary>Resolves a call against the shared ranker; returns null if refused or not a built-in.</summary>
        private ExportFunctionBinder.Binding BindExportCall(CallNode call)
        {
            if (_exportBindings.TryGetValue(call, out var cached))
                return cached;

            ExportFunctionBinder.Binding binding = null;
            if (_typeFacts != null && _exports != null && _exports.TryGet(call.Name, out var entry))
            {
                var argKinds = new OperandKind[call.Arguments.Count];
                var argTypes = new ITypeSymbol[call.Arguments.Count];
                for (int i = 0; i < argKinds.Length; i++)
                {
                    argKinds[i] = Estimate(call.Arguments[i]);
                    argTypes[i] = ArgumentType(call.Arguments[i]);
                }

                binding = ExportFunctionBinder.TryBind(_typeFacts, _resolver, call.Name, entry.Overloads, argKinds,
                    argTypes, out var refusal);
                RecordIfProvenIllegal(call, refusal);

                if (binding != null && !CanWriteCallTo(binding.Overload, out var display))
                {
                    _unnameableCalls.Add((call.Name, call.Position, display));
                    binding = null;
                }
            }

            _exportBindings[call] = binding;
            return binding;
        }

        /// <summary>
        /// Whether generated code may write a call to this export at all. The emitted call spells two names — the
        /// container type and the method — and reflection ignores both <c>[Obsolete]</c> and accessibility where the
        /// consumer's compiler does not: an export declared <c>[Obsolete(…, error: true)]</c>, or one whose
        /// container this assembly may not name, renders perfectly well on the dynamic tier and stops the
        /// <em>consumer's</em> build with CS0619 or CS0122, off a <c>.g.cs</c> no one can edit. That is a build the
        /// template's author did not break and cannot fix, so the template degrades instead.
        /// <para>Warning-level <c>[Obsolete]</c> is deliberately not one of these: the generated file opens with a
        /// blanket <c>#pragma warning disable</c>, so it raises nothing there, and refusing it would take every
        /// deprecated helper in a host's codebase off the precompiled tier to prevent a message no one would see.
        /// Neither is a signature the call site does not spell — an export whose <b>return</b> type the consumer
        /// may not name compiles and renders identically, because what is written here is the method, not what it
        /// hands back.</para>
        /// <para>Accessibility is asked of the container rather than through the value-position classifier: the
        /// container is a static class, which may not hold a value and is perfectly writable as a call receiver. It
        /// is a reachable arm — the registry accepts a public container nested in an <c>internal</c> one, exactly
        /// as reflection's <c>IsNestedPublic</c> does, and from a referenced assembly that name is CS0122 here.
        /// From the compilation's own source it is nameable, and those exports keep pre-compiling.</para>
        /// </summary>
        private bool CanWriteCallTo(FunctionExportResolver.ExportOverloadInfo overload, out string display)
        {
            display = null;
            if (!SymbolTypeResolver.IsObsoleteError(overload.Method) &&
                !SymbolTypeResolver.IsObsoleteError(overload.Container) &&
                (_resolver == null || _resolver.IsAccessibleFromCompilation(overload.Container)))
                return true;

            display = overload.Method.ToDisplayString();
            return false;
        }

        /// <summary>Records proven-illegal calls once per site to prevent duplicate reports.</summary>
        private void RecordIfProvenIllegal(CallNode call, in Binding.BindRefusal refusal)
        {
            if (refusal.Kind == Binding.BindRefusalKind.ProvenIllegal)
                _unbindableCalls.Add((call.Name, call.Position, refusal.Detail, refusal.RuntimeDiagnosticId));
        }

        private DefaultFunctionBinder.Binding BindDefaultCall(CallNode call)
        {
            if (_bindings.TryGetValue(call, out var cached))
                return cached;

            DefaultFunctionBinder.Binding binding = null;
            if (DefaultShims.ContainsKey(call.Name))
            {
                var argKinds = new OperandKind[call.Arguments.Count];
                var argTypes = new ITypeSymbol[call.Arguments.Count];
                for (int i = 0; i < argKinds.Length; i++)
                {
                    argKinds[i] = Estimate(call.Arguments[i]);
                    argTypes[i] = ArgumentType(call.Arguments[i]);
                }

                binding = DefaultFunctionBinder.TryBind(call.Name, argKinds, argTypes, out var refusal);
                RecordIfProvenIllegal(call, refusal);
            }

            _bindings[call] = binding;
            return binding;
        }

        #endregion
    }
}
