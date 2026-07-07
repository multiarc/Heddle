using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 <c>@profile</c> document-order flip (phase 2 D4, README D22): the emitter tracks the running output
    /// profile and binds each unnamed <c>@(…)</c> carrier to EmptyExtension (Text) or EmptyHtmlExtension (Encode) by
    /// the profile active at that document position — flipped by <c>@profile(){{html|text}}</c>. Differential-gated
    /// against the runtime, which flips <c>context.OutputProfile</c> in the same order.
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
