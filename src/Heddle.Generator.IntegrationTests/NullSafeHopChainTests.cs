using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A member path that walks through a null reference and then reads a member of the value it could not fetch.
    /// The engine substitutes <c>default(T)</c> at the hop that failed and keeps walking, so the read lands on that
    /// default; C#'s <c>?.</c> abandons the whole rest of the chain instead. The two answers are different values,
    /// and for <c>HasValue</c> they are both renderable — the divergence shows up as wrong output, not as an error.
    /// </summary>
    public class NullSafeHopChainTests
    {
        private const string Header =
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.NullableHopModel}}@\\\n";

        [Theory]
        [InlineData("Inner.Maybe.HasValue")]
        [InlineData("Inner.Maybe.Value")]
        [InlineData("Inner.When.Value.Year")]
        [InlineData("Inner.When.HasValue")]
        public void AHopThroughNullReadsTheMemberOfTheDefaultOnBothTiers(string path)
        {
            const string key = "views/nullable-hop-chain.heddle";
            var template = Header + "@(" + path + ")\n";

            // Two of these paths read `Value` off a default, which throws on the engine tier; the tiers have to agree
            // on that too, so each backend is invoked separately rather than as a tuple that dies on the first one.
            var backends = DifferentialHarness.DeferredWithOptions(
                key, template, typeof(NullableHopModel), new NullableHopModel(), new TemplateOptions());

            var precompiledError = Record.Exception(() => backends.precompiled());
            var dynamicError = Record.Exception(() => backends.dynamic());

            Assert.Equal(dynamicError?.GetType(), precompiledError?.GetType());
            Assert.Equal(dynamicError?.Message, precompiledError?.Message);
            if (dynamicError == null)
                Assert.Equal(backends.dynamic(), backends.precompiled());
        }

        /// <summary>A ref-struct hop cannot use <c>?.</c> for its null test — there is no nullable form to widen
        /// into — so the receiver has to be named some other way, and here that receiver is itself a
        /// null-conditional chain. Respelling the chain into the read half re-applied the propagation to a type that
        /// cannot carry it: a compile error in the generated source rather than a wrong answer.</summary>
        [Fact]
        public void ARefStructHopBehindAReferenceHopCompiles()
        {
            const string key = "views/nested-ref-struct.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.NestedRefStructModel}}@(Inner.Buf.Length)";
            var model = new NestedRefStructModel { Inner = new RefStructModel() };

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(NestedRefStructModel), model);

            Assert.Equal(dyn, precompiled);
            Assert.Equal("5", precompiled);
        }

        /// <summary>
        /// A ref-struct hop has no nullable form, so its null test cannot be written with <c>?.</c> and the receiver
        /// has to be named. Naming it by respelling the expression reads it twice, and the engine reads it once —
        /// which is not a performance difference: a getter that answers differently the second time turns a rendered
        /// value into a <c>NullReferenceException</c> on one tier only.
        /// </summary>
        [Fact]
        public void ARefStructHopReadsItsReceiverOnce()
        {
            const string key = "views/counting-ref-struct.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.CountingRefStructModel}}@(Inner.Buf.Length)";
            var backends = DifferentialHarness.DeferredWithOptions(
                key, template, typeof(CountingRefStructModel), new CountingRefStructModel(), new TemplateOptions());

            // Each backend is invoked exactly once: the model answers differently on every read, so a second call
            // would be measuring a different model.
            CountingRefStructModel.Reads = 0;
            string precompiled = null;
            var precompiledError = Record.Exception(() => precompiled = backends.precompiled());
            var precompiledReads = CountingRefStructModel.Reads;

            CountingRefStructModel.Reads = 0;
            string dyn = null;
            var dynamicError = Record.Exception(() => dyn = backends.dynamic());
            var dynamicReads = CountingRefStructModel.Reads;

            Assert.Equal(1, dynamicReads);
            Assert.Equal(dynamicReads, precompiledReads);
            Assert.Equal(dynamicError?.GetType(), precompiledError?.GetType());
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// A ref struct read <em>through</em> to a member of its own is fine — what leaves the path is an
        /// <c>int</c>. The ref struct itself as the path's value is not: every consumer boxes it, and a ref struct
        /// cannot be boxed.
        /// <para>Neither tier can render this, and that is not the point. The engine refuses it with
        /// <c>HED0005</c> when it compiles the template — an id, a position, something a host can report. Emitting
        /// it put <c>CS0030</c> into the consumer's build instead: no Heddle id, reported against a
        /// <c>.heddle</c> file. Degrading is what lets the engine's refusal be the one the reader sees.</para>
        /// </summary>
        [Fact]
        public void APathEndingOnARefStructDegradesInsteadOfBreakingTheBuild()
        {
            const string key = "views/ref-struct-as-value.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.NestedRefStructModel}}@(Inner.Buf)";

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.DoesNotContain(gen.Diagnostics,
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

            // And the refusal the degrade hands off to is a real one, with an id.
            var dynamicTemplate = new Heddle.HeddleTemplate(template,
                new Heddle.Runtime.CompileContext(typeof(NestedRefStructModel)));
            Assert.False(dynamicTemplate.CompileResult.Success);
        }

        /// <summary>
        /// Two ref-struct hops in one expression. Each has to name its receiver in a local of its own, because the
        /// pattern that names it is a declaration and C# lets a name be declared once per scope. Every other use of
        /// the allocator produces one local per generated file, so a single template with a single such hop is
        /// indifferent to what the allocator returns — and the whole suite was.
        /// </summary>
        [Fact]
        public void TwoRefStructHopsInOneExpressionGetDistinctLocals()
        {
            const string key = "views/two-ref-struct-hops.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.NestedRefStructModel}}@\\\n" +
                "@(Inner.Buf.Length + Inner.Buf.Length)";
            var model = new NestedRefStructModel { Inner = new RefStructModel() };

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(NestedRefStructModel), model);

            Assert.Equal(dyn, precompiled);
            Assert.Equal("10", precompiled);
        }

        /// <summary>
        /// A ref struct as the <b>model</b>, which the hop rule never looked at — it guards the far end of a path,
        /// and a model type is not the far end of anything. Every generated entry point takes the model twice, once
        /// as a typed parameter and once as a cast off an <c>object</c>, and a ref struct can be neither: the
        /// parameter is refused where the strategy hands it over and the cast is refused outright, so the consumer's
        /// build broke on a template that had nothing wrong with its syntax.
        /// <para>The engine <em>compiles</em> this one and refuses it at render, with a catchable exception naming
        /// the mismatch. That is a far better answer than a broken build, and only the dynamic tier can give it.
        /// The naming is what <see cref="TemplateOptions.ValidateModelType"/> buys, so this test asks for it: the
        /// option defaults to off, and only a <c>DEBUG</c> build turns it on regardless. Left unset, the assertion
        /// below would be pinning the build configuration instead of the engine.</para>
        /// </summary>
        [Fact]
        public void ARefStructModelDegradesInsteadOfBreakingTheBuild()
        {
            const string key = "views/ref-struct-model.heddle";
            const string template = "@model(){{System.ReadOnlySpan<char>}}@\\\n@(Length)";

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.DoesNotContain(gen.Diagnostics,
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

            // The engine's own answer, which the degrade exists to let through: it compiles, and says what is wrong
            // when asked to render.
            var dynamicTemplate = new Heddle.HeddleTemplate(template,
                new Heddle.Runtime.CompileContext(
                    new TemplateOptions { ValidateModelType = true }, typeof(System.ReadOnlySpan<char>)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            var error = Assert.ThrowsAny<Heddle.Exceptions.TemplateProcessingException>(
                () => dynamicTemplate.Generate("hello"));
            Assert.Contains("Type mismatch", error.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The other half of the same rule, so both halves are pinned rather than one: with
        /// <see cref="TemplateOptions.ValidateModelType"/> left at its default the engine does not look at the model
        /// at all, and the wrong-typed data reaches the cast the compiled accessor performs. That cast's
        /// <see cref="System.InvalidCastException"/> is what opting out of the check costs — still a render-time
        /// answer the host can catch, just without the Heddle-shaped message. Written per configuration because
        /// <c>DEBUG</c> validates whatever the option says.
        /// </summary>
        [Fact]
        public void ARefStructModelWithoutTheGuardFaultsAtTheCastInstead()
        {
            const string template = "@model(){{System.ReadOnlySpan<char>}}@\\\n@(Length)";

            var dynamicTemplate = new Heddle.HeddleTemplate(template,
                new Heddle.Runtime.CompileContext(
                    new TemplateOptions { ValidateModelType = false }, typeof(System.ReadOnlySpan<char>)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
#if DEBUG
            var error = Assert.ThrowsAny<Heddle.Exceptions.TemplateProcessingException>(
                () => dynamicTemplate.Generate("hello"));
            Assert.Contains("Type mismatch", error.Message, System.StringComparison.OrdinalIgnoreCase);
#else
            Assert.Throws<System.InvalidCastException>(() => dynamicTemplate.Generate("hello"));
#endif
        }

        /// <summary>A ref struct as a <b>definition's</b> model type — the same refusal one level down, where the
        /// generated body casts the scope's <c>object</c> to it.</summary>
        [Fact]
        public void ARefStructDefinitionModelDegradesInsteadOfBreakingTheBuild()
        {
            const string key = "views/ref-struct-definition-model.heddle";
            const string template = "@%<card>{{[@(Length)]}} :: System.ReadOnlySpan<char>%@\\\n@card()\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.DoesNotContain(gen.Diagnostics,
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }

        /// <summary>The same paths with the intermediate reference present: the value is there, so neither tier has
        /// to invent one. This is the half that was already green, and it has to stay that way — a fix that makes
        /// the null case agree by breaking the non-null case has traded one divergence for another.</summary>
        [Theory]
        [InlineData("Inner.Maybe.HasValue")]
        [InlineData("Inner.Maybe.Value")]
        [InlineData("Inner.When.Value.Year")]
        [InlineData("Inner.When.HasValue")]
        public void APresentValueRendersIdenticallyOnBothTiers(string path)
        {
            const string key = "views/nullable-hop-chain-present.heddle";
            var template = Header + "@(" + path + ")\n";
            var model = new NullableHopModel
            {
                Inner = new NullableHolder { Maybe = 41, When = new System.DateTime(2019, 3, 4) }
            };

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(NullableHopModel), model);

            Assert.Equal(dyn, precompiled);
            Assert.Contains(path == "Inner.Maybe.HasValue" || path == "Inner.When.HasValue" ? "True"
                : path == "Inner.Maybe.Value" ? "41" : "2019", precompiled);
        }
    }
}
