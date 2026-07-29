using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// What the two tiers do about a model symbol only one of them can see. The engine resolves model types and
    /// members by reflection, which ignores assembly boundaries; generated code lives in the consumer's assembly and
    /// obeys them. So an <c>internal</c> type or member in a <b>referenced</b> assembly renders on the dynamic tier
    /// and cannot be pre-compiled at all — and the build must say so without failing.
    /// <para>This suite's fixtures only carry their point from a referenced assembly. They are declared in this test
    /// assembly, which the generator's compilation holds as a metadata reference and the generated output assembly
    /// has no <c>InternalsVisibleTo</c> grant from — the exact shape a real consumer is in with its model library.</para>
    /// </summary>
    public class InaccessibleModelSymbolTests
    {
        private const string InternalMember = "Heddle.Generator.IntegrationTests.Fixtures.InternalMemberModel";
        private const string InternalType = "Heddle.Generator.IntegrationTests.Fixtures.InternalModel";

        private static string Dynamic(string content, System.Type modelType, object model)
        {
            var template = new HeddleTemplate(content,
                new CompileContext(new TemplateOptions(), new ExType(modelType)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(model);
        }

        /// <summary>
        /// An <c>internal</c> member on a public model type from a referenced assembly. Roslyn imports from metadata
        /// only what the importing assembly could legally name, so the emitter's symbol model shows the member as
        /// absent — indistinguishable from a typo, which is why the build used to fail the consumer with HED7008 at
        /// <b>error</b> severity over a template the engine renders.
        /// </summary>
        [Fact]
        public void AnInternalMemberOnAReferencedModelDegradesWithAWarningRatherThanFailingTheBuild()
        {
            const string key = "views/internal-member.heddle";
            var template = "@model(){{" + InternalMember + "}}@\\\n@(Secret)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var hed7030 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7030"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7030.Severity);
            Assert.Contains("InternalMemberModel.Secret", hed7030.GetMessage());
            Assert.Contains(key, hed7030.Location.GetLineSpan().Path);
            DifferentialHarness.ExpectDegrade(gen, key);

            Assert.Equal("s3cret\n", Dynamic(template, typeof(InternalMemberModel), new InternalMemberModel()));
        }

        /// <summary>
        /// An <c>internal</c> model type from a referenced assembly. Unlike members, types <i>are</i> imported from
        /// metadata whatever their accessibility, so this never reached HED7007: the emitter resolved the type, wrote
        /// its fully-qualified name into the generated cast, and the consumer's build failed on a wall of CS0122
        /// against <c>.g.cs</c> — no Heddle id, no <c>.heddle</c> position, unfixable without editing the model.
        /// <para><see cref="DifferentialHarness.Generate"/> compiles what it generates and throws when that fails, so
        /// this test reddens on the generated code as well as on the diagnostics.</para>
        /// </summary>
        [Fact]
        public void AnInternalReferencedModelTypeDegradesRatherThanEmittingACastTheConsumerCannotCompile()
        {
            const string key = "views/internal-type.heddle";
            var template = "@model(){{" + InternalType + "}}@\\\n@(Title)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var hed7030 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7030"));
            Assert.Contains("InternalModel", hed7030.GetMessage());
            Assert.Contains(key, hed7030.Location.GetLineSpan().Path);
            DifferentialHarness.ExpectDegrade(gen, key);

            Assert.Equal("hidden\n", Dynamic(template, typeof(InternalModel), new InternalModel()));
        }

        /// <summary>
        /// The near miss the degrade must not swallow. A <c>private</c> member is visible in the full-metadata view
        /// the emitter consults, so only applying the <b>engine's own</b> visibility policy there keeps this an
        /// error — the engine rejects a private getter too, and both tiers must refuse the same template.
        /// </summary>
        [Fact]
        public void APrivateMemberStaysAnError()
        {
            const string key = "views/private-member.heddle";
            var template = "@model(){{" + InternalMember + "}}@\\\n@(Hidden)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            var hed7008 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7008"));
            Assert.Equal(DiagnosticSeverity.Error, hed7008.Severity);
            Assert.Contains("Hidden", hed7008.GetMessage());
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7030");

            var dynamicTemplate = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), new ExType(typeof(InternalMemberModel))));
            Assert.False(dynamicTemplate.CompileResult.Success);
        }

        /// <summary>A member that is genuinely not there, on the same referenced type — the diagnostic whose value
        /// the degrade is there to preserve. Models normally live in referenced assemblies, so a rule that answered
        /// "metadata receiver, therefore only a warning" would have downgraded this one too.</summary>
        [Fact]
        public void AMisspelledMemberOnAReferencedModelStaysAnError()
        {
            const string key = "views/typo-member.heddle";
            var template = "@model(){{" + InternalMember + "}}@\\\n@(Secrett)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            var hed7008 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7008"));
            Assert.Equal(DiagnosticSeverity.Error, hed7008.Severity);
            Assert.Contains("Secrett", hed7008.GetMessage());
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7030");
        }

        /// <summary>The public member on the same type still pre-compiles: the degrade is scoped to what is actually
        /// invisible, not to every model that came from a reference.</summary>
        [Fact]
        public void APublicMemberOnTheSameReferencedModelStillPrecompiles()
        {
            const string key = "views/public-member.heddle";
            var template = "@model(){{" + InternalMember + "}}@\\\n@(Title)\n";
            var (precompiled, dynamic) = DifferentialHarness.Render(key, template,
                typeof(InternalMemberModel), new InternalMemberModel());

            Assert.Equal(dynamic, precompiled);
            Assert.Equal("public\n", dynamic);
        }
    }
}
