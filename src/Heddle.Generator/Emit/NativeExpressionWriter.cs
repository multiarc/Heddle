using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Heddle.Generator.Binding;
using Heddle.Language.Expressions;
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

        private readonly SymbolTypeResolver _resolver;
        private readonly ITypeSymbol _modelType;
        private readonly string _modelLocal;
        private readonly TemplateEmitter.PropLayoutInfo _props;
        private readonly FunctionExportResolver _exports;
        private bool _usedModel;
        private readonly HashSet<string> _usedDefaultFunctions = new HashSet<string>();
        private readonly List<(string Name, string Aqn, int OverloadCount)> _usedExports =
            new List<(string, string, int)>();
        private readonly List<(string Name, Heddle.Strings.Core.BlockPosition Position)> _unresolvableFunctions =
            new List<(string, Heddle.Strings.Core.BlockPosition)>();
        private readonly List<SymbolMemberResolver.MemberFailure> _memberFailures =
            new List<SymbolMemberResolver.MemberFailure>();

        private readonly List<(string Name, Heddle.Strings.Core.BlockPosition Position, string Detail,
            string RuntimeDiagnosticId)> _unbindableCalls =
            new List<(string, Heddle.Strings.Core.BlockPosition, string, string)>();

        private readonly List<(string Name, Heddle.Strings.Core.BlockPosition Position, string Display)>
            _unnameableCalls = new List<(string, Heddle.Strings.Core.BlockPosition, string)>();

        private readonly SymbolTypeFacts _typeFacts;

        public NativeExpressionWriter(SymbolTypeResolver resolver, ITypeSymbol modelType, string modelLocal,
            FunctionExportResolver exports, SymbolTypeFacts typeFacts, Func<string> allocateHopLocal,
            TemplateEmitter.PropLayoutInfo props = null)
        {
            _resolver = resolver;
            _modelType = modelType;
            _modelLocal = modelLocal;
            _props = props;
            _exports = exports;
            _typeFacts = typeFacts;
            // Shared with the emitter that made this writer, so the names cannot collide with the ones it allocates
            // for paths of its own in the same block.
            _allocateHopLocal = allocateHopLocal;
        }

        private readonly Func<string> _allocateHopLocal;

        public bool UsedModel => _usedModel;

        /// <summary>Default (built-in) function names bound to the <c>PrecompiledFunctions</c> shim in this
        /// expression; the emitter records one manifest <c>FunctionBindings</c> row per name.</summary>
        public IEnumerable<string> UsedDefaultFunctions => _usedDefaultFunctions;

        /// <summary>Discovered <c>[ExportFunctions]</c> names bound directly to their container in this expression:
        /// the function name with its container AQN-sans-version target and that container's overload count.
        /// A name exported by more than one container yields one row <b>per container</b>, matching
        /// the runtime's merged registry — the gauntlet compares each row's count exactly.</summary>
        public IReadOnlyList<(string Name, string Aqn, int OverloadCount)> UsedExports => _usedExports;

        /// <summary>Function names in this expression resolvable from neither the default table nor any referenced
        /// export; each name with its <c>.heddle</c> position. When non-empty,
        /// the emitter degrades the template to a fallback-marker entry and reports <c>HED7014</c>.</summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position)> UnresolvableFunctions =>
            _unresolvableFunctions;

        /// <summary>Genuine member-path failures in this expression — a property not found on a resolved,
        /// non-dynamic receiver (<c>HED7008</c>). Drained by the emitter and reported at the <c>.heddle</c>
        /// span.</summary>
        public IReadOnlyList<SymbolMemberResolver.MemberFailure> MemberFailures => _memberFailures;

        /// <summary>Function calls the <b>shared</b> overload ranker <i>proved</i> illegal — an
        /// ambiguous flat-Pareto front or no applicable overload, over arguments the estimator typed — each with its
        /// <c>.heddle</c> position, the runtime-shaped sentence naming the candidates, and the diagnostic id
        /// HED7025. Drained by the emitter and reported at Error.
        /// <para>Refusals the generator could <b>not</b> prove (an <c>Unknown</c> argument estimate, an unspellable
        /// cast target, a params-expanded bind) never land here: they stay the silent degrade they always were,
        /// because the generator has established nothing about what the runtime will do.</para></summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position, string Detail,
            string RuntimeDiagnosticId)> UnbindableFunctionCalls => _unbindableCalls;

        /// <summary>Exported host functions this compilation may not write a call to, each with its
        /// <c>.heddle</c> position and the signature to name in the message. Drained by the emitter as
        /// <c>HED7030</c>, the same id a model type the consumer's compiler rejects gets — from the consumer's
        /// side it is the same situation.</summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position, string Display)>
            UnnameableFunctionCalls => _unnameableCalls;

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
                return BindExportCall(call)?.ReturnType;
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
                default:
                    return null; // IndexNode / MethodCallNode / ThisNode handled elsewhere or unsupported
            }
        }

        private string WriteCall(CallNode call)
        {
            bool isDefault = DefaultShims.TryGetValue(call.Name, out var shim);
            bool hasExport = _exports != null && _exports.TryGet(call.Name, out var export);

            if (isDefault && hasExport)
                return null; // forwarder group across shim + export — deferred
            if (!isDefault && !hasExport)
            {
                _unresolvableFunctions.Add((call.Name, call.Position));
                return null;
            }

            var args = new string[call.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
            {
                // An argument the two tiers evaluate in different types picks a different overload on each of them,
                // so it degrades here even though the number it carries is the same.
                if (ConstantFolding.TiersEvaluateDifferently(call.Arguments[i]))
                    return null;
                args[i] = Write(call.Arguments[i]);
                if (args[i] == null)
                    return null;
            }

            if (hasExport)
            {
                var exportBinding = BindExportCall(call);
                if (exportBinding == null)
                    return null;

                for (int i = 0; i < args.Length; i++)
                    if (exportBinding.ArgumentCasts[i] != null)
                        args[i] = "(" + exportBinding.ArgumentCasts[i] + ")(" + args[i] + ")";

                _exports.TryGet(call.Name, out var entry);
                foreach (var row in entry.ManifestRows)
                    if (!_usedExports.Contains((call.Name, row.Aqn, row.OverloadCount)))
                        _usedExports.Add((call.Name, row.Aqn, row.OverloadCount));

                return exportBinding.Overload.ContainerGlobalName + "." + exportBinding.Overload.MethodName +
                       "(" + string.Join(", ", args) + ")";
            }

            var binding = BindDefaultCall(call);
            if (binding == null)
                return null;

            for (int i = 0; i < args.Length; i++)
            {
                if (binding.ArgumentCasts[i] != null)
                    args[i] = "(" + binding.ArgumentCasts[i] + ")(" + args[i] + ")";
            }

            _usedDefaultFunctions.Add(call.Name);
            return "global::Heddle.Precompiled.PrecompiledFunctions." + shim + "(" + string.Join(", ", args) + ")";
        }

        /// <summary>
        /// The prop this path is rooted at, or null where it is not rooted at one. A body prop read wins over the
        /// model on a path's first segment — resolution is syntactic, which is what lets both tiers agree on it by
        /// rule rather than by coincidence — and once it does the model is out of the picture entirely: the read
        /// either roots at the prop or the expression degrades, never falls back to the member the prop shadows.
        /// </summary>
        private TemplateEmitter.PropSlotInfo PropRoot(PathNode path)
        {
            if (path.Target != null || path.RootRef || _props == null || path.Segments.Count == 0)
                return null;
            _props.ByName.TryGetValue(path.Segments[0], out var slot);
            return slot;
        }

        private string WritePath(PathNode path)
        {
            var prop = PropRoot(path);
            if (prop != null)
                return WritePropPath(prop, path);

            if (path.Target != null || path.RootRef || _modelType == null)
                return null;

            var resolution = _resolver.ResolvePath(_modelType, path.Segments);
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
                        ? _modelType
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

                return null;
            }

            // An expression's operands are boxed, and a ref struct cannot be. Left to the dynamic tier, which reads
            // it reflectively; emitted here it was CS0030 in the consumer's build over a template that renders.
            if (SymbolTypeResolver.EndsOnRefStruct(resolution))
                return null;

            _usedModel = true;
            var hops = new List<MemberPathWriter.HopEmit>(resolution.Hops.Count);
            foreach (var hop in resolution.Hops)
            {
                hops.Add(new MemberPathWriter.HopEmit(
                    hop.Receiver.IsValueType,
                    SymbolTypeResolver.IsNonNullableValueType(hop.Property),
                    SymbolTypeResolver.FullyQualified(hop.Property),
                    hop.Name,
                    !hop.Property.IsRefLikeType,
                    SymbolTypeResolver.FullyQualified(hop.Receiver)));
            }

            return MemberPathWriter.Write(_modelLocal, hops, _allocateHopLocal);
        }

        /// <summary>Emits the prop read: the boxed slot cast back to its declared type, then the remaining segments
        /// hopped off that type — the same shape a prop-rooted call-site parameter emits. Null degrades the
        /// expression; it never re-reads the model member the prop shadows.</summary>
        private string WritePropPath(TemplateEmitter.PropSlotInfo slot, PathNode path)
        {
            if (slot.Type == null)
                return null;

            var read = "((" + slot.TypeFq + ")global::Heddle.Precompiled.PrecompiledRuntime.Prop(in scope, " +
                       slot.Index + "))";
            if (path.Segments.Count == 1)
                return read;

            if (slot.Type.TypeKind == TypeKind.Dynamic)
                return null;

            var resolution = _resolver.ResolvePath(slot.Type, Rest(path));
            if (resolution.Kind != SymbolTypeResolver.PathKind.Resolved ||
                SymbolTypeResolver.EndsOnRefStruct(resolution))
                return null;

            return MemberPathWriter.Write(read, TemplateEmitter.MapHops(resolution), _allocateHopLocal);
        }

        /// <summary>The static type of a prop-rooted path, or null where the remaining segments do not resolve.</summary>
        private ITypeSymbol PropPathType(TemplateEmitter.PropSlotInfo slot, PathNode path)
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
            if (ConstantFolding.CompilerWouldReject(node))
                return null;
            var operand = Write(node.Operand);
            if (operand == null)
                return null;
            if (NativeOperatorRules.ClassifyUnary(node.Operator, Estimate(node.Operand)) != OperatorVerdict.Supported)
                return null;
            return "(" + op + operand + ")";
        }

        private string WriteBinary(BinaryNode node)
        {
            var op = OperatorLexeme.ForBinary(node.Operator);
            if (op == null)
                return null;
            // Degrade rather than emit something the host's compiler will reject: the engine discovers a constant
            // divide-by-zero or overflow when it renders, C# refuses to build it at all, and the tier whose behaviour
            // is the contract is the one that renders.
            if (ConstantFolding.CompilerWouldReject(node))
                return null;
            if (TierPromotionEscapes(node.Left, node.Right) || TierPromotionEscapes(node.Right, node.Left))
                return null;
            var left = Write(node.Left);
            var right = Write(node.Right);
            if (left == null || right == null)
                return null;
            if (NativeOperatorRules.Classify(node.Operator, Estimate(node.Left), Estimate(node.Right)) !=
                OperatorVerdict.Supported)
                return null;
            return "(" + left + " " + op + " " + right + ")";
        }

        /// <summary>
        /// Whether a constant operand the two tiers evaluated in different types can still meet
        /// <paramref name="partner"/> here. C# reached the constant in <c>uint</c> and the engine in <c>long</c>;
        /// promoting either against a signed integral, a real or a string lands both on the same type, so the
        /// operator produces the same bytes. An unsigned or sub-<c>int</c> partner does not — it leaves C# in
        /// <c>uint</c> while the engine stays in <c>long</c>, and <c>0u - (0-0u)</c> is 0 on one tier and
        /// 4294967296 short of it on the other — and neither does a partner this writer cannot type.
        /// </summary>
        private bool TierPromotionEscapes(ExprNode operand, ExprNode partner)
        {
            if (!ConstantFolding.TiersEvaluateDifferently(operand))
                return false;

            var kind = Estimate(partner);
            switch (kind.Category)
            {
                case OperandCategory.String:
                    return false;
                case OperandCategory.Numeric:
                    switch (kind.Kind)
                    {
                        case NumericKind.SByte:
                        case NumericKind.Int16:
                        case NumericKind.Int32:
                        case NumericKind.Int64:
                        case NumericKind.Single:
                        case NumericKind.Double:
                        case NumericKind.Decimal:
                            return false;
                        default:
                            return true;
                    }

                default:
                    return true;
            }
        }

        private string WriteTernary(TernaryNode node)
        {
            var c = Write(node.Condition);
            var t = Write(node.WhenTrue);
            var f = Write(node.WhenFalse);
            if (c == null || t == null || f == null)
                return null;
            if (NativeOperatorRules.ClassifyTernary(Estimate(node.Condition), Estimate(node.WhenTrue),
                    Estimate(node.WhenFalse)) != OperatorVerdict.Supported)
                return null;
            return "(" + c + " ? " + t + " : " + f + ")";
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
                case PathNode path:
                    return EstimatePath(path);
                case CallNode call:
                    return EstimateCall(call);
                case UnaryNode unary:
                    return NativeOperatorRules.UnaryResult(unary.Operator, Estimate(unary.Operand));
                case BinaryNode binary:
                    return NativeOperatorRules.BinaryResult(binary.Operator, Estimate(binary.Left),
                        Estimate(binary.Right));
                case TernaryNode ternary:
                {
                    var whenTrue = Estimate(ternary.WhenTrue);
                    var whenFalse = Estimate(ternary.WhenFalse);
                    return NativeOperatorRules.TernaryResult(whenTrue, whenFalse);
                }
                default:
                    return OperandKind.Unknown;   // IndexNode / MethodCallNode / ThisNode — never emitted here
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
            var prop = PropRoot(path);
            if (prop != null)
                return PropPathType(prop, path);

            if (path.Target != null || path.RootRef || _modelType == null)
                return null;
            var resolution = _resolver.ResolvePath(_modelType, path.Segments);
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
                case PathNode path:
                    return PathType(path);
                case CallNode call:
                    return _typeFacts?.Compilation == null ? null : CallReturnType(call, _typeFacts.Compilation);
                default:
                    return null;
            }
        }

        /// <summary>Built-in and export calls contribute their return type if the ranker accepts them; otherwise unknown.</summary>
        private OperandKind EstimateCall(CallNode call)
        {
            if (_exports != null && _exports.TryGet(call.Name, out _))
            {
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
            bool shadowedByExport = _exports != null && _exports.TryGet(call.Name, out _);
            if (!shadowedByExport && DefaultShims.ContainsKey(call.Name))
            {
                var argKinds = new OperandKind[call.Arguments.Count];
                for (int i = 0; i < argKinds.Length; i++)
                    argKinds[i] = Estimate(call.Arguments[i]);
                binding = DefaultFunctionBinder.TryBind(call.Name, argKinds, out var refusal);
                RecordIfProvenIllegal(call, refusal);
            }

            _bindings[call] = binding;
            return binding;
        }

        #endregion
    }
}
