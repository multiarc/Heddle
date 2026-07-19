using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 D9 / WI6 — custom <c>[ExtensionName]</c> extensions bind from referenced assemblies (never inlined) so
    /// custom-extension templates precompile, rendering byte-identically with the dynamic backend. A resolved
    /// extension that overrides a compile-time hook is refused (<c>HED7015</c>); an extension-only call shape whose
    /// name resolves nowhere is <c>HED7006</c>.
    /// </summary>
    public class CustomExtensionTests
    {
        [Theory]
        [InlineData("hello world")]
        [InlineData("")]
        [InlineData("<b>x</b>")]
        public void RawEngineExtensionBindsAndRendersIdentically(string value)
        {
            // @raw is EmptyExtension ([ExtensionName("raw")]) — a plain engine extension the binder now binds. It is
            // the trusted-value opt-out, so it must render unencoded under both profiles.
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
            // @yell resolves to the test assembly's YellExtension (a plain custom extension), bound directly.
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
        public void HookOverridingCustomExtensionReportsHed7015()
        {
            // @hooked resolves to HookedExtension, which overrides InitStart — unevaluable at build time (D22).
            var t = "@model(){{System.String}}@\\\n@hooked(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/hooked.heddle", t) });
            var hed7015 = gen.Diagnostics.FirstOrDefault(d => d.Id == "HED7015");
            Assert.NotEqual(default, hed7015);
            Assert.Equal(DiagnosticSeverity.Error, hed7015.Severity);
            Assert.Contains("hooked", hed7015.GetMessage());
            // No entry class emitted — the template is refused, not precompiled.
            Assert.DoesNotContain("class Views_Hooked", gen.ManifestSource ?? string.Empty);
        }

        [Fact]
        public void UnresolvableBodiedExtensionReportsHed7006()
        {
            // A bodied call (extension-only shape) whose name resolves to no [ExtensionName] type anywhere.
            var t = "@model(){{System.String}}@\\\n@nosuchext(this){{body}}\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/x.heddle", t) });
            var hed7006 = gen.Diagnostics.FirstOrDefault(d => d.Id == "HED7006");
            Assert.NotEqual(default, hed7006);
            Assert.Equal(DiagnosticSeverity.Error, hed7006.Severity);
            Assert.Contains("nosuchext", hed7006.GetMessage());
        }
    }
}
