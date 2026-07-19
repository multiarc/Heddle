using System;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 6 (post-2.0) unit rows for the <see cref="Heddle.Models.Range"/> value type: value
    /// equality/hashing, the D4 readable <c>ToString()</c> (the phase's sole intentional
    /// rendered-byte change class), and the <c>FromSystemRange</c> interop (net6.0+ only —
    /// <c>System.Range</c> does not exist on netstandard2.0/net48).
    /// </summary>
    public class RangeTypeTests
    {
        [Fact]
        public void ValueEqualityOverAllThreeFields()
        {
            Assert.True(new Heddle.Models.Range(2, 10, 2) == new Heddle.Models.Range(2, 10, 2));
            Assert.True(new Heddle.Models.Range(2, 10, 2).Equals((object) new Heddle.Models.Range(2, 10, 2)));
            Assert.True(new Heddle.Models.Range(2, 10, 2) != new Heddle.Models.Range(2, 10));
            Assert.True(new Heddle.Models.Range(2, 10) != new Heddle.Models.Range(0, 10));
            Assert.False(new Heddle.Models.Range(2, 10).Equals(null));
        }

        [Fact]
        public void TwoArgConstructorDefaultsStepToOne()
        {
            var range = new Heddle.Models.Range(2, 10);
            Assert.Equal(2, range.Start);
            Assert.Equal(10, range.Last);
            Assert.Equal(1, range.Step);
        }

        [Fact]
        public void EqualValuesHashEqual()
        {
            Assert.Equal(new Heddle.Models.Range(2, 10, 2).GetHashCode(), new Heddle.Models.Range(2, 10, 2).GetHashCode());
            Assert.Equal(new Heddle.Models.Range(5, 2).GetHashCode(), new Heddle.Models.Range(5, 2, 1).GetHashCode());
        }

        [Fact] // D4 exact strings
        public void ToStringMirrorsTheCallForm()
        {
            Assert.Equal("range(2, 10, 2)", new Heddle.Models.Range(2, 10, 2).ToString());
            Assert.Equal("range(2, 10)", new Heddle.Models.Range(2, 10).ToString());
            Assert.Equal("range(5, 2)", new Heddle.Models.Range(5, 2).ToString());
        }

#if NET6_0_OR_GREATER
        [Fact] // D3 interop — TFMs where System.Range exists
        public void FromSystemRangeMapsFromStartEndpoints()
        {
            var range = Heddle.Models.Range.FromSystemRange(2..10);
            Assert.Equal(new Heddle.Models.Range(2, 10), range);
            Assert.Equal("range(2, 10)", range.ToString());
        }

        [Fact]
        public void FromSystemRangeRejectsFromEndIndices()
        {
            var ex = Assert.Throws<ArgumentException>(() => Heddle.Models.Range.FromSystemRange(2..^1));
            Assert.Equal("range", ex.ParamName);
            Assert.Throws<ArgumentException>(() => Heddle.Models.Range.FromSystemRange(^3..5));
        }
#endif
    }
}
