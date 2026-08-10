using System;
using System.Collections.Generic;
using Heddle.Language.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Operator lexeme table drift guard. Adding an <see cref="ExprOperator"/> once had four touch points;
    /// the asymmetry hid damage (missing rows, transposed operators like <c>&amp;</c> for <c>&amp;&amp;</c>).
    /// Now the supported set is derived from the table, so a new member without a row fails here.
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

        /// <summary>Runtime's error-text copy. Omits <c>AndAlso</c>/<c>OrElse</c>/<c>Coalesce</c> (unreachable: those failures build their own message).</summary>
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

            // Contract: BinarySymbol(unary) must return null so emitter refuses impossible nodes like Negate in binary position.
            foreach (var pair in LegacyUnaryLexeme)
                Assert.Null(OperatorLexeme.ForBinary(pair.Key));
        }
    }
}
