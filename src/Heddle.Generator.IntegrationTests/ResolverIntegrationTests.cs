using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI8 (D7/D8/D17): a host that registers a compiled manifest renders from the precompiled strategy —
    /// <see cref="TemplateResolver"/> consults <see cref="PrecompiledTemplates"/> before the cache/file probe and
    /// returns a <see cref="HeddleTemplate"/> in precompiled-adapter mode, byte-identical to the dynamic engine. A
    /// gauntlet failure falls back (Fallback) or throws (Strict).
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class ResolverIntegrationTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private static (System.Reflection.Assembly asm, string key, string content) BuildAndRegister(string tag)
        {
            var key = "views/wi8-" + tag + ".heddle";
            var content = "@model(){{" + CartType + "}}@\\\n" +
                          "Cart @(Name): count @(Count), total @(Price * Quantity).\n" +
                          "@if(IsFeatured){{ FEATURED }}\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.NotNull(gen.Assembly);
            PrecompiledTemplates.Register(gen.Assembly);
            return (gen.Assembly, key, content);
        }

        [Fact]
        public void ResolverServesPrecompiledAdapter_ByteIdenticalToDynamic()
        {
            var (_, key, content) = BuildAndRegister("serve");
            var model = new Cart { Name = "Basket", Count = 3, Price = 2.5m, Quantity = 4, IsFeatured = true };

            var resolver = new TemplateResolver(@"C:\nonexistent\root.marker", false);
            var template = resolver.GetTemplate(key, "", out _, null, TemplatePathType.None);
            Assert.NotNull(template);
            Assert.True(template.CompileResult.Success);
            var precompiled = template.Generate(model);

            var dyn = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), typeof(Cart))).Generate(model);
            Assert.Equal(dyn, precompiled);
        }

        [Fact]
        public void TryResolve_Miss_ReturnsFalse()
        {
            Assert.False(PrecompiledTemplates.TryResolve("views/never-registered-xyz.heddle",
                new TemplateOptions(), out var entry));
            Assert.Null(entry);
        }

        [Fact]
        public void TryResolve_OptionsMismatch_Fallback_ReturnsFalseAndFires()
        {
            var (_, key, _) = BuildAndRegister("fallback");
            PrecompiledFallbackEvent? captured = null;
            var prev = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.OnFallback = e => captured = e;
            try
            {
                // Manifest fingerprint is (Html, Native, true) under the 2.0 defaults; request Text → OptionsMismatch.
                var options = new TemplateOptions { OutputProfile = OutputProfile.Text };
                Assert.False(PrecompiledTemplates.TryResolve(key, options, out var entry));
                Assert.Null(entry);
                Assert.NotNull(captured);
                Assert.Equal(PrecompiledFallbackReason.OptionsMismatch, captured.Value.Reason);
            }
            finally
            {
                PrecompiledTemplates.OnFallback = prev;
            }
        }

        [Fact]
        public void TryResolve_OptionsMismatch_Strict_Throws()
        {
            var (_, key, _) = BuildAndRegister("strict");
            var options = new TemplateOptions
            {
                OutputProfile = OutputProfile.Text,
                PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict
            };
            var ex = Assert.Throws<PrecompiledMismatchException>(() =>
                PrecompiledTemplates.TryResolve(key, options, out _));
            Assert.Equal(PrecompiledFallbackReason.OptionsMismatch, ex.Reason);
            Assert.Contains("OutputProfile", ex.Detail);
        }
    }

    [CollectionDefinition("PrecompiledRegistry", DisableParallelization = true)]
    public class PrecompiledRegistryCollection { }
}
