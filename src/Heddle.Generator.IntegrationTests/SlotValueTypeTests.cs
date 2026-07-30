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
        private const string OptionType = "Heddle.Generator.IntegrationTests.Fixtures.MenuOption";

        private static string Definition(string slotType, string bodyModel) =>
            "@%\n<frame(out:: " + slotType + ")>{{[@out(this)]}} :: " + bodyModel + "\n%@\n";

        /// <summary>The engine's own verdict, asserted on its message and not only on the id. <c>HED5014</c> carries
        /// two distinct rules — a slot value with no static type at all, and one whose type does not fit — and a test
        /// that greps for the id alone cannot tell which of them it just reproduced, so it goes on passing when the
        /// template it describes has stopped being the one it means.</summary>
        private static void AssertEngineRefuses(string template, System.Type modelType, string message)
        {
            var dynamicTemplate = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), modelType));
            Assert.False(dynamicTemplate.CompileResult.Success);
            var text = dynamicTemplate.CompileResult.ToString();
            Assert.Contains("HED5014", text);
            Assert.Contains(message, text);
        }

        private static void AssertEngineAccepts(string template, System.Type modelType)
        {
            var dynamicTemplate = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), modelType));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
        }

        private static string Mismatch(string valueType, string slotType) =>
            "The slot value type " + valueType + " is not assignable to the declared slot parameter type " +
            slotType + ".";

        private const string NoStaticType = "The slot value must have a static type, but it is 'dynamic' here.";

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
            AssertEngineRefuses(template, typeof(string), Mismatch("System.String", ArticleType));
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
            AssertEngineRefuses(template, typeof(string), Mismatch("System.String", ArticleType));
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
            AssertEngineRefuses(template, typeof(int), Mismatch("System.Int32", "System.Object"));
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
            AssertEngineRefuses(template, typeof(string), Mismatch("System.String", ArticleType));
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
        /// The call forms that hand a <c>:: dynamic</c> definition's body a model with no static type at all. The
        /// engine's model accessor takes its dynamic exit for a member path, and its bare-call twin does the same
        /// with nothing to resolve; only a native expression or a read of the caller's own prop survives with a
        /// static type. Every <c>@out</c> in the body that reads that model is then refused for having no static
        /// type — the other half of <c>HED5014</c>, and not the assignability half. The emitter had been typing both
        /// forms off the caller's own model, which is a type the engine never gives this body, so it precompiled and
        /// rendered templates the engine will not compile at all.
        /// </summary>
        [Theory]
        [InlineData("bare", ArticleType, "@frame()", "this")]
        // An `object` slot is the row that has to be asked before the conversion table rather than through it: C#
        // gives `dynamic` an identity conversion to `object`, so the table would wave this one through while the
        // engine refuses it like every other value with no static type.
        [InlineData("bare-object-slot", "object", "@frame()", "this")]
        [InlineData("member-path", "System.String", "@frame(Title)", "this")]
        // And the same again read one level down: a member path off a dynamic model is dynamic in its turn, so the
        // body's own `@out(Title)` is refused for the same reason its model was.
        [InlineData("out-path", "System.String", "@frame(Title)", "Title")]
        public void ACallFormThatGivesTheBodyADynamicModelDegrades(string name, string slotType, string call,
            string outValue)
        {
            var key = "views/slot-value-dynamic-model-" + name + ".heddle";
            var template = "@model(){{" + ArticleType + "}}@%\n<frame(out:: " + slotType + ")>{{[@out(" + outValue +
                           ")]}} :: dynamic\n%@\n" + call + "{{[q]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(Article), NoStaticType);
        }

        /// <summary>
        /// The cost control for the rule above, and why the dynamic model is not simply a refusal of the call form:
        /// the engine hands this body a <c>dynamic</c> model too, and still type-checks the <c>@out</c> because the
        /// value read is the definition's own prop and not the model. Both tiers precompile and render it.
        /// </summary>
        [Fact]
        public void ADynamicModelStillPrecompilesWhenTheOutValueNeverReadsIt()
        {
            const string key = "views/slot-value-dynamic-model-prop.heddle";
            var template = "@model(){{" + ArticleType + "}}@%\n" +
                           "<frame(label: string, out:: System.String)>{{[@out(label)]}} :: dynamic\n%@\n" +
                           "@frame(label: \"L\"){{[q]}}\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[[q]]\n", dyn);
        }

        /// <summary>
        /// A read of the <em>caller's</em> prop, which the engine resolves before it ever asks whether the callee is
        /// <c>:: dynamic</c> — so the body keeps the prop's static type and the <c>@out</c> is checked against it.
        /// The mismatching row is what proves the type is the prop's and not something the emitter guessed: an
        /// <c>int</c> prop into a <c>string</c> slot is named by the engine in full.
        /// </summary>
        [Theory]
        [InlineData("fits", "string", "\"L\"", true)]
        [InlineData("misfits", "int", "3", false)]
        public void ACallerPropReadKeepsItsStaticTypeThroughADynamicDefinition(string name, string propType,
            string argument, bool precompiles)
        {
            var key = "views/slot-value-dynamic-model-caller-prop-" + name + ".heddle";
            var template = "@model(){{" + ArticleType + "}}@%\n" +
                           "<frame(out:: System.String)>{{[@out(this)]}} :: dynamic\n" +
                           "<outer(p: " + propType + ")>{{@frame(p){{[q]}}}} :: " + ArticleType + "\n%@\n" +
                           "@outer(this, p: " + argument + ")\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            if (precompiles)
            {
                DifferentialHarness.ExpectPrecompiled(gen, key);
                AssertEngineAccepts(template, typeof(Article));
            }
            else
            {
                DifferentialHarness.ExpectDegrade(gen, key);
                AssertEngineRefuses(template, typeof(Article), Mismatch("System.Int32", "System.String"));
            }
        }

        /// <summary>
        /// An <c>@out(this)</c> inside an <c>@list</c> body, where the value is the element the host iterates. The
        /// element type is a real static type — the host resolves the collection's <c>IEnumerable&lt;T&gt;</c> — so
        /// the slot check applies there like anywhere else: a fitting element renders the engine's bytes, and an
        /// element the slot cannot take degrades on the same refusal the engine raises. Refusing the shape wholesale
        /// would have taken the ordinary per-item slot projection off the precompiled tier.
        /// </summary>
        [Fact]
        public void AnOutValueInsideAListBodyIsCheckedAgainstTheElementType()
        {
            const string fitsKey = "views/slot-value-list-element-fits.heddle";
            var fits = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + OptionType + ")>{{[@out(this)]}} :: dynamic\n%@\n" +
                       "@list(Options){{@frame(this){{[@(Label)]}}}}\n";

            var (precompiled, dyn) = DifferentialHarness.Render(fitsKey, fits, typeof(Menu),
                new Menu { Options = new System.Collections.Generic.List<MenuOption> { new MenuOption { Label = "L" } } });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[[L]]\n", dyn);

            const string misfitsKey = "views/slot-value-list-element-misfits.heddle";
            var misfits = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + ArticleType + ")>{{[@out(this)]}} :: dynamic\n%@\n" +
                          "@list(Options){{@frame(this){{[q]}}}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (misfitsKey, misfits) }), misfitsKey);
            AssertEngineRefuses(misfits, typeof(Menu), Mismatch(OptionType, ArticleType));
        }

        /// <summary>
        /// Two call sites into one <c>:: dynamic</c> slot definition, each passing <c>this</c> out of a differently
        /// typed enclosing body — the same call form, two models. The body is emitted once and cached, so the model
        /// it was checked against has to be part of what the cache is keyed on; otherwise the first call site's
        /// verdict stands for the second, which precompiles having been checked against a type it never passes. The
        /// single-call-site file is the other half of the pin: a key that refused every repeat call would satisfy the
        /// degrade on its own.
        /// </summary>
        [Fact]
        public void TheSecondCallSiteIntoADynamicSlotDefinitionIsCheckedOnItsOwnValue()
        {
            const string bothKey = "views/slot-value-two-call-sites.heddle";
            const string oneKey = "views/slot-value-one-call-site.heddle";
            const string preamble = "@model(){{" + ArticleType + "}}@%\n" +
                                    "<frame(out:: System.String)>{{[@out(this)]}} :: dynamic\n" +
                                    "<fits>{{@frame(this){{[X]}}}} :: System.String\n" +
                                    "<misfits>{{@frame(this){{[Y]}}}} :: System.Int32\n%@\n";
            const string bothTemplate = preamble + "@fits(\"s\")@misfits(5)\n";
            const string oneTemplate = preamble + "@fits(\"s\")\n";

            var gen = DifferentialHarness.Generate(new[] { (bothKey, bothTemplate), (oneKey, oneTemplate) });
            DifferentialHarness.ExpectDegrade(gen, bothKey);
            DifferentialHarness.ExpectPrecompiled(gen, oneKey);
            AssertEngineRefuses(bothTemplate, typeof(Article), Mismatch("System.Int32", "System.String"));
        }

        /// <summary>
        /// A literal slot value. Its type is not an estimate the emitter has to be careful about — the parser
        /// decoded it, and the engine types the same literal the same way when it refuses the template — yet it
        /// took the "cannot say" exit reserved for values with no static type, and the template precompiled and
        /// rendered what the engine will not compile.
        /// </summary>
        [Theory]
        [InlineData("int", "5", "System.Int32")]
        [InlineData("string", "\"q\"", "System.String")]
        [InlineData("bool", "true", "System.Boolean")]
        public void ALiteralSlotValueOfAnUnrelatedTypeDegrades(string name, string literal, string clr)
        {
            var key = "views/slot-value-literal-" + name + ".heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + ArticleType + ")>{{[@out(" + literal +
                           ")]}} :: " + MenuType + "\n%@\n@frame(this){{[q]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(Menu), Mismatch(clr, ArticleType));
        }

        /// <summary>
        /// The null literal, which the engine does not treat as an absence of type: it compiles it as a constant of
        /// <c>System.Object</c> and checks that against the slot type like any other value, so <c>@out(null)</c> is
        /// refused for every slot type but <c>object</c> — including a reference type, and including a
        /// <c>Nullable&lt;T&gt;</c>, neither of which C#'s own rules would object to. The emitter read the null
        /// literal as "cannot say" and precompiled all three.
        /// </summary>
        [Theory]
        [InlineData("string", "string")]
        [InlineData("int", "int")]
        [InlineData("nullable", "System.Nullable<System.Int32>")]
        public void ANullSlotValueDegradesBecauseTheEngineTypesItAsObject(string name, string slotType)
        {
            var key = "views/slot-value-null-" + name + ".heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + slotType + ")>{{[@out(null)]}} :: " +
                           MenuType + "\n%@\n@frame(this){{[q]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            AssertEngineRefuses(template, typeof(Menu), "The slot value type System.Object is not assignable");
        }

        /// <summary>The cost control: an <c>object</c> slot takes the null literal on both tiers — the engine's own
        /// conversion table says identity — so typing it is a check and not a refusal of <c>null</c>.</summary>
        [Fact]
        public void ANullSlotValueStillPrecompilesForAnObjectSlot()
        {
            const string key = "views/slot-value-null-object.heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: object)>{{[@out(null)]}} :: " + MenuType +
                           "\n%@\n@frame(this){{[q]}}\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Menu), new Menu());
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[[q]]\n", dyn);
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
