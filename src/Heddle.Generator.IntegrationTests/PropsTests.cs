using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 props (generated-code.md example 5, all-constant call sites): the definition prop layout resolves over
    /// symbols; an all-constant call site shares one frozen <c>object[]</c> prototype installed on the definition-body
    /// scope via <c>BindDefinition</c>; body reads go through <c>PrecompiledRuntime.Prop(in scope, i)</c> resolved
    /// prop-first. Differential-gated against the runtime <c>PropsBinder</c>.
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
            // generated-code.md example 5: props with defaults, an all-constant call, a prop-conditioned @ifnot,
            // an @out() splice. Rendered off a typed root here (dynamic root is exercised elsewhere).
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
            // The prop 'Title' shadows the model member of the same name — the prop wins (prop-first resolution).
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<hdr(Title: string = \"P\")>{{[@(Title)]}} :: " + ArticleType + "%@\n" +
                    "@hdr(this, Title: \"shadowed\")\n";
            AssertParity("views/hdr.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void NumericWideningDefault(Article model)
        {
            // A double prop defaulted with an int literal (1) — the runtime widens the boxed prototype value
            // (Convert.ChangeType 1 -> 1.0d); the emitter bakes (double)(1). Also a decimal, long, and float default.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<num(d: double = 1, m: decimal = 2, l: long = 3, f: float = 4)>{{[@(d)|@(m)|@(l)|@(f)]}} :: " + ArticleType + "%@\n" +
                    "@num(this)@num(this, d: 1.5, m: 9)\n";
            AssertParity("views/num.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void MultiHopPropRead(Article model)
        {
            // A prop typed by the model's type; the body reads a member off the boxed prop (prop.Title) — the
            // multi-hop prop read casts the boxed prop to its slot type and walks the member tier.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<wrap(art: " + ArticleType + " = null)>{{[@(art.Title)/@(art.Summary)]}} :: " + ArticleType + "%@\n" +
                    "@wrap(this)\n";
            AssertParity("views/wrap.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void DynamicPropArgument_MemberPath(Article model)
        {
            // A non-constant argument (a model member path) becomes a dynamic setter evaluated per invocation
            // against the caller view — the runtime PropsBinder's dynamic-slot plan, reproduced.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<hd(text: string = \"d\")>{{[@(text)]}} :: " + ArticleType + "%@\n" +
                    "@hd(this, text: Title)@hd(this, text: Summary)\n";
            AssertParity("views/dyn-arg.heddle", t, typeof(Article), model);
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void DynamicPropArgument_WideningAndObject(Article model)
        {
            // A dynamic arg widened to the prop type (int model member -> double prop) and one boxed to an object prop.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%<mix(n: double = 0, any: object = null)>{{[@(n)|@(any)]}} :: " + ArticleType + "%@\n" +
                    "@mix(this, n: Title.Length, any: Title)\n";
            AssertParity("views/dyn-mix.heddle", t, typeof(Article), model);
        }
    }
}
