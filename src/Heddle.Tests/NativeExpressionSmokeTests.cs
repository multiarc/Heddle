using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    public class NativeExpressionSmokeTests
    {
        public class Model
        {
            public int Count { get; set; }
            public decimal Price { get; set; }
            public int Quantity { get; set; }
            public string Name { get; set; }
            public bool IsFeatured { get; set; }
            public Inner A { get; set; }
        }

        public class Inner
        {
            public int B { get; set; }
        }

        private static string Render(string template, object model, Assembly configure = null)
        {
            HeddleTemplate.Configure(typeof(NativeExpressionSmokeTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), model?.GetType() ?? typeof(Model)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Fact]
        public void Multiplication()
        {
            Assert.Equal("10.0", Render("@(Price * Quantity)", new Model { Price = 2.5m, Quantity = 4 }));
        }

        [Fact]
        public void Coalesce()
        {
            Assert.Equal("anon", Render("@(Name ?? \"anon\")", new Model { Name = null }));
        }

        [Fact]
        public void Ternary()
        {
            Assert.Equal("★", Render("@(IsFeatured ? \"★\" : \"\")", new Model { IsFeatured = true }));
        }

        [Fact]
        public void IfComparison()
        {
            Assert.Equal("has items", Render("@if(Count > 0){{has items}}", new Model { Count = 1 }));
        }

        [Fact]
        public void NullSafeHop()
        {
            Assert.Equal("", Render("@if(A.B > 0){{x}}", new Model { A = null }));
        }

        [Fact]
        public void Precedence()
        {
            // A + B * C == D && !E with ints/bools computed against C#.
            var model = new PrecedenceModel { A = 1, B = 2, C = 3, D = 7, E = false };
            // 1 + 2*3 == 7 && !false => 7==7 && true => true
            Assert.Equal("yes", Render("@if(A + B * C == D && !E){{yes}}", model));
        }

        public class PrecedenceModel
        {
            public int A { get; set; }
            public int B { get; set; }
            public int C { get; set; }
            public int D { get; set; }
            public bool E { get; set; }
        }

        [Fact]
        public void RegistryMaxInsideExpression()
        {
            Assert.Equal("7", Render("@(max(A, B))", new MaxModel { A = 3, B = 7 }));
        }

        public class MaxModel
        {
            public int A { get; set; }
            public int B { get; set; }
        }

        [Fact]
        public void StandaloneFunctionCall()
        {
            Assert.Equal("HELLO", Render("@upper(Name)", new Model { Name = "hello" }));
        }
    }
}
