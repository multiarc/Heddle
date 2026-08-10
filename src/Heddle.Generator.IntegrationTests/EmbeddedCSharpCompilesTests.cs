using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Whether an embedded C# expression compiles at all — the question the engine asks Roslyn and refuses the whole
    /// template on, and the one nothing on the build tier asked. The typer answered "cannot say" for an expression
    /// that does not bind, and "cannot say" is the exempting answer at every gate that reads a call-site value's
    /// type, so the text was pasted into the generated file and became the consumer's build error, reported against
    /// a <c>.heddle</c> file with no Heddle diagnostic on it.
    /// <para>Every row here is measured against the engine in the same test, so the assertion is agreement rather
    /// than this tier's opinion.</para>
    /// </summary>
    public class EmbeddedCSharpCompilesTests
    {
        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";

        private static readonly Dictionary<string, string> FullCSharpBuild =
            new Dictionary<string, string> { ["build_property.HeddleExpressionMode"] = "FullCSharp" };

        private static TemplateOptions Runtime => new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

        private static Fixtures.Catalog TwoProducts() => new Fixtures.Catalog
        {
            Title = "shelf",
            Products = new List<Fixtures.Product>
            {
                new Fixtures.Product { Name = "Cheap" },
                new Fixtures.Product { Name = "Pricey" }
            }
        };

        private static string Template(string csharp, string import = null) =>
            (import == null ? "" : "@using(){{" + import + "}}@\\\n") +
            "@model(){{" + CatalogType + "}}@\\\n[@(@" + csharp + ")]\n";

        /// <summary>
        /// Expressions the engine's compiler rejects. The engine reports the error and refuses the template, so the
        /// build tier has to refuse it too — and the outcome that is not allowed is the one every row here produced:
        /// generated code the consumer's own compiler will not build.
        /// </summary>
        [Theory]
        // A member the model does not have.
        [InlineData("model.NoSuchMember", null)]
        // Not an expression at all.
        [InlineData("model.Title +", null)]
        // The right member, the wrong number of arguments.
        [InlineData("model.Title.Substring(1,2,3)", null)]
        // An extension method with no import to reach it. The neighbour with the import is below.
        [InlineData("model.Products.Where(p => p.Name.Length > 5).Count()", null)]
        // A checked constant overflow, which C# refuses whatever the compilation says.
        [InlineData("checked(2147483647+1)", null)]
        // A `using` that does not qualify what the expression needs is not the same as none.
        [InlineData("model.Products.Where(p => p.Name.Length > 5).Count()", "System.Text")]
        public void AnEmbeddedExpressionTheEnginesCompilerRejectsDegrades(string csharp, string import)
        {
            var template = Template(csharp, import);
            var key = "views/embedded-bad-" + (csharp + import).GetHashCode() + ".heddle";

            var engine = new HeddleTemplate(template, new CompileContext(Runtime, new ExType(typeof(Fixtures.Catalog))));
            Assert.False(engine.CompileResult.Success, "the engine was expected to refuse: " + csharp);

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>
        /// The near neighbours, which keep the rule about "does it compile" rather than about embedded C# as such:
        /// each is one edit away from a row above, each is one the engine compiles, and each must still precompile
        /// and render the engine's bytes.
        /// </summary>
        [Theory]
        [InlineData("model.Title", null)]
        [InlineData("model.Title.Substring(1,2)", null)]
        [InlineData("model.Products.Count", null)]
        [InlineData("model.Products.Where(p => p.Name.Length > 5).Count()", "System.Linq")]
        [InlineData("unchecked(2147483647+1)", null)]
        public void AnEmbeddedExpressionTheEngineCompilesStillPrecompiles(string csharp, string import)
        {
            var template = Template(csharp, import);
            var key = "views/embedded-ok-" + (csharp + import).GetHashCode() + ".heddle";

            var engine = new HeddleTemplate(template, new CompileContext(Runtime, new ExType(typeof(Fixtures.Catalog))));
            Assert.True(engine.CompileResult.Success, engine.CompileResult.ToString());

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(Fixtures.Catalog), TwoProducts(),
                FullCSharpBuild, Runtime);
            Assert.Equal(dyn, pre);
        }

        /// <summary>
        /// A member the compiler refuses to let anyone reference. <c>[Obsolete(…, error: true)]</c> is invisible to
        /// reflection and fatal to a compile, so the engine's preparse reports <c>CS0619</c> and refuses, while this
        /// tier pasted the reference into the generated file and handed the consumer the same error with no
        /// explanation attached to it.
        /// </summary>
        [Fact]
        public void AnErrorObsoleteMemberInEmbeddedCSharpDegrades()
        {
            const string type = "Heddle.Generator.IntegrationTests.Fixtures.ObsoleteMemberModel";
            const string bad = "@model(){{" + type + "}}@\\\n[@(@model.Bad)]\n";
            const string good = "@model(){{" + type + "}}@\\\n[@(@model.Title)]\n";

            var engine = new HeddleTemplate(bad,
                new CompileContext(Runtime, new ExType(typeof(Fixtures.ObsoleteMemberModel))));
            Assert.False(engine.CompileResult.Success);

            var genBad = DifferentialHarness.Generate(new[] { ("views/obsolete-embedded.heddle", bad) },
                FullCSharpBuild);
            Assert.Empty(genBad.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectDegrade(genBad, "views/obsolete-embedded.heddle");

            // The near neighbour on the same model: the property that carries no attribute is untouched.
            var genGood = DifferentialHarness.Generate(new[] { ("views/obsolete-embedded-ok.heddle", good) },
                FullCSharpBuild);
            DifferentialHarness.ExpectPrecompiled(genGood, "views/obsolete-embedded-ok.heddle");
        }

        /// <summary>
        /// The model's own namespace is a term of the unit the engine compiles — <c>ImportNamespace</c> on the model
        /// type — so an expression naming a sibling type by its short name binds there. The probe's wrapper did not
        /// have that term and the generated file did not have the directive, so the same expression was refused by
        /// one and rejected by the consumer's compiler in the other.
        /// </summary>
        [Fact]
        public void TheModelsOwnNamespaceReachesTheEmbeddedExpression()
        {
            const string key = "views/embedded-model-namespace.heddle";
            const string template = "@model(){{" + CatalogType + "}}@\\\n[@(@new Product { Name = \"n\" }.Name)]\n";

            var engine = new HeddleTemplate(template, new CompileContext(Runtime, new ExType(typeof(Fixtures.Catalog))));
            Assert.True(engine.CompileResult.Success, engine.CompileResult.ToString());

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(Fixtures.Catalog), TwoProducts(),
                FullCSharpBuild, Runtime);
            Assert.Equal("[n]\n", dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>
        /// The two identifiers the emitter cannot reproduce, told apart from the two <i>words</i>. The refusal was a
        /// word-boundary search over the raw text, so a string literal, a lambda parameter and a member name all
        /// counted as references and took templates the engine renders off the tier.
        /// </summary>
        [Theory]
        [InlineData("model.Title + \"root\"")]
        [InlineData("model.Products.Select(chained => chained.Name).Count()")]
        [InlineData("model.Products.Select(root => root.Name).Count()")]
        public void AnExpressionMerelyContainingTheWordsStillPrecompiles(string csharp)
        {
            var key = "views/embedded-word-" + csharp.GetHashCode() + ".heddle";
            var template = Template(csharp, "System.Linq");

            var engine = new HeddleTemplate(template, new CompileContext(Runtime, new ExType(typeof(Fixtures.Catalog))));
            Assert.True(engine.CompileResult.Success, engine.CompileResult.ToString());

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(Fixtures.Catalog), TwoProducts(),
                FullCSharpBuild, Runtime);
            Assert.Equal(dyn, pre);
        }

        /// <summary>The refusal those two words used to draw is retired: the fragment declares the engine's own
        /// three parameters — <c>chained</c> spelled <c>dynamic</c>, <c>root</c> as the entry model — and the call
        /// site passes <c>scope.ChainedData</c> and the root read, so an expression reading either precompiles and
        /// renders the engine's bytes instead of costing the whole template its tier.</summary>
        [Theory]
        [InlineData("chained")]
        [InlineData("root")]
        [InlineData("root.Title")]
        [InlineData("chained == null ? \"top\" : \"nested\"")]
        public void AnExpressionReadingChainedOrRootPrecompilesByteIdentically(string csharp)
        {
            var key = "views/embedded-chained-" + csharp.GetHashCode() + ".heddle";
            var template = Template(csharp);

            // The engine compiles over the assemblies LOADED in this process, and a dynamic operation on
            // `chained` needs the runtime binder — an input a host rendering dynamic expressions has loaded.
            // Made explicit the way the harness loads model assemblies (RememberExtraReferences).
            System.Reflection.Assembly.Load("Microsoft.CSharp");

            var engine = new HeddleTemplate(template, new CompileContext(Runtime, new ExType(typeof(Fixtures.Catalog))));
            Assert.True(engine.CompileResult.Success, engine.CompileResult.ToString());

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(Fixtures.Catalog), TwoProducts(),
                FullCSharpBuild, Runtime);
            Assert.Equal(dyn, pre);
        }

        /// <summary>
        /// The engine reads an embedded expression's constant value <b>before</b> its semantic type and types it by
        /// what came back, so a constant <c>null</c> is <c>typeof(object)</c> there where Roslyn says
        /// <c>string</c>. An <c>out:: string</c> slot then refuses it on the engine and accepted it here — the
        /// build tier pre-compiled and rendered a template the engine will not compile.
        /// <para>The refusal is the projection's own now: the site carries the type the build read, the real
        /// <c>InitStart</c> checks it against the declared slot type when it runs at registration, and a hook that
        /// reports the engine's error there faults the template onto the dynamic tier.</para>
        /// </summary>
        [Theory]
        [InlineData("default(string)")]
        [InlineData("null")]
        public void AConstantNullIsTypedTheWayTheEngineTypesIt(string csharp)
        {
            var key = "views/embedded-constant-" + csharp.GetHashCode() + ".heddle";
            var template = "@model(){{" + CatalogType + "}}@%\n<frame(out:: string)>{{[@out(@" + csharp +
                           ")]}} :: " + CatalogType + "\n%@\n@frame(this){{[q]}}\n";

            var engine = new HeddleTemplate(template, new CompileContext(Runtime, new ExType(typeof(Fixtures.Catalog))));
            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

            if (engine.CompileResult.Success)
                DifferentialHarness.ExpectPrecompiled(gen, key);
            else
                DifferentialHarness.ExpectInitRefusal(gen, key, HeddleDiagnosticIds.SlotValueTypeMismatch);
        }
    }
}
