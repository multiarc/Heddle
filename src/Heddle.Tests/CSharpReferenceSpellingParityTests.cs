using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Helpers;
#if NET10_0_OR_GREATER
using Heddle.Tool.Compile.Sites;
#endif
using Xunit;

namespace Heddle.Tests.@namespace
{
    public class @class
    {
        public class @int
        {
        }
    }

    public class Box<T>
    {
        public class @in<U>
        {
        }
    }
}

namespace Heddle.Tests
{
    /// <summary>The engine's C# tier and the build's generated sites both write a type as C# source. Pins that
    /// they write it the same way — one escaping of keyword identifiers, one treatment of nested and generic
    /// names — over one table, so the two cannot drift: the regression was a namespace or type named like a
    /// keyword, which the engine escaped and the build did not, leaving generated source that does not parse.
    /// The build's printer ships for one framework; the engine's half runs on all of them.</summary>
    public class CSharpReferenceSpellingParityTests
    {
        private const string Root = "global::Heddle.Tests.@namespace.";

        public static IEnumerable<object[]> Types()
        {
            yield return new object[] { typeof(@namespace.@class), Root + "@class" };
            yield return new object[] { typeof(@namespace.@class.@int), Root + "@class.@int" };
            yield return new object[]
            {
                typeof(@namespace.Box<@namespace.@class>.@in<@namespace.@class.@int>),
                Root + "Box<" + Root + "@class>.@in<" + Root + "@class.@int>"
            };
            yield return new object[] { typeof(@namespace.@class[][,]), Root + "@class[][,]" };
            yield return new object[]
            {
                typeof(Dictionary<Guid, List<@namespace.@class>>),
                "global::System.Collections.Generic.Dictionary<global::System.Guid, global::System.Collections.Generic.List<" +
                Root + "@class>>"
            };
            yield return new object[] { typeof(Uri), "global::System.Uri" };
        }

        public static IEnumerable<object[]> AliasedTypes()
        {
            yield return new object[] { typeof(int) };
            yield return new object[] { typeof(string) };
            yield return new object[] { typeof(object) };
            yield return new object[] { typeof(int?) };
            yield return new object[] { typeof(decimal?) };
            yield return new object[] { typeof(Guid?) };
            yield return new object[] { typeof(List<int?>) };
            yield return new object[] { typeof(Dictionary<string, long?>[]) };
            yield return new object[] { typeof(@namespace.Box<int?>.@in<string[]>) };
        }

#if NET10_0_OR_GREATER
        /// <summary>Where the build writes an alias or <c>T?</c> and the engine writes the full name, the text
        /// differs by design; what has to hold is that a C# compiler reads both as one type.</summary>
        [Theory]
        [MemberData(nameof(AliasedTypes))]
        public void WhereTheTwoSpellingsDifferTheyDenoteTheSameType(Type type)
        {
            Assert.True(TypeNamePrinter.TrySpell(type, out var printed, out var why), why);
            string engine = TypeNameHelper.GetCSharpReference(type);

            var references = new List<Microsoft.CodeAnalysis.MetadataReference>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && System.IO.File.Exists(assembly.Location))
                    references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(assembly.Location));
            }

            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                "class P { static " + printed + " a; static " + engine + " b; }");
            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("SpellingProbe", new[] { tree },
                references, new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                    Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            var errors = compilation.GetDiagnostics()
                .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
            Assert.True(errors.Count == 0, printed + " / " + engine + "\n" + string.Join("\n", errors));

            var probe = compilation.GetTypeByMetadataName("P");
            var a = ((Microsoft.CodeAnalysis.IFieldSymbol) probe.GetMembers("a").Single()).Type;
            var b = ((Microsoft.CodeAnalysis.IFieldSymbol) probe.GetMembers("b").Single()).Type;
            Assert.True(Microsoft.CodeAnalysis.SymbolEqualityComparer.Default.Equals(a, b),
                "'" + printed + "' and '" + engine + "' are different types to the compiler.");
        }
#endif

        [Theory]
        [MemberData(nameof(Types))]
        public void TheBuildAndTheEngineSpellATypeReferenceAlike(Type type, string expected)
        {
            Assert.Equal(expected, TypeNameHelper.GetCSharpReference(type));
#if NET10_0_OR_GREATER
            Assert.True(TypeNamePrinter.TrySpell(type, out var printed, out var why), why);
            Assert.Equal(expected, printed);
#endif
        }
    }
}
