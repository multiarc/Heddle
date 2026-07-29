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
        private const string MenuType = "Heddle.Generator.IntegrationTests.Fixtures.Menu";

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
        /// The same mismatch under a definition that declares <c>:: dynamic</c>. The engine compiles a definition
        /// body per call site, off the type actually passed, so it still catches it; the emitter emits one untyped
        /// body for every call site but the value passed at <em>this</em> one has a static type all the same, and
        /// that is the type the engine would compile this body against. This is the shape the defect was reported
        /// in.
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

        /// <summary>
        /// The cost control for the rule above, and the reason it is a per-call-site check rather than a blanket
        /// refusal of <c>:: dynamic</c> slot definitions: a reusable wrapper whose slot value fits is an ordinary
        /// shape, both tiers produce the same bytes for it, and refusing it took it off the precompiled tier with no
        /// diagnostic to say so.
        /// </summary>
        [Fact]
        public void ASlotDefinitionWithADynamicBodyModelStillPrecompilesWhenTheCallerValueFits()
        {
            const string key = "views/slot-value-dynamic-body-assignable.heddle";
            var template = "@model(){{" + ArticleType + "}}" + Definition(ArticleType, "dynamic") +
                           "@frame(this){{[@(Title)]}}\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[[T]]\n", dyn);
        }

        /// <summary>
        /// The half of the <c>:: dynamic</c> rule that must stay a refusal. Called from inside a <c>@list</c> body,
        /// the value handed to the definition is the list element, whose type the emitter deliberately does not
        /// guess — so there is no type to check the body's <c>@out</c> against and nothing but the caller-content
        /// cast to fail on at render, which is not where the engine reports it.
        /// </summary>
        [Fact]
        public void ASlotDefinitionWithADynamicBodyModelDegradesWhenTheCallerValueCannotBeTyped()
        {
            const string key = "views/slot-value-dynamic-body-untypeable.heddle";
            var template = "@model(){{" + MenuType + "}}" +
                           "@%\n<frame(out:: " + ArticleType + ")>{{[@out(this)]}} :: dynamic\n%@\n" +
                           "@list(Options){{@frame(this)}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
        }

        /// <summary>
        /// Two call sites into one <c>:: dynamic</c> slot definition, the first passing a value that fits and the
        /// second one that does not. The body is emitted once and cached, so the check has to be part of what the
        /// cache is keyed on — otherwise the first call site's verdict stands for the second, which then precompiles
        /// having been checked against a type it never passes. The engine refuses the template at the second call.
        /// </summary>
        [Fact]
        public void TheSecondCallSiteIntoADynamicSlotDefinitionIsCheckedOnItsOwnValue()
        {
            const string key = "views/slot-value-dynamic-body-two-call-sites.heddle";
            var template = "@model(){{" + ArticleType + "}}" +
                           "@%\n<frame(out:: " + ArticleType + ")>{{[@out(this)]}} :: dynamic\n%@\n" +
                           "@frame(this){{[X]}}@frame(Title){{[Y]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(Article));
        }

        /// <summary>
        /// A literal slot value. Its type is not an estimate the emitter has to be careful about — the parser
        /// decoded it, and the engine types the same literal the same way when it refuses the template — yet it
        /// took the "cannot say" exit reserved for values with no static type, and the template precompiled and
        /// rendered what the engine will not compile.
        /// </summary>
        [Theory]
        [InlineData("int", "5")]
        [InlineData("string", "\"q\"")]
        [InlineData("bool", "true")]
        public void ALiteralSlotValueOfAnUnrelatedTypeDegrades(string name, string literal)
        {
            var key = "views/slot-value-literal-" + name + ".heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + ArticleType + ")>{{[@out(" + literal +
                           ")]}} :: " + MenuType + "\n%@\n@frame(this){{[q]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(Menu));
        }

        /// <summary>The cost control: a literal the slot type does accept — identically on both tiers, boxing
        /// excluded — still precompiles, so the new typing is a check and not a refusal of literals.</summary>
        [Theory]
        [InlineData("int", "int", "5", "5")]
        [InlineData("widened", "long", "5", "5")]
        [InlineData("string", "string", "\"q\"", "q")]
        public void AnAssignableLiteralSlotValueStillPrecompiles(string name, string slotType, string literal,
            string expected)
        {
            var key = "views/slot-value-literal-ok-" + name + ".heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + slotType + ")>{{[@out(" + literal +
                           ")]}} :: " + MenuType + "\n%@\n@frame(this){{[@(this)]}}\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Menu), new Menu());
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[[" + expected + "]]\n", dyn);
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
