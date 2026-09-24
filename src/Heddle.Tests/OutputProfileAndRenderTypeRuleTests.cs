using System;
using Heddle.Attributes;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Encoding-deciding rule functions guarding against build/run tier divergence on carrier binding and
    /// [EncodeOutput] semantics; theories enumerate all cases so unilateral edits fail.</summary>
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

        /// <summary>The (profile × hasBody) matrix; bodied @(X){{…}} are rescoping containers never redirected.</summary>
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

        /// <summary>[NotEncode] targets Property while [EncodeOutput] targets Class, making simultaneous application
        /// impossible in C#; the veto row is reachable only from forged IL metadata, where both tiers get RenderType.Raw.</summary>
        [Fact]
        public void TheNotEncodeVetoRowIsUnreachableFromAnyDeclaration()
        {
            var notEncode = (AttributeUsageAttribute) Attribute.GetCustomAttribute(
                typeof(NotEncodeAttribute), typeof(AttributeUsageAttribute));
            var encodeOutput = (AttributeUsageAttribute) Attribute.GetCustomAttribute(
                typeof(EncodeOutputAttribute), typeof(AttributeUsageAttribute));

            Assert.NotNull(notEncode);
            Assert.NotNull(encodeOutput);
            Assert.Equal(AttributeTargets.Property, notEncode.ValidOn);
            Assert.Equal(AttributeTargets.Class, encodeOutput.ValidOn);
            Assert.Equal((AttributeTargets) 0, notEncode.ValidOn & encodeOutput.ValidOn);

            // The one observable state of the pair is behaviourally identical to carrying neither attribute.
            Assert.Equal(RenderTypeRules.Derive(false, false), RenderTypeRules.Derive(true, true));
        }

        /// <summary>The valid-values fragment is built from the same constants the parser matches.</summary>
        [Fact]
        public void ValidValuesFragmentNamesExactlyTheAcceptedSpellings()
        {
            Assert.Equal("text, html", OutputProfileRules.ValidProfileValues);
            Assert.True(OutputProfileRules.TryParseProfile(OutputProfileRules.TextProfileName, out _));
            Assert.True(OutputProfileRules.TryParseProfile(OutputProfileRules.HtmlProfileName, out _));
        }
    }
}
