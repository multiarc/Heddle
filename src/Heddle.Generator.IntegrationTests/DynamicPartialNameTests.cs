using System;
using System.IO;
using Heddle;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A <c>@partial</c> whose name body computes: the engine evaluates that body <b>once, at its compile time,
    /// against <see cref="Scope.Null"/></b> (<c>PartialExtension.InitStart</c> → <c>GetInnerResult(Scope.Null)</c>)
    /// and treats the result as the resolved name — it is not render-time-dynamic at all. The precompiled tier
    /// reproduces the same evaluation at static initialization of the generated class, failure behavior included:
    /// an evaluation that throws is the engine's HED0005 compile fault, re-raised as the
    /// <see cref="TemplateCompileException"/> the engine's own <c>Generate</c> throws for a failed compile.
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class DynamicPartialNameTests
    {
        private static string StageChildren(params (string name, string content)[] children)
        {
            var dir = Path.Combine(Path.GetTempPath(), "heddle_dpn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            foreach (var (name, content) in children)
                File.WriteAllText(Path.Combine(dir, name + ".heddle"), content);
            return dir + Path.DirectorySeparatorChar;
        }

        private static TemplateOptions Options(string root) =>
            new TemplateOptions { RootPath = root, FileNamePostfix = ".heddle" };

        private static string RenderDynamic(string template, TemplateOptions options, Type modelType, object model)
        {
            var dynamicTemplate = new HeddleTemplate(template,
                new CompileContext(options, modelType == null ? ExType.Dynamic : new ExType(modelType)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            return dynamicTemplate.Generate(model);
        }

        private const string ModelHeader =
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.PartialCaller}}@\\\n";

        /// <summary>The literal-equivalent form: the name body is one computed chain whose value is a constant, so
        /// the evaluated name equals what a static spelling would have produced.</summary>
        [Fact]
        public void ALiteralEquivalentComputedNameRendersTheStaticNamesBytes()
        {
            const string key = "views/dpn-literal-equivalent.heddle";
            const string template = ModelHeader + "A@partial(){{dpn-child@(\"x\")}}B\n";
            var root = StageChildren(("dpn-childx", "[CHILD-X]"));
            var options = Options(root);
            var model = new PartialCaller { Name = "N" };

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var pre = DifferentialHarness.RenderGenerated(gen, key, model, options);
            var dyn = RenderDynamic(template, options, typeof(PartialCaller), model);
            Assert.Equal("A[CHILD-X]B\n", dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>A concatenated name — static text around a computed chain — resolves the concatenation.</summary>
        [Fact]
        public void AConcatenatedComputedNameResolvesTheConcatenatedChild()
        {
            const string key = "views/dpn-concat.heddle";
            const string template = ModelHeader + "A@partial(){{@(\"dpn-\" + \"part\")2}}B\n";
            var root = StageChildren(("dpn-part2", "[CHILD-2:@(Name)]"));
            var options = Options(root);
            var model = new PartialCaller { Name = "N" };

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var pre = DifferentialHarness.RenderGenerated(gen, key, model, options);
            var dyn = RenderDynamic(template, options, typeof(PartialCaller), model);
            Assert.Equal("A[CHILD-2:N]B\n", dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>The Scope.Null pin: a name body reading the model evaluates against a <b>null</b> model — at
        /// compile time on the engine, at static init here — never against the render model. A decoy child named
        /// for the render-time value proves which scope the evaluation saw.</summary>
        [Fact]
        public void ANameBodyReadingTheModelEvaluatesAgainstTheNullScopeNotTheRenderModel()
        {
            const string key = "views/dpn-null-scope.heddle";
            const string template = ModelHeader + "A@partial(){{dpn-plain@(Name)}}B\n";
            var root = StageChildren(("dpn-plain", "[NULL-SCOPE]"), ("dpn-plainN", "[RENDER-SCOPE]"));
            var options = Options(root);
            var model = new PartialCaller { Name = "N" };

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var pre = DifferentialHarness.RenderGenerated(gen, key, model, options);
            var dyn = RenderDynamic(template, options, typeof(PartialCaller), model);
            Assert.Equal("A[NULL-SCOPE]B\n", dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>A name that evaluates to empty schedules no child at all: the engine renders nothing at the
        /// call, and so does the precompiled tier.</summary>
        [Fact]
        public void ANameEvaluatingToEmptyRendersNoPartialAtAll()
        {
            const string key = "views/dpn-empty-name.heddle";
            const string template = ModelHeader + "A@partial(){{@(Name)}}B\n";
            var root = StageChildren(("dpn-unused", "[NEVER]"));
            var options = Options(root);
            var model = new PartialCaller { Name = "N" };

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var pre = DifferentialHarness.RenderGenerated(gen, key, model, options);
            var dyn = RenderDynamic(template, options, typeof(PartialCaller), model);
            Assert.Equal("AB\n", dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>The failure mode, reproduced diagnostic-for-diagnostic: a name body whose model is a
        /// non-nullable value type casts <see cref="Scope.Null"/>'s null model and throws. The engine catches that
        /// inside its compile (<c>CompileItemFault</c>), records HED0005 positioned at the <c>@partial</c> call, and
        /// its <c>Generate</c> then throws <see cref="TemplateCompileException"/>. The precompiled tier captures the
        /// same fault at static init and throws the same exception, carrying the same id, text and position, on
        /// every render — its first observable moment.</summary>
        [Fact]
        public void AThrowingNameEvaluationReproducesTheEngineCompileFault()
        {
            const string key = "views/dpn-eval-throws.heddle";
            const string template = ModelHeader + "A@partial(When){{dpn-child@(Year)}}B\n";
            var root = StageChildren(("dpn-child2026", "[NEVER]"));
            var options = Options(root);
            var model = new PartialCaller { When = new DateTime(2026, 1, 1) };

            var dynamicTemplate = new HeddleTemplate(template,
                new CompileContext(options, new ExType(typeof(PartialCaller))));
            Assert.False(dynamicTemplate.CompileResult.Success);
            var engineError = Assert.Single(dynamicTemplate.CompileResult.ErrorList);
            Assert.Equal(HeddleDiagnosticIds.CompilationFailed, engineError.DiagnosticId);
            var engineThrow = Assert.Throws<TemplateCompileException>(() => dynamicTemplate.Generate(model));
            var engineThrown = Assert.Single(engineThrow.Errors);

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var preThrow = Assert.Throws<TemplateCompileException>(
                () => DifferentialHarness.RenderGenerated(gen, key, model, options));

            var preError = Assert.Single(preThrow.Errors);
            Assert.Equal(engineThrown.DiagnosticId, preError.DiagnosticId);
            Assert.Equal(engineThrown.Error, preError.Error);
            Assert.Equal(engineThrown.Position.StartIndex, preError.Position.StartIndex);
            Assert.Equal(engineThrown.Position.Length, preError.Position.Length);
        }

        /// <summary>The latent divergence the old blanket degrade hid: a "static" name carrying an escaped
        /// <c>@@</c> is a raw-output item, and the engine's evaluation collapses it to a single <c>@</c> before
        /// resolving. The raw spelling must never be used as the key.</summary>
        [Fact]
        public void AnEscapedAtInAStaticNameResolvesTheCollapsedName()
        {
            const string key = "views/dpn-escaped-at.heddle";
            const string template = "X@partial(){{dpn-x@@y}}Y\n";
            var root = StageChildren(("dpn-x@y", "[AT-CHILD]"));
            var options = Options(root);

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var pre = DifferentialHarness.RenderGenerated(gen, key, null, options);
            var dyn = RenderDynamic(template, options, null, null);
            Assert.Equal("X[AT-CHILD]Y\n", dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>The child-typing half of the same latent divergence: the engine compiles a dynamically-resolved
        /// child against the <c>@partial</c> call's <b>value</b> type (its <c>dataType</c>), not the caller's model.
        /// A child reading a member of the passed value — <c>Year</c> off a <c>DateTime</c> member here — compiles
        /// on the engine and must compile identically when the precompiled parent resolves it at render.</summary>
        [Fact]
        public void APartialChildIsTypedByTheCallValueNotTheCallersModel()
        {
            const string key = "views/dpn-child-typing.heddle";
            const string template = ModelHeader + "A@partial(When){{dpn-when-child}}B\n";
            var root = StageChildren(("dpn-when-child", "[Y:@(Year)]"));
            var options = Options(root);
            var model = new PartialCaller { When = new DateTime(2026, 3, 4) };

            var gen = DifferentialHarness.Generate(new[] { (key, template) },
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var pre = DifferentialHarness.RenderGenerated(gen, key, model, options);
            var dyn = RenderDynamic(template, options, typeof(PartialCaller), model);
            Assert.Equal("A[Y:2026]B\n", dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>A comment inside a static name is a hidden token the lexer already excludes from the parsed
        /// parameter, so both tiers resolve the commentless name. Pinned so the static fast path never regresses to
        /// reading raw source text.</summary>
        [Fact]
        public void ACommentInsideAStaticNameResolvesTheCommentlessName()
        {
            const string key = "views/dpn-comment-name.heddle";
            const string template = "X@partial(){{dpn-plain@*c*@}}Y\n";
            var root = StageChildren(("dpn-plain", "[PLAIN]"));
            var options = Options(root);

            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var pre = DifferentialHarness.RenderGenerated(gen, key, null, options);
            var dyn = RenderDynamic(template, options, null, null);
            Assert.Equal("X[PLAIN]Y\n", dyn);
            Assert.Equal(dyn, pre);
        }
    }
}
