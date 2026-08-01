using System;
using System.Linq;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The preparse cache keys on the generated C#, which encodes the expression and its types but not where the
    /// expression sits — so a cached failure must replay its diagnostics, and each replay must carry the position
    /// of the caller receiving it, not the position of whoever paid for the compile.
    /// </summary>
    public class PreparseDiagnosticReplayTests
    {
        /// <summary>
        /// Two contexts compile the same broken expression from different positions; the second is served from the
        /// cache and must still report — at its own position, with the first caller's message text. Dropping the
        /// replay silenced every caller after the first; storing the first caller's position stamped a one-line
        /// document with an error from another document entirely.
        /// <para>What is NOT claimed: that the second answer came from the cache rather than a recompile — both
        /// produce these bytes, and cache effectiveness is a different property. What reddens here is removing the
        /// replay, or replaying any position other than the receiving caller's.</para>
        /// </summary>
        [Fact]
        public void AReplayedFailureDiagnosticCarriesEachCallersOwnPosition()
        {
            var expression = "no_such_symbol_" + Guid.NewGuid().ToString("N");

            var first = Compile(expression, position: 7);
            var second = Compile(expression, position: 41);

            Assert.NotEmpty(first.CompileErrors);
            Assert.All(first.CompileErrors, error => Assert.Equal(7, error.Position.StartIndex));

            Assert.NotEmpty(second.CompileErrors);
            Assert.All(second.CompileErrors, error => Assert.Equal(41, error.Position.StartIndex));
            Assert.Equal(first.CompileErrors.Select(error => error.Error),
                second.CompileErrors.Select(error => error.Error));
        }

        private static CompileContext Compile(string expression, int position)
        {
            var context = new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp });
            new CSharpContext().ParseAndGetResultType(context,
                new ExpressionOptions
                {
                    Expression = expression,
                    ChainedType = (ExType)typeof(object),
                    ExtensionName = "t",
                    Position = new BlockPosition(position, expression.Length)
                }, out _);
            return context;
        }
    }
}
