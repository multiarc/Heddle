using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The value path's <c>as string ?? string.Empty</c> read as rendered bytes, driven by a host extension whose
    /// result is always a boxed <see cref="int"/>. One call is placed on both paths of one template: the render path
    /// keeps the number, the value path drops it.
    /// <para>
    /// How far a host extension can carry that is bounded, and the bound is asserted here rather than assumed. A
    /// host extension that does not override <c>InitStart</c> declares <c>System.String</c> as its return type —
    /// <c>AbstractExtension</c> hard-codes it — so the engine's DEBUG-only return-type check faults the dynamic
    /// tier before its coercion runs. The precompiled tier has no such check and coerces in both configurations,
    /// which is why the tier comparison below is a Release assertion and DEBUG pins the fault instead.
    /// </para>
    /// </summary>
    public class ValueRailCoercionDifferentialTests
    {
        private const string Header = "@model(){{System.String}}@\\\n";

        /// <summary>A definition splicing its caller content: the caller body's Execute result is what the definition
        /// body receives, which is how a body reaches the value path on both tiers.</summary>
        private const string WrapDefinition = "@%\n<wrap>{{<w>@out()</w>}}\n%@\n";

        private const string Model = "abcd";

        /// <summary>The boxed result on both paths of one document — "4" rendered, nothing spliced.</summary>
        private const string BothPaths = "R@tally(this)V@wrap(){{@tally(this)}}";

        private const string BothPathsExpected = "R4V<w></w>";

        private static DifferentialHarness.GenResult Generate(string key, string content)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            return gen;
        }

        private static string RenderDynamic(string content)
        {
            using var template = new HeddleTemplate(content,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(Model);
        }

        /// <summary>The render half of the asymmetry, with a host extension rather than the engine's own chained
        /// channel: the box is stringified, identically on both tiers, in either configuration.</summary>
        [Fact]
        public void TheBoxedResultIsStringifiedOnTheRenderPathIdenticallyOnBothTiers()
        {
            var (precompiled, dyn) = DifferentialHarness.Render("views/value-rail-render.heddle",
                Header + "[@tally(this)]", typeof(string), Model);
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[4]", precompiled);
        }

        /// <summary>The value half on the tier that coerces in every configuration.</summary>
        [Fact]
        public void ThePrecompiledTierDropsTheBoxedResultOnTheValuePathAndKeepsItOnTheRenderPath()
        {
            const string key = "views/value-rail-both-paths.heddle";
            var gen = Generate(key, Header + WrapDefinition + BothPaths);
            Assert.Equal(BothPathsExpected, DifferentialHarness.RenderGenerated(gen, key, Model));
        }

        /// <summary>
        /// The drop survives each arm of the three-case shape — one part returned as-is, several concatenated, and
        /// the piece-bearing shape whose runtime counterpart carries the piece fallback.
        /// </summary>
        [Theory]
        [InlineData("single", "@tally(this)", "<w></w>")]
        [InlineData("concat", "@tally(this)@tally(this)", "<w></w>")]
        [InlineData("pieces", "[@tally(this)]", "<w>[]</w>")]
        public void ThePrecompiledTierDropsTheBoxedResultInEveryArmOfTheThreeCaseShape(string shape,
            string callerContent, string expected)
        {
            var key = "views/value-rail-" + shape + ".heddle";
            var gen = Generate(key, Header + WrapDefinition + "@wrap(){{" + callerContent + "}}");
            Assert.Equal(expected, DifferentialHarness.RenderGenerated(gen, key, Model));
        }

#if DEBUG
        /// <summary>
        /// What stops the tier comparison from being unconditional: the engine checks the returned value against the
        /// type the extension declared, and a host extension that leaves <c>InitStart</c> alone always declares
        /// <c>System.String</c>. The check is DEBUG-only, so this is where the two tiers part company.
        /// </summary>
        [Fact]
        public void TheEngineFaultsOnTheDeclaredReturnTypeBeforeItsCoercionRuns()
        {
            var exception = Assert.Throws<Heddle.Exceptions.TemplateProcessingException>(
                () => RenderDynamic(Header + WrapDefinition + BothPaths));
            Assert.Equal("Returned data type not valid. Needed [System.String] Got [System.Int32]",
                exception.Message);
        }
#else
        /// <summary>Without that check the engine reaches its coercion, and the two tiers agree byte for byte.</summary>
        [Fact]
        public void TheDynamicTierDropsTheSameBoxedResultToTheSameBytes()
        {
            const string key = "views/value-rail-both-paths.heddle";
            var gen = Generate(key, Header + WrapDefinition + BothPaths);
            var precompiled = DifferentialHarness.RenderGenerated(gen, key, Model);
            var dyn = RenderDynamic(Header + WrapDefinition + BothPaths);

            Assert.Equal(dyn, precompiled);
            Assert.Equal(BothPathsExpected, precompiled);
        }
#endif

        /// <summary>
        /// The bodied twin: an extension that consumes its own body's <c>Execute</c> result. Its body typing is the
        /// extension's to decide, and the build no longer has to guess it — the body is emitted with no model cast
        /// and the extension's own hook types it at static-init, so both this call and its bodiless neighbour
        /// precompile and both render the engine's bytes.
        /// </summary>
        [Fact]
        public void ABodiedCallToTheSameExtensionPrecompilesAlongsideItsBodilessNeighbour()
        {
            const string bodied = "views/value-rail-bodied.heddle";
            var bodiedTemplate = Header + "@tally(this){{x}}";
            var gen = Generate(bodied, bodiedTemplate);
            DifferentialHarness.ExpectPrecompiled(gen, bodied);
            var (bodiedPre, bodiedDyn) = DifferentialHarness.Render(bodied, bodiedTemplate, typeof(string), Model);
            Assert.Equal(bodiedDyn, bodiedPre);
            Assert.Equal("4", bodiedPre);

            var (precompiled, dyn) = DifferentialHarness.Render("views/value-rail-bodiless.heddle",
                Header + "@tally(this)", typeof(string), Model);
            Assert.Equal(dyn, precompiled);
            Assert.Equal("4", precompiled);
        }
    }
}
