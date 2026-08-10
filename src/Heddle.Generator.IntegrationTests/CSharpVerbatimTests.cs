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
    /// The FullCSharp verbatim tier: the parser-captured C# expression pastes verbatim into the generated body
    /// against a local named <c>model</c>, exactly as the runtime's <c>CSharpClassTemplate</c> compiles it. Emitted
    /// only under <c>ExpressionMode.FullCSharp</c>; differential-gated.
    /// </summary>
    public class CSharpVerbatimTests
    {
        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";

        private static readonly Dictionary<string, string> FullCSharp =
            new Dictionary<string, string> { ["build_property.HeddleExpressionMode"] = "FullCSharp" };

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var runtime = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model, FullCSharp, runtime);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Catalogs()
        {
            yield return new object[] { new Catalog { Title = "Store", Products = new List<Product>
            {
                new Product { Name = "Cheap", Manufacturer = new Manufacturer { Name = "Acme" } },
                new Product { Name = "Pricey", Manufacturer = null },
            } } };
            yield return new object[] { new Catalog { Title = "Empty", Products = new List<Product>() } };
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void ListOverLinqExpression(Catalog model)
        {
            // An embedded LINQ expression drives @list.
            var t = "@using(){{System.Linq}}@\\\n" +
                    "@model(){{" + CatalogType + "}}@\\\n" +
                    "@list(@model.Products.Where(p => p.Name.Length > 5)){{ <div>@(Name)</div> }}\n";
            AssertParity("views/cs-list.heddle", t, typeof(Catalog), model);
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void UnnamedCSharpOutput(Catalog model)
        {
            // A bare @(@ expr) unnamed output: the verbatim C# value stringifies through EmptyExtension.
            var t = "@model(){{" + CatalogType + "}}@\\\n" +
                    "Count: @(@model.Products.Count)\n";
            AssertParity("views/cs-count.heddle", t, typeof(Catalog), model);
        }

        [Theory]
        [MemberData(nameof(Catalogs))]
        public void CSharpInsideIfCondition(Catalog model)
        {
            var t = "@model(){{" + CatalogType + "}}@\\\n" +
                    "@using(){{System.Linq}}@\\\n" +
                    "@if(@model.Products.Any()){{has @(@model.Products.Count) items}}@else(){{empty}}\n";
            AssertParity("views/cs-if.heddle", t, typeof(Catalog), model);
        }

        private static void AssertDegrades(string key, string content)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, content) }, FullCSharp);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        private static bool EngineCompiles(string content)
        {
            var template = new HeddleTemplate(content,
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp },
                    new ExType(typeof(Catalog))));
            return template.CompileResult.Success;
        }

        /// <summary>
        /// An embedded expression is the one call-site value the <b>engine</b> types most definitely of all: it hands
        /// the text to Roslyn and reads the semantic type back. The emitter had no arm for it, and every gate that
        /// consults a call-site value's type reads "cannot say" as permission — so an <c>int</c> reached a
        /// <c>string</c> slot and a non-enumerable reached <c>@list</c>, both of which the engine refuses to compile,
        /// and both of which pre-compiled and rendered.
        /// </summary>
        [Theory]
        // The slot row is refused by the projection's own hook at registration rather than by the build: the site
        // carries the type Roslyn gave the fragment, and OutExtension checks it against the declared slot type
        // there. The list row is still a build refusal — @list's accepted-type gate is the compiler's own.
        [InlineData("slot-value",
            "@%\n<frame(out:: string)>{{[@out(@model.Products.Count)]}} :: " + CatalogType + "\n%@\n@frame(this){{[q]}}\n",
            true)]
        [InlineData("list-data", "@list(@model.Products.Count){{x}}\n", false)]
        public void AValueTypedFromEmbeddedCSharpIsCheckedTheWayTheEngineChecksIt(string name, string body,
            bool refusedAtInit)
        {
            var key = "views/cs-typed-" + name + ".heddle";
            var content = "@model(){{" + CatalogType + "}}@\\\n" + body;

            if (refusedAtInit)
            {
                var gen = DifferentialHarness.Generate(new[] { (key, content) }, FullCSharp);
                Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
                DifferentialHarness.ExpectInitRefusal(gen, key, HeddleDiagnosticIds.SlotValueTypeMismatch);
            }
            else
            {
                AssertDegrades(key, content);
            }

            Assert.False(EngineCompiles(content));
        }

        /// <summary>The near neighbours that keep the rule about the type and not about embedded C#: the same two
        /// shapes with an expression whose type <em>does</em> satisfy the gate still precompile and render the
        /// engine's bytes.</summary>
        [Theory]
        [InlineData("slot-value",
            "@%\n<frame(out:: string)>{{[@out(@model.Title)]}} :: " + CatalogType + "\n%@\n@frame(this){{[q]}}\n",
            "[[q]]\n")]
        [InlineData("list-data", "@list(@model.Products){{<i>@(Name)</i>}}\n", "<i>Cheap</i><i>Pricey</i>\n")]
        public void TheSameShapeWithASatisfyingCSharpTypeStillPrecompiles(string name, string body, string expected)
        {
            var key = "views/cs-typed-ok-" + name + ".heddle";
            var content = "@model(){{" + CatalogType + "}}@\\\n" + body;
            var model = new Catalog
            {
                Title = "Store",
                Products = new List<Product> { new Product { Name = "Cheap" }, new Product { Name = "Pricey" } }
            };

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Catalog), model, FullCSharp,
                new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp });
            Assert.Equal(expected, dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The other direction of the same arm, and the one that pays for it: a definition whose model resolves to
        /// <c>System.Object</c> takes the call-site value's own static type as its body model. With no answer for an
        /// embedded expression the whole template left the precompiled tier; with one, the body is typed
        /// <c>string</c> and renders the engine's bytes.
        /// </summary>
        [Fact]
        public void AnObjectDefinitionBodyFedByEmbeddedCSharpPrecompiles()
        {
            const string key = "views/cs-object-body.heddle";
            var content = "@model(){{" + CatalogType + "}}@\\\n" +
                          "@%\n<frame(out:: string)>{{[@out(this)]}} :: object\n%@\n@frame(@model.Title){{[q]}}\n";
            var model = new Catalog { Title = "Store", Products = new List<Product>() };

            var (precompiled, dyn) = DifferentialHarness.Render(key, content, typeof(Catalog), model, FullCSharp,
                new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp });
            Assert.Equal("[[q]]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }
    }
}
