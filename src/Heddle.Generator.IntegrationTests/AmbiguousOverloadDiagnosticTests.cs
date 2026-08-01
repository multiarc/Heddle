using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// <b>HED7025</b>. The generator used to run the shared <c>OverloadRank</c> core,
    /// obtain <c>BindOutcome.Ambiguous</c> — a <i>proof</i> that the runtime will refuse the call — and then report
    /// nothing at all, so a provably illegal template got a green build and a hard <c>HED1013</c> at first render.
    /// That contradicted both the <b>match principle</b> ("errors always match") and the
    /// <b>fallback-legitimacy principle</b> ("everything else surfaces as an error"): the closest legitimate analogue,
    /// <c>UnsupportedFunction</c>, is legitimate precisely <i>because</i> the build refused on purpose <b>and</b>
    /// warned <c>HED7014</c> — the refusal is legitimate, the silence was not.
    /// <para>The load-bearing half of the fix is the <b>side condition</b>: the report fires only when the ranker
    /// reached <c>Ambiguous</c>/<c>None</c> over arguments the estimator could <i>type</i>. A <c>null</c> bind used to
    /// conflate "provably ambiguous" with "an argument I could not describe", and only the first is a proof about the
    /// runtime. The Unknown-estimate half is asserted here as its own case, because it is the half a later change is
    /// most likely to regress — and regressing it breaks the build for templates that are perfectly legal.</para>
    /// </summary>
    public class AmbiguousOverloadDiagnosticTests
    {
        private const string OrderType = "Heddle.Generator.IntegrationTests.Fixtures.Order";
        private const string PayloadType = "Heddle.Generator.IntegrationTests.Fixtures.OverloadPayload";

        private static string Template(string modelType, string expression) =>
            "@model(){{" + modelType + "}}@\\\nvalue: @(" + expression + ")\n";

        private static Diagnostic[] Unbindable(DifferentialHarness.GenResult gen) =>
            gen.Diagnostics.Where(d => d.Id == HeddleDiagnosticIds.BuildFunctionCallNotBindable).ToArray();

        /// <summary>The shipped counter-example. <c>min(1, 2u)</c> leaves <c>(long,long)</c>, <c>(double,double)</c>
        /// and <c>(decimal,decimal)</c> all at rank <c>(1,1)</c>, so the flat Pareto front has three members. The
        /// build must say so — at Error, positioned in the <c>.heddle</c> file, naming the call and the candidate
        /// signatures so the author can see which overloads collided.</summary>
        [Fact]
        public void AnAmbiguousBuiltInCallIsABuildErrorNamingTheCandidates()
        {
            const string key = "overload/ambiguous.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(OrderType, "min(1, 2u)")) });

            var reported = Unbindable(gen);
            var single = Assert.Single(reported);
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            var message = single.GetMessage();
            Assert.Contains("'min'", message);
            Assert.Contains("ambiguous", message);
            Assert.Contains("min(long, long)", message);
            Assert.Contains("min(double, double)", message);
            Assert.Contains("min(decimal, decimal)", message);
            Assert.Contains(HeddleDiagnosticIds.AmbiguousFunctionCall, message);

            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>The sibling outcome, the same class: <c>BindOutcome.None</c> over typed arguments. No <c>min</c>
        /// overload takes three arguments, which the runtime raises as <c>HED1012</c>.</summary>
        [Fact]
        public void ABuiltInCallWithNoApplicableOverloadIsABuildError()
        {
            const string key = "overload/inapplicable.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(OrderType, "min(1, 2, 3)")) });

            var single = Assert.Single(Unbindable(gen));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            var message = single.GetMessage();
            Assert.Contains("'min'", message);
            Assert.Contains("(int, int, int)", message);
            Assert.Contains(HeddleDiagnosticIds.NoFunctionOverload, message);
        }

        /// <summary>An argument the estimator types only <i>categorically</i> — <c>Maker</c> is a reference type the
        /// name-keyed rank model cannot name — is still a proof, because no built-in parameter type accepts a
        /// conversion from it that the model could have missed (the property
        /// <see cref="NoBuiltInParameterTypeIsAReferenceTypeTheNameModelCannotDecide"/> pins). Both tiers reach
        /// `None`. The message spells the placeholder in English rather than leaking the internal token.</summary>
        [Fact]
        public void ACategoricallyTypedArgumentIsStillAProofAndReadsAsEnglish()
        {
            const string key = "overload/reference-arg.heddle";
            var content = Template(OrderType, "min(1, Maker)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var single = Assert.Single(Unbindable(gen));
            var message = single.GetMessage();
            Assert.Contains("(int, a reference type)", message);
            Assert.DoesNotContain("?reference", message);

            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(new TemplateOptions(), typeof(Order)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains(compiled.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.NoFunctionOverload);
        }

        /// <summary>The build error and the runtime error are the same verdict about the same template.</summary>
        [Fact]
        public void TheBuildErrorAndTheRuntimeErrorAreTheSameVerdict()
        {
            const string key = "overload/verdict-pair.heddle";
            var content = Template(OrderType, "min(1, 2u)");

            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.Single(Unbindable(gen));

            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(new TemplateOptions(), typeof(Order)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains(compiled.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.AmbiguousFunctionCall);
        }

        /// <summary>
        /// <b>The side condition.</b> <c>Payload</c> is <c>object</c>-typed, which the estimator classifies as
        /// <see cref="OperandCategory.Unknown"/> on purpose (an object-typed operand carries no usable static facts).
        /// The path still <i>writes</i>, so the binder <i>is</i> reached — the ranker simply has nothing to rank.
        /// No <c>min</c> overload is applicable to a token the generator cannot describe, so a naive implementation
        /// reaches <c>None</c> here and reports; but the generator has proved nothing about the runtime, which binds
        /// on the <i>expression</i> type and may well succeed. This must stay a silent degrade.
        /// </summary>
        [Fact]
        public void AnUnknownArgumentEstimateStaysASilentDegrade()
        {
            const string key = "overload/unknown-estimate.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(PayloadType, "min(1, Payload)")) });

            Assert.Empty(Unbindable(gen));
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>The same side condition one level in: the <i>outer</i> call's argument estimate is Unknown
        /// because the inner call's own argument was. Nothing in the chain has been proved, so nothing is
        /// reported — for either call.</summary>
        [Fact]
        public void AnUnknownEstimateInheritedFromANestedCallStaysSilentToo()
        {
            const string key = "overload/unknown-nested.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(PayloadType, "min(1, max(1, Payload))")) });

            Assert.Empty(Unbindable(gen));
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        /// <summary>The negative control for the whole feature: a call the ranker <i>resolves</i> is silent and
        /// precompiles. Without this, "report whenever the binder returns null" would pass every assertion
        /// above.</summary>
        [Fact]
        public void AResolvableCallIsNeitherReportedNorDegraded()
        {
            const string key = "overload/resolvable.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(OrderType, "min(1, 2)")) });

            Assert.Empty(Unbindable(gen));
            DifferentialHarness.ExpectPrecompiled(gen, key);
        }

        /// <summary>A host export set with the same collision shape: <c>blend(long, long)</c> and
        /// <c>blend(double, double)</c> both rank <c>(1,1)</c> for <c>(int, uint)</c>.</summary>
        [Fact]
        public void AnAmbiguousExportCallIsABuildErrorToo()
        {
            const string key = "overload/export-ambiguous.heddle";
            var content = Template(OrderType, "blend(1, 2u)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var single = Assert.Single(Unbindable(gen));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            var message = single.GetMessage();
            Assert.Contains("'blend'", message);
            Assert.Contains("blend(long, long)", message);
            Assert.Contains("blend(double, double)", message);

            var options = new TemplateOptions();
            var registry = new Runtime.Expressions.FunctionRegistry();
            registry.RegisterFrom(typeof(TemplateFunctions).Assembly);
            options.Functions = registry;
            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(options, typeof(Order)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains(compiled.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.AmbiguousFunctionCall);
        }

        /// <summary>The export path's <c>None</c> arm: <c>shout</c> is exported as <c>(string)</c> and
        /// <c>(int)</c>, and a <c>uint</c> argument converts implicitly to neither.</summary>
        [Fact]
        public void AnExportCallWithNoApplicableOverloadIsABuildError()
        {
            const string key = "overload/export-inapplicable.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(OrderType, "shout(2u)")) });

            var single = Assert.Single(Unbindable(gen));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            Assert.Contains("'shout'", single.GetMessage());
        }

        /// <summary>
        /// The export path does not take the built-in path's exit here, and the difference is measured rather than
        /// assumed. <c>Payload</c> is <c>object</c>-typed; the shared <i>descriptor</i> calls that
        /// <see cref="OperandCategory.Unknown"/> on purpose, but the export binder is handed the resolved
        /// <b>symbol</b>, which is the same <c>object</c> the engine ranks — <c>NativeExpressionCompiler</c> gives
        /// <c>OverloadRank</c> the compiled expression's own <c>Type</c>. So the generator has proved what the
        /// runtime will do, and the runtime is asserted here doing it, with the same sentence.
        /// <para>The side condition itself is unchanged and still pinned, by the built-in twin above: the
        /// name-keyed rank model has no symbol to be handed, so <c>min(1, Payload)</c> stays the silent degrade it
        /// always was.</para>
        /// </summary>
        [Fact]
        public void AnObjectTypedArgumentIsAProofOnTheExportPathBecauseTheBinderRanksTheSameSymbol()
        {
            const string key = "overload/export-unknown.heddle";
            var content = Template(PayloadType, "blend(1, Payload)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var single = Assert.Single(Unbindable(gen));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            Assert.Contains("(int, object)", single.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, key);

            var options = new TemplateOptions();
            var registry = new Runtime.Expressions.FunctionRegistry();
            registry.RegisterFrom(typeof(TemplateFunctions).Assembly);
            options.Functions = registry;
            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(options, typeof(OverloadPayload)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains(compiled.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.NoFunctionOverload &&
                     e.Error.Contains("(int, object)"));
        }

        /// <summary>The binder is consulted twice per call — once by the operand-kind estimator that guards the
        /// enclosing operator, once by the emission walk — and the writer's per-<c>CallNode</c> memo is what makes
        /// that one report rather than two.</summary>
        [Fact]
        public void OneCallSiteReportsOnce()
        {
            const string key = "overload/dedupe.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(OrderType, "min(1, 2u) > 0")) });
            Assert.Single(Unbindable(gen));
        }

        /// <summary>A definition body reached from two call sites reports its illegal call once, not once per
        /// caller — the shape most likely to double-report. It holds because the writer records only on its
        /// per-<c>CallNode</c> memo miss.</summary>
        [Fact]
        public void ADefinitionBodyCalledTwiceReportsItsCallOnce()
        {
            const string key = "overload/definition-body.heddle";
            const string content = "@model(){{" + OrderType + "}}@\\\n" +
                                   "@%<box>{{[@(min(1, 2u))]}}%@\n" +
                                   "@box(this)@box(this)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.Single(Unbindable(gen));
        }

        /// <summary>
        /// An illegal call in a position where the emitter <b>types the value before it writes it</b> — an
        /// <c>@list</c> data expression, an <c>@out</c> slot value. The typing pass builds a writer of its own and
        /// throws it away, so a refusal that pass proves is discarded; the report has to come from the writer that
        /// emits, and it does, because a call the ranker refuses has no return type either — the value stays "cannot
        /// say", no accepted-type or slot-type gate can refuse on it, and the emission walk is always reached.
        /// </summary>
        [Theory]
        [InlineData("list", "@list(min(1, 2u)){{<@()>}}")]
        [InlineData("out", "@%<s(out:: int)>{{[@out(min(1, 2u))]}}%@\n@s(this){{|@()|}}")]
        public void AnIllegalCallInATypedValuePositionIsStillReported(string name, string body)
        {
            var key = "overload/typed-position-" + name + ".heddle";
            var content = "@model(){{" + OrderType + "}}@\\\n" + body + "\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var single = Assert.Single(Unbindable(gen));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            Assert.Contains("'min'", single.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>The dedupe is per call site, not per template or per pass: two templates in one compilation each
        /// report their own illegal call.</summary>
        [Fact]
        public void EachTemplateReportsItsOwnCallSite()
        {
            var gen = DifferentialHarness.Generate(new[]
            {
                ("overload/site-a.heddle", Template(OrderType, "min(1, 2u)")),
                ("overload/site-b.heddle", Template(OrderType, "max(1, 2u)"))
            });

            var reported = Unbindable(gen);
            Assert.Equal(2, reported.Length);
            Assert.Contains(reported, d => d.GetMessage().Contains("'min'"));
            Assert.Contains(reported, d => d.GetMessage().Contains("'max'"));
        }

        /// <summary>Two illegal call sites in <i>one</i> template report <b>twice</b>. The body build used to abandon
        /// at the first construct it could not write, so the second call was never reached; the element walk now
        /// records the refusal, skips the element and keeps walking, so both calls are reported at their own spans
        /// and the author fixes both in one pass.
        /// <para>Refusal still propagates — the walk returns "refused" at the end, so the template still does not
        /// precompile. <c>ExpectDegrade</c> asserts that half, because collecting diagnostics must never become
        /// emitting past a refusal.</para></summary>
        [Fact]
        public void TwoCallSitesInOneTemplateReportBothBecauseTheBodyWalkCollectsRefusals()
        {
            const string key = "overload/two-sites.heddle";
            const string content = "@model(){{" + OrderType + "}}@\\\n@(min(1, 2u)) @(max(1, 2u))\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var reported = Unbindable(gen);
            Assert.Equal(2, reported.Length);
            Assert.Contains(reported, d => d.GetMessage().Contains("'min'"));
            Assert.Contains(reported, d => d.GetMessage().Contains("'max'"));

            // Two reports, still zero precompiled output.
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>
        /// A <b>sole</b> exported overload, and an argument that converts to nothing. Having one candidate settles
        /// which overload is meant; it settles nothing about whether the call is legal, and the engine draws that
        /// line by running the ranker unconditionally — one candidate or ten — so <c>titlecase(5)</c> against
        /// <c>titlecase(string)</c> is <c>HED1012</c> there. Binding it on arity alone wrote
        /// <c>TitleCase(5)</c> into the consumer's assembly, where the answer was two <c>CS1503</c> against a
        /// <c>.g.cs</c> nobody can edit.
        /// </summary>
        [Theory]
        [InlineData("titlecase(5)", "titlecase")]
        [InlineData("twice(5)", "twice")]
        public void ASoleExportedOverloadThatTakesNoneOfTheseArgumentsIsABuildError(string call, string name)
        {
            var key = "overload/sole-" + name + ".heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(OrderType, call)) });

            var single = Assert.Single(Unbindable(gen));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            Assert.Contains("'" + name + "'", single.GetMessage());
            Assert.Contains(HeddleDiagnosticIds.NoFunctionOverload, single.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>The near neighbours that keep the row above an applicability check rather than a refusal of
        /// single-overload exports: the same sole overload with a literal that fits, and with a member path that
        /// fits.</summary>
        [Theory]
        [InlineData("string-literal", "titlecase(\"ab\")")]
        [InlineData("member-path", "titlecase(Name)")]
        public void ASoleExportedOverloadTheArgumentsDoNotRuleOutStillPrecompiles(string name, string call)
        {
            var key = "overload/sole-ok-" + name + ".heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Template(OrderType, call)) });

            Assert.Empty(Unbindable(gen));
            DifferentialHarness.ExpectPrecompiled(gen, key);
        }

        /// <summary>
        /// The generator's name-keyed rank model answers <c>IsReferenceAssignable</c> <b>false</b> by construction —
        /// it cannot decide a reference conversion from a metadata name alone — and its <c>?reference</c>/
        /// <c>?enum</c>/<c>?struct</c> placeholders are deliberate under-rankings. Under-ranking is harmless while it
        /// only ever <i>degrades</i>; once it can raise an error, it is only sound because no shipped built-in
        /// parameter type is a reference type other than <c>String</c>/<c>Object</c> (so there is no conversion for
        /// the model to miss). If the table ever gains an interface or class parameter, that reasoning lapses and
        /// this pin goes red — which is the intended prompt to revisit the side condition rather than discover a
        /// false build error in the field.
        /// </summary>
        [Fact]
        public void NoBuiltInParameterTypeIsAReferenceTypeTheNameModelCannotDecide()
        {
            // Every spelling the shipped table uses, exhaustively. String and Object are the only reference types,
            // and both are decided without the IsReferenceAssignable arm (AreSame / IsObject); the rest are value
            // types the numeric-kind table or the struct placeholder handles. Adding an interface or class
            // parameter type here is what would make a placeholder-typed argument's `None` unsound.
            var allowed = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
            {
                "System.Boolean", "System.Int32", "System.Int64", "System.Double", "System.Decimal",
                "System.String", "System.Object", "System.Object[]", "Heddle.Models.Range"
            };

            var used = new System.Collections.Generic.SortedSet<string>(System.StringComparer.Ordinal);
            foreach (var row in DefaultFunctionTable.Rows)
                foreach (var name in row.ParameterTypeNames)
                    used.Add(name);

            var unexpected = used.Where(n => !allowed.Contains(n)).ToArray();
            Assert.True(unexpected.Length == 0,
                "Built-in parameter type(s) outside the set the name-keyed rank model can decide: " +
                string.Join(", ", unexpected) + ". HED7025's side condition needs revisiting (see the remarks).");
        }
    }
}
