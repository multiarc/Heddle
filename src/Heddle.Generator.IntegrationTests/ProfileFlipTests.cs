using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Runtime;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The <c>@profile</c> document-order flip: the emitter tracks the running output profile and binds each unnamed
    /// <c>@(…)</c> carrier to EmptyExtension (Text) or EmptyHtmlExtension (Encode) by the profile active at that
    /// document position — flipped by <c>@profile(){{html|text}}</c>. Differential-gated against the runtime, which
    /// flips <c>context.OutputProfile</c> in the same order.
    /// </summary>
    public class ProfileFlipTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Products()
        {
            yield return new object[] { new Product { Name = "<b>Ada & Co</b>" } };
            yield return new object[] { new Product { Name = "plain" } };
            yield return new object[] { new Product { Name = null } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void FlipToHtmlEncodesSubsequentOutput(Product model)
        {
            // Text before the flip (raw), Html after (encoded).
            var t = "@model(){{" + ProductType + "}}@\\\nraw:@(Name)@profile(){{html}}enc:@(Name)\n";
            AssertParity("views/profile-flip.heddle", t, typeof(Product), model);
        }

        /// <summary>
        /// The unknown-<c>@profile</c> twin. The runtime rejects <c>@profile(){{htlm}}</c> outright with HED2001 and
        /// never compiles the template. The emitter used to fall through the string match with a comment asserting
        /// "the template falls back" — it does not: nothing else refuses, so the template pre-compiled with the flip
        /// silently ignored and rendered output the dynamic tier would never produce. The options fingerprint keeps
        /// the COMPILE-TIME profile, not the post-flip value. Asserted as the twin relationship in one test.
        /// </summary>
        [Fact]
        public void UnknownProfileValueIsABuildErrorAndTheRuntimeTwinIsHed2001()
        {
            const string key = "views/profile-unknown-value.heddle";
            var t = "@model(){{" + ProductType + "}}@\\\n" + "raw:@(Name)@profile(){{htlm}}x:@(Name)\n";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            var build = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7022"));
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, build.Severity);
            Assert.Contains("'htlm'", build.GetMessage());
            Assert.Contains("text, html", build.GetMessage());
            var dynamic = new HeddleTemplate(t,
                new CompileContext(new Heddle.Data.TemplateOptions(), typeof(Product)));
            Assert.False(dynamic.CompileResult.Success);
            var runtimeError = Assert.Single(dynamic.CompileResult.ErrorList.Where(
                e => e.DiagnosticId == Heddle.Data.HeddleDiagnosticIds.UnknownOutputProfile));

            // Same anchoring on both tiers: the @profile directive, not the document start. (The block position
            // starts just past the '@', which is why the raw index is compared against the runtime's own.)
            Assert.Equal(t.IndexOf("@profile", StringComparison.Ordinal) + 1, build.Location.SourceSpan.Start);
            Assert.Equal(runtimeError.Position.StartIndex, build.Location.SourceSpan.Start);
            Assert.Contains("Unknown output profile 'htlm'. Valid values: text, html.", runtimeError.Error);
        }

        /// <summary>Two distinct typos get two distinct squiggles — the diagnostic is per directive, not per
        /// template, so a second mistake is not hidden by the first.</summary>
        [Fact]
        public void EachUnknownProfileDirectiveGetsItsOwnDiagnostic()
        {
            const string key = "views/profile-unknown-twice.heddle";
            var t = "@model(){{" + ProductType + "}}@\\\n" + "@profile(){{htlm}}a@profile(){{tekst}}b\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.Equal(2, gen.Diagnostics.Count(d => d.Id == "HED7022"));
        }

        /// <summary>Case and padding tolerance is identical on both tiers — the shared
        /// <c>OutputProfileRules.TryParseProfile</c> trims and matches ordinal-case-insensitively — and produces
        /// no diagnostic.</summary>
        [Theory]
        [InlineData("HTML")]
        [InlineData("Html")]
        [InlineData("  html  ")]
        [InlineData("TEXT")]
        [InlineData(" Text ")]
        public void ProfileValueCaseAndPaddingVariantsParseOnBothTiers(string value)
        {
            var key = "views/profile-case-variants.heddle";
            var t = "@model(){{" + ProductType + "}}@\\\n" + "raw:@(Name)@profile(){{" + value + "}}x:@(Name)\n";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7022");

            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(Product),
                new Product { Name = "<b>Ada & Co</b>" });
            Assert.Equal(dyn, precompiled);
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void FlipToTextFromHtmlDefault(Product model)
        {
            // Start Html (via build option), flip to Text mid-document.
            var t = "enc:@(Name)@profile(){{text}}raw:@(Name)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/profile-untflip.heddle", t, typeof(Product), model,
                globalOptions: new Dictionary<string, string> { ["build_property.HeddleOutputProfile"] = "Html" },
                runtimeOptions: new Heddle.Data.TemplateOptions { OutputProfile = Heddle.Data.OutputProfile.Html });
            Assert.Equal(dyn, precompiled);
        }
    }
}
