using System.Collections.Generic;
using Heddle.Generator.Emit;
using Heddle.Tests;
using Xunit;

// The vector tables carry a row label as their first argument. It is not read by the body -- its job is to
// name the failing row in the runner's output, which is what makes a red row in a table of dozens
// identifiable at all. Dropping it to satisfy the analyzer would trade that for nothing.
#pragma warning disable xUnit1026

namespace Heddle.Generator.Tests
{
    /// <summary>The <c>#line</c> mapper is a wrapper over the shared
    /// <c>LineIndex</c>, so it must report the shared golden vectors — the same table
    /// <c>HeddleCompileResult</c> and the LSP's <c>LineMap</c> assert — in the <c>#line</c> convention
    /// (1-based line, 1-based column).</summary>
    public class LineMapperAgreementTests
    {
        public static IEnumerable<object[]> Vectors() => LineIndexVectors.Rows();

        [Theory]
        [MemberData(nameof(Vectors))]
        public void MapMatchesTheSharedLineVectors(string name, string text, int offset, int line, int column)
        {
            Assert.Equal((line, column + 1), new LineMapper(text).Map(offset));
        }
    }
}
