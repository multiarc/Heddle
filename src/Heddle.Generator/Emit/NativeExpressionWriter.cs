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
    /// </summary>
    internal sealed class NativeExpressionWriter
    {
        private static readonly Dictionary<string, string> DefaultShims = BuildDefaultShims();
        private static readonly Dictionary<string, int> DefaultOverloadCounts = BuildDefaultOverloadCounts();

        private readonly SymbolTypeResolver _resolver;
        private readonly ITypeSymbol _modelType;
        private readonly string _modelLocal;
        private readonly FunctionExportResolver _exports;
        private bool _usedModel;
        private readonly HashSet<string> _usedDefaultFunctions = new HashSet<string>();
        private readonly Dictionary<string, (string Aqn, int OverloadCount)> _usedExports =
            new Dictionary<string, (string, int)>(System.StringComparer.Ordinal);
        private readonly List<(string Name, Heddle.Strings.Core.BlockPosition Position)> _unresolvableFunctions =
            new List<(string, Heddle.Strings.Core.BlockPosition)>();
        private readonly List<SymbolMemberResolver.MemberFailure> _memberFailures =
            new List<SymbolMemberResolver.MemberFailure>();

        public NativeExpressionWriter(SymbolTypeResolver resolver, ITypeSymbol modelType, string modelLocal,
            FunctionExportResolver exports = null)
        {
            _resolver = resolver;
            _modelType = modelType;
            _modelLocal = modelLocal;
            _exports = exports;
        }

        public bool UsedModel => _usedModel;

        /// <summary>Default (built-in) function names bound to the <c>PrecompiledFunctions</c> shim in this
        /// expression (D21); the emitter records one manifest <c>FunctionBindings</c> row per name.</summary>
        public IEnumerable<string> UsedDefaultFunctions => _usedDefaultFunctions;

        /// <summary>Discovered <c>[ExportFunctions]</c> names bound directly to their container in this expression
        /// (D21): the function name mapped to its container AQN-sans-version target and overload count — one
        /// manifest <c>FunctionBindings</c> row per name.</summary>
        public IEnumerable<KeyValuePair<string, (string Aqn, int OverloadCount)>> UsedExports => _usedExports;

        /// <summary>Function names in this expression resolvable from neither the default table nor any referenced
        /// export (the OQ1 delegate-only remainder, D21): each name with its <c>.heddle</c> position. When non-empty,
        /// the emitter degrades the template to a fallback-marker entry and reports <c>HED7014</c> (never emitted
        /// code).</summary>
        public IReadOnlyList<(string Name, Heddle.Strings.Core.BlockPosition Position)> UnresolvableFunctions =>
            _unresolvableFunctions;

        /// <summary>Genuine member-path failures in this expression (a property not found on a resolved, non-dynamic
        /// receiver — milestone 2 / HED7008). Drained by the emitter and reported at the <c>.heddle</c> span.</summary>
        public IReadOnlyList<SymbolMemberResolver.MemberFailure> MemberFailures => _memberFailures;

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
            // emitter). In every case the consumer's C# compiler resolves the overload over the same candidate set
            // as the runtime registry, so the phase 1 D12 rank is reproduced by construction (differential-gated).
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
                _exports.TryGet(call.Name, out var e);
                _usedExports[call.Name] = (e.ContainerAqnSansVersion, e.OverloadCount);
                var method = e.MethodNamesByFunction[call.Name];
                return e.ContainerGlobalName + "." + method + "(" + string.Join(", ", args) + ")";
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
            var operand = Write(node.Operand);
            if (operand == null)
                return null;
            string op;
            switch (node.Operator)
            {
                case ExprOperator.Not: op = "!"; break;
                case ExprOperator.Negate: op = "-"; break;
                case ExprOperator.UnaryPlus: op = "+"; break;
                case ExprOperator.OnesComplement: op = "~"; break;
                default: return null;
            }

            return "(" + op + operand + ")";
        }

        private string WriteBinary(BinaryNode node)
        {
            var left = Write(node.Left);
            var right = Write(node.Right);
            if (left == null || right == null)
                return null;
            var op = BinarySymbol(node.Operator);
            if (op == null)
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
            return "(" + c + " ? " + t + " : " + f + ")";
        }

        private static string BinarySymbol(ExprOperator op)
        {
            switch (op)
            {
                case ExprOperator.Add: return "+";
                case ExprOperator.Subtract: return "-";
                case ExprOperator.Multiply: return "*";
                case ExprOperator.Divide: return "/";
                case ExprOperator.Modulo: return "%";
                case ExprOperator.LeftShift: return "<<";
                case ExprOperator.RightShift: return ">>";
                case ExprOperator.LessThan: return "<";
                case ExprOperator.LessThanOrEqual: return "<=";
                case ExprOperator.GreaterThan: return ">";
                case ExprOperator.GreaterThanOrEqual: return ">=";
                case ExprOperator.Equal: return "==";
                case ExprOperator.NotEqual: return "!=";
                case ExprOperator.And: return "&";
                case ExprOperator.ExclusiveOr: return "^";
                case ExprOperator.Or: return "|";
                case ExprOperator.AndAlso: return "&&";
                case ExprOperator.OrElse: return "||";
                case ExprOperator.Coalesce: return "??";
                default: return null;
            }
        }
    }

    /// <summary>Round-trips a decoded literal value to an invariant C# literal preserving its CLR type.</summary>
    internal static class LiteralFormatter
    {
        public static string Format(object value)
        {
            switch (value)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case string s: return PieceWriter.Escape(s);
                case char c: return "'" + EscapeChar(c) + "'";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case uint ui: return ui.ToString(CultureInfo.InvariantCulture) + "U";
                case long l: return l.ToString(CultureInfo.InvariantCulture) + "L";
                case ulong ul: return ul.ToString(CultureInfo.InvariantCulture) + "UL";
                case float f: return f.ToString("R", CultureInfo.InvariantCulture) + "F";
                case double d: return d.ToString("R", CultureInfo.InvariantCulture) + "D";
                case decimal m: return m.ToString(CultureInfo.InvariantCulture) + "M";
                default: return null;
            }
        }

        private static string EscapeChar(char c)
        {
            switch (c)
            {
                case '\'': return "\\'";
                case '\\': return "\\\\";
                case '\n': return "\\n";
                case '\r': return "\\r";
                case '\t': return "\\t";
                case '\0': return "\\0";
                default:
                    if (c < 0x20)
                        return "\\u" + ((int) c).ToString("x4");
                    return c.ToString();
            }
        }
    }
}
