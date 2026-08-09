extern alias generator;
using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;
using RefusalCategory = generator::Heddle.Generator.Emit.RefusalCategory;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Where the generic boundary actually runs. A <b>constructed</b> generic model is an ordinary type on both
    /// tiers and precompiles like any other; an <b>open</b> one — a generic definition, a type nested in one, an
    /// array of either — is served by neither, and the interesting half of this suite is why the build must keep
    /// refusing it rather than grow a generic entry class to spell it.
    /// <para>The build could write <c>static class View&lt;T&gt;</c> and name <c>List&lt;T&gt;</c> perfectly well.
    /// The wall is in the engine, twice over, and both walls are pinned below: no value is an instance of a generic
    /// type <em>definition</em>, so the dynamic tier's render-time model gate refuses every model a caller could
    /// pass; and a template that reads one member never reaches render at all, because the engine's model accessor
    /// is an <c>Expression.Convert</c> to the scope type and <c>System.Linq.Expressions</c> rejects an open generic
    /// outright. Emitting one would therefore render where the engine refuses — the one divergence worse than a
    /// missed optimisation — so the refusal is <see cref="RefusalCategory.EngineParity"/>, reproducing a failure
    /// rather than reporting a limit.</para>
    /// </summary>
    public class GenericModelTypeTests
    {
        private const string Fixtures = "Heddle.Generator.IntegrationTests.Fixtures";
        private const string ListOf = "System.Collections.Generic.List";
        private const string OpenNested = Fixtures + ".ArityOuter`1.ArityInner";

        private static HeddleTemplate Engine(string template, Type modelType) =>
            new HeddleTemplate(template, new CompileContext(new TemplateOptions(), modelType));

        // ---- Constructed generics: ordinary types, and they precompile ----

        /// <summary>Every closed spelling the shared grammar builds — a one-argument generic, a two-argument one,
        /// and a type nested in a generic outer — reaches the entry point as a written type and renders the
        /// engine's bytes. This is the cost control the refusal below needs: without it, "generics degrade" would
        /// pass as a description of the tier.</summary>
        [Theory]
        [InlineData("list", ListOf + "<int>", "[@(Count)]\n")]
        [InlineData("dictionary", "System.Collections.Generic.Dictionary<string, int>", "[@(Count)]\n")]
        [InlineData("nested", Fixtures + ".ArityOuter<int>.ArityInner", "[@(Amount)]\n")]
        public void AConstructedGenericModelPrecompiles(string name, string spelling, string body)
        {
            var key = "views/generic-closed-" + name + ".heddle";
            var template = "@model(){{" + spelling + "}}@\\\n" + body;
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, key);
        }

        /// <summary>The bytes, for the shape whose type arguments the entry point has to carry verbatim.</summary>
        [Fact]
        public void AConstructedGenericModelRendersTheEngineSBytes()
        {
            const string key = "views/generic-closed-render.heddle";
            const string template = "@model(){{" + ListOf + "<int>}}@\\\n[@(Count)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(List<int>),
                new List<int> { 1, 2, 3 });
            Assert.Equal("[3]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The registry round trip for a constructed generic: registered, resolved through the gauntlet
        /// against a request typed with the same constructed type, served by the precompiled adapter, byte-identical
        /// to the engine. A tier that keyed the manifest on the generic <em>definition</em> would pass every step
        /// here and still serve the wrong entry, which is what the next test rules out.</summary>
        [Fact]
        public void AConstructedGenericModelSurvivesTheRegistryRoundTrip()
        {
            const string key = "views/generic-closed-resolver.heddle";
            const string template = "@model(){{" + ListOf + "<int>}}@\\\n[@(Count)]\n";

            var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(key, template, typeof(List<int>),
                new List<int> { 1, 2 });
            Assert.Equal("[2]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A model of the wrong constructed generic — a <c>List&lt;string&gt;</c> where the entry was
        /// compiled against <c>List&lt;int&gt;</c> — is refused by the adapter with the dynamic tier's own fault and
        /// the dynamic tier's own sentence. It used to reach the generated cast and come back as a raw
        /// <see cref="InvalidCastException"/>, which no other render fault looks like; a body reading no member did
        /// worse and rendered a page the engine refuses. Type arguments decide it: the two types share a definition
        /// and nothing else.</summary>
        [Fact]
        public void AWrongConstructedGenericModelIsRefusedTheWayTheEngineRefusesIt()
        {
            const string key = "views/generic-closed-wrong-model.heddle";
            const string template = "@model(){{" + ListOf + "<int>}}@\\\n[@(Count)]\n";

            var engineThrow = Assert.Throws<TemplateProcessingException>(
                () => Engine(template, typeof(object)).Generate(new List<string>()));
            var adapterThrow = Assert.Throws<TemplateProcessingException>(
                () => DifferentialHarness.RenderViaResolver(key, template, typeof(List<int>), new List<string>()));

            Assert.Equal(engineThrow.Message, adapterThrow.Message);
        }

        // ---- Open generics: refused, and the refusal reproduces the engine's ----

        /// <summary>Wall one. A template that reads a member of an open generic model never compiles on the dynamic
        /// tier: the model accessor converts to the scope type, and an open generic is not a type an expression tree
        /// admits. The build degrades on the same input, and its refusal says so.</summary>
        [Theory]
        [InlineData("list", ListOf + "`1", "[@(Count)]\n")]
        [InlineData("nested", OpenNested, "[@(Amount)]\n")]
        public void AnOpenGenericModelThatIsReadRefusesOnBothTiers(string name, string spelling, string body)
        {
            var key = "views/generic-open-read-" + name + ".heddle";
            var template = "@model(){{" + spelling + "}}@\\\n" + body;

            Assert.False(Engine(template, typeof(object)).CompileResult.Success);

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key, RefusalCategory.EngineParity, "carries a type parameter");
        }

        /// <summary>Wall two, and the one a generic entry class would have walked straight into. This template
        /// compiles on the dynamic tier — nothing reads the model, so no accessor is built — and then refuses every
        /// model a caller can hand it, because no value is an instance of a generic type definition. A precompiled
        /// entry would have rendered it.</summary>
        [Fact]
        public void AnOpenGenericModelThatIsNeverReadStillAcceptsNoModelValue()
        {
            const string key = "views/generic-open-static.heddle";
            const string template = "@model(){{" + ListOf + "`1}}@\\\nstatic text\n";

            var dynamicTier = Engine(template, typeof(object));
            Assert.True(dynamicTier.CompileResult.Success, dynamicTier.CompileResult.ToString());
            Assert.Throws<TemplateProcessingException>(() => dynamicTier.Generate(new List<int>()));

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key, RefusalCategory.EngineParity, "carries a type parameter");
        }

        /// <summary>An open generic model is not a name the author got wrong, so it draws no HED7007 and no build
        /// error — only the degrade. The spelling resolves; it is the tier that cannot serve it.</summary>
        [Fact]
        public void AnOpenGenericModelIsNotReportedAsAnUnresolvableName()
        {
            const string key = "views/generic-open-quiet.heddle";
            const string template = "@model(){{" + ListOf + "`1}}@\\\n[@(Count)]\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7007");
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7030");
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }
    }
}
