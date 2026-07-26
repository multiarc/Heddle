using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Runtime;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Document-order profile flip: emitter binds <c>@(…)</c> by active profile (flipped by <c>@profile(){{html|text}}</c>),
    /// mirrored by runtime <c>context.OutputProfile</c> flip.
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
            var t = "@model(){{" + ProductType + "}}@\\\nraw:@(Name)@profile(){{html}}enc:@(Name)\n";
            AssertParity("views/profile-flip.heddle", t, typeof(Product), model);
        }

        /// <summary>
        /// Unknown-profile twin: runtime rejects with HED2001; emitter must also error to prevent silent mismatches.
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

            // Both tiers anchor to the @profile directive (block position starts past '@').
            Assert.Equal(t.IndexOf("@profile", StringComparison.Ordinal) + 1, build.Location.SourceSpan.Start);
            Assert.Equal(runtimeError.Position.StartIndex, build.Location.SourceSpan.Start);
            Assert.Contains("Unknown output profile 'htlm'. Valid values: text, html.", runtimeError.Error);
        }

        /// <summary>Each directive error gets its own diagnostic (not suppressed by earlier errors).</summary>
        [Fact]
        public void EachUnknownProfileDirectiveGetsItsOwnDiagnostic()
        {
            const string key = "views/profile-unknown-twice.heddle";
            var t = "@model(){{" + ProductType + "}}@\\\n" + "@profile(){{htlm}}a@profile(){{tekst}}b\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.Equal(2, gen.Diagnostics.Count(d => d.Id == "HED7022"));
        }

        /// <summary>Case and padding variance parse identically (both use <c>OutputProfileRules.TryParseProfile</c>).</summary>
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
            var t = "enc:@(Name)@profile(){{text}}raw:@(Name)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/profile-untflip.heddle", t, typeof(Product), model,
                globalOptions: new Dictionary<string, string> { ["build_property.HeddleOutputProfile"] = "Html" },
                runtimeOptions: new Heddle.Data.TemplateOptions { OutputProfile = Heddle.Data.OutputProfile.Html });
            Assert.Equal(dyn, precompiled);
        }
    }
}
