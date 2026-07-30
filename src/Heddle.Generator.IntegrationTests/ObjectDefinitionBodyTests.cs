using System;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// What <c>:: object</c> on a definition declares — and what declaring nothing at all declares, since the
    /// parser fills the same word in. The engine's predicate for "compile this body against the value the call
    /// site passes" is that the declared model type resolves to <c>System.Object</c>, which <c>:: dynamic</c>,
    /// <c>:: object</c> and an undeclared model all satisfy. Every rule in this area was keyed on the word
    /// <c>dynamic</c> instead, so <c>:: object</c> kept the whole class of divergence <c>:: dynamic</c> had.
    /// <para>It ran both ways. The emitter typed such a body <c>object</c> — a supertype of whatever the engine
    /// typed it — so a body calling <c>@for</c> or <c>@list</c> on its own model, or reading a member of it, was
    /// refused over a value the engine had typed perfectly well; and in slot mode the same supertype made an
    /// <c>@out(this)</c> look like the identity <c>object → object</c> where the engine saw the box it will not
    /// take, and the template precompiled and rendered what the engine refuses to compile.</para>
    /// </summary>
    public class ObjectDefinitionBodyTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private static string Doc(string definition, string call) =>
            "@model(){{" + CartType + "}}@%\n" + definition + "\n%@\n" + call + "\n";

        private static void AssertRenders(string key, string template, object model, string expected)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart), model);
            Assert.Equal(expected, dyn);
            Assert.Equal(dyn, precompiled);
        }

        private static void AssertDegradesAndEngineRefuses(string key, string template, string id, string message)
        {
            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);
            var dynamicTemplate = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), typeof(Cart)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            var text = dynamicTemplate.CompileResult.ToString();
            Assert.Contains(id, text, StringComparison.Ordinal);
            Assert.Contains(message, text, StringComparison.Ordinal);
        }

        /// <summary>
        /// An extension inside the body whose <c>[DataType]</c> is checked against the body's own model. The
        /// engine hands this body an <c>int</c> and <c>@for</c> takes it; the emitter handed it <c>object</c>, and
        /// <c>@for</c> declares no such accepted type.
        /// </summary>
        [Fact]
        public void ABodyCallingForOnItsOwnModelPrecompilesUnderAnObjectDefinition()
        {
            AssertRenders("views/obj-for.heddle",
                Doc("<frame>{{@for(this){{<@out()>}}}} :: object", "@frame(2)"),
                new Cart(), "<0><1>\n");
        }

        /// <summary>The near neighbour that keeps the rule above from being a blanket "let it through": the same
        /// definition over a value <c>@for</c> does not accept. The engine refuses the template; the emitter must
        /// not precompile it.</summary>
        [Fact]
        public void ABodyCallingForOnAModelForDoesNotAcceptDegrades()
        {
            AssertDegradesAndEngineRefuses("views/obj-for-refused.heddle",
                Doc("<frame>{{@for(this){{<@out()>}}}} :: object", "@frame(\"ab\")"),
                HeddleDiagnosticIds.ReturnTypeMismatch, "Return Type is System.String");
        }

        /// <summary>The same rule through <c>@list</c>, whose accepted type is an interface rather than a pair of
        /// value types — a different arm of the same gate.</summary>
        [Fact]
        public void ABodyCallingListOnItsOwnModelPrecompilesUnderAnObjectDefinition()
        {
            AssertRenders("views/obj-list.heddle",
                Doc("<frame>{{@list(this){{.}}}} :: object", "@frame(\"ab\")"),
                new Cart(), "..\n");
        }

        /// <summary>A plain member read in the body. <c>object</c> has no <c>Length</c>; the <c>string</c> the
        /// engine compiles this body against does.</summary>
        [Fact]
        public void ABodyReadingAMemberOfItsOwnModelPrecompilesUnderAnObjectDefinition()
        {
            AssertRenders("views/obj-member.heddle",
                Doc("<frame>{{[@(Length)]}} :: object", "@frame(\"ab\")"),
                new Cart(), "[2]\n");
        }

        /// <summary>The caller's content, which the engine compiles against the same value — before it swaps the
        /// callee's layout in, but off the value all the same.</summary>
        [Fact]
        public void CallerContentUnderAnObjectDefinitionIsTypedByTheValueToo()
        {
            AssertRenders("views/obj-caller-content.heddle",
                Doc("<frame>{{[@out()]}} :: object", "@frame(\"ab\"){{(@(Length))}}"),
                new Cart(), "[(2)]\n");
        }

        /// <summary>Declaring no model at all is the same declaration: the parser fills in <c>object</c>, and the
        /// engine resolves it to the same type. A rule keyed on the spelling would leave this one behind.</summary>
        [Fact]
        public void ADefinitionThatDeclaresNoModelBehavesTheSameWay()
        {
            AssertRenders("views/obj-undeclared.heddle",
                Doc("<frame>{{[@(Length)]}}", "@frame(\"ab\")"),
                new Cart(), "[2]\n");
        }

        /// <summary>
        /// The over-acceptance half, and the one that mattered: a slot value the engine will not take. The engine
        /// types this body <c>Int32</c> and refuses to box it into an <c>object</c> slot; the emitter typed the
        /// body <c>object</c>, saw an identity conversion, and rendered the page.
        /// </summary>
        [Fact]
        public void ASlotValueTheEngineWillNotBoxDegradesUnderAnObjectDefinition()
        {
            AssertDegradesAndEngineRefuses("views/obj-slot-box.heddle",
                Doc("<s(out:: object)>{{[@out(this)]}} :: object", "@s(5){{|@()|}}"),
                "HED5014",
                "The slot value type System.Int32 is not assignable to the declared slot parameter type System.Object.");
        }

        /// <summary>The cost control for the refusal above: a slot definition whose value does fit is an ordinary
        /// shape and must still precompile.</summary>
        [Fact]
        public void ASlotValueThatFitsStillPrecompilesUnderAnObjectDefinition()
        {
            AssertRenders("views/obj-slot-fits.heddle",
                Doc("<s(out:: System.String)>{{[@out(this)]}} :: object", "@s(\"ab\"){{|@(Length)|}}"),
                new Cart(), "[|2|]\n");
        }

        /// <summary>
        /// Where <c>:: object</c> and <c>:: dynamic</c> genuinely part. Only <c>dynamic</c> sends the engine's
        /// model accessor down its dynamic exit, so under <c>:: object</c> a member path at the call site is
        /// resolved statically and the body is typed by the result — <c>@frame(Name)</c> gives a <c>string</c>
        /// body, and a member the <c>string</c> does not have is a compile-time refusal rather than a
        /// render-time throw. Routing <c>:: object</c> to the <c>:: dynamic</c> rule would emit a dynamically
        /// bound read for a template the engine will not compile.
        /// </summary>
        [Fact]
        public void AMemberPathCallSiteTypesAnObjectBodyStatically()
        {
            AssertRenders("views/obj-path-model.heddle",
                Doc("<frame>{{[@(Length)]}} :: object", "@frame(Name)"),
                new Cart { Name = "abc" }, "[3]\n");

            AssertDegradesAndEngineRefuses("views/obj-path-model-bad.heddle",
                Doc("<frame>{{[@(Nope)]}} :: object", "@frame(Name)"),
                HeddleDiagnosticIds.PropertyNotFound, "Property Nope not found in Type [String]");
        }
    }
}
