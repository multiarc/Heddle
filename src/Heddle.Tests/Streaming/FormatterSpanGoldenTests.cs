using System;
using System.Globalization;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// The formatter built-ins render byte-identically to <c>ToString(format, provider)</c> after the
    /// span-funnel migration, on every TFM (net6+ uses the span/UTF-8 tiers; net48/netstandard the string tier),
    /// through all three sinks. Includes the &gt; 256-char format row exercising the tier-3 fallback (stackalloc
    /// overflow → ToString).
    /// </summary>
    public class FormatterSpanGoldenTests
    {
        public class FM
        {
            public int I { get; set; }
            public long L { get; set; }
            public decimal D { get; set; }
            public DateTime T { get; set; }
            public Guid G { get; set; }
        }

        private static FM Model() => new FM
        {
            I = -1234567,
            L = 9876543210L,
            D = 12345.678m,
            T = new DateTime(2026, 7, 7, 9, 5, 3, DateTimeKind.Utc),
            G = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")
        };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Reference semantics: ToString(format, culture).
        public static TheoryData<string, string> Cases()
        {
            var m = Model();
            return new TheoryData<string, string>
            {
                { "@int(I)", m.I.ToString(Inv) },
                { "@int(I){{N0}}", m.I.ToString("N0", Inv) },
                { "@int(I){{X8}}", m.I.ToString("X8", Inv) },
                { "@date(T)", m.T.ToString("d", Inv) },
                { "@date(T){{yyyy-MM-dd HH:mm:ss}}", m.T.ToString("yyyy-MM-dd HH:mm:ss", Inv) },
                { "@time(T)", m.T.ToString("t", Inv) },
                { "@time(T){{HH:mm:ss.fff}}", m.T.ToString("HH:mm:ss.fff", Inv) },
                { "@guid(G)", m.G.ToString() },
                { "@guid(G){{N}}", m.G.ToString("N") },
                { "@guid(G){{B}}", m.G.ToString("B") },
            };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void FormatterOutput_MatchesToString_AndThreeSinkParity(string doc, string expected)
        {
            var s = SinkTestHarness.AssertThreeSinkParity(doc, Model(), typeof(FM), OutputProfile.Text);
            Assert.Equal(expected, s);
        }

        [Fact]
        public void MoneyDefaultLocale_UsesThreadCurrentCulture_ThreeSinkParity()
        {
            // Pin culture to en-US for deterministic CI results; test proves extension reads thread-local culture.
            var prior = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                var s = SinkTestHarness.AssertThreeSinkParity("@money(D)", Model(), typeof(FM), OutputProfile.Text);
                Assert.Equal(Model().D.ToString("c"), s);
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
            }
        }

        [Fact]
        public void MoneyExplicitLocale_ThreeSinkParity()
        {
            var s = SinkTestHarness.AssertThreeSinkParity("@money(D){{en-US}}", Model(), typeof(FM), OutputProfile.Text);
            Assert.Equal(Model().D.ToString("c", CultureInfo.GetCultureInfo("en-US")), s);
        }

        [Fact]
        public void LongFormat_OverflowsStackallocTier_FallsBackToString()
        {
            // Stackalloc overflow test: format with 300 zero-pad digits falls back to ToString tier.
            var pad = new string('0', 300);
            var doc = "@int(I){{" + pad + "}}";
            var expected = Model().I.ToString(pad, Inv);
            Assert.True(expected.Length >= 300);
            var s = SinkTestHarness.AssertThreeSinkParity(doc, Model(), typeof(FM), OutputProfile.Text);
            Assert.Equal(expected, s);
        }

        [Fact]
        public void FormattersUnderHtml_ByteIdenticalAcrossSinks()
        {
            // Verify formatters are byte-identical across sinks despite different implementations.
            SinkTestHarness.AssertThreeSinkParity(
                "i=@int(I){{N0}} d=@date(T){{yyyy}} m=@money(D){{en-US}} g=@guid(G)", Model(), typeof(FM),
                OutputProfile.Html);
        }
    }
}
