using System;
using System.Collections.Generic;
using Heddle.Language.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The operator lexeme table. The drift this guards is structural: adding an
    /// <see cref="ExprOperator"/> member used to be four touch points, and the asymmetry hid the damage (a missing
    /// generator row degraded silently, a missing runtime row only garbled error text, and a <b>transposed</b> row —
    /// <c>&amp;</c> for <c>&amp;&amp;</c> — was valid C# with different semantics). The supported set is now derived
    /// from the table, so a new enum member without a row fails here.
    /// <para><b>Legacy*</b> below is the pre-extraction data, transcribed from the generator's <c>BinarySymbol</c>,
    /// its inline unary lexemes, and the runtime's error-text <c>Symbol</c> before they were deleted.</para>
    /// </summary>
    public class OperatorLexemeTests
    {
        private static readonly Dictionary<ExprOperator, string> LegacyBinarySymbol =
            new Dictionary<ExprOperator, string>
            {
                [ExprOperator.Add] = "+", [ExprOperator.Subtract] = "-", [ExprOperator.Multiply] = "*",
                [ExprOperator.Divide] = "/", [ExprOperator.Modulo] = "%", [ExprOperator.LeftShift] = "<<",
                [ExprOperator.RightShift] = ">>", [ExprOperator.LessThan] = "<",
                [ExprOperator.LessThanOrEqual] = "<=", [ExprOperator.GreaterThan] = ">",
                [ExprOperator.GreaterThanOrEqual] = ">=", [ExprOperator.Equal] = "==",
                [ExprOperator.NotEqual] = "!=", [ExprOperator.And] = "&", [ExprOperator.ExclusiveOr] = "^",
                [ExprOperator.Or] = "|", [ExprOperator.AndAlso] = "&&", [ExprOperator.OrElse] = "||",
                [ExprOperator.Coalesce] = "??",
            };

        private static readonly Dictionary<ExprOperator, string> LegacyUnaryLexeme =
            new Dictionary<ExprOperator, string>
            {
                [ExprOperator.Not] = "!", [ExprOperator.Negate] = "-", [ExprOperator.UnaryPlus] = "+",
                [ExprOperator.OnesComplement] = "~",
            };

        /// <summary>The runtime's error-text copy, verbatim — note it never carried
        /// <c>AndAlso</c>/<c>OrElse</c>/<c>Coalesce</c>, which is why the fold is only asserted over the rows it
        /// actually reached (the three missing ones were unreachable: logical and coalesce failures build their own
        /// message).</summary>
        private static readonly Dictionary<ExprOperator, string> LegacyRuntimeSymbol =
            new Dictionary<ExprOperator, string>
            {
                [ExprOperator.Add] = "+", [ExprOperator.Subtract] = "-", [ExprOperator.Multiply] = "*",
                [ExprOperator.Divide] = "/", [ExprOperator.Modulo] = "%", [ExprOperator.LeftShift] = "<<",
                [ExprOperator.RightShift] = ">>", [ExprOperator.LessThan] = "<",
                [ExprOperator.LessThanOrEqual] = "<=", [ExprOperator.GreaterThan] = ">",
                [ExprOperator.GreaterThanOrEqual] = ">=", [ExprOperator.Equal] = "==",
                [ExprOperator.NotEqual] = "!=", [ExprOperator.And] = "&", [ExprOperator.ExclusiveOr] = "^",
                [ExprOperator.Or] = "|",
            };

        [Fact]
        public void EveryOperatorHasExactlyOneLexeme_InExactlyOnePosition()
        {
            foreach (ExprOperator op in Enum.GetValues(typeof(ExprOperator)))
            {
                var binary = OperatorLexeme.ForBinary(op);
                var unary = OperatorLexeme.ForUnary(op);
                Assert.True(binary != null || unary != null,
                    $"ExprOperator.{op} has no lexeme row — add one to OperatorLexeme (and a verdict row to " +
                    "NativeOperatorRules) before the enum member ships.");
                Assert.True(binary == null || unary == null,
                    $"ExprOperator.{op} claims both a binary and a unary lexeme.");
                Assert.Equal(binary ?? unary, OperatorLexeme.For(op));
                Assert.Equal(binary != null, OperatorLexeme.IsBinary(op));
                Assert.Equal(unary != null, OperatorLexeme.IsUnary(op));
            }
        }

        [Fact]
        public void DerivedSupportedSetMatchesTheDocumentedNineteenPlusFour()
        {
            int binary = 0, unary = 0;
            foreach (ExprOperator op in Enum.GetValues(typeof(ExprOperator)))
            {
                if (OperatorLexeme.IsBinary(op)) binary++;
                if (OperatorLexeme.IsUnary(op)) unary++;
            }

            Assert.Equal(19, binary);
            Assert.Equal(4, unary);
        }

        [Fact]
        public void TableMatchesTheThreeLegacyCopies()
        {
            foreach (var pair in LegacyBinarySymbol)
                Assert.Equal(pair.Value, OperatorLexeme.ForBinary(pair.Key));
            foreach (var pair in LegacyUnaryLexeme)
                Assert.Equal(pair.Value, OperatorLexeme.ForUnary(pair.Key));
            foreach (var pair in LegacyRuntimeSymbol)
                Assert.Equal(pair.Value, OperatorLexeme.ForBinary(pair.Key));

            // The generator's BinarySymbol returned null for a unary operator in binary position — the contract the
            // emitter relies on to refuse an impossible node rather than emit "(a - b)" for a Negate.
            foreach (var pair in LegacyUnaryLexeme)
                Assert.Null(OperatorLexeme.ForBinary(pair.Key));
        }
    }
}
