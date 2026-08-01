using System.Collections.Generic;
using Heddle.Data;
using Heddle.Language.Expressions;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>A CLR type as the generator's overload ranker sees it — a metadata-name token plus the two facts the
    /// rank rules need. Equality is ordinal on <see cref="Name"/>.</summary>
    internal readonly struct GenTypeRef
    {
        public GenTypeRef(string name, bool isValueType, string underlyingName = null)
        {
            Name = name;
            IsValueType = isValueType;
            UnderlyingName = underlyingName;
        }

        public string Name { get; }

        public bool IsValueType { get; }

        /// <summary>The <c>Nullable&lt;T&gt;</c> underlying type's name, or null.</summary>
        public string UnderlyingName { get; }

        public bool IsNone => Name == null;
    }

    /// <summary>
    /// The generator's half of the shared overload rule. The candidate set was already shared
    /// — <c>DefaultFunctionTable</c> mirrors the runtime registry row for row — but the <b>selection</b> used to be
    /// delegated to the consumer's C# compiler, whose betterness rules are not Heddle's flat Pareto rank. This type
    /// resolves a built-in call with <see cref="OverloadRank"/> instead, so the generator reaches the runtime's
    /// verdict: degrade when the ranker reports ambiguity or an inapplicable call, and otherwise emit the call
    /// <b>cast-pinned</b> to the chosen signature — every argument an exact match, so the consumer's compiler has no
    /// remaining choice to make.
    /// </summary>
    internal static class DefaultFunctionBinder
    {
        private const string ObjectName = "System.Object";

        private sealed class NameRankModel : IRankModel<GenTypeRef>
        {
            public static readonly NameRankModel Instance = new NameRankModel();

            public bool AreSame(GenTypeRef a, GenTypeRef b) =>
                a.Name != null && string.Equals(a.Name, b.Name, System.StringComparison.Ordinal);

            public bool IsObject(GenTypeRef type) => string.Equals(type.Name, ObjectName, System.StringComparison.Ordinal);

            public bool IsValueType(GenTypeRef type) => type.IsValueType;

            public bool TryGetNullableUnderlying(GenTypeRef type, out GenTypeRef underlying)
            {
                if (type.UnderlyingName == null)
                {
                    underlying = default;
                    return false;
                }

                underlying = new GenTypeRef(type.UnderlyingName, true);
                return true;
            }

            public NumericKind KindOf(GenTypeRef type) => KindOfName(type.Name);

            /// <summary>Reference conversions the generator cannot decide from a name alone answer <b>false</b>.
            /// Under-ranking can only make the call inapplicable, which degrades; over-ranking would let the
            /// generator pick an overload the runtime does not.</summary>
            public bool IsReferenceAssignable(GenTypeRef from, GenTypeRef to) => false;
        }

        private static readonly Dictionary<string, IReadOnlyList<DefaultFunctionRow>> RowsByName = BuildRowsByName();

        private static Dictionary<string, IReadOnlyList<DefaultFunctionRow>> BuildRowsByName()
        {
            var map = new Dictionary<string, List<DefaultFunctionRow>>(System.StringComparer.Ordinal);
            foreach (var row in DefaultFunctionTable.Rows)
            {
                if (!map.TryGetValue(row.Name, out var list))
                    map[row.Name] = list = new List<DefaultFunctionRow>();
                list.Add(row);
            }

            var result = new Dictionary<string, IReadOnlyList<DefaultFunctionRow>>(System.StringComparer.Ordinal);
            foreach (var pair in map)
                result[pair.Key] = pair.Value;
            return result;
        }

        /// <summary>The resolved built-in call: which row won, whether the params-expanded tier was used, and the
        /// per-argument cast targets (null where the argument is already an exact match).</summary>
        internal sealed class Binding
        {
            public DefaultFunctionRow Row;
            public bool Expanded;
            public string[] ArgumentCasts;
        }

        /// <summary>Resolves <paramref name="name"/> against the shared candidate rows with the shared ranker, or
        /// returns null when the ranker reports ambiguity, no applicable overload, or an argument the generator
        /// cannot describe — every one of which degrades the template rather than guessing.
        /// <para>The two refusals are <b>not</b> the same thing, so <paramref name="refusal"/> distinguishes
        /// them instead of letting the bare <c>null</c> conflate them. An ambiguous or inapplicable front over
        /// arguments the estimator typed is a <i>proof</i> that the runtime will refuse the call, and the build
        /// reports <c>HED7025</c>; an argument it could not describe is a generator limitation and still degrades
        /// silently, because nothing has been proved.</para>
        /// <para><paramref name="argumentTypes"/> carries the caller's own resolved symbol per argument, which is
        /// what the engine ranks on — <c>NativeExpressionCompiler</c> hands <c>OverloadRank</c> the compiled
        /// expression's <c>Type</c>. It is consulted only where the shared descriptor has nothing, and entries may
        /// be null.</para></summary>
        public static Binding TryBind(string name, IReadOnlyList<OperandKind> arguments,
            IReadOnlyList<ITypeSymbol> argumentTypes, out BindRefusal refusal)
        {
            refusal = BindRefusal.Unproven;
            if (!RowsByName.TryGetValue(name, out var rows))
                return null;

            var args = new RankArgument<GenTypeRef>[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
            {
                // An argument neither the descriptor nor its symbol can name has no rank token; returning early
                // keeps it a silent degrade, not a false ranking against the generator's ignorance.
                var symbol = argumentTypes != null && i < argumentTypes.Count ? argumentTypes[i] : null;
                if (!TryDescribe(arguments[i], symbol, out var described))
                    return null;
                args[i] = described;
            }

            var candidates = new RankCandidate<GenTypeRef>[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                candidates[i] = ToCandidate(rows[i]);

            var binding = OverloadRank.Bind(NameRankModel.Instance, candidates, args);
            if (binding.Outcome != BindOutcome.Bound)
            {
                // Every argument was describable and the shared core still reached Ambiguous/None: the runtime,
                // running the identical core, reaches the same verdict and fails its compile.
                refusal = Refuse(name, binding.Outcome, rows, args);
                return null;
            }

            var row = rows[binding.Index];
            var casts = new string[arguments.Count];
            int fixedCount = binding.Expanded ? row.ParameterTypeNames.Length - 1 : arguments.Count;
            for (int i = 0; i < arguments.Count; i++)
            {
                var target = i < fixedCount
                    ? new GenTypeRef(row.ParameterTypeNames[i], IsValueTypeName(row.ParameterTypeNames[i]))
                    : ElementOf(row);
                int rank = OverloadRank.ConversionRank(NameRankModel.Instance, args[i], target);
                casts[i] = rank == OverloadRank.Exact ? null : CSharpName(target.Name);
                if (casts[i] == null && rank != OverloadRank.Exact)
                    return null;   // no spelling for the cast target — degrade rather than emit unpinned
            }

            refusal = BindRefusal.Bound;
            return new Binding { Row = row, Expanded = binding.Expanded, ArgumentCasts = casts };
        }

        /// <summary>The <c>HED7025</c> payload for a proven-illegal built-in call: the runtime's own sentence for the
        /// same input, so the two tiers say the same thing about the same template. The candidate list is every
        /// overload of the name (not just the non-dominated front), matching <c>NativeExpressionCompiler</c>.</summary>
        private static BindRefusal Refuse(string name, BindOutcome outcome, IReadOnlyList<DefaultFunctionRow> rows,
            IReadOnlyList<RankArgument<GenTypeRef>> args)
        {
            var candidates = new List<string>(rows.Count);
            foreach (var row in rows)
                candidates.Add(SignatureText(name, row));
            var candidateText = string.Join(", ", candidates);

            if (outcome == BindOutcome.Ambiguous)
                return BindRefusal.ProvenIllegal(
                    "The call to function '" + name + "' is ambiguous between: " + candidateText + ".",
                    HeddleDiagnosticIds.AmbiguousFunctionCall);

            var argTexts = new List<string>(args.Count);
            foreach (var arg in args)
                argTexts.Add(DisplayName(arg));
            return BindRefusal.ProvenIllegal(
                "No overload of function '" + name + "' takes (" + string.Join(", ", argTexts) + "). Candidates: " +
                candidateText + ".",
                HeddleDiagnosticIds.NoFunctionOverload);
        }

        private static string SignatureText(string name, in DefaultFunctionRow row)
        {
            var parts = new string[row.ParameterTypeNames.Length];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = FriendlyName(row.ParameterTypeNames[i]);
            return name + "(" + string.Join(", ", parts) + ")";
        }

        private static string DisplayName(in RankArgument<GenTypeRef> arg) =>
            arg.IsNullLiteral ? "null" : FriendlyName(arg.Type.Name);

        /// <summary>Signature-text spelling for one CLR metadata name. The alias half is the <b>shared</b> table
        /// (<see cref="CSharpName"/>), so a signature the author reads in a build error is spelled the way the
        /// runtime's <c>HED1013</c> spells it; everything else degrades to the bare type name, which is the
        /// runtime's own fallback too. The estimator's placeholder tokens are spelled in plain English rather than
        /// leaked verbatim — the author never wrote <c>?reference</c>, and the candidate list beside it is what
        /// actually explains the error.</summary>
        private static string FriendlyName(string clrName)
        {
            if (clrName == null)
                return "?";
            switch (clrName)
            {
                case "?reference": return "a reference type";
                case "?enum": return "an enum";
                case "?struct": return "a struct";
            }

            var alias = CSharpName(clrName);
            if (alias != null)
                return alias;
            if (clrName.StartsWith("System.Nullable`1[", System.StringComparison.Ordinal) && clrName.EndsWith("]",
                    System.StringComparison.Ordinal))
                return FriendlyName(clrName.Substring(18, clrName.Length - 19)) + "?";
            bool isArray = clrName.EndsWith("[]", System.StringComparison.Ordinal);
            var core = isArray ? clrName.Substring(0, clrName.Length - 2) : clrName;
            var aliasCore = CSharpName(core);
            if (aliasCore != null)
                return aliasCore + (isArray ? "[]" : string.Empty);
            int dot = core.LastIndexOf('.');
            var shortName = dot >= 0 ? core.Substring(dot + 1) : core;
            return shortName + (isArray ? "[]" : string.Empty);
        }

        /// <summary>The chosen row's return type as an operand kind, so an enclosing operator can be guarded against
        /// the type the call actually produces (<c>len(s) &gt; 0</c>, <c>min(1, 2) &gt; 0</c>).</summary>
        public static OperandKind ReturnKind(in DefaultFunctionRow row)
        {
            var kind = KindOfName(row.ReturnTypeName);
            if (kind != NumericKind.None)
                return OperandKind.Numeric(kind, false);
            switch (row.ReturnTypeName)
            {
                case "System.String": return OperandKind.Of(OperandCategory.String);
                case "System.Boolean": return OperandKind.Of(OperandCategory.Bool);
                default:
                    // A non-primitive return (Heddle.Models.Range) is describable enough to be boxed into an
                    // object parameter — str(range(…)) still precompiles — but is classified as a value type so
                    // every operator rule treats it the way it treats any user struct: degrade.
                    return OperandKind.Of(OperandCategory.Other);
            }
        }

        private static RankCandidate<GenTypeRef> ToCandidate(in DefaultFunctionRow row)
        {
            var names = row.ParameterTypeNames;
            var types = new GenTypeRef[names.Length];
            for (int i = 0; i < names.Length; i++)
                types[i] = new GenTypeRef(names[i], IsValueTypeName(names[i]));
            bool hasParams = names.Length != 0 && names[names.Length - 1].EndsWith("[]", System.StringComparison.Ordinal);
            return new RankCandidate<GenTypeRef>(types, hasParams, ElementOf(row));
        }

        private static GenTypeRef ElementOf(in DefaultFunctionRow row)
        {
            var names = row.ParameterTypeNames;
            if (names.Length == 0)
                return default;
            var last = names[names.Length - 1];
            if (!last.EndsWith("[]", System.StringComparison.Ordinal))
                return default;
            var element = last.Substring(0, last.Length - 2);
            return new GenTypeRef(element, IsValueTypeName(element));
        }

        /// <summary>Maps an operand estimate to a rank token. Categories the generator cannot name precisely get a
        /// placeholder that can only ever rank as a boxing conversion to <c>object</c> — exactly what the runtime
        /// scores for the same operand — and <see cref="OperandCategory.Unknown"/> falls through to the argument's
        /// own symbol.</summary>
        private static bool TryDescribe(in OperandKind kind, ITypeSymbol type, out RankArgument<GenTypeRef> argument)
        {
            argument = default;
            switch (kind.Category)
            {
                case OperandCategory.NullLiteral:
                    argument = RankArgument<GenTypeRef>.Null();
                    return true;
                case OperandCategory.Numeric:
                {
                    var name = NameOfKind(kind.Kind);
                    argument = RankArgument<GenTypeRef>.Of(Lift(name, true, kind.IsNullable));
                    return true;
                }
                case OperandCategory.Bool:
                    argument = RankArgument<GenTypeRef>.Of(Lift("System.Boolean", true, kind.IsNullable));
                    return true;
                case OperandCategory.String:
                    argument = RankArgument<GenTypeRef>.Of(new GenTypeRef("System.String", false));
                    return true;
                case OperandCategory.Reference:
                    argument = RankArgument<GenTypeRef>.Of(new GenTypeRef("?reference", false));
                    return true;
                case OperandCategory.Enum:
                    argument = RankArgument<GenTypeRef>.Of(Lift("?enum", true, kind.IsNullable));
                    return true;
                case OperandCategory.Other:
                    argument = RankArgument<GenTypeRef>.Of(Lift("?struct", true, kind.IsNullable));
                    return true;
                default:
                    return TryDescribeSymbol(type, out argument);
            }
        }

        /// <summary>
        /// The rank token for an argument the shared descriptor deliberately refuses to describe. The descriptor
        /// answers <c>Unknown</c> in five situations, and <c>object</c> is the only one this model can name:
        /// <list type="bullet">
        /// <item><description><b><c>object</c></b> — named exactly, and the whole shipped candidate set decides an
        /// <c>object</c> argument through <c>AreSame</c> and <c>IsObject</c> alone, never through the reference
        /// conversion this model has to answer false to. So the rank vector here is the one the runtime computes
        /// from the same <c>Type</c>, and a refusal over it is a proof rather than a guess.</description></item>
        /// <item><description><b><c>dynamic</c></b> — the engine's compiler refuses the whole expression before any
        /// call in it is bound, so there is no bind to prove anything about.</description></item>
        /// <item><description><b>an error type</b> — the consumer's compilation is already broken.</description></item>
        /// <item><description><b>a type parameter</b> — the engine ranks the closed type the host supplies, which
        /// is not the open one visible here.</description></item>
        /// <item><description><b>no symbol at all</b> — nothing was resolved, which is the original "cannot
        /// say".</description></item>
        /// </list>
        /// The last four keep the silent degrade they always had.
        /// </summary>
        private static bool TryDescribeSymbol(ITypeSymbol type, out RankArgument<GenTypeRef> argument)
        {
            argument = default;
            if (type == null || type.SpecialType != SpecialType.System_Object)
                return false;
            argument = RankArgument<GenTypeRef>.Of(new GenTypeRef(ObjectName, false));
            return true;
        }

        private static GenTypeRef Lift(string name, bool isValueType, bool isNullable) =>
            isNullable
                ? new GenTypeRef("System.Nullable`1[" + name + "]", true, name)
                : new GenTypeRef(name, isValueType);

        private static NumericKind KindOfName(string name)
        {
            switch (name)
            {
                case "System.SByte": return NumericKind.SByte;
                case "System.Byte": return NumericKind.Byte;
                case "System.Int16": return NumericKind.Int16;
                case "System.UInt16": return NumericKind.UInt16;
                case "System.Int32": return NumericKind.Int32;
                case "System.UInt32": return NumericKind.UInt32;
                case "System.Int64": return NumericKind.Int64;
                case "System.UInt64": return NumericKind.UInt64;
                case "System.Char": return NumericKind.Char;
                case "System.Single": return NumericKind.Single;
                case "System.Double": return NumericKind.Double;
                case "System.Decimal": return NumericKind.Decimal;
                default: return NumericKind.None;
            }
        }

        private static string NameOfKind(NumericKind kind)
        {
            switch (kind)
            {
                case NumericKind.SByte: return "System.SByte";
                case NumericKind.Byte: return "System.Byte";
                case NumericKind.Int16: return "System.Int16";
                case NumericKind.UInt16: return "System.UInt16";
                case NumericKind.Int32: return "System.Int32";
                case NumericKind.UInt32: return "System.UInt32";
                case NumericKind.Int64: return "System.Int64";
                case NumericKind.UInt64: return "System.UInt64";
                case NumericKind.Char: return "System.Char";
                case NumericKind.Single: return "System.Single";
                case NumericKind.Double: return "System.Double";
                default: return "System.Decimal";
            }
        }

        private static bool IsValueTypeName(string name) =>
            KindOfName(name) != NumericKind.None || string.Equals(name, "System.Boolean", System.StringComparison.Ordinal);

        /// <summary>The C# spelling of a cast target, or null when the generator has no safe spelling for it.</summary>
        private static string CSharpName(string clrName)
        {
            switch (clrName)
            {
                case "System.SByte": return "sbyte";
                case "System.Byte": return "byte";
                case "System.Int16": return "short";
                case "System.UInt16": return "ushort";
                case "System.Int32": return "int";
                case "System.UInt32": return "uint";
                case "System.Int64": return "long";
                case "System.UInt64": return "ulong";
                case "System.Char": return "char";
                case "System.Single": return "float";
                case "System.Double": return "double";
                case "System.Decimal": return "decimal";
                case "System.Boolean": return "bool";
                case "System.String": return "string";
                case "System.Object": return "object";
                default: return null;
            }
        }
    }
}
