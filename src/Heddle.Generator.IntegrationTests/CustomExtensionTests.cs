extern alias generator;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Custom <c>[ExtensionName]</c> extensions bind from referenced assemblies (never inlined) so custom-extension
    /// templates precompile, rendering byte-identically with the dynamic backend. A resolved extension that
    /// overrides a compile-time hook degrades to the dynamic tier under the <c>HED7015</c> warning; an
    /// extension-only call shape whose name resolves nowhere is the <c>HED7006</c> error.
    /// </summary>
    public class CustomExtensionTests
    {
        [Theory]
        [InlineData("hello world")]
        [InlineData("")]
        [InlineData("<b>x</b>")]
        public void RawEngineExtensionBindsAndRendersIdentically(string value)
        {
            // @raw (EmptyExtension) is the trusted-value opt-out; must render unencoded under both profiles.
            var t = "@model(){{System.String}}@\\\n<x>@raw(this)</x>\n";
            var (pre, dyn) = DifferentialHarness.Render("views/raw.heddle", t, typeof(string), value);
            Assert.Equal(dyn, pre);
        }

        [Theory]
        [InlineData("wonder")]
        [InlineData("")]
        [InlineData(null)]
        public void CustomExtensionBindsAndRendersIdentically(string value)
        {
            // @yell resolves to YellExtension from the test assembly.
            var t = "@model(){{System.String}}@\\\n<x>@yell(this)</x>\n";
            var (pre, dyn) = DifferentialHarness.Render("views/yell.heddle", t, typeof(string), value);
            Assert.Equal(dyn, pre);
        }

        [Fact]
        public void CustomExtensionRecordedInManifestAsAqnSansVersion()
        {
            var t = "@model(){{System.String}}@\\\n@yell(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/yell.heddle", t) });
            Assert.NotNull(gen.ManifestSource);
            Assert.Contains(
                "Heddle.Generator.IntegrationTests.Fixtures.YellExtension, Heddle.Generator.IntegrationTests",
                gen.ManifestSource);
        }

        [Fact]
        public void HookOverridingCustomExtensionWarnsHed7015AndDegrades()
        {
            // HookedExtension overrides InitStart, unevaluable at build time. That costs the call site its tier —
            // it does not fail the consumer's build: a third-party extension the generator cannot reason about is
            // not an authoring error.
            var t = "@model(){{System.String}}@\\\n@hooked(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/hooked.heddle", t) });
            var hed7015 = gen.Diagnostics.FirstOrDefault(d => d.Id == "HED7015");
            Assert.NotEqual(default, hed7015);
            Assert.Equal(DiagnosticSeverity.Warning, hed7015.Severity);
            Assert.Contains("hooked", hed7015.GetMessage());
            // Nothing the generator reports for this template is an error, so the consumer's build survives it.
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            // No entry class emitted — the template degrades, it is not precompiled.
            Assert.DoesNotContain("class Views_Hooked", gen.ManifestSource ?? string.Empty);
        }

        [Fact]
        public void HookOverridingCustomExtensionStillRendersThroughTheDynamicTier()
        {
            // The other half of the degrade: the template is refused at build time and the reader still gets a page,
            // rendered by the tier the refusal routes to.
            const string key = "views/hooked-render.heddle";
            var t = "@model(){{System.String}}@\\\n<x>@hooked(this)</x>\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Empty(gen.TemplateSources);   // degraded — no .g.cs
            DifferentialHarness.ExpectDegrade(gen, key,
                generator::Heddle.Generator.Emit.RefusalCategory.HookBehavior,
                "overrides a compile-time hook");

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            Assert.Equal("<x>wonder</x>\n", dynamicTemplate.Generate("wonder"));
        }

        [Fact]
        public void UnresolvableBodiedExtensionReportsHed7006()
        {
            // Bodied call shape with no matching [ExtensionName].
            var t = "@model(){{System.String}}@\\\n@nosuchext(this){{body}}\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/x.heddle", t) });
            var hed7006 = gen.Diagnostics.FirstOrDefault(d => d.Id == "HED7006");
            Assert.NotEqual(default, hed7006);
            Assert.Equal(DiagnosticSeverity.Error, hed7006.Severity);
            Assert.Contains("nosuchext", hed7006.GetMessage());
        }
    }
}
