using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The slot value's type, which the engine checks against the declared slot type when it compiles a slot
    /// definition's body and refuses the template outright when it does not fit (<c>HED5014</c>). The emitter ran no
    /// such check: it precompiled the same template and either rendered it — the engine having refused to compile it
    /// at all — or threw <c>InvalidCastException</c> from the caller-content cast at render, depending on whether
    /// the caller's body happened to read a member.
    /// </summary>
    public class SlotValueTypeTests
    {
        private const string ArticleType = "Heddle.Generator.IntegrationTests.Fixtures.Article";

        private static string Definition(string slotType, string bodyModel) =>
            "@%\n<frame(out:: " + slotType + ")>{{[@out(this)]}} :: " + bodyModel + "\n%@\n";

        private static void AssertEngineRefuses(string template, System.Type modelType)
        {
            var dynamicTemplate = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), modelType));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("HED5014", dynamicTemplate.CompileResult.ToString());
        }

        /// <summary>The reference mismatch: the definition's model is a <c>string</c> and its slot takes an
        /// <c>Article</c>. The engine names the two types and refuses; the precompiled tier rendered
        /// <c>[[q]]</c>.</summary>
        [Fact]
        public void ASlotValueOfAnUnrelatedTypeDegradesInsteadOfRenderingWhatTheEngineRefuses()
        {
            const string key = "views/slot-value-mismatch.heddle";
            var template = "@model(){{System.String}}" + Definition(ArticleType, "System.String") +
                           "@frame(this){{[q]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(string));
        }

        /// <summary>The same refusal with the caller's content reading a member, which is what turned the divergence
        /// into an <c>InvalidCastException</c> at render rather than a rendered page.</summary>
        [Fact]
        public void ASlotValueOfAnUnrelatedTypeDegradesEvenWhenTheCallerContentReadsAMember()
        {
            const string key = "views/slot-value-mismatch-read.heddle";
            var template = "@model(){{System.String}}" + Definition(ArticleType, "System.String") +
                           "@frame(this){{[@(Title)]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(string));
        }

        /// <summary>A slot value that would have to be boxed. The engine asks its conversion table with boxing
        /// switched off for exactly this check, so <c>int</c> into an <c>object</c> slot is a refusal and not the
        /// widening it looks like.</summary>
        [Fact]
        public void ASlotValueThatWouldHaveToBeBoxedDegrades()
        {
            const string key = "views/slot-value-boxing.heddle";
            var template = "@model(){{System.Int32}}" + Definition("object", "System.Int32") +
                           "@frame(this){{[q]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(int));
        }

        /// <summary>
        /// The same mismatch under a definition that declares <c>:: dynamic</c>. The engine still catches it — it
        /// compiles a definition body per call site, off the type actually passed — while the emitter compiles one
        /// body for all of them and has no type to check against, so the definition is handed back to the tier that
        /// does. This is the shape the defect was reported in.
        /// </summary>
        [Fact]
        public void ASlotDefinitionWithADynamicBodyModelDegrades()
        {
            const string key = "views/slot-value-dynamic-body.heddle";
            var template = "@model(){{System.String}}" + Definition(ArticleType, "dynamic") +
                           "@frame(this){{[q]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(string));
        }

        public static TheoryData<string, string, string, System.Type, object> Assignable() =>
            new TheoryData<string, string, string, System.Type, object>
            {
                // Identity, and the two conversions the engine's table does allow without boxing.
                { "identity", "int", "System.Int32", typeof(int), 7 },
                { "widening", "long", "System.Int32", typeof(int), 7 },
                { "reference", "object", ArticleType, typeof(Article), new Article { Title = "T" } },
                // `dynamic` as a slot type is the object type on both tiers, not a dynamic ExType: the engine
                // accepts a reference value into it, so the emitter must not read it as "unknown, refuse".
                { "dynamic-slot", "dynamic", "System.String", typeof(string), "S" },
            };

        /// <summary>The cost control: every conversion the engine's own table accepts still precompiles. A refusal
        /// that over-reaches here is invisible — the template renders identically through the dynamic tier — which
        /// is exactly why it is worth pinning.</summary>
        [Theory]
        [MemberData(nameof(Assignable))]
        public void AnAssignableSlotValueStillPrecompiles(string name, string slotType, string bodyModel,
            System.Type modelType, object model)
        {
            var key = "views/slot-value-" + name + ".heddle";
            var template = "@model(){{" + bodyModel + "}}" + Definition(slotType, bodyModel) +
                           "@frame(this){{[q]}}\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, modelType, model);
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[[q]]\n", dyn);
        }
    }
}
