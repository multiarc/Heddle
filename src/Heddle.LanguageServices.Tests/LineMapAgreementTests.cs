using System.Collections.Generic;
using Heddle.Tests;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>The LSP's line map is a wrapper over the shared <c>LineIndex</c>, so it must report the shared
    /// golden vectors — the same <c>HeddleCompileResult</c> and the generator's <c>LineMapper</c> assert — in LSP
    /// convention (zero-based line and character).</summary>
    public class LineMapAgreementTests
    {
        public static IEnumerable<object[]> Vectors() => LineIndexVectors.Rows();

        [Theory]
        [MemberData(nameof(Vectors))]
        public void OffsetToPositionMatchesTheSharedLineVectors(string name, string text, int offset, int line,
            int column)
        {
            Assert.Equal((line - 1, column), new LineMap(text).OffsetToPosition(offset));
        }

        [Theory]
        [MemberData(nameof(Vectors))]
        public void PositionToOffsetRoundTripsWithinTheLine(string name, string text, int offset, int line,
            int column)
        {
            Assert.Equal(offset, new LineMap(text).PositionToOffset(line - 1, column));
        }
    }
}
