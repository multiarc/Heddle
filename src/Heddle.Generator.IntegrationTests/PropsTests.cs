using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
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
    }
}
