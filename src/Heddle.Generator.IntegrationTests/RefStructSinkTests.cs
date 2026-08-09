using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The ref-struct position split: what decides a ref-like value's fate is the <b>sink</b> the value lands in,
    /// not its type. A value in output position needs no box — the carrier renders <c>ToString()</c> — so a member
    /// path ending on a ref struct precompiles there, stringified in place, composing with the carrier's
    /// encode-vs-raw rule exactly as a boxed value's <c>ToString()</c> would have. Every boxing sink (an extension's
    /// model value, a slot value, an expression operand, a function argument) stays refused, each with a reason
    /// naming that sink.
    /// <para>The rendered case is deliberately <b>more capable than the modern-TFM engine</b>, whose expression
    /// trees reject by-ref-like types wholesale: it fails the same template at compile with an unpositioned
    /// HED0005 (<see cref="System.InvalidProgramException"/>) where the .NET Framework engine — whose
    /// <c>System.Memory</c> spans carry no by-ref-like marking — boxes and renders <c>ToString()</c>. The
    /// stringified emission reproduces that functioning tier's bytes; the modern engine's crash is the defect the
    /// corrected <c>SymbolTypeResolver</c> docs now describe.</para>
    /// </summary>
    public class RefStructSinkTests
    {
        private const string ModelHeader =
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.SpanHost}}@\\\n";

        private static readonly System.Collections.Generic.Dictionary<string, string> TextProfileBuild =
            new System.Collections.Generic.Dictionary<string, string>
            {
                ["build_property.HeddleOutputProfile"] = "Text"
            };

        /// <summary>Output position under the HTML profile: the value stringifies in place and the encoded carrier
        /// encodes that string — byte-identical to the dynamic tier rendering the same characters off a string
        /// member, which is the control that pins the encoding composition.</summary>
        [Fact]
        public void ARenderedRefStructMemberPrecompilesAndEncodesLikeItsStringTwin()
        {
            const string key = "views/refstruct-rendered-html.heddle";
            const string template = ModelHeader + "<p>@(Buf)</p>\n";
            const string controlTemplate = ModelHeader + "<p>@(BufText)</p>\n";
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html };

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var pre = DifferentialHarness.RenderGenerated(gen, key, new SpanHost(), options);

            var control = new Heddle.HeddleTemplate(controlTemplate,
                new Heddle.Runtime.CompileContext(options, new ExType(typeof(SpanHost))));
            Assert.True(control.CompileResult.Success, control.CompileResult.ToString());
            var dynControl = control.Generate(new SpanHost());

            Assert.Equal("<p>h&amp;i</p>\n", dynControl);
            Assert.Equal(dynControl, pre);
        }

        /// <summary>Output position under the text profile: the raw carrier renders the stringified value
        /// unencoded.</summary>
        [Fact]
        public void ARenderedRefStructMemberUnderTheTextProfileRendersRaw()
        {
            const string key = "views/refstruct-rendered-text.heddle";
            const string template = ModelHeader + "<p>@(Buf)</p>\n";
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text };

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, TextProfileBuild,
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var pre = DifferentialHarness.RenderGenerated(gen, key, new SpanHost(), options);

            Assert.Equal("<p>h&i</p>\n", pre);
        }

        /// <summary>A rendered ref-struct path behind a null reference hop: the stringified emission still walks the
        /// engine's null-safe hop chain, so a null receiver stringifies the default span — empty for a span of
        /// chars — instead of throwing.</summary>
        [Fact]
        public void ARenderedRefStructPathBehindANullHopStringifiesTheDefault()
        {
            const string key = "views/refstruct-rendered-null-hop.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.NestedRefStructModel}}@\\\n" +
                "[@(Inner.Buf)]\n";
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text };

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, TextProfileBuild,
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);

            Assert.Equal("[hello]\n", DifferentialHarness.RenderGenerated(gen, key,
                new NestedRefStructModel { Inner = new RefStructModel() }, options));
            Assert.Equal("[]\n", DifferentialHarness.RenderGenerated(gen, key,
                new NestedRefStructModel(), options));
        }

        /// <summary>A hop <em>through</em> a ref struct to a member of its own was already precompiled and must stay
        /// so — what leaves the path there is the member's own type.</summary>
        [Fact]
        public void AHopThroughARefStructMemberStaysPrecompiledAndByteIdentical()
        {
            const string key = "views/refstruct-hop-through.heddle";
            const string template = ModelHeader + "x@(Buf.Length)y\n";

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(SpanHost), new SpanHost(),
                extraReferences: DifferentialHarness.EngineTestModelReferences());

            Assert.Equal("x3y\n", dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>The model sink: an extension's positional value is boxed into <c>Scope.ModelData</c>, which a
        /// ref struct cannot be (CS1503). Stays refused, naming the sink. The branch carrier is the probe because
        /// it accepts any value type — an extension with a declared accept list (say <c>@string</c>) refuses the
        /// span on that list first, before the value is ever built.</summary>
        [Fact]
        public void ARefStructExtensionModelValueDegradesNamingTheModelSink()
        {
            const string key = "views/refstruct-model-sink.heddle";
            const string template = ModelHeader + "x@if(Buf){{y}}z\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectDegrade(gen, key,
                "member path ends on a ref struct, which a model value cannot box (CS1503)");
        }

        /// <summary>The same member the rendered tests precompile, used as a function argument: stays refused,
        /// naming the sink.</summary>
        [Fact]
        public void ARefStructFunctionArgumentDegradesNamingTheArgumentSink()
        {
            const string key = "views/refstruct-funcarg-sink.heddle";
            const string template = ModelHeader + "x@(len(Buf))y\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectDegrade(gen, key,
                "a member path ending on a ref struct, which a function argument cannot box (CS1503)");
        }

        /// <summary>The same member as an expression operand: stays refused, naming the sink.</summary>
        [Fact]
        public void ARefStructOperandDegradesNamingTheOperandSink()
        {
            const string key = "views/refstruct-operand-sink.heddle";
            const string template = ModelHeader + "x@(Buf == null)y\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectDegrade(gen, key,
                "a member path ending on a ref struct, which an expression operand cannot box (CS0029)");
        }

        /// <summary>An indexer producing a ref struct inside an expression: the operand refusal, from the indexer
        /// arm.</summary>
        [Fact]
        public void ARefStructIndexerResultDegradesNamingTheOperandSink()
        {
            const string key = "views/refstruct-indexer-sink.heddle";
            const string template = ModelHeader + "x@(this[0].Length + Name)y\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectDegrade(gen, key,
                "an indexer returning a ref struct, which an expression operand cannot box");
        }

        /// <summary>The slot value, which is the projecting extension's own positional value and lands in the same
        /// <c>Scope.ModelData</c> box every other call's does. Stays refused, and names that sink: the value cannot
        /// be built at all, so no site is written and the assignability question the extension's own hook would ask
        /// at registration is never reached. The reason used to be the HED5014 assignability twin, which the
        /// projection's real <c>InitStart</c> now makes for itself.</summary>
        [Fact]
        public void ARefStructSlotValueDegradesNamingTheModelSink()
        {
            const string key = "views/refstruct-slot-sink.heddle";
            const string template =
                "@%\n<frame(out:: object)>{{[@out(Buf)]}} :: Heddle.Generator.IntegrationTests.Fixtures.SpanHost\n%@\n" +
                "@frame(this){{[q]}}\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectDegrade(gen, key,
                "member path ends on a ref struct, which a model value cannot box (CS1503)");
        }
    }
}
