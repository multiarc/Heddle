using System.Collections.Generic;
using Heddle.Data;
using Heddle.Language.Expressions;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// Resolves a call whose name is both a shipped built-in and a discovered <c>[ExportFunctions]</c> export —
    /// the same merge the runtime registry performs when the host registers the export: an export with the exact
    /// parameter types of a built-in overload takes its registry slot, every other export joins the overload set,
    /// and one shared <see cref="OverloadRank"/> pass picks over the merged candidates. The winner keeps its
    /// ordinary call spelling — the <c>PrecompiledFunctions</c> shim for a built-in row, the container for an
    /// export — so a collided name pre-compiles to whichever target the engine's own ranker reaches.
    /// </summary>
    internal static class MergedFunctionBinder
    {
        /// <summary>The resolved merged call: which side won and the facts each side's emission needs.</summary>
        internal sealed class Binding
        {
            /// <summary>True when a built-in row won; <see cref="Row"/> is then valid and <see cref="Overload"/>
            /// is null. False for an export winner, where the reverse holds.</summary>
            public bool IsBuiltIn;

            public DefaultFunctionRow Row;

            public FunctionExportResolver.ExportOverloadInfo Overload;

            /// <summary>Per-argument cast target, or null where the argument already matches exactly. For an
            /// expanded export bind the tail entries target the element type.</summary>
            public string[] ArgumentCasts;

            public ITypeSymbol ReturnType;

            /// <summary>True when an export won through the params-expanded tier, so the emitted call must spell
            /// the tail as an explicitly typed array creation.</summary>
            public bool Expanded;

            /// <summary>The <c>global::</c>-qualified element type of the expanded array; null unless
            /// <see cref="Expanded"/>.</summary>
            public string ParamsElementTypeName;

            /// <summary>Built-in overloads still live after exact-signature exports took theirs — the count the
            /// shim-target manifest row must carry for the gauntlet's exact comparison; zero records no row.</summary>
            public int RemainingBuiltInOverloads;
        }

        /// <summary>
        /// Ranks the merged candidate set, or returns null (degrade to dynamic) when an argument cannot be typed,
        /// a built-in row's types are unresolvable in this compilation, or a cast target has no safe
        /// spelling. <paramref name="refusal"/> distinguishes
        /// ambiguous/inapplicable over typed arguments — a proof the engine reaches the same verdict over its own
        /// merged registry, reported as <c>HED7025</c> — from the unproven refusals that stay silent.
        /// </summary>
        public static Binding TryBind(SymbolTypeFacts facts, SymbolTypeResolver resolver, string name,
            IReadOnlyList<FunctionExportResolver.ExportOverloadInfo> exports, IReadOnlyList<OperandKind> argKinds,
            IReadOnlyList<ITypeSymbol> argTypes, out BindRefusal refusal)
        {
            refusal = BindRefusal.Unproven;
            var compilation = facts?.Compilation;
            if (compilation == null || exports == null || exports.Count == 0)
                return null;
            var rows = DefaultFunctionBinder.RowsFor(name);
            if (rows == null)
                return null;

            var builtIns = new List<(DefaultFunctionRow Row, ITypeSymbol[] Parameters)>(rows.Count);
            foreach (var row in rows)
            {
                var parameters = new ITypeSymbol[row.ParameterTypeNames.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    parameters[i] = ResolveClrName(compilation, row.ParameterTypeNames[i]);
                    if (parameters[i] == null)
                        return null;
                }

                // The registry's replace-on-exact-signature, mirrored: an export whose parameter types are the
                // row's exact CLR types (return type not compared) takes the built-in's slot, so the row leaves
                // the candidate set here exactly when it leaves the engine's.
                if (!ReplacedByAnExport(compilation, parameters, exports))
                    builtIns.Add((row, parameters));
            }

            var model = new ExportFunctionBinder.SymbolRankModel(facts);
            var args = new RankArgument<ITypeSymbol>[argKinds.Count];
            for (int i = 0; i < argKinds.Count; i++)
            {
                if (argKinds[i].Category == OperandCategory.NullLiteral)
                {
                    args[i] = RankArgument<ITypeSymbol>.Null();
                    continue;
                }

                var type = argTypes != null && i < argTypes.Count && argTypes[i] != null
                    ? argTypes[i]
                    : ExportFunctionBinder.ToSymbol(compilation, argKinds[i]);
                if (type == null || type.TypeKind == TypeKind.Dynamic || type.TypeKind == TypeKind.Error)
                    return null;   // untypeable argument: nothing proved, silent degrade
                args[i] = RankArgument<ITypeSymbol>.Of(type);
            }

            var candidates = new RankCandidate<ITypeSymbol>[builtIns.Count + exports.Count];
            for (int i = 0; i < builtIns.Count; i++)
                candidates[i] = BuiltInCandidate(builtIns[i].Parameters);
            for (int i = 0; i < exports.Count; i++)
                candidates[builtIns.Count + i] = ExportCandidate(exports[i].Method);

            var binding = OverloadRank.Bind(model, candidates, args);
            if (binding.Outcome != BindOutcome.Bound)
            {
                refusal = Refuse(name, binding.Outcome, builtIns, exports, args);
                return null;
            }

            if (binding.Index < builtIns.Count)
                return BindBuiltIn(builtIns[binding.Index], builtIns.Count, model, args, binding.Expanded,
                    compilation, ref refusal);

            return BindExport(exports[binding.Index - builtIns.Count], builtIns.Count, resolver, args,
                binding.Expanded, ref refusal);
        }

        private static Binding BindBuiltIn((DefaultFunctionRow Row, ITypeSymbol[] Parameters) winner,
            int remainingBuiltIns, ExportFunctionBinder.SymbolRankModel model, RankArgument<ITypeSymbol>[] args,
            bool expanded, Compilation compilation, ref BindRefusal refusal)
        {
            var names = winner.Row.ParameterTypeNames;
            int fixedCount = expanded ? names.Length - 1 : args.Length;
            var casts = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var targetName = i < fixedCount ? names[i] : ElementName(names);
                var target = i < fixedCount
                    ? winner.Parameters[i]
                    : ((IArrayTypeSymbol)winner.Parameters[names.Length - 1]).ElementType;
                int rank = OverloadRank.ConversionRank(model, args[i], target);
                casts[i] = rank == OverloadRank.Exact ? null : DefaultFunctionBinder.CSharpName(targetName);
                if (casts[i] == null && rank != OverloadRank.Exact)
                    return null;   // no spelling for the cast target — degrade rather than emit unpinned
            }

            refusal = BindRefusal.Bound;
            return new Binding
            {
                IsBuiltIn = true,
                Row = winner.Row,
                ArgumentCasts = casts,
                ReturnType = compilation.GetTypeByMetadataName(winner.Row.ReturnTypeName),
                RemainingBuiltInOverloads = remainingBuiltIns
            };
        }

        private static Binding BindExport(FunctionExportResolver.ExportOverloadInfo winner, int remainingBuiltIns,
            SymbolTypeResolver resolver, RankArgument<ITypeSymbol>[] args, bool expanded, ref BindRefusal refusal)
        {
            if (!ExportFunctionBinder.TryComputeCasts(winner.Method, resolver, args, expanded, out var casts,
                    out var elementName))
                return null;   // a name the call must spell is out of this assembly's reach — degrade in silence

            refusal = BindRefusal.Bound;
            return new Binding
            {
                Overload = winner,
                ArgumentCasts = casts,
                ReturnType = winner.Method.ReturnType,
                RemainingBuiltInOverloads = remainingBuiltIns,
                Expanded = expanded,
                ParamsElementTypeName = elementName
            };
        }

        private static RankCandidate<ITypeSymbol> BuiltInCandidate(ITypeSymbol[] parameters)
        {
            // A trailing array parameter is the table's spelling of params (format's object[] tail).
            bool hasParams = parameters.Length != 0 &&
                             parameters[parameters.Length - 1] is IArrayTypeSymbol;
            var element = hasParams
                ? ((IArrayTypeSymbol)parameters[parameters.Length - 1]).ElementType
                : null;
            return new RankCandidate<ITypeSymbol>(parameters, hasParams, element);
        }

        private static RankCandidate<ITypeSymbol> ExportCandidate(IMethodSymbol method)
        {
            var parameterTypes = new ITypeSymbol[method.Parameters.Length];
            for (int p = 0; p < method.Parameters.Length; p++)
                parameterTypes[p] = method.Parameters[p].Type;

            bool hasParams = method.Parameters.Length > 0 &&
                             method.Parameters[method.Parameters.Length - 1].IsParams;
            ITypeSymbol elementType = null;
            if (hasParams && method.Parameters[method.Parameters.Length - 1].Type is IArrayTypeSymbol array)
                elementType = array.ElementType;
            else
                hasParams = false;

            return new RankCandidate<ITypeSymbol>(parameterTypes, hasParams, elementType);
        }

        private static bool ReplacedByAnExport(Compilation compilation, ITypeSymbol[] rowParameters,
            IReadOnlyList<FunctionExportResolver.ExportOverloadInfo> exports)
        {
            foreach (var export in exports)
            {
                var parameters = export.Method.Parameters;
                if (parameters.Length != rowParameters.Length)
                    continue;
                bool same = true;
                for (int i = 0; i < parameters.Length && same; i++)
                    same = SymbolEqualityComparer.Default.Equals(
                        RuntimeVisible(compilation, parameters[i].Type), rowParameters[i]);
                if (same)
                    return true;
            }

            return false;
        }

        /// <summary><c>dynamic</c> erases to <see cref="object"/> in metadata, which is the type the registry's
        /// reflection-side exact-signature test compares.</summary>
        private static ITypeSymbol RuntimeVisible(Compilation compilation, ITypeSymbol type) =>
            type.TypeKind == TypeKind.Dynamic ? compilation.GetSpecialType(SpecialType.System_Object) : type;

        private static ITypeSymbol ResolveClrName(Compilation compilation, string clrName)
        {
            bool isArray = clrName.EndsWith("[]", System.StringComparison.Ordinal);
            var core = isArray ? clrName.Substring(0, clrName.Length - 2) : clrName;
            var named = compilation.GetTypeByMetadataName(core);
            if (named == null)
                return null;
            return isArray ? (ITypeSymbol)compilation.CreateArrayTypeSymbol(named) : named;
        }

        /// <summary>The <c>HED7025</c> payload for a proven-illegal merged call: the runtime's own sentence over
        /// the same merged candidate set the engine's registry holds for this name.</summary>
        private static BindRefusal Refuse(string name, BindOutcome outcome,
            List<(DefaultFunctionRow Row, ITypeSymbol[] Parameters)> builtIns,
            IReadOnlyList<FunctionExportResolver.ExportOverloadInfo> exports,
            IReadOnlyList<RankArgument<ITypeSymbol>> args)
        {
            var candidates = new List<string>(builtIns.Count + exports.Count);
            foreach (var builtIn in builtIns)
                candidates.Add(Signature(name, builtIn.Parameters));
            foreach (var export in exports)
            {
                var parts = new List<string>(export.Method.Parameters.Length);
                foreach (var parameter in export.Method.Parameters)
                    parts.Add(Display(parameter.Type));
                candidates.Add(name + "(" + string.Join(", ", parts) + ")");
            }

            var candidateText = string.Join(", ", candidates);
            if (outcome == BindOutcome.Ambiguous)
                return BindRefusal.ProvenIllegal(
                    "The call to function '" + name + "' is ambiguous between: " + candidateText + ".",
                    HeddleDiagnosticIds.AmbiguousFunctionCall);

            var argTexts = new List<string>(args.Count);
            foreach (var arg in args)
                argTexts.Add(arg.IsNullLiteral ? "null" : Display(arg.Type));
            return BindRefusal.ProvenIllegal(
                "No overload of function '" + name + "' takes (" + string.Join(", ", argTexts) + "). Candidates: " +
                candidateText + ".",
                HeddleDiagnosticIds.NoFunctionOverload);
        }

        private static string Signature(string name, ITypeSymbol[] parameters)
        {
            var parts = new string[parameters.Length];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = Display(parameters[i]);
            return name + "(" + string.Join(", ", parts) + ")";
        }

        private static string Display(ITypeSymbol type) =>
            type == null ? "?" : type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        private static string ElementName(string[] parameterTypeNames)
        {
            var last = parameterTypeNames[parameterTypeNames.Length - 1];
            return last.Substring(0, last.Length - 2);
        }
    }
}
