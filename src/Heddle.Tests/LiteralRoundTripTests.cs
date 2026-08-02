using System;
using System.Collections.Generic;
using System.Globalization;
using Heddle.Language.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The literal formatter is the documented inverse of the AST decoder, so <c>decode(format(v))</c> must return
    /// <c>v</c> bit-for-bit and with the identical CLR type. The generator runs inside the compiler process and the
    /// runtime never re-formats a literal (the compiler keeps the decoder's boxed value), so a formatter that does
    /// not round-trip makes the same template compute a different value precompiled than at runtime — depending on
    /// which machine built it. That is exactly what <c>ToString("R")</c> did for <c>double</c> under a .NET Framework
    /// host.
    /// <para>The randomized legs use a fixed seed so a failure is reproducible; the corner cases are enumerated.
    /// Values the decoder rejects by its own range rules (NaN, ±∞) can never come out of it, so the formatter never
    /// sees them and they are out of scope.</para>
    /// <para>The formatter lives in <c>Heddle</c> rather than the generator, so this test lives in the runtime
    /// suite — which puts it on the <b>net48</b> leg, the exact TFM where <c>"R"</c> misbehaves, and makes a future
    /// decoder change break a shared test instead of only breaking generated code.</para>
    /// </summary>
    public class LiteralRoundTripTests
    {
        private const int Seed = 20260725;

        /// <summary>Formats <paramref name="value"/> and decodes it: leading '-' is unary, not part of the token.</summary>
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
                    continue;
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
            // Human-scale decimals: where "R" on .NET Framework diverges most from G17.
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
            Assert.Equal(1.10m, RoundTrip(1.10m));
            Assert.Equal(-7, RoundTrip(-7));
            Assert.Equal(-7L, RoundTrip(-7L));
        }

        [Fact]
        public void IntMinValue_RetypesToLong_TheDocumentedDeviation3()
        {
            // Sign is unary; 2147483648 does not fit int, so type is long: documented exception.
            Assert.Equal(-2147483648L, RoundTrip(int.MinValue));
        }

        [Fact]
        public void NoRoundTripFormatRemainsInTheGenerator()
        {
            // Regression guard: "R" does not round-trip on .NET Framework.
            Assert.Equal("0.10000000000000001D", LiteralFormatter.Format(0.1d));
            Assert.Equal("0.100000001F", LiteralFormatter.Format(0.1f));
        }

        // On .NET Core "R" is shortest-round-trippable, so round-trip tests alone cannot catch a revert.
        // This guard asserts the formatter uses G17/G9, not "R", which is the property that keeps .NET Framework safe.

        /// <summary>
        /// The fixed-digit form with the sign of negative zero restored.
        ///
        /// <para>The formatter deliberately does NOT emit the runtime's raw G17/G9 text for negative
        /// zero: .NET Framework renders <c>-0.0</c> as <c>"0"</c>, which decodes back to POSITIVE
        /// zero and breaks the bit-for-bit round-trip these tests exist to protect. Comparing
        /// against the raw form would therefore demand the bug. .NET Core keeps the sign, so this
        /// helper is the identity there.</para>
        /// </summary>
        private static string RestoreNegativeZero(string text, double value) =>
            text.Length != 0 && text[0] != '-'
            && value == 0d && BitConverter.DoubleToInt64Bits(value) < 0
                ? "-" + text
                : text;

        // Separate overloads deliberately: formatting a float THROUGH double would change the text
        // (G9 of (double)0.1f is "0.1", of the float itself "0.100000001"). Only the sign test is
        // shared, and widening to double preserves the sign bit.
        private static string G17(double value) =>
            RestoreNegativeZero(value.ToString("G17", CultureInfo.InvariantCulture), value);

        private static string G9(float value) =>
            RestoreNegativeZero(value.ToString("G9", CultureInfo.InvariantCulture), value);

        [Theory]
        [MemberData(nameof(DoubleCorners))]
        public void DoubleText_IsExactlyTheG17Form(double value) =>
            Assert.Equal(G17(value) + "D", LiteralFormatter.Format(value));

        [Theory]
        [MemberData(nameof(SingleCorners))]
        public void SingleText_IsExactlyTheG9Form(float value) =>
            Assert.Equal(G9(value) + "F", LiteralFormatter.Format(value));

        [Fact]
        public void EveryFormattedReal_IsTheFixedDigitForm_OverTheRandomizedValueSpace()
        {
            var random = new Random(Seed);
            var buffer = new byte[8];
            int doubleChanged = 0, singleChanged = 0;
            for (int i = 0; i < 20000; i++)
            {
                random.NextBytes(buffer);
                double d = BitConverter.ToDouble(buffer, 0);
                if (!double.IsNaN(d) && !double.IsInfinity(d))
                {
                    Assert.Equal(d.ToString("G17", CultureInfo.InvariantCulture) + "D", LiteralFormatter.Format(d));
                    if (d.ToString("R", CultureInfo.InvariantCulture) != d.ToString("G17", CultureInfo.InvariantCulture))
                        doubleChanged++;
                }

                float f = BitConverter.ToSingle(buffer, 0);
                if (!float.IsNaN(f) && !float.IsInfinity(f))
                {
                    Assert.Equal(f.ToString("G9", CultureInfo.InvariantCulture) + "F", LiteralFormatter.Format(f));
                    if (f.ToString("R", CultureInfo.InvariantCulture) != f.ToString("G9", CultureInfo.InvariantCulture))
                        singleChanged++;
                }
            }

            // Guard is reliable only if "R" and G17/G9 disagree on a large portion of the value space.
            //
            // The floor is runtime-specific. On .NET Core "R" is shortest-round-trippable, so it
            // disagrees with the fixed-digit forms across most of the space. On .NET Framework "R"
            // is itself a (defective) 15-to-17-digit form, so it COINCIDES with G17/G9 far more
            // often — measured at ~964 of the sampled doubles here. A single Core-calibrated floor
            // therefore reddened netfx for having the very behaviour that motivates this test.
            // Both floors are still large enough that a revert to "R" would be caught.
#if NETFRAMEWORK
            const int floor = 500;
#else
            const int floor = 4000;
#endif
            Assert.True(doubleChanged > floor,
                $"'R' and G17 produced different text for only {doubleChanged} of the sampled doubles — the " +
                "format-identity guard above would no longer reliably detect a revert to \"R\".");
            Assert.True(singleChanged > floor,
                $"'R' and G9 produced different text for only {singleChanged} of the sampled singles.");
        }

        [Fact]
        public void HumanScaleDecimals_AreTheFixedDigitForm_WhereRIsAtItsWorst()
        {
            // Test cases where .NET Framework's "R" diverges most: short literals with long G17 expansion.
            Assert.Equal("0.29999999999999999D", LiteralFormatter.Format(0.3d));
            Assert.Equal("0.33333333333333331D", LiteralFormatter.Format(1d / 3d));
            Assert.Equal("4.9406564584124654E-324D", LiteralFormatter.Format(double.Epsilon));
            Assert.Equal("1.00000002E+30F", LiteralFormatter.Format(1E+30F));
            Assert.Equal("3.40282347E+38F", LiteralFormatter.Format(float.MaxValue));
            foreach (var value in new[] { 0.1d, 0.2d, 0.3d, 1d / 3d, 2d / 3d })
            {
                var text = LiteralFormatter.Format(value);
                var g17 = value.ToString("G17", CultureInfo.InvariantCulture);
                var r = value.ToString("R", CultureInfo.InvariantCulture);
                Assert.Equal(g17 + "D", text);

                // The NotEqual is the anti-revert guard: it proves the formatter is not using "R".
                // It can only prove that where the two forms actually differ on this runtime. On
                // .NET Framework "R" and G17 coincide for some of these values (1/3 renders
                // "0.33333333333333331" either way), and asserting inequality there would be
                // asserting a property of the runtime's formatter, not of ours.
                if (r != g17) Assert.NotEqual(r + "D", text);
            }
        }
    }
}
