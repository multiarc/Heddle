using System;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// What <c>:: dynamic</c> on a definition actually declares. It is not an untyped body: the engine compiles the
    /// body once per call site, off the value that call site passes, so a caller handing it something with a static
    /// type gets a body bound against that type — and a member the type does not have is an <c>HED0001</c> raised
    /// when the template is compiled, not when it is rendered.
    /// <para>The emitter bound every such body dynamically instead. Where the value was <c>null</c> — whose model
    /// the engine types <c>System.Object</c> — the dynamic read yielded empty and the page rendered what the engine
    /// will not compile at all.</para>
    /// </summary>
    public class DynamicDefinitionBodyTests
    {
        private const string ArticleType = "Heddle.Generator.IntegrationTests.Fixtures.Article";
        private const string MenuType = "Heddle.Generator.IntegrationTests.Fixtures.Menu";

        /// <summary>A <c>:: dynamic</c> definition read straight through, in both the plain and the slot-declaring
        /// shape — the slot machinery is a separate rule and the body typing must not be a property of it.</summary>
        private static string Template(bool slot, string callerModel, string body, string argument) =>
            "@model(){{" + callerModel + "}}@%\n" +
            (slot ? "<frame(out:: object)>{{" + body + "@out(this)}}" : "<frame>{{" + body + "}}") +
            " :: dynamic\n%@\n@frame(" + argument + ")" + (slot ? "{{[q]}}" : "") + "\n";

        private static void AssertEngineRefuses(string template, Type modelType, string message)
        {
            var dynamicTemplate = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), modelType));
            Assert.False(dynamicTemplate.CompileResult.Success);
            var text = dynamicTemplate.CompileResult.ToString();
            Assert.Contains("HED0001", text);
            Assert.Contains(message, text);
        }

        public static TheoryData<string, string, string, string, string> Refused() =>
            new TheoryData<string, string, string, string, string>
            {
                // `this` out of a typed caller: the body's model is the caller's model, member and all.
                { "this", MenuType, "this", "Property Title not found in Type [Menu]", "HED7008" },
                // The null literal, which the engine types System.Object rather than reading as an absence of type.
                // Its receiver is untyped, so there is nothing to report — HED7008 would call the member a typo.
                { "null", ArticleType, "null", "Property Title not found in Type [Object]", null },
                { "int", ArticleType, "5", "Property Title not found in Type [Int32]", "HED7008" },
                { "string", ArticleType, "\"s\"", "Property Title not found in Type [String]", "HED7008" },
            };

        /// <summary>
        /// A body reading a member the call site's value does not have. Asserted on the engine's message and not
        /// only its id, because <c>HED0001</c> names the type it was compiled against — which is the whole claim
        /// about what <c>:: dynamic</c> means.
        /// </summary>
        [Theory]
        [MemberData(nameof(Refused))]
        public void ABodyReadingAMemberTheCallSiteValueLacksDegradesInsteadOfBindingItDynamically(string name,
            string callerModel, string argument, string engineMessage, string diagnostic)
        {
            foreach (var slot in new[] { false, true })
            {
                var key = "views/dynamic-body-" + name + (slot ? "-slot" : "") + ".heddle";
                var template = Template(slot, callerModel, "[@(Title)]", argument);
                var gen = DifferentialHarness.Generate(new[] { (key, template) });

                DifferentialHarness.ExpectDegrade(gen, key);
                if (diagnostic == null)
                    Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
                else
                    Assert.Contains(gen.Diagnostics, d => d.Id == diagnostic);

                AssertEngineRefuses(template,
                    callerModel == MenuType ? typeof(Menu) : typeof(Article), engineMessage);
            }
        }

        /// <summary>The cost control, and the reason this is a typing and not a refusal of <c>:: dynamic</c>: the
        /// same definition over a value that does carry the member precompiles and renders the engine's bytes.
        /// </summary>
        [Theory]
        [InlineData("plain", false, "[T]\n")]
        [InlineData("slot", true, "[T][q]\n")]
        public void ABodyReadingAMemberTheCallSiteValueHasStillPrecompiles(string name, bool slot, string expected)
        {
            var key = "views/dynamic-body-hit-" + name + ".heddle";
            var template = Template(slot, ArticleType, "[@(Title)]", "this");

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal(expected, dyn);
        }

        /// <summary>
        /// The call forms the engine really does hand a <c>dynamic</c> model — a bare call and a member path, whose
        /// accessor takes its dynamic exit before resolving anything. There the body has no model to be typed
        /// against on either tier, so it stays untyped and a missing member is a render-time binder failure on both,
        /// which is the match. Reading these as static models would have degraded them for nothing.
        /// </summary>
        [Fact]
        public void ABareCallKeepsAnUntypedBodyOnBothTiers()
        {
            const string key = "views/dynamic-body-untyped-bare.heddle";
            var template = Template(false, ArticleType, "[@(Title)]", string.Empty);

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Article),
                new Article { Title = "T" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[T]\n", dyn);
        }

        [Fact]
        public void AMemberPathCallKeepsAnUntypedBodyOnBothTiers()
        {
            const string key = "views/dynamic-body-untyped-path.heddle";
            var template = Template(false, "Heddle.Generator.IntegrationTests.Fixtures.Product", "[@(Name)]",
                "Manufacturer");

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Product),
                new Product { Manufacturer = new Manufacturer { Name = "M" } });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[M]\n", dyn);
        }

        /// <summary>
        /// Two call sites into one <c>:: dynamic</c> definition, each passing a differently typed value. The body is
        /// emitted once and cached, so the model it was typed against has to be part of what the cache is keyed on;
        /// otherwise the first call site's body stands for the second, which precompiles code written against a type
        /// it never passes. The single-call-site file is the other half of the pin: a key that refused every repeat
        /// call would satisfy the degrade on its own.
        /// </summary>
        [Fact]
        public void TheSecondCallSiteIntoADynamicDefinitionIsTypedOnItsOwnValue()
        {
            const string bothKey = "views/dynamic-body-two-call-sites.heddle";
            const string oneKey = "views/dynamic-body-one-call-site.heddle";
            const string preamble = "@model(){{" + ArticleType + "}}@%\n<frame>{{[@(Title)]}} :: dynamic\n%@\n";
            const string bothTemplate = preamble + "@frame(this)@frame(5)\n";
            const string oneTemplate = preamble + "@frame(this)\n";

            var gen = DifferentialHarness.Generate(new[] { (bothKey, bothTemplate), (oneKey, oneTemplate) });
            DifferentialHarness.ExpectDegrade(gen, bothKey);
            DifferentialHarness.ExpectPrecompiled(gen, oneKey);
            AssertEngineRefuses(bothTemplate, typeof(Article), "Property Title not found in Type [Int32]");
        }
    }
}
