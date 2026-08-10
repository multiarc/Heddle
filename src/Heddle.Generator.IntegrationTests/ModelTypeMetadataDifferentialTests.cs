using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The <c>ModelType</c> item metadata as the typed fast path for a directive-less template. Without a model
    /// type, <c>this</c> operands and model-rooted native paths refuse and the whole template falls to the dynamic
    /// tier; the metadata closes exactly those refusals, so each lane renders on both tiers and byte-compares —
    /// "it precompiled" is not the claim.
    /// </summary>
    public class ModelTypeMetadataDifferentialTests
    {
        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";

        private static Catalog Sample() => new Catalog
        {
            Title = "T",
            Tags = new[] { "xy" },
            Products = new List<Product>
            {
                new Product { Name = "a" },
                new Product { Name = "b" }
            }
        };

        private static Dictionary<string, Dictionary<string, string>> Meta(string key) =>
            new Dictionary<string, Dictionary<string, string>>
            {
                [key] = new Dictionary<string, string>
                {
                    ["build_metadata.AdditionalFiles.ModelType"] = CatalogType
                }
            };

        /// <summary>The refusals the metadata closes, one lane each: a model-rooted native path, a
        /// <c>this</c> hop, a <c>this</c> operand, and a model-rooted operand — every one of which degrades the
        /// whole template when no model type exists.</summary>
        [Theory]
        [InlineData("model-rooted-path", "[@(Title)]", "[T]\n")]
        [InlineData("this-hop", "[@(this.Title)]", "[T]\n")]
        [InlineData("this-operand", "[@(this.Title + \"!\")]", "[T!]\n")]
        [InlineData("model-rooted-operand", "[@(Title == \"T\" ? 1 : 2)]", "[1]\n")]
        [InlineData("list-over-model", "@list(Products){{[@upper(this.Name)]}}", "[A][B]\n")]
        public void MetadataTypedTemplatesPrecompileAndRenderTheEnginesBytes(string name, string body,
            string expected)
        {
            var key = "modeltype-metadata/" + name + ".heddle";
            var (precompiled, dynamic) = DifferentialHarness.Render(key, body + "\n", typeof(Catalog), Sample(),
                perFileMetadata: Meta(key));

            Assert.Equal(expected, dynamic);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>The emission is the typed one, not a precompile that happens to route the path through the
        /// DLR: the entry point takes the metadata's type and the body carries no dynamic-evaluation helper.</summary>
        [Fact]
        public void MetadataTypingIsNativeEmissionNotDlrRouting()
        {
            const string key = "modeltype-metadata/native-emission.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, "[@(this.Title)] [@(Title)]\n") },
                perFileMetadata: Meta(key));

            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var source = Assert.Single(gen.TemplateSources.Values);
            Assert.Contains("public static string Generate(global::" + CatalogType + " model", source);
            Assert.Contains("__ModelType = typeof(global::" + CatalogType + ")", source);
            Assert.DoesNotContain("DynEval", source);
        }

        /// <summary>The control the closure is measured against: the same template without the metadata still has
        /// no model type, so the native paths refuse and the template degrades to the dynamic tier as before.</summary>
        [Fact]
        public void WithoutTheMetadataTheSameTemplateStillDegrades()
        {
            const string key = "modeltype-metadata/untyped-control.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, "[@(this.Title)]\n") });

            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>Directive and metadata resolving to one type render exactly as the directive alone does —
        /// agreement adds nothing and takes nothing.</summary>
        [Fact]
        public void AnAgreeingDirectiveAndMetadataRenderTheEnginesBytes()
        {
            const string key = "modeltype-metadata/agreeing.heddle";
            var content = "@model(){{" + CatalogType + "}}@\\\n[@(this.Title)]\n";
            var (precompiled, dynamic) = DifferentialHarness.Render(key, content, typeof(Catalog), Sample(),
                perFileMetadata: Meta(key));

            Assert.Equal("[T]\n", dynamic);
            Assert.Equal(dynamic, precompiled);
        }
    }
}
