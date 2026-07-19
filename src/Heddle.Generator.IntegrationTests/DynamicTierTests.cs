using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 dynamic-tier family (generated-code.md example 4, body model-typing rule): a model-less template's
    /// root body is emitted on the C# <c>dynamic</c> tier (<c>((dynamic)m).A?.B</c>) while the runtime backend types
    /// the same root concretely from the passed model. The C# runtime binder resolves the same members against the
    /// same runtime types, so rendered bytes are identical — the differential is the gate.
    /// </summary>
    public class DynamicTierTests
    {
        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> ProductModels()
        {
            yield return new object[] { new Product { Name = "Widget", Manufacturer = new Manufacturer { Name = "Acme", Address = new Address { City = "Springfield" } } } };
            yield return new object[] { new Product { Name = null, Manufacturer = null } };
            yield return new object[] { new Product { Name = "X", Manufacturer = new Manufacturer { Name = null, Address = null } } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(ProductModels))]
        public void DynamicRootSingleHop(Product model)
        {
            var t = "<h1>@(Name)</h1>\n";
            AssertParity("views/dyn-single.heddle", t, typeof(Product), model);
        }

        [Theory]
        [MemberData(nameof(ProductModels))]
        public void DynamicRootMultiHop(Product model)
        {
            var t = "<article>\n  <h1>@(Name)</h1>\n  <p>Made by @(Manufacturer.Name)</p>\n</article>\n";
            AssertParity("views/dyn-multi.heddle", t, typeof(Product), model);
        }

        [Theory]
        [MemberData(nameof(ProductModels))]
        public void DynamicRootDeepHop(Product model)
        {
            var t = "City: @(Manufacturer.Address.City); name @(Name).\n";
            AssertParity("views/dyn-deep.heddle", t, typeof(Product), model);
        }

        [Theory]
        [MemberData(nameof(ProductModels))]
        public void DynamicRootExplicitDynamicDeclaration(Product model)
        {
            var t = "@model(){{dynamic}}@\\\n<h1>@(Name)</h1> by @(Manufacturer.Name)\n";
            AssertParity("views/dyn-decl.heddle", t, typeof(Product), model);
        }
    }
}
