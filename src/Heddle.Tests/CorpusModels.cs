using System;
using System.Collections.Generic;
using System.Linq;

namespace Heddle.Tests
{
    /// <summary>
    /// The model each <c>WithModel</c> intent row compiles under. Single-sourced: the types are the owning
    /// suites' own models (the props models, the expression goldens' models, the lint suite's model), so a
    /// model change in the owning suite fails the compile here rather than silently testing another shape.
    /// <para>A null value is a dynamic root — the document spells <c>@model(){{dynamic}}</c> and the render
    /// passes its own instance while the static type stays dynamic (the recursion flagships' arrangement).
    /// </para>
    /// </summary>
    internal static class CorpusModels
    {
        private static readonly IReadOnlyDictionary<string, Type> ByName =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                ["ergo-for.heddle"] = typeof(Data.ErgoForData),
                ["range-for.heddle"] = typeof(Data.ErgoForData),
                ["ergo-trim-preamble.heddle"] = typeof(Data.TestDataStructure),
                ["props-defaults.heddle"] = typeof(PropRoot),
                ["props-inherit.heddle"] = typeof(PropRoot),
                ["props-card.heddle"] = typeof(PropRoot),
                ["slot-compose.heddle"] = typeof(PropRoot),
                ["slot-picker.heddle"] = typeof(PropRoot),
                ["expr-flagship.heddle"] = typeof(NativeExpressionGoldenTests.FlagshipModel),
                ["expr-functions.heddle"] = typeof(NativeExpressionGoldenTests.FunctionsModel),
                ["context-lint-corpus.heddle"] = typeof(HtmlContextLintTests.LintModel),
                ["recursion.heddle"] = null,
                ["dynamic-recursion.heddle"] = null,
            };

        /// <summary>Every row name this table supplies a model for, ordinal-sorted. The declared half of the
        /// coverage gate: it must equal the intent table's <c>WithModel</c> set exactly.</summary>
        public static IReadOnlyList<string> DeclaredNames() =>
            ByName.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>The model type for one <c>WithModel</c> row, or null for a dynamic root. Throws naming the
        /// file on a miss — a <c>WithModel</c> row without a model entry is a gap in this table, not a
        /// dynamic row.</summary>
        public static Type For(string name)
        {
            if (ByName.TryGetValue(name, out var model))
                return model;
            throw new InvalidOperationException(
                "No CorpusModels entry declares '" + name + "'. Every intent row with Render = WithModel " +
                "needs exactly one entry in src/Heddle.Tests/CorpusModels.cs (null for a dynamic root).");
        }
    }
}
