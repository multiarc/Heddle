using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Where two <c>@using</c> bodies both answer a spelling, which one wins is a C# name-lookup question, and it
    /// is asked <b>of the C# compiler</b> here rather than recalled. Each row compiles the same directives over the
    /// same type universe — this assembly, referenced by the probe compilation, so both resolvers see one set of
    /// types — and compares what Roslyn binds against what the runtime resolver binds.
    /// <para>This is the ordering gate. It says nothing about which spellings are reachable at all: the runtime
    /// indexes every type of every loaded assembly under its short name and C# does not, so rows are chosen where
    /// an alias, an import or a <c>static</c> target is what decides the answer.</para>
    /// </summary>
    public class NameLookupPrecedenceOracleTests
    {
        public static IEnumerable<object[]> Rows() => new[]
        {
            // An alias and an imported namespace both answer the simple name.
            new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha", "TieProbe = Heddle.Tests.TieBeta.TieProbe" } },
            // ...and declaration order does not decide it.
            new object[] { "TieProbe", new[] { "TieProbe = Heddle.Tests.TieBeta.TieProbe", "Heddle.Tests.TieAlpha" } },
            // An alias against two imports that would otherwise tie.
            new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha", "Heddle.Tests.TieBeta",
                "TieProbe = Heddle.Tests.TieBeta.TieProbe" } },
            // The tie itself, with no alias to settle it.
            new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha", "Heddle.Tests.TieBeta" } },
            // One import, no alias.
            new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha" } },
            // An alias whose name is not the head: the import still answers.
            new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha", "Other = Heddle.Tests.TieBeta.TieProbe" } },
            // A namespace alias qualifying a type.
            new object[] { "X.TieProbe", new[] { "X = Heddle.Tests.TieAlpha" } },
            new object[] { "X.TieProbe", new[] { "Heddle.Tests.TieBeta", "X = Heddle.Tests.TieAlpha" } },
            // Claiming the head commits: neither resolver may fall back to the import.
            new object[] { "TieProbe", new[] { "Heddle.Tests.TieAlpha", "TieProbe = Heddle.Tests.NoSuchNs.TieProbe" } },
            new object[] { "X.TieProbe", new[] { "Heddle.Tests.TieAlpha", "X = Heddle.Tests.NoSuchNs" } },
            // `global::` is read past every alias.
            new object[] { "global::Heddle.Tests.TieAlpha.TieProbe", new[] { "X = Heddle.Tests.TieBeta" } },
            // An alias against a `using static` target contributing the same name.
            new object[] { "AliasNested", new[] { "static Heddle.Tests.AliasNestAlpha.AliasHost",
                "AliasNested = Heddle.Tests.TieBeta.TieProbe" } },
            // The `static` target alone.
            new object[] { "AliasNested", new[] { "static Heddle.Tests.AliasNestAlpha.AliasHost" } },
            // Two `static` targets contributing one name.
            new object[] { "AliasNested", new[] { "static Heddle.Tests.AliasNestAlpha.AliasHost",
                "static Heddle.Tests.AliasNestBeta.AliasHost" } },

            // What a `using` namespace directive imports: the types DECLARED in the namespace, and not the
            // namespaces nested inside it. Both halves are here, because the rule is only pinned by the pair —
            // a nested TYPE keeps its outer type's namespace and stays reachable, a nested NAMESPACE does not.
            new object[] { "TieAlpha.TieProbe", new[] { "Heddle.Tests" } },
            new object[] { "AliasNestAlpha.AliasHost", new[] { "Heddle.Tests" } },
            new object[] { "AliasNestAlpha.AliasHost.AliasNested", new[] { "Heddle.Tests" } },
            new object[] { "Tests.TieAlpha.TieProbe", new[] { "Heddle" } },
            new object[] { "Text.StringBuilder", new[] { "System" } },
            new object[] { "AliasHost.AliasNested", new[] { "Heddle.Tests.AliasNestAlpha" } },
            // A namespace ALIAS names the namespace itself, so it does reach what is nested inside it.
            new object[] { "X.TieAlpha.TieProbe", new[] { "X = Heddle.Tests" } },
            // Neither import nor alias is needed for a spelling that is already complete.
            new object[] { "Heddle.Tests.TieAlpha.TieProbe", new string[0] },
        }.Select(r => r).ToArray();

        [Theory]
        [MemberData(nameof(Rows))]
        public void TheRuntimeResolverBindsWhatTheCSharpCompilerBinds(string spelling, string[] usings)
        {
            Assert.Equal(CSharpVerdict(spelling, usings), RuntimeVerdict(spelling, usings));
        }

        /// <summary>What the C# compiler binds the spelling to, given the same directives.</summary>
        private static string CSharpVerdict(string spelling, string[] usings)
        {
            var source = string.Join("\n", usings.Select(u => "using " + u + ";")) +
                         "\nclass Oracle { " + spelling + " Field; }\n";

            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
                .ToList();

            var tree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create("oracle", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var model = compilation.GetSemanticModel(tree);

            var typeSyntax = tree.GetCompilationUnitRoot()
                .DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Single().Declaration.Type;

            var info = model.GetSymbolInfo(typeSyntax);
            if (info.Symbol is INamedTypeSymbol bound)
                return Canonical(bound);
            if (info.CandidateReason == CandidateReason.Ambiguous)
                return "AMBIGUOUS";
            return "UNRESOLVED";
        }

        /// <summary>What the runtime's reflection resolver binds it to.</summary>
        private static string RuntimeVerdict(string spelling, string[] usings)
        {
            try
            {
                var type = ReflectionHelper.ResolveType(spelling, usings);
                // A nested type is `Outer+Inner` in metadata and `Outer.Inner` in a C# spelling.
                return type == null ? "UNRESOLVED" : type.ToString().Replace('+', '.');
            }
            catch (InvalidOperationException e)
            {
                return e.Message.Contains("ambigous") ? "AMBIGUOUS" : "UNRESOLVED";
            }
        }

        private static string Canonical(INamedTypeSymbol symbol) =>
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty);
    }
}
