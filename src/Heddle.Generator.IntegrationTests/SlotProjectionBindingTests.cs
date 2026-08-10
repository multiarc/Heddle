using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The slot projection as a <b>bound extension</b> rather than an arm of the compiler. The build used to decide
    /// for <c>@out</c> what its own <c>InitStart</c> decides — slot mode, whether a value belongs at the call, and
    /// whether the value fits the declared slot type — and every one of those decisions is now the extension's,
    /// made for real at registration off the slot parameter type the call site already carried.
    /// <para>Each refusal below is asserted as the reader meets it: the id and the position, on the tier that
    /// records it and on the dynamic tier that renders the request afterwards. None of them is a build error on
    /// either tier — the engine has always reported these at compile time, which for a template is first render, and
    /// that is where they still arrive.</para>
    /// </summary>
    public class SlotProjectionBindingTests
    {
        private const string MenuType = "Heddle.Generator.IntegrationTests.Fixtures.Menu";
        private const string OptionType = "Heddle.Generator.IntegrationTests.Fixtures.MenuOption";
        private const string ArticleType = "Heddle.Generator.IntegrationTests.Fixtures.Article";

        private static Menu OneOption() =>
            new Menu { Options = new List<MenuOption> { new MenuOption { Id = 7, Label = "Home" } } };

        private static HeddleCompileError EngineRefusal(string template, Type modelType, string diagnosticId)
        {
            var dynamicTemplate = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), modelType));
            Assert.False(dynamicTemplate.CompileResult.Success, "the engine was expected to refuse: " + template);
            return dynamicTemplate.CompileResult.Errors.First(e => e.DiagnosticId == diagnosticId);
        }

        /// <summary>Both tiers, one diagnostic: the build keeps the template, the projection's own hook reports the
        /// engine's error at registration as a template-scope fault, and the dynamic tier the fault sends the
        /// request to reports the same id at the same position.</summary>
        private static void AssertSameRefusalOnBothTiers(string key, string template, Type modelType,
            string diagnosticId)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);

            var initError = DifferentialHarness.ExpectInitRefusal(gen, key, diagnosticId);
            var engineError = EngineRefusal(template, modelType, diagnosticId);
            Assert.Equal(engineError.Position.StartIndex, initError.Position.StartIndex);
            Assert.Equal(engineError.Position.Length, initError.Position.Length);
        }

        /// <summary>
        /// The shape the whole arm exists for: a slot-declaring definition iterating a collection and projecting the
        /// caller's content per item. Both tiers render it and the bytes agree — which is the proof that the hook
        /// reached slot mode on the precompiled tier, because a projection whose <c>_slotMode</c> stayed false
        /// splices the chained value instead of the caller's content and renders something else entirely.
        /// <para><see cref="DifferentialHarness.ExpectInitClean"/> rather than a bare precompiled expectation: this
        /// row is also the cost control for every refusal below, and a hook that faulted here would take the
        /// template to the dynamic tier byte-identically, with nothing but this assertion to say so.</para>
        /// </summary>
        [Fact]
        public void AValidSlotProjectionKeepsItsTierAndItsHookReachesSlotMode()
        {
            const string key = "views/projection-valid.heddle";
            var template = "@model(){{" + MenuType + "}}@\\\n@%\n<picker(out:: " + OptionType +
                           ")>{{<ul>@list(Options){{<li>@out(this)</li>}}</ul>}} :: " + MenuType + "\n%@\n" +
                           "@picker(this){{<a id=\"@(Id)\">@(Label)</a>}}\n";

            DifferentialHarness.ExpectInitClean(DifferentialHarness.Generate(new[] { (key, template) }), key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Menu), OneOption());
            Assert.Equal(dyn, precompiled);
            Assert.Equal("<ul><li><a id=\"7\">Home</a></li></ul>\n", dyn);
        }

        /// <summary>A value the declared slot type cannot take. The conversion table asked is the engine's own, in
        /// the engine's own hook; the build carries the value's static type to it and nothing else.</summary>
        [Fact]
        public void AProjectionValueTheSlotTypeCannotTakeIsRefusedByItsOwnHook()
        {
            const string key = "views/projection-value-mismatch.heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + ArticleType + ")>{{[@out(this)]}} :: " +
                           MenuType + "\n%@\n@frame(this){{[q]}}\n";

            AssertSameRefusalOnBothTiers(key, template, typeof(Menu), HeddleDiagnosticIds.SlotValueTypeMismatch);
        }

        /// <summary>A slot-declaring definition every <c>@out</c> in whose body must carry a value. The build used
        /// to refuse this shape itself; the hook reports it, with the sentence naming the declared slot type, which
        /// no build-time twin ever had in hand.</summary>
        [Fact]
        public void AValuelessProjectionInsideASlotDefinitionIsRefusedByItsOwnHook()
        {
            const string key = "views/projection-valueless-in-slot.heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + OptionType + ")>{{[@out()]}} :: " +
                           MenuType + "\n%@\n@frame(this){{[q]}}\n";

            AssertSameRefusalOnBothTiers(key, template, typeof(Menu), HeddleDiagnosticIds.SlotValueRequired);
        }

        /// <summary>A value on a projection that has no slot to project into — at the top level of a document, and
        /// inside a definition that declares no slot. The engine writes a different sentence for each, and which one
        /// it writes is decided by the parse context the call site carries.</summary>
        [Theory]
        [InlineData("top-level", "@out(this)\n")]
        [InlineData("plain-definition", "@%\n<frame>{{[@out(this)]}} :: " + MenuType + "\n%@\n@frame(this)\n")]
        public void AProjectionValueOutsideASlotDefinitionIsRefusedByItsOwnHook(string name, string body)
        {
            var key = "views/projection-value-no-slot-" + name + ".heddle";
            var template = "@model(){{" + MenuType + "}}@\\\n" + body;

            AssertSameRefusalOnBothTiers(key, template, typeof(Menu), HeddleDiagnosticIds.SlotValueWithoutSlot);
        }

        /// <summary>
        /// A projection carrying both a value and a body, inside a slot-declaring definition. The build refuses
        /// every bodied projection before the hook is reached — see
        /// <see cref="OutStaticBodyFallbackTests"/> for the rendered byte that keeps that refusal — so this one is a
        /// build-tier degrade, and the engine's own <c>HED5018</c> is what the reader gets from the dynamic tier the
        /// template renders on.
        /// </summary>
        [Fact]
        public void ABodiedProjectionIsTheOneShapeTheBuildStillRefusesItself()
        {
            const string key = "views/projection-bodied.heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + OptionType +
                           ")>{{[@out(this){{B}}]}} :: " + MenuType + "\n%@\n@frame(this){{[q]}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key,
                "bodied @out");
            EngineRefusal(template, typeof(Menu), HeddleDiagnosticIds.SlotValueWithBody);
        }

        /// <summary>
        /// A projection composed after a chained call — the arm that arms <c>OutExtension</c>'s composed guard,
        /// which is a render-time throw rather than a compile error. This used to be a build-tier degrade for the
        /// blunt reason that the build precompiled no multi-item chain at all, and the guard was the one piece of
        /// the projection's state a call site could not carry.
        /// <para>It carries it now: a chain's non-leading items write
        /// <c>PrecompiledInitSite.IsChainedConsumer</c>, the hook reads it off the witness source item, and the
        /// throw is the same sentence from both tiers — which is the only way the flag can be shown to have
        /// arrived, since what it decides is a message rather than a byte.</para>
        /// </summary>
        [Fact]
        public void AProjectionComposedAfterAChainedCallThrowsTheEnginesGuardOnBothTiers()
        {
            const string key = "views/projection-composed.heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<wrap>{{(@out())}}\n<frame(out:: " + MenuType +
                           ")>{{[@wrap():out(this)]}} :: " + MenuType + "\n%@\n@frame(this){{[q]}}\n";

            var (precompiled, dynamic) = DifferentialHarness.DeferredWithOptions(key, template, typeof(Menu),
                OneOption(), new TemplateOptions());

            var fromBuild = Assert.Throws<TemplateProcessingException>(() => precompiled());
            var fromEngine = Assert.Throws<TemplateProcessingException>(() => dynamic());
            Assert.Contains("chained call", fromEngine.Message, StringComparison.Ordinal);
            Assert.Equal(fromEngine.Message, fromBuild.Message);
        }

        /// <summary>
        /// The third-party half of the role. <see cref="ProjectExtension"/> carries nothing but
        /// <c>[SlotProjection]</c> — not derived from the engine's projection, not named after it — and a call to it
        /// inside a slot-declaring definition body takes the ordinary bound-extension route and keeps its tier.
        /// <para>The <c>@out</c> beside it is what makes the definition compile at all: the engine requires every
        /// <b>projection of its own</b> in a slot body to carry a value, and requires nothing of anyone else's.</para>
        /// </summary>
        [Fact]
        public void AThirdPartyProjectionPrecompilesInsideASlotDeclaringDefinition()
        {
            const string key = "views/projection-third-party.heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<picker(out:: " + OptionType +
                           ")>{{<ul>@list(Options){{<li>[@project(this)]@out(this)</li>}}</ul>}} :: " + MenuType +
                           "\n%@\n@picker(this){{<a>@(Label)</a>}}\n";

            DifferentialHarness.ExpectInitClean(DifferentialHarness.Generate(new[] { (key, template) }), key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Menu), OneOption());
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The point of binding the role rather than routing it: a third-party projection does not inherit the
        /// built-in's refusals. A valueless call, and a value the declared slot type could not take, are errors
        /// <c>OutExtension</c> raises about <em>itself</em>; this extension raises neither, the engine therefore
        /// compiles both templates, and the build tier keeps both. Under the name-keyed arm each of these was
        /// refused for a rule its extension never had.
        /// </summary>
        [Theory]
        [InlineData("valueless", "@project()")]
        [InlineData("unassignable-value", "@project(this)")]
        public void AThirdPartyProjectionDoesNotInheritTheBuiltInsSlotRefusals(string name, string call)
        {
            var key = "views/projection-third-party-" + name + ".heddle";
            var template = "@model(){{" + MenuType + "}}@%\n<frame(out:: " + OptionType + ")>{{[" + call +
                           "]@list(Options){{@out(this)}}}} :: " + MenuType + "\n%@\n" +
                           "@frame(this){{<a>@(Label)</a>}}\n";

            var dynamicTemplate = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), typeof(Menu)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());

            DifferentialHarness.ExpectInitClean(DifferentialHarness.Generate(new[] { (key, template) }), key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Menu), OneOption());
            Assert.Equal(dyn, precompiled);
        }
    }
}
