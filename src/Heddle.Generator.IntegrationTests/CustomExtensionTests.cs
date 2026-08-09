extern alias generator;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Custom <c>[ExtensionName]</c> extensions bind from referenced assemblies (never inlined) so custom-extension
    /// templates precompile, rendering byte-identically with the dynamic backend. An extension that overrides a
    /// compile-time hook, bodied or not, precompiles too — the hook runs for real at static-init — and an
    /// extension-only call shape whose name resolves nowhere is still the <c>HED7006</c> error.
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

        /// <summary>
        /// <b>The headline capability, half one: a custom extension that overrides the compile-time hook
        /// precompiles in a DEFAULT build.</b> No opt-in property, no probe, no name list — the override runs for
        /// real inside the consumer's assembly at static-init, through <c>PrecompiledRuntime.Init</c>. It used to
        /// cost the template its tier under an <c>HED7015</c> warning, and that warning must now be absent, because
        /// the fact it reported is no longer true of this build.
        /// </summary>
        [Fact]
        public void HookOverridingCustomExtensionPrecompilesInADefaultBuild()
        {
            const string key = "views/hooked.heddle";
            var t = "@model(){{System.String}}@\\\n<x>@hooked(this)</x>\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7015");
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, key);
            Assert.Contains("PrecompiledRuntime.Init(", Assert.Single(gen.TemplateSources).Value);

            var (pre, dyn) = DifferentialHarness.Render(key, t, typeof(string), "wonder");
            Assert.Equal(dyn, pre);
            Assert.Equal("<x>wonder</x>\n", pre);
        }

        /// <summary>
        /// <b>The headline capability, half two: a BODIED call to a third-party extension precompiles in a default
        /// build.</b> <c>@bellow</c>'s hook re-types its body against the caller's scope — the step-back shape nine
        /// engine extensions share — and the build has never read it. It no longer needs to: the body is emitted
        /// with no model cast, the hook chooses the typing at static-init, and the bytes match the dynamic tier's.
        /// </summary>
        [Theory]
        [InlineData("wonder")]
        [InlineData("")]
        [InlineData(null)]
        public void BodiedCallToAThirdPartyExtensionPrecompilesInADefaultBuild(string value)
        {
            const string key = "views/bellow.heddle";
            var t = "@model(){{System.String}}@\\\n<x>@bellow(){{loud}}</x>\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7015");
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, key);

            // @bellow upper-cases its value, and steps back to its body only when the value is null — so the
            // body is what the null case renders and the pinned bytes cover both arms.
            var (pre, dyn) = DifferentialHarness.Render(key, t, typeof(string), value);
            Assert.Equal(dyn, pre);
            Assert.Equal("<x>" + (value == null ? "LOUD" : value.ToUpperInvariant()) + "</x>\n", pre);
        }

        /// <summary>The type-agnostic body is not a blank one: a member read inside it resolves through the
        /// engine's own member walk against the type the hook chose, bound once at static-init, and renders the
        /// dynamic tier's bytes.</summary>
        [Fact]
        public void ATypeAgnosticBodyReadsItsModelThroughTheEnginesOwnAccessor()
        {
            const string key = "views/bellow-read.heddle";
            // The value is null, so @bellow steps back to its body — which is where the member read lives, typed
            // by the model this hook chose rather than by anything the build resolved.
            var t = "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Product}}@\\\n" +
                    "<x>@bellow(Description){{@(Name)}}</x>\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, key);
            Assert.Contains("PrecompiledLateAccessor(", Assert.Single(gen.TemplateSources).Value);

            var (pre, dyn) = DifferentialHarness.Render(key, t, typeof(Fixtures.Product),
                new Fixtures.Product { Name = "photos", Description = null });
            Assert.Equal(dyn, pre);
            Assert.Equal("<x>PHOTOS</x>\n", pre);
        }

        /// <summary>
        /// The first of the three shapes a type-agnostic body cannot carry: a <b>computed</b> native expression.
        /// Its result type depends on an operand type that does not exist until the hook has answered, and routing
        /// it through the DLR would be <i>wrong</i> rather than slow — the binder's numeric promotion is not the
        /// engine's for every operand pair, and that is a rendered-bytes difference. It costs this call site, which
        /// renders by compiling its own text; the neighbouring call and the template keep the tier.
        /// </summary>
        [Fact]
        public void AComputedExpressionInATypeAgnosticBodyCostsThatCallSiteAndNotTheTemplate()
        {
            const string key = "views/bellow-computed.heddle";
            var t = "@model(){{Heddle.Generator.IntegrationTests.Fixtures.GridModel}}@\\\n" +
                    "<x>@bellow(Name){{@(Cols + 1)}}</x><y>@yell(Name)</y>\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("PrecompiledRuntime.SiteFallback(", source);
            Assert.Contains("() => new global::Heddle.Generator.IntegrationTests.Fixtures.YellExtension()", source);

            var (pre, dyn) = DifferentialHarness.Render(key, t, typeof(Fixtures.GridModel),
                new Fixtures.GridModel { Name = "photos", Cols = 2 });
            Assert.Equal(dyn, pre);
        }

        /// <summary>
        /// The bound the substitute states about itself, enforced. A fragment compiled as its own document sees no
        /// enclosing definitions — so where a body would have to take the substitute <i>and</i> calls one, the
        /// substitute is refused and the <b>whole template</b> goes to the dynamic tier, where the definition
        /// exists. Both halves are needed to reach it: a definition call the body can emit outright is not a
        /// problem, and a computed expression on its own costs only the call site.
        /// </summary>
        [Fact]
        public void ATypeAgnosticBodyNeedingTheSubstituteAndNamingADefinitionRefusesTheWholeTemplate()
        {
            const string key = "views/bellow-definition.heddle";
            var t = "@model(){{Heddle.Generator.IntegrationTests.Fixtures.GridModel}}@\\\n" +
                    "@%<greet>{{hello}} :: System.String%@\n" +
                    "<x>@bellow(Name){{@greet(Name)@(Cols + 1)}}</x>\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key,
                generator::Heddle.Generator.Emit.RefusalCategory.HookBehavior, "body");

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(Fixtures.GridModel)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            Assert.Contains("<x>",
                dynamicTemplate.Generate(new Fixtures.GridModel { Name = "photos", Cols = 2 }));
        }

        /// <summary>
        /// <b><c>[PrecompileUnsupported]</c> costs one call site, not the template.</b> The declaring call binds
        /// dynamically — it renders by compiling its own source text at first render — while the neighbouring call
        /// in the same template is precompiled as usual, and the whole template keeps its manifest entry. The
        /// <c>HED7033</c> warning quotes the extension author's declared reason verbatim.
        /// </summary>
        [Fact]
        public void APrecompileUnsupportedExtensionFallsBackPerCallSiteAndReportsHed7033()
        {
            const string key = "views/scanner.heddle";
            var t = "@model(){{System.String}}@\\\n<x>@scanner(this)</x><y>@yell(this)</y>\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });

            var hed7033 = gen.Diagnostics.FirstOrDefault(d => d.Id == "HED7033");
            Assert.NotEqual(default, hed7033);
            Assert.Equal(DiagnosticSeverity.Warning, hed7033.Severity);
            Assert.Contains("scanner", hed7033.GetMessage());
            Assert.Contains("reads the enclosing document through InitContext.ParseContext", hed7033.GetMessage());
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

            // The template is still precompiled: only the declaring call left the tier.
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("PrecompiledRuntime.SiteFallback(", source);
            Assert.Contains("() => new global::Heddle.Generator.IntegrationTests.Fixtures.YellExtension()", source);

            var (pre, dyn) = DifferentialHarness.Render(key, t, typeof(string), "wonder");
            Assert.Equal(dyn, pre);
            Assert.Equal("<x>scanned:wonder</x><y>WONDER!</y>\n", pre);
        }

        /// <summary>The declaration is read off the <b>live</b> type as well as the symbol, so a package that adds
        /// it after a consumer's assembly was built still falls back rather than binding through a seam its author
        /// has disowned.</summary>
        [Fact]
        public void TheRuntimeReadsPrecompileUnsupportedOffTheLiveTypeToo()
        {
            var site = new Heddle.Precompiled.PrecompiledInitSite
            {
                ExtensionName = "scanner",
                SourceText = "@scanner(this)",
                ModelType = typeof(string)
            };
            var bound = Heddle.Precompiled.PrecompiledRuntime.Init(
                () => new Fixtures.ScannerExtension(), site, null);

            Assert.NotNull(site.Fault);
            Assert.Equal(Heddle.Precompiled.PrecompiledInitFaultScope.CallSite, site.Fault.Scope);
            Assert.Contains("[PrecompileUnsupported]", site.Fault.Detail);
            Assert.Contains("InitContext.ParseContext", site.Fault.Detail);
            Assert.IsNotType<Fixtures.ScannerExtension>(bound);

            // And the reason the declaration is not decorative: the hook never ran, so the observation it would
            // have made is not one this seam could have given it.
            Assert.Equal(0, new Fixtures.ScannerExtension().Observed);
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
