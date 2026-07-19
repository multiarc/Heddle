using System.Collections.Generic;

namespace Heddle.Precompiled
{
    /// <summary>One default built-in overload as pure strings (phase 7 D21): the registered function name, the
    /// <see cref="PrecompiledFunctions"/> shim method that binds it, and the parameter/return CLR type names. Value
    /// data only — safe to hold in an incremental-generator pipeline model.</summary>
    internal readonly struct DefaultFunctionRow
    {
        public DefaultFunctionRow(string name, string shimMethodName, string[] parameterTypeNames,
            string returnTypeName)
        {
            Name = name;
            ShimMethodName = shimMethodName;
            ParameterTypeNames = parameterTypeNames;
            ReturnTypeName = returnTypeName;
        }

        public string Name { get; }

        public string ShimMethodName { get; }

        public string[] ParameterTypeNames { get; }

        public string ReturnTypeName { get; }
    }

    /// <summary>
    /// The shared-sourced table of the 18 default built-in function names (35 overloads) that a fresh
    /// <c>FunctionRegistry</c> starts with (phase 1 D12 + phase 4 D2's <c>range</c>). Compiled into <b>both</b>
    /// <c>Heddle</c> and the <c>Heddle.Generator</c> analyzer (like <see cref="TemplateKey"/>, D1/D12): the generator
    /// seeds its merged function table from these rows and emits <see cref="PrecompiledFunctions"/> shim calls for
    /// default-bound names; the engine's <c>DefaultFunctionLockstepTests</c> gate this table against
    /// <c>FunctionRegistry.Default</c> and the shim in both directions.
    /// </summary>
    internal static class DefaultFunctionTable
    {
        /// <summary>The assembly-qualified type name (without version) the shim forwards to — the target recorded on
        /// every shim-bound <see cref="PrecompiledFunctionBinding"/> row (D21).</summary>
        public const string ShimTargetTypeName = "Heddle.Runtime.Expressions.BuiltInFunctions, Heddle";

        private const string Str = "System.String";
        private const string Int = "System.Int32";
        private const string Long = "System.Int64";
        private const string Dbl = "System.Double";
        private const string Dec = "System.Decimal";
        private const string Bool = "System.Boolean";
        private const string Obj = "System.Object";
        private const string ObjArr = "System.Object[]";
        private const string Range = "Heddle.Models.Range";

        /// <summary>The 35 default overloads, in <c>BuiltInFunctions.CreateEntries()</c> order.</summary>
        public static readonly IReadOnlyList<DefaultFunctionRow> Rows = new[]
        {
            new DefaultFunctionRow("upper", "Upper", new[] { Str }, Str),
            new DefaultFunctionRow("lower", "Lower", new[] { Str }, Str),
            new DefaultFunctionRow("trim", "Trim", new[] { Str }, Str),
            new DefaultFunctionRow("len", "Len", new[] { Str }, Int),
            new DefaultFunctionRow("contains", "Contains", new[] { Str, Str }, Bool),
            new DefaultFunctionRow("startswith", "StartsWith", new[] { Str, Str }, Bool),
            new DefaultFunctionRow("endswith", "EndsWith", new[] { Str, Str }, Bool),
            new DefaultFunctionRow("replace", "Replace", new[] { Str, Str, Str }, Str),
            new DefaultFunctionRow("substr", "Substr", new[] { Str, Int }, Str),
            new DefaultFunctionRow("substr", "Substr", new[] { Str, Int, Int }, Str),
            new DefaultFunctionRow("format", "Format", new[] { Obj, Str }, Str),
            new DefaultFunctionRow("format", "Format", new[] { Str, ObjArr }, Str),
            new DefaultFunctionRow("str", "Str", new[] { Obj }, Str),
            new DefaultFunctionRow("abs", "Abs", new[] { Int }, Int),
            new DefaultFunctionRow("abs", "Abs", new[] { Long }, Long),
            new DefaultFunctionRow("abs", "Abs", new[] { Dbl }, Dbl),
            new DefaultFunctionRow("abs", "Abs", new[] { Dec }, Dec),
            new DefaultFunctionRow("min", "Min", new[] { Int, Int }, Int),
            new DefaultFunctionRow("min", "Min", new[] { Long, Long }, Long),
            new DefaultFunctionRow("min", "Min", new[] { Dbl, Dbl }, Dbl),
            new DefaultFunctionRow("min", "Min", new[] { Dec, Dec }, Dec),
            new DefaultFunctionRow("max", "Max", new[] { Int, Int }, Int),
            new DefaultFunctionRow("max", "Max", new[] { Long, Long }, Long),
            new DefaultFunctionRow("max", "Max", new[] { Dbl, Dbl }, Dbl),
            new DefaultFunctionRow("max", "Max", new[] { Dec, Dec }, Dec),
            new DefaultFunctionRow("round", "Round", new[] { Dbl }, Dbl),
            new DefaultFunctionRow("round", "Round", new[] { Dbl, Int }, Dbl),
            new DefaultFunctionRow("round", "Round", new[] { Dec }, Dec),
            new DefaultFunctionRow("round", "Round", new[] { Dec, Int }, Dec),
            new DefaultFunctionRow("floor", "Floor", new[] { Dbl }, Dbl),
            new DefaultFunctionRow("floor", "Floor", new[] { Dec }, Dec),
            new DefaultFunctionRow("ceil", "Ceil", new[] { Dbl }, Dbl),
            new DefaultFunctionRow("ceil", "Ceil", new[] { Dec }, Dec),
            new DefaultFunctionRow("range", "Range", new[] { Int, Int }, Range),
            new DefaultFunctionRow("range", "Range", new[] { Int, Int, Int }, Range),
        };
    }
}
