using System;
using System.Collections.Generic;
using Heddle.Language.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 4 WI1 (D2) — the literal formatter is the documented inverse of the AST decoder, so
    /// <c>decode(format(v))</c> must return <c>v</c> bit-for-bit and with the identical CLR type. The generator runs
    /// inside the compiler process and the runtime never re-formats a literal (the compiler keeps the decoder's boxed
    /// value), so a formatter that does not round-trip makes the same template compute a different value precompiled
    /// than at runtime — <i>depending on which machine built it</i>. That is exactly what <c>ToString("R")</c> did
    /// for <c>double</c> under a .NET Framework host.
    /// <para>The randomized legs use a fixed seed so a failure is reproducible; the corner cases are enumerated.
    /// Values the decoder rejects by its own range rules (NaN, ±∞) can never come out of it, so the formatter never
    /// sees them and they are out of scope.</para>
    /// <para>WI7 relocated the formatter into <c>Heddle</c>, so this test lives in the runtime suite — which is
    /// what puts it on the <b>net48</b> leg, the exact TFM where <c>"R"</c> misbehaves, and what makes a future
    /// decoder change break a shared test instead of only breaking generated code.</para>
    /// </summary>
    public class LiteralRoundTripTests
    {
        private const int Seed = 20260725;

        /// <summary>Formats <paramref name="value"/> and runs the formatted text back through the decoder the same
        /// way the lexer would: a leading '-' is a unary sign prefix over the magnitude literal, never part of the
        /// literal token.</summary>
        private static object RoundTrip(object value)
        {
            var text = LiteralFormatter.Format(value);
            Assert.NotNull(text);

            bool negated = false;
            if (text[0] == '-')
            {
                negated = true;
                text = text.Substring(1);
            }

            object decoded = Decode(text);
            return negated ? ExpressionAstBuilder.Negate(decoded) : decoded;
        }

        private static object Decode(string text)
        {
            char last = text[text.Length - 1];
            bool real = last == 'F' || last == 'D' || last == 'M';
            var node = real
                ? ExpressionAstBuilder.DecodeReal(text, default)
                : ExpressionAstBuilder.DecodeInteger(text, default);
            Assert.Null(node.LiteralError);
            return node.Value;
        }

        private static void AssertRoundTrips(double value)
        {
            var back = RoundTrip(value);
            Assert.IsType<double>(back);
            Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits((double) back));
        }

        private static void AssertRoundTrips(float value)
        {
            var back = RoundTrip(value);
            Assert.IsType<float>(back);
            Assert.Equal(FloatBits(value), FloatBits((float) back));
        }

        private static int FloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

        public static IEnumerable<object[]> DoubleCorners()
        {
            var values = new[]
            {
                0d, -0d, 1d, -1d, 0.1d, 0.2d, 0.3d, 1d / 3d, 2d / 3d, 1e3d, 1e-3d, 1e30d, 1e-30d,
                double.Epsilon, -double.Epsilon, 2.2250738585072014E-308d, 2.2250738585072009E-308d,
                double.MaxValue, double.MinValue, 3.14159265358979d, 123456789.123456789d,
                9007199254740993d, 1e16d, 1e17d, 1.0000000000000002d
            };
            foreach (var v in values)
                yield return new object[] { v };
        }

        public static IEnumerable<object[]> SingleCorners()
        {
            var values = new[]
            {
                0f, -0f, 1f, -1f, 0.1f, 1f / 3f, 1e3f, 1e-3f, 1e30f, 1e-30f,
                float.Epsilon, -float.Epsilon, 1.17549435E-38f, float.MaxValue, float.MinValue,
                3.14159f, 16777217f, 1.00000012f
            };
            foreach (var v in values)
                yield return new object[] { v };
        }

        [Theory]
        [MemberData(nameof(DoubleCorners))]
        public void DoubleCornerCase_RoundTripsExactly(double value) => AssertRoundTrips(value);

        [Theory]
        [MemberData(nameof(SingleCorners))]
        public void SingleCornerCase_RoundTripsExactly(float value) => AssertRoundTrips(value);

        [Fact]
        public void RandomDoubles_RoundTripExactly()
        {
            var random = new Random(Seed);
            var buffer = new byte[8];
            for (int i = 0; i < 20000; i++)
            {
                random.NextBytes(buffer);
                double value = BitConverter.ToDouble(buffer, 0);
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;   // the decoder's range rules make these unreachable literals
                AssertRoundTrips(value);
            }
        }

        [Fact]
        public void RandomSingles_RoundTripExactly()
        {
            var random = new Random(Seed);
            var buffer = new byte[4];
            for (int i = 0; i < 20000; i++)
            {
                random.NextBytes(buffer);
                float value = BitConverter.ToSingle(buffer, 0);
                if (float.IsNaN(value) || float.IsInfinity(value))
                    continue;
                AssertRoundTrips(value);
            }
        }

        [Fact]
        public void RandomScaledDoubles_RoundTripExactly()
        {
            // The shape real templates actually contain: human-scale decimals, where "R" on .NET Framework is at
            // its worst and where G17's longer text is the visible change.
            var random = new Random(Seed + 1);
            for (int i = 0; i < 20000; i++)
            {
                double value = (random.NextDouble() - 0.5) * Math.Pow(10, random.Next(-12, 13));
                AssertRoundTrips(value);
                AssertRoundTrips((float) value);
            }
        }

        [Fact]
        public void IntegerAndDecimalLiterals_RoundTripWithTheirClrType()
        {
            Assert.Equal(0, RoundTrip(0));
            Assert.Equal(int.MaxValue, RoundTrip(int.MaxValue));
            Assert.Equal(42u, RoundTrip(42u));
            Assert.Equal(uint.MaxValue, RoundTrip(uint.MaxValue));
            Assert.Equal(long.MaxValue, RoundTrip(long.MaxValue));
            Assert.Equal(ulong.MaxValue, RoundTrip(ulong.MaxValue));
            Assert.Equal(decimal.MaxValue, RoundTrip(decimal.MaxValue));
            Assert.Equal(decimal.MinValue, RoundTrip(decimal.MinValue));
            Assert.Equal(0.1m, RoundTrip(0.1m));
            Assert.Equal(1.10m, RoundTrip(1.10m));   // trailing-zero scale is part of decimal's identity
            Assert.Equal(-7, RoundTrip(-7));
            Assert.Equal(-7L, RoundTrip(-7L));
        }

        [Fact]
        public void IntMinValue_RetypesToLong_TheDocumentedDeviation3()
        {
            // docs/native-expressions.md deviation 3: '-2147483648' types as long, because the sign is a unary
            // operator over a first-fit magnitude literal and 2147483648 does not fit int. The *value* is identical;
            // pinned here so the round-trip property's one type exception stays deliberate.
            Assert.Equal(-2147483648L, RoundTrip(int.MinValue));
        }

        [Fact]
        public void NoRoundTripFormatRemainsInTheGenerator()
        {
            // The regression this whole file exists for: "R" is the format that does not round-trip on a .NET
            // Framework build host. Pinned as a value assertion on the two formats the formatter must use.
            Assert.Equal("0.10000000000000001D", LiteralFormatter.Format(0.1d));
            Assert.Equal("0.100000001F", LiteralFormatter.Format(0.1f));
        }
    }
}
