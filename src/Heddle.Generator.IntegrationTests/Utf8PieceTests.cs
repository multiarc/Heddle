using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 D15 — the opt-in <c>"…"u8</c> piece tier. With <c>HeddleEmitUtf8Pieces=true</c> the emitter emits, for
    /// every piece, the compiler-embedded UTF-8 twin (<c>PnU8 =&gt; "…"u8;</c>) alongside the string constant, records
    /// the <c>Utf8Pieces</c> capability, and the generated code still compiles and renders identically (the string
    /// path is what v1 renders; the bytes are dead until phase 8). Default (toggle off) emits no twins.
    /// </summary>
    public class Utf8PieceTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        [Fact]
        public void Utf8TwinsEmittedAndCapabilityRecordedWhenEnabled()
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<h1>@(Name)</h1>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/u8.heddle", t) },
                globalOptions: new Dictionary<string, string> { ["build_property.HeddleEmitUtf8Pieces"] = "true" });

            var src = gen.TemplateSources.Values.Single();
            Assert.Contains("U8 => ", src);
            Assert.Contains("u8;", src);
            Assert.Contains("Utf8Pieces", gen.ManifestSource);
            Assert.NotNull(gen.Assembly); // the u8 tier compiles (LangVersion latest in the harness).
        }

        [Fact]
        public void NoTwinsByDefault()
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<h1>@(Name)</h1>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/u8-off.heddle", t) });
            var src = gen.TemplateSources.Values.Single();
            Assert.DoesNotContain("u8;", src);
            Assert.DoesNotContain("Utf8Pieces", gen.ManifestSource);
        }

        [Theory]
        [InlineData("Widget")]
        [InlineData(null)]
        public void RenderParityUnchangedWithU8(string name)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<h1>@(Name)</h1> <p>Привет</p>\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/u8-render.heddle", t, typeof(Product),
                new Product { Name = name },
                globalOptions: new Dictionary<string, string> { ["build_property.HeddleEmitUtf8Pieces"] = "true" });
            Assert.Equal(dyn, precompiled);
        }
    }
}
