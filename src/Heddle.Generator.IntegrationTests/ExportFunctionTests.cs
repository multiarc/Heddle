using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 D21 / OQ1 — declaratively exported host functions bind <b>directly</b> to their discovered container
    /// (no shim, no runtime registry). The generator discovers <c>[ExportFunctions]</c> over the compilation's
    /// reference to this test assembly; the dynamic backend renders the same <c>MethodInfo</c> after a
    /// <c>RegisterFrom</c> at startup — differential-gated byte-for-byte.
    /// </summary>
    public class ExportFunctionTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        private static TemplateOptions OptionsWithExports()
        {
            var options = new TemplateOptions();
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(TemplateFunctions).Assembly);
            options.Functions = registry;
            return options;
        }

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model,
                runtimeOptions: OptionsWithExports());
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Products()
        {
            yield return new object[] { new Product { Name = "wonder widget" } };
            yield return new object[] { new Product { Name = "" } };
            yield return new object[] { new Product { Name = null } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void ExportedFunctionDirectBinding(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(titlecase(Name))</span>\n";
            AssertParity("views/label-export.heddle", t, typeof(Product), model);
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void ExportedAndDefaultFunctionsInOneTemplate(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(upper(Name)) - @(titlecase(Name)) - @(shout(Name))</span>\n";
            AssertParity("views/label-mixed.heddle", t, typeof(Product), model);
        }

        [Fact]
        public void ExportRowRecordedInManifest()
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(titlecase(Name))</span>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/label-export.heddle", t) });
            Assert.NotNull(gen.ManifestSource);
            Assert.Contains("titlecase", gen.ManifestSource);
            // Direct-bound to the discovered container as AQN sans version (not the shim target).
            Assert.Contains("Heddle.Generator.IntegrationTests.Fixtures.TemplateFunctions, Heddle.Generator.IntegrationTests",
                gen.ManifestSource);
        }
    }
}
