extern alias gen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AssignabilityCorpus = gen::Heddle.Language.Binding.AssignabilityCorpus;
using SymbolTypeFacts = gen::Heddle.Generator.Binding.SymbolTypeFacts;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Phase 3 (F6) — the <b>symbol-side</b> driver of the shared assignability conformance corpus. Same data file
    /// as the reflection-side driver in <c>Heddle.Tests</c>; this one asserts that the Roslyn
    /// <c>ITypeFacts</c> adapter's two nullable corrections land it on the CLR's answer, row for row.
    /// <para>Spellings are resolved through a probe compilation's <c>typeof</c> expressions, so the corpus can use
    /// any legal C# type syntax (generics, arrays, tuple syntax) without this test growing a second parser.</para>
    /// </summary>
    public class AssignabilityCorpusSymbolTests
    {
        private static readonly (SymbolTypeFacts Facts, IReadOnlyDictionary<string, ITypeSymbol> Types) Probe =
            BuildProbe();

        public static IEnumerable<object[]> Rows()
        {
            foreach (var row in AssignabilityCorpus.Rows)
                yield return new object[] { row.Source, row.Target, row.Expected, row.Family };
        }

        private static (SymbolTypeFacts, IReadOnlyDictionary<string, ITypeSymbol>) BuildProbe()
        {
            var spellings = new List<string>();
            foreach (var row in AssignabilityCorpus.Rows)
            {
                if (!spellings.Contains(row.Source)) spellings.Add(row.Source);
                if (!spellings.Contains(row.Target)) spellings.Add(row.Target);
            }

            var source = "class CorpusProbe { static readonly System.Type[] T = new System.Type[] { " +
                         string.Join(", ", spellings.Select(s => "typeof(" + s + ")")) + " }; }";

            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var references = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();

            var tree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create("AssignabilityCorpusProbe", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var errors = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errors);

            var model = compilation.GetSemanticModel(tree);
            var map = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
            var typeOfs = tree.GetRoot().DescendantNodes().OfType<TypeOfExpressionSyntax>().ToList();
            for (int i = 0; i < spellings.Count; i++)
                map[spellings[i]] = model.GetTypeInfo(typeOfs[i].Type).Type;

            return (new SymbolTypeFacts(compilation), map);
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void RoslynAdapterMatchesTheCorpus(string source, string target, bool expected, string family)
        {
            var sourceType = Probe.Types[source];
            var targetType = Probe.Types[target];
            Assert.NotNull(sourceType);
            Assert.NotNull(targetType);

            Assert.True(Probe.Facts.IsAssignableFrom(targetType, sourceType) == expected,
                $"{family}: {source} -> {target}");
        }

        [Fact]
        public void TheTwoNullableCorrectionsAreWhatRoslynAloneWouldGetWrong()
        {
            // Documents *why* the adapter is not a bare ClassifyConversion call: the raw classification disagrees
            // with the CLR in both directions on exactly these two rows.
            var csharp = (CSharpCompilation) Probe.Facts.Compilation;
            var intType = Probe.Types["System.Int32"];
            var nullableInt = Probe.Types["System.Nullable<System.Int32>"];
            var comparable = Probe.Types["System.IComparable"];

            var aToNullable = csharp.ClassifyConversion(intType, nullableInt);
            Assert.True(aToNullable.IsNullable);              // "ImplicitNullable" — neither reference nor boxing
            Assert.False(aToNullable.IsReference);
            Assert.False(aToNullable.IsBoxing);
            Assert.True(Probe.Facts.IsAssignableFrom(nullableInt, intType));   // …but the CLR says true

            var cToInterface = csharp.ClassifyConversion(nullableInt, comparable);
            Assert.True(cToInterface.IsBoxing);                                // Roslyn: boxing
            Assert.False(Probe.Facts.IsAssignableFrom(comparable, nullableInt)); // …but the CLR says false
        }
    }
}
