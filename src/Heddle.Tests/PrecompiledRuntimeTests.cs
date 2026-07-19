using Heddle.Core;
using Heddle.Data;
using Heddle.Extensions;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 7 D5/D20 (WI4 runtime side): the <see cref="PrecompiledRuntime"/> render funnel. Hand-built strategies
    /// reproducing the generated-code shape (example 1: static pieces + a bound <c>EmptyExtension</c> over member
    /// paths, per-hop null-safe) must render <b>byte-identical</b> to the dynamic engine — the differential contract
    /// the generator's emitter will target.
    /// </summary>
    public class PrecompiledRuntimeTests
    {
        public sealed class Nested
        {
            public string Name { get; set; }
        }

        public sealed class Product
        {
            public string Name { get; set; }
            public Nested Manufacturer { get; set; }
        }

        // <h1>@(Name)</h1>
        private sealed class ArticleStrategy : IProcessStrategy
        {
            private static readonly EmptyExtension E0 =
                PrecompiledRuntime.Bind(new EmptyExtension(), null, RenderType.Raw, false, 1, 5);

            public void Render(in Scope scope)
            {
                var m = (Product)scope.ModelData;
                scope.Renderer.Render("<h1>");
                E0.RenderData(scope.Model((object)(m?.Name)));
                scope.Renderer.Render("</h1>");
            }

            public string Execute(in Scope scope)
            {
                var m = (Product)scope.ModelData;
                var v0 = E0.ProcessData(scope.Model((object)(m?.Name))) as string ?? string.Empty;
                return string.Concat("<h1>", v0, "</h1>");
            }
        }

        // Made by @(Manufacturer.Name)
        private sealed class NestedPathStrategy : IProcessStrategy
        {
            private static readonly EmptyExtension E0 =
                PrecompiledRuntime.Bind(new EmptyExtension(), null, RenderType.Raw, false, 1, 9);

            public void Render(in Scope scope)
            {
                var m = (Product)scope.ModelData;
                scope.Renderer.Render("Made by ");
                E0.RenderData(scope.Model((object)(m?.Manufacturer?.Name)));
            }

            public string Execute(in Scope scope)
            {
                var m = (Product)scope.ModelData;
                var v0 = E0.ProcessData(scope.Model((object)(m?.Manufacturer?.Name))) as string ?? string.Empty;
                return string.Concat("Made by ", v0);
            }
        }

        private static HeddleTemplate Compile(string template)
        {
            var t = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), typeof(Product)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        public static TheoryData<Product> Models => new TheoryData<Product>
        {
            new Product { Name = "Widget", Manufacturer = new Nested { Name = "Acme" } },
            new Product { Name = null, Manufacturer = null },
            new Product { Name = "X", Manufacturer = new Nested { Name = null } },
            null,
        };

        [Theory]
        [MemberData(nameof(Models))]
        public void MemberPathMatchesDynamic(Product model)
        {
            var dynamicTemplate = Compile("<h1>@(Name)</h1>");
            var dynamic = dynamicTemplate.Generate(model);
            var precompiled = PrecompiledRuntime.GenerateString(new ArticleStrategy(), model, null, null);
            Assert.Equal(dynamic, precompiled);
        }

        [Theory]
        [MemberData(nameof(Models))]
        public void NestedMemberPathMatchesDynamic(Product model)
        {
            var dynamicTemplate = Compile("Made by @(Manufacturer.Name)");
            var dynamic = dynamicTemplate.Generate(model);
            var precompiled = PrecompiledRuntime.GenerateString(new NestedPathStrategy(), model, null, null);
            Assert.Equal(dynamic, precompiled);
        }

        [Fact]
        public void BindReturnsSameInstanceAndSetsPosition()
        {
            var ext = new EmptyExtension();
            var bound = PrecompiledRuntime.Bind(ext, null, RenderType.Raw, false, 3, 7);
            Assert.Same(ext, bound);
            Assert.Equal(3, bound.Position.StartIndex);
        }

        [Fact]
        public void PropReadIsBoundsSafe()
        {
            // No PropsData on a plain scope -> null, never throws.
            var renderer = new ScopeRenderer(16);
            var scope = new Scope(null, null, null, null, renderer, null, null);
            Assert.Null(PrecompiledRuntime.Prop(scope, 0));
            Assert.Null(PrecompiledRuntime.RootModel(scope));
        }
    }
}
