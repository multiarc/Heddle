using System.IO;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Flagship fixtures rendered against committed goldens, plus the zero-Roslyn proof (success criterion 1):
    /// after compiling native-expression fixtures, no in-memory C# assembly is produced and no C# method is
    /// queued.
    /// </summary>
    public class NativeExpressionGoldenTests
    {
        public class Inner { public int B { get; set; } }

        public class FlagshipModel
        {
            public decimal Price { get; set; }
            public int Quantity { get; set; }
            public string Name { get; set; }
            public bool IsFeatured { get; set; }
            public int Total { get; set; }
            public int Amount { get; set; }
            public int Count { get; set; }
            public Inner A { get; set; }
        }

        public class FunctionsModel
        {
            public string Name { get; set; }
            public string Padded { get; set; }
            public int Negative { get; set; }
            public int A { get; set; }
            public int B { get; set; }
            public double Ratio { get; set; }
        }

        private static string RenderFixture(string name, object model)
        {
            HeddleTemplate.Configure(typeof(NativeExpressionGoldenTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText($"TestTemplate/{name}.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document, new CompileContext(new TemplateOptions(), model.GetType()));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        private static void AssertGolden(string name, string actual)
        {
            File.WriteAllText($"TestTemplate/test-{name}.html", actual);
            var expected = File.ReadAllText($"TestTemplate/generated-{name}.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FlagshipFixture()
        {
            var model = new FlagshipModel
            {
                Price = 2.5m,
                Quantity = 4,
                Name = null,
                IsFeatured = true,
                Total = 100,
                Amount = 30,
                Count = 3,
                A = null
            };
            AssertGolden("expr-flagship", RenderFixture("expr-flagship", model));
        }

        [Fact]
        public void FunctionsFixture()
        {
            var model = new FunctionsModel
            {
                Name = "Hello",
                Padded = "  spaced  ",
                Negative = -5,
                A = 3,
                B = 7,
                Ratio = 3.14159
            };
            AssertGolden("expr-functions", RenderFixture("expr-functions", model));
        }

        [Fact]
        public void FlagshipCompilesWithoutRoslyn()
        {
            var model = new FlagshipModel { Name = "x", A = new Inner() };
            using var scope = new CompileScope(new CompileContext(new TemplateOptions(), typeof(FlagshipModel)));
            var document = File.ReadAllText("TestTemplate/expr-flagship.heddle").Replace("\r\n", "\n");
            var parseContext = Heddle.Language.DocumentParser.Parse(document, scope.CompileContext, out var clean);
            HeddleCompiler.Compile(clean, scope, parseContext, null);
            Assert.Empty(scope.CompileErrors);
            Assert.Empty(scope.CSharpContext.Methods);
            Assert.Null(scope.CSharpContext.CompiledAssembly);
        }

        [Fact]
        public void FunctionsCompilesWithoutRoslyn()
        {
            using var scope = new CompileScope(new CompileContext(new TemplateOptions(), typeof(FunctionsModel)));
            var document = File.ReadAllText("TestTemplate/expr-functions.heddle").Replace("\r\n", "\n");
            var parseContext = Heddle.Language.DocumentParser.Parse(document, scope.CompileContext, out var clean);
            HeddleCompiler.Compile(clean, scope, parseContext, null);
            Assert.Empty(scope.CompileErrors);
            Assert.Empty(scope.CSharpContext.Methods);
            Assert.Null(scope.CSharpContext.CompiledAssembly);
        }
    }
}
