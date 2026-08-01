using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Tests props with all-constant call sites: prototype-based resolution, prop-first body reads, and differential
    /// verification against the runtime <c>PropsBinder</c>.
    /// </summary>
    public class PropsTests
    {
        private const string ArticleType = "Heddle.Generator.IntegrationTests.Fixtures.Article";

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Articles()
        {
            yield return new object[] { new Article { Title = "Hi", Summary = "A short note." } };
            yield return new object[] { new Article { Title = null, Summary = null } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void CardExample(Article model)
        {
            // Props with defaults, constant call, @ifnot condition, @out() splice.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%\n" +
                    "  <card(style: string = \"plain\", compact: bool = false)>\n" +
                    "  {{<article class=\"card @(style)\"><h2>@(Title)</h2>@ifnot(compact){{ <p>@(Summary)</p> }}@out()</article>}} :: " + ArticleType + "\n" +
                    "%@\n" +
                    "@card(this, style: \"wide\", compact: true){{<a>more</a>}}\n";
            AssertParity("views/card.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void PropDefaultsWhenUnbound(Article model)
        {
            // No arguments passed → the prototype is entirely defaults.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<tag(label: string = \"note\", loud: bool = false)>{{[@(label)/@(loud)]}} :: " + ArticleType + "%@\n" +
                    "@tag(this)\n";
            AssertParity("views/tag.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void PropShadowsModelMember(Article model)
        {
            // Prop shadows model member; prop-first resolution wins.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<hdr(Title: string = \"P\")>{{[@(Title)]}} :: " + ArticleType + "%@\n" +
                    "@hdr(this, Title: \"shadowed\")\n";
            AssertParity("views/hdr.heddle", t, typeof(Article), model);
        }

        /// <summary>
        /// The prop layout survives into a nested <c>@list</c> body. The engine saves and restores it around
        /// <b>definition</b> bodies only, so inside a definition an <c>@list</c> body still resolves its first path
        /// segment as a prop before it ever looks at the element — and the emitter, which built the item body with
        /// no layout at all, silently read the element's member of that name instead. Both tiers precompiled and
        /// rendered different text.
        /// <para>The element row is the other half: a name the layout does <b>not</b> carry still reads off the
        /// element, so this is prop-first resolution and not the layout swallowing the body.</para>
        /// </summary>
        [Theory]
        [InlineData("shadowed", "Name", "[PROP]\n")]
        [InlineData("element", "Description", "[D]\n")]
        public void APropSurvivesIntoANestedListBody(string name, string read, string expected)
        {
            const string catalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";
            var t = "@model(){{" + catalogType + "}}@%\n" +
                    "<host(Name: string)>{{@list(Products){{[@(" + read + ")]}}}} :: " + catalogType + "\n%@\n" +
                    "@host(this, Name: \"PROP\")\n";
            var model = new Catalog
            {
                Products = new List<Product> { new Product { Name = "ELEMENT", Description = "D" } }
            };

            var (precompiled, dyn) = DifferentialHarness.Render("views/list-prop-" + name + ".heddle", t,
                typeof(Catalog), model);
            Assert.Equal(dyn, precompiled);
            Assert.Equal(expected, dyn);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void NumericWideningDefault(Article model)
        {
            // Prototype defaults widen: int → double, etc.; emitter bakes conversions.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<num(d: double = 1, m: decimal = 2, l: long = 3, f: float = 4)>{{[@(d)|@(m)|@(l)|@(f)]}} :: " + ArticleType + "%@\n" +
                    "@num(this)@num(this, d: 1.5, m: 9)\n";
            AssertParity("views/num.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void MultiHopPropRead(Article model)
        {
            // Prop typed as model type; body reads member off boxed prop (multi-hop cast and walk).
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<wrap(art: " + ArticleType + " = null)>{{[@(art.Title)/@(art.Summary)]}} :: " + ArticleType + "%@\n" +
                    "@wrap(this)\n";
            AssertParity("views/wrap.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void DynamicPropArgument_MemberPath(Article model)
        {
            // Non-constant argument (member path) becomes dynamic setter per invocation.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<hd(text: string = \"d\")>{{[@(text)]}} :: " + ArticleType + "%@\n" +
                    "@hd(this, text: Title)@hd(this, text: Summary)\n";
            AssertParity("views/dyn-arg.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void DynamicPropArgument_WideningAndObject(Article model)
        {
            // Dynamic args widened (int → double) and boxed (→ object).
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<mix(n: double = 0, any: object = null)>{{[@(n)|@(any)]}} :: " + ArticleType + "%@\n" +
                    "@mix(this, n: Title.Length, any: Title)\n";
            AssertParity("views/dyn-mix.heddle", t, typeof(Article), model);
        }

        private const string GridType = "Heddle.Generator.IntegrationTests.Fixtures.GridModel";

        /// <summary>The shadowing fixture: a string member and an int member, so a prop can shadow either with the
        /// other's type and the two tiers cannot agree by accident.</summary>
        private static GridModel Grid() => new GridModel { Name = "model", Cols = 7 };

        /// <summary>
        /// A prop read inside a <b>native expression</b> — arithmetic, comparison, concatenation, a function
        /// argument. Prop-first resolution reached the plain member-path reader long ago, but the expression writer
        /// was handed the model and no layout at all, so every prop name in an expression was emitted as a member
        /// read off the model. A name the layout carried and the model did not broke the consumer's build with a
        /// property-not-found error; a name they both carried compiled, rendered, and quietly produced the model's
        /// value where the engine produces the prop's.
        /// <para>The last two rows are the other half of the rule: a name the layout does <b>not</b> carry still
        /// reads off the model, so this is prop-first resolution and not the layout swallowing every name.</para>
        /// </summary>
        [Theory]
        [InlineData("own-int", "n: int = 5", "n + 1", "[6]")]
        [InlineData("own-string", "s: string = \"p\"", "s + \"!\"", "[p!]")]
        [InlineData("own-compare", "n: int = 5", "n > 3", "[True]")]
        [InlineData("own-ternary", "n: int = 5", "n > 3 ? n + 1 : n - 1", "[6]")]
        [InlineData("own-function", "s: string = \"p\"", "upper(s)", "[P]")]
        [InlineData("shadows-int-with-int", "Cols: int = 100", "Cols + 1", "[101]")]
        [InlineData("shadows-int-with-string", "Cols: string = \"P\"", "Cols + \"!\"", "[P!]")]
        [InlineData("shadows-string-with-int", "Name: int = 5", "Name + 1", "[6]")]
        [InlineData("shadows-string-with-string", "Name: string = \"P\"", "Name + \"!\"", "[P!]")]
        [InlineData("unshadowed-member-beside-prop", "n: int = 5", "n + Cols", "[12]")]
        [InlineData("unshadowed-member-alone", "n: int = 5", "Cols + 1", "[8]")]
        public void ANativeExpressionReadsAPropBeforeTheModel(string name, string decl, string expr, string expected)
        {
            var t = "@model(){{" + GridType + "}}@%\n" +
                    "<host(" + decl + ")>{{[@(" + expr + ")]}} :: " + GridType + "\n%@\n" +
                    "@host(this)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/native-prop-" + name + ".heddle", t,
                typeof(GridModel), Grid());
            Assert.Equal(dyn, precompiled);
            Assert.Equal(expected + "\n", dyn);
        }

        /// <summary>The same read one level down, where the prop travels into an <c>@list</c> body: the collection
        /// is the prop's own characters, not the model member's.</summary>
        [Fact]
        public void APropInAListDataExpressionIsTheProp()
        {
            const string t = "@model(){{" + GridType + "}}@%\n" +
                             "<host(Name: string)>{{@list(Name + \"\"){{[@()]}}}} :: " + GridType + "\n%@\n" +
                             "@host(this, Name: \"ab\")\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/native-prop-list.heddle", t,
                typeof(GridModel), Grid());
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[a][b]\n", dyn);
        }

        /// <summary>
        /// The type a computed call-site value is <b>judged</b> by, when the value reads a shadowed name. The
        /// <c>:: dynamic</c> definition is typed by whatever its one caller hands it, so typing the operand off the
        /// model gave the body <c>int</c> and made it report a member only the <c>string</c> it really receives has
        /// — an error at build time over a template the engine compiles and renders.
        /// <para>The second row is the near neighbour that settles that this is the prop's type deciding and not a
        /// blanket refusal: the same shape with an <c>int</c> prop is a template the <b>engine</b> refuses
        /// (<c>Length</c> is not on <c>Int32</c>), and there the generated tier has to stop too.</para>
        /// </summary>
        [Fact]
        public void AShadowingPropsTypeDecidesADynamicDefinitionsModel()
        {
            const string key = "views/native-prop-dynamic-body.heddle";
            const string t = "@model(){{" + GridType + "}}@%\n" +
                             "<frame>{{[@(Length)]}} :: dynamic\n" +
                             "<host(Cols: string)>{{@frame(Cols + 1)}} :: " + GridType + "\n%@\n" +
                             "@host(this, Cols: \"P\")\n";
            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(GridModel), Grid());
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[2]\n", dyn);
        }

        [Fact]
        public void AnIntPropThroughTheSameShapeIsRefusedByBothTiers()
        {
            const string key = "views/native-prop-dynamic-body-int.heddle";
            const string t = "@model(){{" + GridType + "}}@%\n" +
                             "<frame>{{[@(Length)]}} :: dynamic\n" +
                             "<host(Cols: int)>{{@frame(Cols + 1)}} :: " + GridType + "\n%@\n" +
                             "@host(this, Cols: 5)\n";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectDegrade(gen, key);

            var dynamicTemplate = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(GridModel)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("Length", dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// The same question at a <b>slot</b>, where the declared parameter type is what the value is checked
        /// against. A string prop makes <c>Cols + 1</c> a concatenation the string slot accepts; typing the operand
        /// off the <c>int</c> member made it arithmetic, and the emitter refused a slot the engine fills.
        /// </summary>
        [Fact]
        public void AShadowingPropsTypeDecidesWhatASlotAccepts()
        {
            const string t = "@model(){{" + GridType + "}}@%\n" +
                             "<frame(out:: string, Cols: string)>{{[@out(Cols + 1)]}} :: " + GridType + "\n%@\n" +
                             "@frame(this, Cols: \"P\"){{[c]}}\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/native-prop-slot.heddle", t,
                typeof(GridModel), Grid());
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[[c]]\n", dyn);
        }

        [Fact]
        public void AnIntPropIntoAStringSlotIsRefusedByBothTiers()
        {
            const string key = "views/native-prop-slot-int.heddle";
            const string t = "@model(){{" + GridType + "}}@%\n" +
                             "<frame(out:: string, Cols: int)>{{[@out(Cols + 1)]}} :: " + GridType + "\n%@\n" +
                             "@frame(this, Cols: 5){{[c]}}\n";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectDegrade(gen, key);

            var dynamicTemplate = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(GridModel)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("not assignable to the declared slot parameter type",
                dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// The same read inside the content a call site hands a definition. The engine compiles caller content
        /// <b>before</b> it swaps in the callee's layout — the save/restore is around the definition body alone —
        /// so the calling definition's props are still what a first path segment names there. The emitter built
        /// that body from the callee's model with no layout at all, and every row here diverged in its own way: a
        /// shadowing prop read the model's member and rendered different text with no diagnostic, a prop the model
        /// has no member of became an <c>HED7008</c> build error over a template the engine renders, and an
        /// <c>@list</c> whose data is a prop enumerated the model member's characters instead.
        /// <para>The last row is the near neighbour: a name the layout does not carry still reads off the callee's
        /// model, so the caller's layout does not swallow the body.</para>
        /// </summary>
        [Theory]
        [InlineData("shadow-path", "Cols: string = \"PP\"", "[@(Cols)]", "([PP])")]
        [InlineData("shadow-native", "Cols: string = \"PP\"", "[@(Cols + \"!\")]", "([PP!])")]
        [InlineData("no-such-member", "label: string = \"PP\"", "[@(label)]", "([PP])")]
        [InlineData("list-data", "Name: string = \"PP\"", "@list(Name){{[@()]}}", "([P][P])")]
        [InlineData("unshadowed-callee-member", "n: int = 5", "[@(Name)]", "([model])")]
        public void CallerContentKeepsTheCallingDefinitionsProps(string name, string decl, string content,
            string expected)
        {
            var t = "@model(){{" + GridType + "}}@%\n" +
                    "<inner>{{(@out())}} :: " + GridType + "\n" +
                    "<outer(" + decl + ")>{{@inner(this){{" + content + "}}}} :: " + GridType + "\n%@\n" +
                    "@outer(this)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/caller-prop-" + name + ".heddle", t,
                typeof(GridModel), Grid());
            Assert.Equal(dyn, precompiled);
            Assert.Equal(expected + "\n", dyn);
        }

        /// <summary>The same rule where the callee declares a slot: the caller content is typed by the slot type,
        /// but the layout it reads is still the caller's.</summary>
        [Fact]
        public void SlotModeCallerContentKeepsThemToo()
        {
            const string t = "@model(){{" + GridType + "}}@%\n" +
                             "<inner(out:: " + GridType + ")>{{(@out(this))}} :: " + GridType + "\n" +
                             "<outer(Cols: string = \"PP\")>{{@inner(this){{[@(Cols)]}}}} :: " + GridType + "\n%@\n" +
                             "@outer(this)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/caller-prop-slot.heddle", t,
                typeof(GridModel), Grid());
            Assert.Equal(dyn, precompiled);
            Assert.Equal("([PP])\n", dyn);
        }

        /// <summary>
        /// Slot <b>mode</b> travels into caller content for the same reason the layout does: the engine installs the
        /// callee's slot type after this text is compiled, so an <c>@out</c> here still belongs to the enclosing
        /// slot definition. Read as "outside a slot" the two tiers disagreed twice over — a valued <c>@out</c>
        /// dropped a template the engine renders, and a bare one precompiled and rendered a template the engine
        /// refuses with <c>HED5013</c>.
        /// </summary>
        [Fact]
        public void CallerContentInsideASlotBodyIsStillInsideTheSlot()
        {
            const string prefix = "@model(){{" + GridType + "}}@%\n" +
                                  "<plain>{{(@out())}} :: " + GridType + "\n";
            const string valued = prefix +
                                  "<mid(out:: string)>{{[@out(\"S\")]<@plain(this){{@out(\"Q\")}}>}} :: " + GridType +
                                  "\n%@\n@mid(this){{|@()|}}\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/caller-slot-valued.heddle", valued,
                typeof(GridModel), Grid());
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[|S|]<(|Q|)>\n", dyn);

            const string bareKey = "views/caller-slot-bare.heddle";
            const string bare = prefix +
                                "<mid(out:: string)>{{[@out(\"S\")]<@plain(this){{@out()}}>}} :: " + GridType +
                                "\n%@\n@mid(this){{|@()|}}\n";
            var gen = DifferentialHarness.Generate(new[] { (bareKey, bare) });
            DifferentialHarness.ExpectDegrade(gen, bareKey);

            var dynamicTemplate = new HeddleTemplate(bare, new CompileContext(new TemplateOptions(), typeof(GridModel)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("HED5013", dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// The type a prop <b>argument</b> is checked against. The value is resolved prop-first — the writer three
        /// lines below the check has been doing so for a while — so the check has to be too, or the two disagree
        /// about which value the argument even is: the emitter emitted the caller's prop and then approved it
        /// against the shadowed member's type, boxing a <c>string</c> into an <c>int</c>-declared slot that the
        /// engine refuses outright with <c>HED5003</c>.
        /// <para>The two accepted rows are the near neighbours. A prop whose type <b>fits</b> the slot — including
        /// one the model has no member of at all, which used to degrade — still precompiles and renders; and a
        /// plain model member in the same position is unaffected.</para>
        /// </summary>
        [Theory]
        [InlineData("shadow-string-into-int", "q: int = 0", "Cols: string = \"PP\"", "Cols")]
        [InlineData("shadow-int-into-string", "q: string = \"\"", "Name: int = 5", "Name")]
        public void APropArgumentIsCheckedAgainstThePropItReads(string name, string calleeDecl, string callerDecl,
            string argument)
        {
            var key = "views/prop-arg-" + name + ".heddle";
            var t = "@model(){{" + GridType + "}}@%\n" +
                    "<inner(" + calleeDecl + ")>{{[@(q)]}} :: " + GridType + "\n" +
                    "<outer(" + callerDecl + ")>{{@inner(this, q: " + argument + ")}} :: " + GridType + "\n%@\n" +
                    "@outer(this)\n";

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectDegrade(gen, key);

            var dynamicTemplate = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(GridModel)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("HED5003", dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("fitting-prop", "p: string = \"PP\"", "p", "[PP]")]
        [InlineData("model-member", "p: string = \"PP\"", "Name", "[model]")]
        public void APropArgumentThatFitsStillPrecompiles(string name, string callerDecl, string argument,
            string expected)
        {
            var t = "@model(){{" + GridType + "}}@%\n" +
                    "<inner(q: string = \"\")>{{[@(q)]}} :: " + GridType + "\n" +
                    "<outer(" + callerDecl + ")>{{@inner(this, q: " + argument + ")}} :: " + GridType + "\n%@\n" +
                    "@outer(this)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/prop-arg-ok-" + name + ".heddle", t,
                typeof(GridModel), Grid());
            Assert.Equal(dyn, precompiled);
            Assert.Equal(expected + "\n", dyn);
        }

        /// <summary>
        /// A native expression over a prop inside an <c>@list</c> body. The body is emitted on the dynamic tier, and
        /// the emitter refused every native expression there before it looked at anything — but the engine's own
        /// compiler tries the active layout <b>before</b> it asks whether the scope has a static type, so an
        /// expression rooted at a prop needs no model and the whole template was dropped for nothing.
        /// <para>The rows either side are what make this a rule rather than a blanket admission: a plain prop path
        /// and a function over a prop always worked, and an expression that needs no model at all is fine. The last
        /// row reads the <b>element's</b> own member, which is a different model from the prop layout — and it is
        /// now the element type the body is typed by, so it precompiles and renders the engine's bytes rather than
        /// costing the whole template its tier.</para>
        /// </summary>
        [Fact]
        public void APropRootedExpressionNeedsNoModelInsideAListBody()
        {
            const string catalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";
            var catalog = new Catalog { Tags = new[] { "x", "y" } };

            foreach (var (name, decl, expr, expected) in new[]
                     {
                         ("expression", "n: int = 5", "n + 1", "[6][6]\n"),
                         ("plain-path", "n: int = 5", "n", "[5][5]\n"),
                         ("function", "n: string = \"q\"", "upper(n)", "[Q][Q]\n"),
                         ("no-model-at-all", "n: int = 5", "1 + 1", "[2][2]\n"),
                     })
            {
                var t = "@model(){{" + catalogType + "}}@%\n" +
                        "<host(" + decl + ")>{{@list(Tags){{[@(" + expr + ")]}}}} :: " + catalogType + "\n%@\n" +
                        "@host(this)\n";
                var (precompiled, dyn) = DifferentialHarness.Render("views/list-body-prop-" + name + ".heddle", t,
                    typeof(Catalog), catalog);
                Assert.Equal(dyn, precompiled);
                Assert.Equal(expected, dyn);
            }

            const string elementKey = "views/list-body-element-expression.heddle";
            const string element = "@model(){{" + catalogType + "}}@%\n" +
                                   "<host(n: int = 5)>{{@list(Products){{[@(Name + \"!\")]}}}} :: " + catalogType +
                                   "\n%@\n@host(this)\n";
            var withProducts = new Catalog
            {
                Tags = new[] { "x" },
                Products = new System.Collections.Generic.List<Product>
                {
                    new Product { Name = "a" }, new Product { Name = "b" }
                }
            };
            var (elementPrecompiled, elementDynamic) =
                DifferentialHarness.Render(elementKey, element, typeof(Catalog), withProducts);
            Assert.Equal("[a!][b!]\n", elementDynamic);
            Assert.Equal(elementDynamic, elementPrecompiled);
        }
    }
}
