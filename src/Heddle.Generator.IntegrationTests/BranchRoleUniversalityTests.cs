using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Generator.IntegrationTests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Generator-tier universality of the <see cref="Heddle.Attributes.BranchRoleAttribute"/> contract
    /// with a non-engine custom trio (<c>begin</c>/<c>between</c>/<c>finish</c>, the bodiless <c>flag</c>/<c>gate</c>
    /// pair, and the zebra-style <c>row</c> participant), all resolved from the referenced test assembly:
    /// <list type="bullet">
    /// <item>a bodied custom-branch call degrades safely to the dynamic tier (no HED7015) and renders with full role
    /// semantics;</item>
    /// <item>a bodiless custom role opener binds via the generic custom path (manifest row + render parity);</item>
    /// <item>the generator's role-based strip machine removes inter-block text on the precompiled tier exactly as the
    /// runtime does (differential parity);</item>
    /// <item>a precompiled body binding a bodiless <c>[ScopeChannel]</c> extension provisions a locals frame and
    /// renders without throwing.</item>
    /// </list>
    /// </summary>
    public class BranchRoleUniversalityTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        public static IEnumerable<object[]> CartModels()
        {
            yield return new object[] { new Cart { IsFeatured = true, IsArchived = false } };
            yield return new object[] { new Cart { IsFeatured = false, IsArchived = true } };
            yield return new object[] { new Cart { IsFeatured = false, IsArchived = false } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void BodiedCustomTrio_FallsBackWithoutHed7015_DynamicRendersWithRoleSemantics(Cart model)
        {
            var custom = "@model(){{" + CartType + "}}@\\\n" +
                         "@begin(IsFeatured){{ <b>Featured</b> }}\n" +
                         "@between(IsArchived){{ <i>Archived</i> }}\n" +
                         "@finish(){{ Regular }}\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/custom-trio.heddle", custom) });

            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7015");
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

            DifferentialHarness.ExpectDegrade(gen, "views/custom-trio.heddle");

            var builtin = "@model(){{" + CartType + "}}@\\\n" +
                          "@if(IsFeatured){{ <b>Featured</b> }}\n" +
                          "@elif(IsArchived){{ <i>Archived</i> }}\n" +
                          "@else(){{ Regular }}\n";
            Assert.Equal(RenderDynamic(builtin, model), RenderDynamic(custom, model));
        }

        [Theory]
        [InlineData("wonder")]
        [InlineData("")]
        [InlineData(null)]
        public void BodilessCustomOpener_BindsViaCustomPath_RenderParity(string value)
        {
            var t = "@model(){{System.String}}@\\\n<x>@flag(this)</x>\n";
            var (pre, dyn) = DifferentialHarness.Render("views/flag.heddle", t, typeof(string), value);
            Assert.Equal(dyn, pre);
        }

        [Fact]
        public void BodilessCustomOpener_RecordedInManifest()
        {
            var t = "@model(){{System.String}}@\\\n@flag(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/flag.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.NotNull(gen.ManifestSource);
            Assert.Contains("key: \"views/flag.heddle\"", gen.ManifestSource);
            Assert.Contains(
                "Heddle.Generator.IntegrationTests.Fixtures.FlagExtension, Heddle.Generator.IntegrationTests",
                gen.ManifestSource);
        }

        [Theory]
        [MemberData(nameof(CartModels))]
        public void CustomTrioStripParity_GeneratorMatchesRuntime(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n@flag(IsFeatured) STRAY @gate(IsArchived)\n";
            var (pre, dyn) = DifferentialHarness.Render("views/custom-strip.heddle", t, typeof(Cart), model);
            Assert.Equal(dyn, pre);
            Assert.DoesNotContain("STRAY", pre);
        }

        [Fact]
        public void HostsParticipantFix_BodilessScopeChannelExtension_ProvisionsLocalsAndRenders()
        {
            var t = "@model(){{System.String}}@\\\n@row()@row()@row()\n";
            var (pre, dyn) = DifferentialHarness.Render("views/rows.heddle", t, typeof(string), "seed");
            Assert.Equal(dyn, pre);
            Assert.Equal("evenoddeven", dyn.Trim());
        }

        private static string RenderDynamic(string content, Cart model)
        {
            var t = new HeddleTemplate(content,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(Cart)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }
    }
}
