using System.Collections.Generic;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI6a differential (D22): directive-line trimming (<c>HeddleTrimDirectiveLines</c>). The emitter's
    /// DocumentShaper reproduces the phase 4 D6 whole-line widening (<c>WidenToWholeLine</c>) and remnant-line
    /// trimming, so directive lines are removed byte-identically under both trim settings.
    /// </summary>
    public class TrimTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        public static IEnumerable<object[]> Models()
        {
            yield return new object[] { new Cart { Name = "Widget", Count = 2 } };
            yield return new object[] { null };
        }

        private static void AssertParity(string key, string content, bool trim, object model)
        {
            var global = new Dictionary<string, string>
            {
                ["build_property.HeddleTrimDirectiveLines"] = trim ? "true" : "false"
            };
            var runtime = new TemplateOptions { TrimDirectiveLines = trim };
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Cart), model, global, runtime);
            Assert.Equal(dyn, precompiled);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DirectiveLinesTrimmed(bool trim)
        {
            // Directive lines without @\ leave whole-line remnants that trimming removes.
            var t = "@using(){{System.Text}}\n@model(){{" + CartType + "}}\nName: @(Name); count @(Count).\n";
            AssertParity("views/trim-" + trim + ".heddle", t, trim, new Cart { Name = "W", Count = 5 });
        }

        [Theory]
        [MemberData(nameof(Models))]
        public void TrimWithBranch(Cart model)
        {
            var t = "@model(){{" + CartType + "}}\n" +
                    "@if(Count > 0){{ has @(Count) }}\ntail\n";
            AssertParity("views/trim-branch.heddle", t, true, model);
        }
    }
}
