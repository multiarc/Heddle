using System.Collections.Generic;
using Heddle.Generator.Emit;
using Heddle.Tests;
using Xunit;

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
