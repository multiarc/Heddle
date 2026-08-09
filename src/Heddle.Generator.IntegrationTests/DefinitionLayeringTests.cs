using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Definition layering: a definition re-declared over itself (<c>&lt;foo:foo&gt;</c>), one or more layers deep,
    /// with props, a narrowed model, caller content and calls that reach the layer below. The build used to refuse
    /// every one of these on the belief that it resolved definitions flatly and could only reach the most-derived
    /// layer. It never resolved them itself: it asks the same <c>ParseContext</c> the engine asks, and the parser
    /// has already put the layer each call site sees in it — so the layer arrives materialized and these are
    /// ordinary definition calls. Each case is byte-pinned against the engine with the tier declared.
    /// </summary>
    public class DefinitionLayeringTests
    {
        private const string ManufacturerType = "Heddle.Generator.IntegrationTests.Fixtures.Manufacturer";
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Products()
        {
            yield return new object[] { new Product { Name = "Widget", Manufacturer = new Manufacturer { Name = "Acme" } } };
            yield return new object[] { new Product { Name = "X", Manufacturer = null } };
            yield return new object[] { null };
        }

        /// <summary>The plain full override: the outer call binds the most-derived layer and nothing calls down.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void AFullOverrideBindsTheMostDerivedLayer(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<label>{{base:@(Name)}} :: " + ManufacturerType + "\n" +
                    "<label:label>{{over:@(Name)}} :: " + ManufacturerType + "%@\n" +
                    "[@label(Manufacturer)]\n";
            AssertParity("views/layer-plain.heddle", t, typeof(Product), model);
        }

        /// <summary>The shape the old refusal was written for. A call to its own name inside a full override's body
        /// does not recurse: the parser froze the layer below at the override's declaration, so it reaches the base.
        /// </summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void ACallInsideAnOverrideBodyReachesTheLayerBelowIt(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<label>{{base:@(Name)}} :: " + ManufacturerType + "\n" +
                    "<label:label>{{over[@label()]}} :: " + ManufacturerType + "%@\n" +
                    "[@label(Manufacturer)]\n";
            AssertParity("views/layer-self-call.heddle", t, typeof(Product), model);
        }

        /// <summary>Three layers, each calling down one step: the chain is per-layer, not a flat jump to the base.
        /// </summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void EachLayerOfAMultiLevelOverrideReachesOnlyTheLayerBelowIt(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<label>{{L1:@(Name)}} :: " + ManufacturerType + "\n" +
                    "<label:label>{{L2[@label()]}} :: " + ManufacturerType + "\n" +
                    "<label:label>{{L3[@label()]}} :: " + ManufacturerType + "%@\n" +
                    "[@label(Manufacturer)]\n";
            AssertParity("views/layer-multi.heddle", t, typeof(Product), model);
        }

        /// <summary>Props declared on both layers: the override's body reads the base layer's prop as well as its
        /// own, and the call below it gets the base layer's own defaults rather than the arguments this call
        /// carried.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void AnOverrideCarriesTheBaseLayersPropsAlongsideItsOwn(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<card(style: string = \"plain\")>{{[base @(style) @(Name)]}} :: " + ManufacturerType + "\n" +
                    "<card(tone: string = \"hot\"):card>{{[over @(style)/@(tone) @card()]}} :: " +
                    ManufacturerType + "%@\n" +
                    "[@card(Manufacturer)][@card(Manufacturer, style: \"wide\", tone: \"cold\")]\n";
            AssertParity("views/layer-props.heddle", t, typeof(Product), model);
        }

        /// <summary>A layer that narrows <c>:: T</c> over a base declaring <c>object</c>: the two layers compile
        /// against two different models, and each body gets its own.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void ALayerNarrowingItsModelTypeOverAnObjectBaseKeepsBothTypings(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<label>{{base}} :: object\n" +
                    "<label:label>{{over:@(Name)[@label()]}} :: " + ManufacturerType + "%@\n" +
                    "[@label(Manufacturer)]\n";
            AssertParity("views/layer-narrow.heddle", t, typeof(Product), model);
        }

        /// <summary>Caller content on both the outer call and the call the override makes into its base: each
        /// <c>@out()</c> splices its own site's content.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void EachLayerSplicesItsOwnCallersContent(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<box>{{base<@out()>}} :: " + ManufacturerType + "\n" +
                    "<box:box>{{over<@out()>[@box(){{INNER=@(Name)}}]}} :: " + ManufacturerType + "%@\n" +
                    "[@box(Manufacturer){{OUTER=@(Name)}}]\n";
            AssertParity("views/layer-caller-content.heddle", t, typeof(Product), model);
        }

        /// <summary>A full override declared inside another definition's body, where the enclosing body's own call
        /// to the name reaches the override and the override reaches the outer declaration.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void AnOverrideDeclaredInsideADefinitionBodyLayersOverTheOuterDeclaration(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<inner>{{IN:@(Name)}} :: " + ManufacturerType + "\n" +
                    "<outer>{{OUT[@%<inner:inner>{{IN2[@inner()]}} :: " + ManufacturerType +
                    "%@@inner()]}} :: " + ManufacturerType + "%@\n" +
                    "[@outer(Manufacturer)]\n";
            AssertParity("views/layer-nested.heddle", t, typeof(Product), model);
        }

        /// <summary>Two call sites of one override share the layer and the compiled body, exactly as two call sites
        /// of any other definition do.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void TwoCallSitesOfOneOverrideShareTheSameLayerAndBody(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<label>{{base:@(Name)}} :: " + ManufacturerType + "\n" +
                    "<label:label>{{over[@label()]}} :: " + ManufacturerType + "%@\n" +
                    "[@label(Manufacturer)][@label(Manufacturer)]\n";
            AssertParity("views/layer-two-sites.heddle", t, typeof(Product), model);
        }

        /// <summary>Name-differing inheritance, the control: it was already precompiling, and it resolves the other
        /// way — flatly, through the live document table, so the child's call to its base name reaches whatever that
        /// name's most-derived layer is at the end of the document.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void NameDifferingInheritanceStillResolvesThroughTheDocumentTable(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<b>{{B1:@(Name)}} :: " + ManufacturerType + "\n" +
                    "<c:b>{{C[@b()]}} :: " + ManufacturerType + "\n" +
                    "<b:b>{{B2}} :: " + ManufacturerType + "%@\n" +
                    "[@c(Manufacturer)][@b(Manufacturer)]\n";
            AssertParity("views/layer-name-differing.heddle", t, typeof(Product), model);
        }

        /// <summary>A layer over a slot-declaring definition: the slot carrier is the definition's own, so the
        /// override's <c>@out(value)</c> projects the caller content the same way its base does.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void ALayerOverASlotDefinitionKeepsTheSlotProjection(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<pick(out:: " + ManufacturerType + ")>{{base<@out(Manufacturer)>}} :: " + ProductType + "\n" +
                    "<pick:pick>{{over<@out(Manufacturer)>[@pick(){{B=@(Name)}}]}} :: " + ProductType + "%@\n" +
                    "[@pick(){{O=@(Name)}}]\n";
            AssertParity("views/layer-slot.heddle", t, typeof(Product), model);
        }

        /// <summary>A layer over a definition that carries a default output chain. The chain may only be declared by
        /// the layer underneath — the engine rejects one on a full override at parse time — and the layer above it
        /// is what the document-end render reaches.</summary>
        [Theory]
        [MemberData(nameof(Products))]
        public void ALayerOverADefaultOutputChainRendersTheMostDerivedLayerAtDocumentEnd(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<panel> -> panel(Manufacturer){{base:@(Name)}} :: " + ManufacturerType + "\n" +
                    "<panel:panel>{{over:@(Name)[@panel()]}} :: " + ManufacturerType + "%@\n";
            AssertParity("views/layer-default-chain.heddle", t, typeof(Product), model);
        }

        /// <summary>The one layering shape neither tier serves, and it is the front end that says so: a default
        /// output chain declared ON a full override. The layer underneath may carry one — the test above renders it
        /// — but a re-declaration may not, so the parser errors and the build forwards that error instead of
        /// precompiling something the engine refuses to compile.</summary>
        [Fact]
        public void ADefaultOutputChainOnAFullOverrideIsAFrontEndErrorOnBothTiers()
        {
            const string key = "views/layer-chain-on-override.heddle";
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<panel>{{base}} :: " + ManufacturerType + "\n" +
                    "<panel:panel> -> panel(Manufacturer){{over}} :: " + ManufacturerType + "%@\n";

            var dynamicTemplate = new HeddleTemplate(t,
                new CompileContext(new TemplateOptions(), typeof(Product)));
            Assert.False(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            Assert.Contains("fully overriden", dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.Contains(gen.Diagnostics,
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error &&
                     d.GetMessage().IndexOf("fully overriden", StringComparison.Ordinal) >= 0);
        }

        /// <summary>The recursion-limit path, which is a rendered outcome like any other: a base layer whose own
        /// body calls its name reaches the most-derived layer through the live document table, so the two layers
        /// call each other until the guard stops them. Both tiers must fail the same way — same exception type and
        /// same message — because the guard is the engine's own carrier on both.</summary>
        [Fact]
        public void TheRecursionGuardStopsAMutuallyCallingOverrideIdenticallyOnBothTiers()
        {
            const string key = "views/layer-recursion.heddle";
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<label>{{base[@label()]}} :: " + ManufacturerType + "\n" +
                    "<label:label>{{over[@label()]}} :: " + ManufacturerType + "%@\n" +
                    "[@label(Manufacturer)]\n";
            var model = new Product { Manufacturer = new Manufacturer { Name = "Acme" } };

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var precompiled = Assert.ThrowsAny<Exception>(
                () => DifferentialHarness.RenderGenerated(gen, key, model));

            var dynamicTemplate = new HeddleTemplate(t,
                new CompileContext(new TemplateOptions(), typeof(Product)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            var dyn = Assert.ThrowsAny<Exception>(() => dynamicTemplate.Generate(model));

            Assert.Equal(dyn.GetType(), precompiled.GetType());
            Assert.Equal(dyn.Message, precompiled.Message);
        }
    }
}
