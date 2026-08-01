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

        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";

        private static readonly System.Collections.Generic.Dictionary<string, string> FullCSharpBuild =
            new System.Collections.Generic.Dictionary<string, string>
                { ["build_property.HeddleExpressionMode"] = "FullCSharp" };

        private static string LinqTemplate(string import) =>
            "@using(){{" + import + "}}@\\\n@model(){{" + CatalogType + "}}@\\\n" +
            "@list(@model.Products.Where(p => p.Name.Length > 5)){{<i>@(Name)</i>}}\n";

        private static Catalog TwoProducts() => new Catalog
        {
            Products = new System.Collections.Generic.List<Product>
            {
                new Product { Name = "Cheap" },
                new Product { Name = "Pricey" },
            }
        };

        /// <summary>
        /// The directive is what makes an embedded expression bind, so the question asked of a <c>@using</c> body has
        /// to be the one C# asks of it: <c>global::System.Linq</c> and <c>System . Linq</c> both name
        /// <c>System.Linq</c> to the compiler, and neither survives being split on <c>.</c> and matched segment by
        /// segment. Dropped, the generated file lost the directive its pasted C# needed and the consumer's build
        /// stopped on two <c>CS1061</c> for a <c>Where</c> that was right there.
        /// </summary>
        [Theory]
        [InlineData("System.Linq")]
        [InlineData("global::System.Linq")]
        [InlineData("System . Linq")]
        public void AUsingSpelledAnyWayCSharpAcceptsReachesTheEmbeddedExpression(string import)
        {
            var key = "views/using-linq-" + import.GetHashCode() + ".heddle";
            var runtime = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

            var (precompiled, dyn) = DifferentialHarness.Render(key, LinqTemplate(import), typeof(Catalog),
                TwoProducts(), FullCSharpBuild, runtime);
            Assert.Equal("<i>Pricey</i>\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The other half of the same rule, and the direction the first fix went wrong in: a <c>@using</c> is
        /// <b>not</b> inert once the template has an embedded expression. The engine writes every collected body into
        /// a C# <c>using</c> directive of the code it compiles for one, so a body naming no namespace — or not being
        /// a name at all — makes the <em>engine</em> refuse the whole template. Omitting the directive here and
        /// pre-compiling anyway rendered a template the dynamic tier will not compile.
        /// </summary>
        [Theory]
        [InlineData("no-such-namespace", "Zork.Nope")]
        [InlineData("not-a-name", "1 + 2")]
        public void AUsingTheEnginesCSharpTierWillNotCompileTakesTheTemplateWithIt(string name, string import)
        {
            var key = "views/using-bad-with-csharp-" + name + ".heddle";
            var template = "@using(){{" + import + "}}@\\\n@model(){{" + CatalogType + "}}@\\\n" +
                           "Count: @(@model.Products.Count)\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectDegrade(gen, key);

            var engine = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp },
                    new ExType(typeof(Catalog))));
            Assert.False(engine.CompileResult.Success);
        }

        /// <summary>The near neighbour that keeps the rule about the C# tier rather than about <c>@using</c>: the
        /// same unusable body with no embedded expression anywhere compiles the collected namespace into nothing, so
        /// the engine renders and so must the build tier. The first two cases of this class are the same shape with
        /// their own bodies.</summary>
        [Fact]
        public void TheSameUnusableUsingWithNoEmbeddedExpressionStillPrecompiles()
        {
            const string key = "views/using-bad-no-csharp.heddle";
            const string template = "@using(){{Zork.Nope}}@\\\n@model(){{" + CatalogType + "}}@\\\nCount: 2\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Catalog), TwoProducts(),
                FullCSharpBuild, new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp });
            Assert.Equal("Count: 2\n", dyn);
            Assert.Equal(dyn, precompiled);
        }
    }
}
