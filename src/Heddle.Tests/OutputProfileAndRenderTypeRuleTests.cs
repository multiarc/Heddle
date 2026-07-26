using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 1 WI11 (D11) — the two encoding-deciding rule files. These carry the same blast radius
    /// as an XSS regression: if the build tier and the run tier ever disagree about which carrier a bodiless
    /// <c>@(…)</c> binds, or about what <c>[EncodeOutput]</c> means, one tier emits unencoded output. The rules are
    /// now one function each; these theories enumerate them exhaustively so a unilateral edit is a red test.
    /// </summary>
    public class OutputProfileAndRenderTypeRuleTests
    {
        [Theory]
        [InlineData("text", true, OutputProfile.Text)]
        [InlineData("html", true, OutputProfile.Html)]
        [InlineData("TEXT", true, OutputProfile.Text)]
        [InlineData("HTML", true, OutputProfile.Html)]
        [InlineData("Html", true, OutputProfile.Html)]
        [InlineData("  html  ", true, OutputProfile.Html)]
        [InlineData("\t text \n", true, OutputProfile.Text)]
        [InlineData("htlm", false, OutputProfile.Text)]
        [InlineData("", false, OutputProfile.Text)]
        [InlineData("   ", false, OutputProfile.Text)]
        [InlineData(null, false, OutputProfile.Text)]
        [InlineData("html text", false, OutputProfile.Text)]
        public void TryParseProfileMatchesTheDocumentedRule(string value, bool expected, OutputProfile profile)
        {
            Assert.Equal(expected, OutputProfileRules.TryParseProfile(value, out var parsed));
            if (expected)
                Assert.Equal(profile, parsed);
        }

        [Theory]
        [InlineData("Native", true, ExpressionMode.Native)]
        [InlineData("native", true, ExpressionMode.Native)]
        [InlineData("MemberPathsOnly", true, ExpressionMode.MemberPathsOnly)]
        [InlineData("memberpathsonly", true, ExpressionMode.MemberPathsOnly)]
        [InlineData("FullCSharp", true, ExpressionMode.FullCSharp)]
        [InlineData("  fullcsharp ", true, ExpressionMode.FullCSharp)]
        [InlineData("Full", false, ExpressionMode.Native)]
        [InlineData("", false, ExpressionMode.Native)]
        [InlineData(null, false, ExpressionMode.Native)]
        public void TryParseExpressionModeMatchesTheEnumNames(string value, bool expected, ExpressionMode mode)
        {
            Assert.Equal(expected, OutputProfileRules.TryParseExpressionMode(value, out var parsed));
            if (expected)
                Assert.Equal(mode, parsed);
        }

        /// <summary>The (profile × hasBody) matrix. A bodied <c>@(X){{…}}</c> is a raw rescoping container and is
        /// never redirected, whatever the profile — the one row a "profile decides encoding" shortcut gets wrong.</summary>
        [Theory]
        [InlineData(OutputProfile.Html, false, UnnamedCarrierKind.EmptyHtml, RenderType.Encode, "html")]
        [InlineData(OutputProfile.Html, true, UnnamedCarrierKind.Empty, RenderType.Raw, "")]
        [InlineData(OutputProfile.Text, false, UnnamedCarrierKind.Empty, RenderType.Raw, "")]
        [InlineData(OutputProfile.Text, true, UnnamedCarrierKind.Empty, RenderType.Raw, "")]
        public void ResolveUnnamedCarrierCoversTheWholeMatrix(OutputProfile profile, bool hasBody,
            UnnamedCarrierKind kind, RenderType renderType, string registryName)
        {
            OutputProfileRules.ResolveUnnamedCarrier(profile, hasBody, out var actualKind, out var actualRender);
            Assert.Equal(kind, actualKind);
            Assert.Equal(renderType, actualRender);
            Assert.Equal(registryName, OutputProfileRules.CarrierRegistryName(actualKind));
        }

        /// <summary>All four bool pairs of the <c>[EncodeOutput]</c>/<c>[NotEncode]</c> truth table.</summary>
        [Theory]
        [InlineData(false, false, RenderType.Raw)]
        [InlineData(false, true, RenderType.Raw)]
        [InlineData(true, false, RenderType.Encode)]
        [InlineData(true, true, RenderType.Raw)]
        public void DeriveCoversTheWholeTruthTable(bool hasEncodeOutput, bool hasNotEncode, RenderType expected)
        {
            Assert.Equal(expected, RenderTypeRules.Derive(hasEncodeOutput, hasNotEncode));
        }

        /// <summary>The valid-values fragment both tiers quote in their unknown-profile message is built from the
        /// same two constants the parser matches, so the message can never name a spelling the parser rejects.</summary>
        [Fact]
        public void ValidValuesFragmentNamesExactlyTheAcceptedSpellings()
        {
            Assert.Equal("text, html", OutputProfileRules.ValidProfileValues);
            Assert.True(OutputProfileRules.TryParseProfile(OutputProfileRules.TextProfileName, out _));
            Assert.True(OutputProfileRules.TryParseProfile(OutputProfileRules.HtmlProfileName, out _));
        }
    }
}
