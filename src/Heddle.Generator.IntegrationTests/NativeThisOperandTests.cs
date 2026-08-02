using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// <c>this</c> as an <b>operand</b> inside a native expression — an argument, a hop root, either side of an
    /// operator — as distinct from <c>this</c> standing alone as a whole call parameter.
    /// <para>The two are different constructs with different rules, and only one of them worked. Alone, it is the
    /// model passthrough: the engine compiles it to the empty member path, which needs no static type and works on
    /// a dynamic scope, and the emitter passed the scope's model straight through. Inside an expression it is a
    /// typed operand — the engine converts the model parameter to the scope's own type and refuses the whole
    /// expression where the scope has none — and the expression writer had no arm for it at all, so every template
    /// carrying one fell to the dynamic tier while the engine rendered it.</para>
    /// <para>What it denotes is the enclosing body's own model, whatever built that body, so the rows below walk
    /// the body contexts rather than the expression shapes: the template's declared model, an <c>@list</c>
    /// element, a nested <c>@list</c> element, a definition body typed by its caller, and a branch body.</para>
    /// </summary>
    public class NativeThisOperandTests
    {
        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";

        private static Catalog Sample() => new Catalog
        {
            Title = "T",
            Tags = new[] { "xy" },
            Products = new List<Product>
            {
                new Product { Name = "a", Manufacturer = new Manufacturer { Name = "m" } },
                new Product { Name = "b", Manufacturer = new Manufacturer { Name = "n" } }
            }
        };

        private static string Template(string body) =>
            "@model(){{" + CatalogType + "}}@\\\n" + body + "\n";

        /// <summary>
        /// Every body context a <c>this</c> operand can stand in, each rendered on both tiers and byte-compared —
        /// "it precompiled" is not the claim. The rows that fix the meaning are the nested <c>@list</c>, whose
        /// <c>this</c> is the <i>inner</i> element, and the definition body, whose <c>this</c> is the model its
        /// <b>caller</b> handed it rather than the template's.
        /// </summary>
        [Theory]
        [InlineData("top-level-argument", "[@upper(this.Title)]", "[T]\n")]
        [InlineData("top-level-hop", "[@(this.Title)]", "[T]\n")]
        [InlineData("top-level-operand", "[@(this.Title + \"!\")]", "[T!]\n")]
        [InlineData("list-element-argument", "@list(Tags){{[@upper(this)]}}", "[XY]\n")]
        [InlineData("list-element-operand", "@list(Tags){{[@(this + \"!\")]}}", "[xy!]\n")]
        [InlineData("list-element-comparison", "@list(Tags){{[@(this == \"xy\" ? 1 : 2)]}}", "[1]\n")]
        [InlineData("list-element-hop", "@list(Products){{[@upper(this.Name)]}}", "[A][B]\n")]
        [InlineData("nested-list-element", "@list(Products){{@list(Name){{[@(this)]}}}}", "[a][b]\n")]
        [InlineData("definition-body", "@%<box>{{[@upper(this.Title)]}}%@\n@box(this)", "[T]\n")]
        [InlineData("branch-body", "@if(Title){{[@upper(this.Title)]}}", "[T]\n")]
        public void AThisOperandPrecompilesAndRendersTheEnginesBytes(string name, string body, string expected)
        {
            var key = "this-operand/" + name + ".heddle";
            var (precompiled, dynamic) = DifferentialHarness.Render(key, Template(body), typeof(Catalog), Sample());

            Assert.Equal(expected, dynamic);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>
        /// The body whose model is DECLARED dynamic, which is where the operand rule and the passthrough rule
        /// visibly part. The directive pins the engine's scope dynamic on every compile — it overrides a
        /// caller-supplied model type — so the engine refuses the expression with HED1004 on every input, and the
        /// generator forwards that same id and sentence as a build error while the template still degrades.
        /// A template that merely LACKS a model declaration keeps degrading silently instead: the engine types it
        /// from whatever CompileContext the host supplies, so no refusal is proven there.
        /// </summary>
        [Theory]
        [InlineData("argument", "[@upper(this)]")]
        [InlineData("hop", "[@(this.Title)]")]
        [InlineData("operand", "[@(this + \"!\")]")]
        [InlineData("path-operand", "[@(Title + \"!\")]")]
        public void AThisOperandUnderADeclaredDynamicModelIsRefusedByBothTiers(string name, string body)
        {
            var key = "this-operand/dynamic-" + name + ".heddle";
            var content = "@model(){{dynamic}}@\\\n" + body + "\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(new TemplateOptions(), ExType.Dynamic));
            Assert.False(compiled.CompileResult.Success);
            var engineError = compiled.CompileResult.ErrorList
                .First(e => e.DiagnosticId == HeddleDiagnosticIds.TypedModelRequired);

            var forwarded = Assert.Single(gen.Diagnostics,
                d => d.Id == HeddleDiagnosticIds.TypedModelRequired);
            Assert.Equal(DiagnosticSeverity.Error, forwarded.Severity);
            Assert.Equal(engineError.Error, forwarded.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>
        /// The near-neighbour that keeps the passthrough rule honest: <c>this</c> alone is still the model
        /// passthrough, still needs no static type, and still renders under a dynamic model. Narrowing the operand
        /// arm must not reach it.
        /// </summary>
        [Fact]
        public void ThisAloneIsStillThePassthroughAndStillWorksWithoutAStaticModel()
        {
            const string key = "this-operand/passthrough-dynamic.heddle";
            var content = "@model(){{dynamic}}@\\\n@list(Tags){{[@()]}}\n";
            var (precompiled, dynamic) = DifferentialHarness.Render(key, content, null, Sample());

            Assert.Equal("[xy]\n", dynamic);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>
        /// A prop never shadows <c>this</c>, and a <c>this.</c>-rooted hop reads past one. The engine's compiler
        /// consults the active prop layout only for a path with no target, so <c>Title</c> inside this body is the
        /// prop and <c>this.Title</c> is the model member it shadows — one template, both readings, so the
        /// distinction is asserted rather than assumed.
        /// </summary>
        [Fact]
        public void AThisRootedHopReadsPastAPropThatShadowsTheSameName()
        {
            const string key = "this-operand/prop-shadow.heddle";
            var content = "@model(){{" + CatalogType + "}}@%\n" +
                          "<box(Title: string = \"prop\")>{{[@(Title)][@(this.Title)]}} :: " + CatalogType + "\n%@\n" +
                          "@box(this)\n";
            var (precompiled, dynamic) = DifferentialHarness.Render(key, content, typeof(Catalog), Sample());

            Assert.Equal("[prop][T]\n", dynamic);
            Assert.Equal(dynamic, precompiled);
        }
    }
}
