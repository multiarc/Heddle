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
    /// Maps the phase 1 native-expression AST (<see cref="ExprNode"/>) to C# 1:1 (generated-code.md example 2). The
    /// native tier is a strict C# subset, so operators, ternary/coalesce, and literals emit verbatim and the
    /// consumer's compiler applies the identical numeric promotion / lifting / string-concat semantics the runtime
    /// reconstructs by hand. The one type-sensitive spot is member-path null-safety, resolved through
    /// <see cref="SymbolTypeResolver"/> (protocol rule 6). A construct the writer can not yet reproduce faithfully
    /// returns null, so the emitter degrades the template to the dynamic path.
    /// <para>Phase 4 D6 narrows "strict C# subset" to what it always meant: operators emit verbatim only where the
    /// shared <see cref="NativeOperatorRules"/> table says C# and the native tier agree. The seven documented
    /// deviations, and every operand whose static facts the estimator cannot pin down, degrade instead — which is
    /// what stops the consumer's C# compiler from having an opinion about rendered output.</para>
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
        /// expression (D21); the emitter records one manifest <c>FunctionBindings</c> row per name.</summary>
        public IEnumerable<string> UsedDefaultFunctions => _usedDefaultFunctions;

        /// <summary>Discovered <c>[ExportFunctions]</c> names bound directly to their container in this expression
        /// (D21): the function name with its container AQN-sans-version target and that container's overload count.
        /// Phase 3 (OQ2): a name exported by more than one container yields one row <b>per container</b>, matching
        /// the runtime's merged registry — the gauntlet compares each row's count exactly, and the old
        /// first-container-wins single row made every merged name a permanent FunctionBindingMismatch.</summary>
        public IReadOnlyList<(string Name, string Aqn, int OverloadCount)> UsedExports => _usedExports;

        /// <summary>Function names in this expression resolvable from neither the default table nor any referenced
        /// export (the OQ1 delegate-only remainder, D21): each name with its <c>.heddle</c> position. When non-empty,
        /// the emitter degrades the template to a fallback-marker entry and reports <c>HED7014</c> (never emitted
        /// code).</summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position)> UnresolvableFunctions =>
            _unresolvableFunctions;

        /// <summary>Genuine member-path failures in this expression (a property not found on a resolved, non-dynamic
        /// receiver — milestone 2 / HED7008). Drained by the emitter and reported at the <c>.heddle</c> span.</summary>
        public IReadOnlyList<SymbolMemberResolver.MemberFailure> MemberFailures => _memberFailures;

        /// <summary>Q8.1 / HED7025: function calls the <b>shared</b> overload ranker <i>proved</i> illegal — an
        /// ambiguous flat-Pareto front or no applicable overload, over arguments the estimator typed — each with its
        /// <c>.heddle</c> position, the runtime-shaped sentence naming the candidates, and the run-tier id the build
        /// error is the twin of. Drained by the emitter and reported at Error.
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
            // Resolution (D21 / OQ1): a discovered [ExportFunctions] name binds directly to its container; a default
            // built-in binds through the public PrecompiledFunctions shim (BuiltInFunctions is internal). A name in
            // *both* a container export and the default table is a merged forwarder group — not yet emitted, so the
            // template degrades to the dynamic path. A name in neither is unsupported here (HED7014 handled by the
            // emitter).
            // Phase 4 D10: a default built-in's overload is now selected by the shared OverloadRank core — Heddle's
            // flat Pareto rank — and emitted cast-pinned to the winning signature, instead of being handed to the
            // consumer's C# compiler whose betterness rules are a different algorithm. An ambiguous or inapplicable
            // call degrades, so the two tiers reach the same verdict for min(1, 2u) instead of one rendering and the
            // other raising HED1013. Export calls carry no parameter-type metadata, so they keep resolving through
            // the consumer's compiler for now (phase 3 owns export signature discovery).
            bool isDefault = DefaultShims.TryGetValue(call.Name, out var shim);
            bool hasExport = _exports != null && _exports.TryGet(call.Name, out var export);

            if (isDefault && hasExport)
                return null; // forwarder group across shim + export — deferred
            if (!isDefault && !hasExport)
            {
                // A bare name(args) inside a native expression is unambiguously a function call. Resolvable from
                // neither the default table nor a referenced export → the OQ1 delegate-only remainder (D21): record
                // it so the emitter degrades the template to a HED7014 fallback-marker entry (never emitted code).
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
                // Phase 3 (F2): exports now carry full signatures, so the SHARED ranker chooses the overload and the
                // call is emitted cast-pinned — the same treatment built-ins have had since phase 4. A call the
                // ranker refuses (ambiguous under the flat Pareto rank, inapplicable, or carrying an argument the
                // estimator cannot type) degrades, which is the runtime's own verdict rather than whatever C#
                // betterness would have picked.
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
                // Milestone 2 (HED7008): a genuine property-not-found on a resolved, non-dynamic receiver — the same
                // condition the runtime raises as HED0001. A DynamicHop is a legal dynamic member access, never a
                // failure. Recorded here; the emitter reports it (and still degrades to the dynamic path — same
                // emitted code, better errors).
                if (resolution.Kind == SymbolTypeResolver.PathKind.Failed)
                {
                    var idx = resolution.DynamicIndex;
                    var receiver = resolution.Hops.Count == 0
                        ? _modelType
                        : resolution.Hops[resolution.Hops.Count - 1].Property;
                    // An object-typed (untyped) receiver resolves member access dynamically at runtime — never a typo.
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
                    hop.Name));
            }

            return MemberPathWriter.Write(_modelLocal, hops);
        }

        private string WriteUnary(UnaryNode node)
        {
            var op = OperatorLexeme.ForUnary(node.Operator);
            if (op == null)
                return null;
            // Operands are written first even though the guard may discard the result: writing is what records
            // member-path failures (HED7008) and unresolvable function names (HED7014), and a degrading operator
            // must not silence a diagnostic the emitter would otherwise report.
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
            var left = Write(node.Left);
            var right = Write(node.Right);
            if (left == null || right == null)
                return null;
            // Phase 4 D6: the native tier deliberately deviates from C# at seven points, so emitting `(l op r)`
            // verbatim is only sound where the shared classification table says the two agree. Everything else
            // degrades to the dynamic tier, where the runtime's own compiler — the semantics of record — evaluates
            // the expression (or raises its own positioned error). Never the consumer's compiler's opinion.
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

        #region Operand-kind estimation (the generator's facts adapter for the shared rule tables)

        /// <summary>
        /// The static-kind estimate for a sub-expression, as the shared rule tables see it (phase 4 D3/D6). Anything
        /// the estimator cannot type confidently is <see cref="OperandKind.Unknown"/>, and every rule degrades on
        /// Unknown — the estimator is only ever allowed to be conservative, never optimistic. Memoized because the
        /// operator guards estimate the same sub-trees the emission walk then re-visits.
        /// </summary>
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

        /// <summary>A built-in contributes the return type of the overload the <b>shared ranker</b> selects, so
        /// <c>len(s) &gt; 0</c> and <c>min(1, 2) &gt; 0</c> both keep precompiling while a call the ranker refuses
        /// (ambiguous, inapplicable, or carrying an operand the estimator cannot type) contributes nothing. Since
        /// phase 3 an <b>export</b> call does the same — it now carries the signatures the ranker needs.</summary>
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

        /// <summary>Resolves a default built-in call against the shared candidate rows with the shared ranker, or
        /// null when the name is not a built-in, is shadowed by an export, or the ranker refuses it. Memoized: the
        /// operator guards and the emission walk both ask.</summary>
        /// <summary>Resolves an export call against its merged overload set with the shared ranker, memoized like
        /// the built-in path.</summary>
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

        /// <summary>Records a proven-illegal call once per call site (Q8.1). Called from the memo-miss arm of each
        /// binder so the two consumers — the operand-kind estimator that guards an enclosing operator, and the
        /// emission walk — cannot report the same span twice.</summary>
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
