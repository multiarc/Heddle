using System;
using System.Collections.Generic;
using Heddle.Tool.Compile.Sites;
using Xunit;

namespace Heddle.Tool.Tests
{
    public class TypeNamePrinterTests
    {
        public class Outer<T>
        {
            public class Inner<U>
            {
            }
        }

        /// <summary>C# writes a jagged array's rank specifiers outermost first: an array of <c>int[,]</c> is
        /// <c>int[][,]</c>. Pins the regression where each level appended its own specifier to the element's
        /// spelling, which reverses them — harmless while every rank is the same, a different type when not.</summary>
        [Theory]
        [InlineData(typeof(int[]), "int[]")]
        [InlineData(typeof(int[][,]), "int[][,]")]
        [InlineData(typeof(int[,][]), "int[,][]")]
        [InlineData(typeof(string[][][]), "string[][][]")]
        [InlineData(typeof(int[][,][,,]), "int[][,][,,]")]
        [InlineData(typeof(int?[][,]), "int?[][,]")]
        [InlineData(typeof(List<int[,]>[]), "global::System.Collections.Generic.List<int[,]>[]")]
        [InlineData(typeof(List<int[][,]>[,][]), "global::System.Collections.Generic.List<int[][,]>[,][]")]
        [InlineData(typeof(Outer<int[]>.Inner<string[,]>[]),
            "global::Heddle.Tool.Tests.TypeNamePrinterTests.Outer<int[]>.Inner<string[,]>[]")]
        public void SpellsArraysAndGenericsTheWayCSharpReadsThemBack(Type type, string expected)
        {
            string spelling;
            string why;
            Assert.True(TypeNamePrinter.TrySpell(type, out spelling, out why), why);
            Assert.Equal(expected, spelling);

            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                "class Probe { static System.Type T = typeof(" + spelling + "); }",
                cancellationToken: TestContext.Current.CancellationToken);
            var references = new List<Microsoft.CodeAnalysis.MetadataReference>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) &&
                    System.IO.File.Exists(assembly.Location))
                    references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(assembly.Location));
            }

            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Probe_" + Guid.NewGuid().ToString("N"),
                new[] { tree }, references, new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                    Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            using (var image = new System.IO.MemoryStream())
            {
                var emit = compilation.Emit(image, cancellationToken: TestContext.Current.CancellationToken);
                Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
                var probe = System.Reflection.Assembly.Load(image.ToArray()).GetType("Probe");
                var field = probe.GetField("T", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.Equal(type, (Type)field.GetValue(null));
            }
        }
    }
}
