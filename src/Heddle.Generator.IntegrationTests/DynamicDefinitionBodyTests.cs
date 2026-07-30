using System;
using System.Collections.Generic;
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
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";
        private const string OrderType = "Heddle.Generator.IntegrationTests.Fixtures.Order";

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
        /// The other half of the literal rows above, and the reason they say something: a literal call site whose
        /// body fits the literal's type <b>precompiles</b> and renders the engine's bytes. Without this, refusing
        /// every literal call site outright — or refusing just <c>null</c> — satisfied every literal assertion in
        /// the suite, and "the type is <c>System.Object</c>" was indistinguishable from "the emitter gave up".
        /// <para>The <c>string</c> row is the sharp one: <c>Length</c> exists on <c>string</c> and on nothing else
        /// here, so it fails if the body is left dynamic <i>and</i> if it is typed as anything but
        /// <c>string</c>.</para>
        /// </summary>
        [Theory]
        [InlineData("null", "null", "[]", "[]\n")]
        [InlineData("int", "5", "[@()]", "[5]\n")]
        [InlineData("string", "\"abc\"", "[@(Length)]", "[3]\n")]
        [InlineData("bool", "true", "[@()]", "[True]\n")]
        public void ALiteralCallSiteValuePrecompilesWhenTheBodyFitsIt(string name, string argument, string body,
            string expected)
        {
            var key = "views/dynamic-body-literal-fits-" + name + ".heddle";
            var template = "@model(){{" + ArticleType + "}}@%\n<frame>{{" + body + "}} :: dynamic\n%@\n@frame(" +
                           argument + ")\n";

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
        /// A computed call-site value — arithmetic, a comparison, a ternary, a coalesce, a concatenation. The engine
        /// compiles these in the caller's own scope and the body inherits the expression's static type; the emitter
        /// left the body dynamically bound instead, so the same templates precompiled and then threw
        /// <c>RuntimeBinderException</c> at render where the engine had refused to compile them.
        /// <para>Each row names the type <b>twice</b> — once as the engine spells it in <c>HED0001</c>, once as the
        /// generator spells it in <c>HED7008</c>. A degrade alone would pass whatever type the emitter had picked,
        /// including the wrong one, and a wrong type is a cast the generated body would throw on.</para>
        /// </summary>
        [Theory]
        // name          model      argument                      engine's name       generator's name
        [InlineData("arith",    CartType,  "Count * 2",                "Int32",            "int")]
        [InlineData("promote",  CartType,  "Price * 2",                "Decimal",          "decimal")]
        [InlineData("unary",    CartType,  "-Count",                   "Int32",            "int")]
        [InlineData("not",      CartType,  "!IsArchived",              "Boolean",          "bool")]
        [InlineData("compare",  CartType,  "Count > 0",                "Boolean",          "bool")]
        [InlineData("logical",  CartType,  "IsArchived && IsFeatured", "Boolean",          "bool")]
        [InlineData("shift",    CartType,  "Count << 1",               "Int32",            "int")]
        [InlineData("ternary",  CartType,  "IsArchived ? Name : Name", "String",           "string")]
        [InlineData("coalesce", CartType,  "Name ?? \"x\"",            "String",           "string")]
        [InlineData("concat",   CartType,  "Name + \"x\"",             "String",           "string")]
        [InlineData("hop",      CartType,  "Nested.Amount + 1",        "Int32",            "int")]
        [InlineData("lifted",   OrderType, "Maybe + 1",                "Nullable<Int32>",  "int?")]
        public void AComputedCallSiteValueTypesTheBodyToTheSameTypeOnBothTiers(string name, string model,
            string argument, string engineType, string generatorType)
        {
            var key = "views/dynamic-body-computed-" + name + ".heddle";
            var template = "@model(){{" + model + "}}@%\n<frame>{{[@(Zzz)]}} :: dynamic\n%@\n@frame(" + argument + ")\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.Contains(gen.Diagnostics, d => d.Id == "HED7008" &&
                                                  d.GetMessage().StartsWith("'" + generatorType + "' does not contain",
                                                      StringComparison.Ordinal));
            AssertEngineRefuses(template, model == CartType ? typeof(Cart) : typeof(Order),
                "Property Zzz not found in Type [" + engineType + "]");
        }

        /// <summary>The cost control for the rule above: the same computed value with a body reading a member its
        /// type <b>does</b> have precompiles and renders the engine's bytes. <c>Length</c> is on <c>string</c> and
        /// nowhere near <c>Cart</c>, so this fails both ways — if the body were left dynamic it would still render,
        /// but if the emitter typed the concatenation as anything but <c>string</c> it would not compile at
        /// all.</summary>
        [Fact]
        public void AComputedCallSiteValueStillPrecompilesWhenTheBodyFitsIt()
        {
            const string key = "views/dynamic-body-computed-hit.heddle";
            const string template = "@model(){{" + CartType + "}}@%\n<frame>{{[@(Length)]}} :: dynamic\n%@\n" +
                                    "@frame(Name + \"x\")\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart), new Cart { Name = "N" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[2]\n", dyn);
        }

        /// <summary>
        /// The value inside an <c>@list</c> body, which is the element the host iterates — a static type, and the
        /// one the engine compiles the definition body against. The fitting body renders the engine's bytes; the
        /// body reading a member the element lacks degrades, and both tiers name the element type.
        /// </summary>
        [Fact]
        public void TheValueInsideAListBodyIsTheElementTypeOnBothTiers()
        {
            const string hitKey = "views/dynamic-body-list-element-hit.heddle";
            const string hit = "@model(){{" + MenuType + "}}@%\n<frame>{{[@(Label)]}} :: dynamic\n%@\n" +
                               "@list(Options){{@frame(this)}}\n";
            var menu = new Menu { Options = new List<MenuOption> { new MenuOption { Label = "L" } } };

            var (precompiled, dyn) = DifferentialHarness.Render(hitKey, hit, typeof(Menu), menu);
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[L]\n", dyn);

            const string missKey = "views/dynamic-body-list-element-miss.heddle";
            const string miss = "@model(){{" + MenuType + "}}@%\n<frame>{{[@(Zzz)]}} :: dynamic\n%@\n" +
                                "@list(Options){{@frame(this)}}\n";

            var gen = DifferentialHarness.Generate(new[] { (missKey, miss) });
            DifferentialHarness.ExpectDegrade(gen, missKey);
            Assert.Contains(gen.Diagnostics, d => d.Id == "HED7008" && d.GetMessage().Contains("MenuOption"));
            AssertEngineRefuses(miss, typeof(Menu), "Property Zzz not found in Type [MenuOption]");
        }

        /// <summary>
        /// A chained call as the value. This is not a type the emitter has to guess at: a chain call-parameter's
        /// value is the chain's render type, and that is <c>String</c> for every chain — the last item's
        /// <c>InitStart</c> decides it and the base one returns <c>typeof(string)</c>. So a body reading a member
        /// <c>String</c> lacks is a template the engine refuses, and the generated tier reports the same member on
        /// the same type rather than degrading in silence.
        /// <para>The neighbours keep it from being a blanket refusal in either direction: the same call with a
        /// fitting body precompiles and matches (it used to be the declared cost of a degrade), and a computed value
        /// the shared tables type still precompiles.</para>
        /// </summary>
        [Fact]
        public void AChainedCallSiteValueTypesADynamicBodyByItsRenderType()
        {
            const string divergeKey = "views/dynamic-body-function-value-miss.heddle";
            const string fitsKey = "views/dynamic-body-function-value-fits.heddle";
            const string typedKey = "views/dynamic-body-typed-value.heddle";
            const string model = "@model(){{" + CartType + "}}@%\n";
            const string diverge = model + "<frame>{{[@(Zzz)]}} :: dynamic\n%@\n@frame(len(Name))\n";
            const string fits = model + "<frame>{{[k]}} :: dynamic\n%@\n@frame(len(Name))\n";
            const string typed = model + "<frame>{{[k]}} :: dynamic\n%@\n@frame(Count * 2)\n";

            var gen = DifferentialHarness.Generate(
                new[] { (divergeKey, diverge), (fitsKey, fits), (typedKey, typed) });
            DifferentialHarness.ExpectDegrade(gen, divergeKey);
            DifferentialHarness.ExpectPrecompiled(gen, fitsKey);
            DifferentialHarness.ExpectPrecompiled(gen, typedKey);

            // The degrade carries the engine's own complaint, against the engine's own type.
            Assert.Contains(gen.Diagnostics,
                d => d.Id == "HED7008" && d.GetMessage().Contains("Zzz") && d.GetMessage().Contains("string"));
            AssertEngineRefuses(diverge, typeof(Cart), "Property Zzz not found in Type [String]");

            var (precompiled, dyn) = DifferentialHarness.Render(fitsKey, fits, typeof(Cart),
                new Cart { Name = "abcd" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[k]\n", dyn);
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
