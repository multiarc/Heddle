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

        private readonly SymbolTypeFacts _typeFacts;

        public NativeExpressionWriter(SymbolTypeResolver resolver, ITypeSymbol modelType, string modelLocal,
            FunctionExportResolver exports = null, SymbolTypeFacts typeFacts = null)
        {
            _resolver = resolver;
            _modelType = modelType;
            _modelLocal = modelLocal;
            _exports = exports;
            _typeFacts = typeFacts;
        }

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

        private string WritePath(PathNode path)
        {
            if (path.Target != null || path.RootRef || _modelType == null)
                return null;

            var resolution = _resolver.ResolvePath(_modelType, path.Segments);
            if (resolution.Kind != SymbolTypeResolver.PathKind.Resolved)
            {
                if (resolution.Kind == SymbolTypeResolver.PathKind.Failed)
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
                            SymbolTypeResolver.FullyQualified(receiver), member,
                            MemberPathWriter.Display(path.Segments), path.Position));
                    }
                }

                return null;
            }

            _usedModel = true;
            var hops = new List<MemberPathWriter.HopEmit>(resolution.Hops.Count);
            foreach (var hop in resolution.Hops)
            {
                hops.Add(new MemberPathWriter.HopEmit(
                    hop.Receiver.IsValueType,
                    SymbolTypeResolver.IsNonNullableValueType(hop.Property),
                    SymbolTypeResolver.FullyQualified(hop.Property),
                    hop.Name,
                    !hop.Property.IsRefLikeType));
            }

            return MemberPathWriter.Write(_modelLocal, hops);
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
            var left = Write(node.Left);
            var right = Write(node.Right);
            if (left == null || right == null)
                return null;
            if (NativeOperatorRules.Classify(node.Operator, Estimate(node.Left), Estimate(node.Right)) !=
                OperatorVerdict.Supported)
                return null;
            return "(" + left + " " + op + " " + right + ")";
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

        private OperandKind EstimatePath(PathNode path)
        {
            if (path.Target != null || path.RootRef || _modelType == null)
                return OperandKind.Unknown;
            var resolution = _resolver.ResolvePath(_modelType, path.Segments);
            return resolution.Kind == SymbolTypeResolver.PathKind.Resolved
                ? SymbolFacts.Classify(resolution.ResultType)
                : OperandKind.Unknown;
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
                for (int i = 0; i < argKinds.Length; i++)
                    argKinds[i] = Estimate(call.Arguments[i]);
                binding = ExportFunctionBinder.TryBind(_typeFacts, call.Name, entry.Overloads, argKinds,
                    out var refusal);
                RecordIfProvenIllegal(call, refusal);
            }

            _exportBindings[call] = binding;
            return binding;
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
