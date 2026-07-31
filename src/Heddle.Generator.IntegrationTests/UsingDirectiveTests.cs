using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// What <c>@using</c> means on each tier. To the engine it is advice about resolving a model type name — its
    /// only effect is <c>CSharpContext.ImportNamespace</c>, consulted when an expression reaches the C# tier — so a
    /// text naming nothing is simply never consulted and the template renders. The emitter copied the same text
    /// into the generated file as a C# <c>using</c> directive, where a text naming nothing is CS0246 and a text that
    /// is not a name at all stops the generated compilation unit from parsing.
    /// <para>Generated code is fully qualified everywhere, so the directive buys nothing there; what the collected
    /// namespaces are actually for is resolving a model type spelled by its short name, and that reads the list
    /// rather than the emitted directives.</para>
    /// </summary>
    public class UsingDirectiveTests
    {
        private const string Fixtures = "Heddle.Generator.IntegrationTests.Fixtures";

        private static string Dynamic(string content, System.Type modelType, object model)
        {
            var template = new HeddleTemplate(content,
                new CompileContext(new TemplateOptions(), modelType == null ? ExType.Dynamic : new ExType(modelType)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(model);
        }

        /// <summary>A namespace nothing in this compilation declares. The engine never consults it; the emitted
        /// <c>using Zork.Nope;</c> is CS0246 against a <c>.g.cs</c> the consumer cannot edit.</summary>
        [Fact]
        public void AUsingNamingNoNamespaceThisCompilationCanSeeStillPrecompiles()
        {
            const string key = "views/using-unknown.heddle";
            const string template = "@using(){{Zork.Nope}}@\\\nhello\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, null, null);
            Assert.Equal("hello\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A body that is not a name at all. The engine takes any text — it only ever compares it against
        /// a namespace — while <c>using 1 + 2;</c> is not a using directive, not a member declaration, and not
        /// anything else: the whole generated file stopped parsing, so every other template in the same compilation
        /// went down with it.</summary>
        [Fact]
        public void AUsingWhoseBodyIsNotANameStillPrecompiles()
        {
            const string key = "views/using-expression.heddle";
            const string template = "@using(){{1 + 2}}@\\\nhello\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, null, null);
            Assert.Equal("hello\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The ordinary case, and the neighbour that says the rule is about what the name resolves to
        /// rather than about <c>@using</c> as such.</summary>
        [Fact]
        public void AUsingNamingARealNamespaceStillPrecompiles()
        {
            const string key = "views/using-real.heddle";
            const string template = "@using(){{System.Linq}}@\\\nhello\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, null, null);
            Assert.Equal("hello\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>What the collected namespaces are actually for: a <c>@model</c> spelling that only resolves
        /// once one of them qualifies it. This is the row that reddens if the list itself — rather than the emitted
        /// directive — is filtered, and the row below is its counterpart with the <c>@using</c> taken away.
        /// </summary>
        [Fact]
        public void AModelTypeNameQualifiedByACollectedNamespaceStillResolves()
        {
            const string key = "views/using-model-resolution.heddle";
            const string template =
                "@using(){{Heddle.Generator.IntegrationTests}}@\\\n@model(){{Fixtures.Cart}}@\\\n[@(Name)]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart),
                new Cart { Name = "ab" });
            Assert.Equal("[ab]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The same spelling with no <c>@using</c> to qualify it resolves to nothing and the template
        /// degrades. Without this row the one above cannot say whether the import decided anything.</summary>
        [Fact]
        public void TheSameModelTypeNameWithoutTheUsingDoesNotResolve()
        {
            const string key = "views/using-model-no-import.heddle";
            const string template = "@model(){{Fixtures.Cart}}@\\\n[@(Name)]\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectDegrade(gen, key);
        }
    }
}
