extern alias generator;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Heddle.Helpers;
using Heddle.Language.Binding;
using Heddle.Native;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using SymbolFault = generator::Heddle.Language.Binding.TypeSpellingFault;
using SymbolIndex = generator::Heddle.Generator.Binding.SymbolTypeIndex;

/// <summary>A type with no namespace, so the leading-dot full-name key the two indexes both write has something
/// to answer.</summary>
public class LadderGlobalProbe
{
}

namespace Heddle.Generator.IntegrationTests.LadderAlpha
{
    /// <summary>Two types with one short name in two namespaces: the tie the ambiguity rule exists for.</summary>
    public class LadderProbe { }
}

namespace Heddle.Generator.IntegrationTests.LadderBeta
{
    public class LadderProbe { }
}

namespace Heddle.Generator.IntegrationTests.LadderNestAlpha
{
    /// <summary>Two hosts of one nested name, so the nested name alone answers to two types and the short-name
    /// index therefore answers to neither — without the pair, the <c>using static</c> arm could never be reached.</summary>
    public class LadderHost { public class LadderNested { } }
}

namespace Heddle.Generator.IntegrationTests.LadderNestBeta
{
    public class LadderHost { public class LadderNested { } }
}

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// One ladder, two maps. The alias → <c>using static</c> → import arms, their order, and the ambiguity rule live
    /// once, in shared <c>TypeNameIndex</c>; each tier supplies only an <c>ITypeNameMaps&lt;T&gt;</c> over its own
    /// type universe. This suite runs that ladder through <b>both</b> seams in one process and requires the same
    /// answer for every spelling.
    /// <para>The two universes are made equal by construction: the symbol side's references are exactly the
    /// assemblies the reflection side has loaded, taken in one read. So a row that disagrees is a disagreement
    /// between the two <i>maps</i> or a tier that stopped running the shared ladder — never a difference in what
    /// each side could see. That is the property the extraction bought, and the only one that keeps
    /// <c>@model Foo</c> meaning one thing on both tiers.</para>
    /// <para>The sibling suites <c>TypeSpellingLockstepTests</c> and <c>TypeSpellingSymbolLockstepTests</c> compare
    /// the two tiers across separate universes and pin the corpus itself; this one holds the seams against each
    /// other and needs no expectation column to do it.</para>
    /// </summary>
    public class TypeNameLadderSeamLockstepTests
    {
        private const string Alpha = "Heddle.Generator.IntegrationTests.LadderAlpha";
        private const string Beta = "Heddle.Generator.IntegrationTests.LadderBeta";
        private const string NestAlpha = "Heddle.Generator.IntegrationTests.LadderNestAlpha";
        private const string NestBeta = "Heddle.Generator.IntegrationTests.LadderNestBeta";

        /// <summary>Every spelling the ladder itself decides — no generic arguments, no array suffix and no tuple,
        /// because those are the shared spelling grammar's and are already pinned there.</summary>
        public static IEnumerable<object[]> Corpus()
        {
            // The predefined-type aliases: the one spelling neither index carries.
            yield return Row("int");
            yield return Row("string");
            yield return Row("dynamic");
            yield return Row("NoSuchTypeAnywhere");
            // No tier's grammar has a nullable suffix, so the name carrying one answers to nothing.
            yield return Row("int?");

            // The index arms: dotted, bare, and a bare name that is unique.
            yield return Row("System.String");
            yield return Row("LadderProbe");
            yield return Row("LadderProbe", Alpha);
            yield return Row("LadderProbe", Beta);
            yield return Row("LadderProbe", Alpha, Beta);
            yield return Row("LadderHost.LadderNested", NestAlpha);
            yield return Row("LadderNested", NestAlpha);
            yield return Row("LadderGlobalProbe");

            // `global::` bypasses every import and alias, in both directions.
            yield return Row("global::" + Alpha + ".LadderProbe");
            yield return Row("global::LadderGlobalProbe");
            yield return Row("global::LadderProbe", Alpha);
            yield return Row("global::X.LadderProbe", "X = " + Alpha);

            // The alias arm: a type alias, a namespace alias, a nested reach, an alias to a keyword, a self-
            // qualifying target, a duplicate name, and the commit rule in both its shapes.
            yield return Row("X", "X = " + Alpha + ".LadderProbe");
            yield return Row("X", "X = " + Alpha);
            yield return Row("X.LadderProbe", "X = " + Alpha);
            yield return Row("X.LadderNested", "X = " + NestAlpha + ".LadderHost");
            yield return Row("X", "X = int");
            yield return Row("X.LadderProbe", "X = global::" + Alpha);
            yield return Row("X", "X = " + Alpha + ".LadderProbe", "X = " + Beta + ".LadderProbe");
            yield return Row("LadderProbe", Alpha, "LadderProbe = " + Beta + ".LadderProbe");
            yield return Row("LadderProbe", "LadderProbe = " + Beta + ".LadderProbe", Alpha);
            yield return Row("LadderProbe", Alpha, "LadderProbe = Nope.LadderProbe");
            yield return Row("X.LadderProbe", Alpha, "X = Nope");
            yield return Row("LadderProbe", Alpha, "Other = " + Beta + ".LadderProbe");

            // An alias whose NAME is a predefined-type keyword. The two tiers answered this differently before the
            // ladder was shared — the build tier consulted its keyword table in front of the whole ladder, so the
            // directive that had claimed the name was never read. The alias binds the head first, on both sides.
            yield return Row("int", "int = " + Alpha + ".LadderProbe");
            yield return Row("dynamic", "dynamic = " + Alpha + ".LadderProbe");
            yield return Row("string", "string = Nope.Missing");

            // The `using static` arm, its ambiguity, and the control that the host's namespace being imported is a
            // different question.
            yield return Row("LadderNested", "static " + NestAlpha + ".LadderHost");
            yield return Row("LadderNested", "static " + NestAlpha + ".LadderHost", "static " + NestBeta + ".LadderHost");
            yield return Row("LadderNested", NestAlpha);

            // The assembly-qualified arm is the seam's own on both sides — a loader question here, a reference
            // question there — so the row that matters is that they still agree for an assembly both universes hold.
            yield return Row("Heddle.Attributes.HiddenAttribute, Heddle");
            yield return Row("Heddle.Attributes.HiddenAttribute, NoSuchAssembly");
        }

        private static object[] Row(string spelling, params string[] imports) => new object[] { spelling, imports };

        [Theory]
        [MemberData(nameof(Corpus))]
        public void BothSeamsAnswerTheSharedLadderIdentically(string spelling, string[] imports)
        {
            Assert.Equal(ThroughReflectionMaps(spelling, imports), ThroughSymbolMaps(spelling, imports));
        }

        private static string ThroughReflectionMaps(string spelling, string[] imports)
        {
            if (TypeNameIndex.TryResolve(spelling, imports, ReflectionHelper.NameMapsSnapshot(), out var type,
                    out var fault))
                return type.FullName ?? type.Name;
            return fault.ToString();
        }

        private static string ThroughSymbolMaps(string spelling, string[] imports)
        {
            if (SymbolIndex.Build(Universe.Value).TryResolve(spelling, imports, out var symbol, out SymbolFault fault))
                return MetadataFullName(symbol);
            return fault.ToString();
        }

        /// <summary>The reflection tier's <c>Type.FullName</c> spelling of a symbol: namespace, then the
        /// containment chain joined by <c>+</c>, then the metadata name with its arity tick.</summary>
        private static string MetadataFullName(INamedTypeSymbol symbol)
        {
            var chain = new StringBuilder(symbol.MetadataName);
            for (var outer = symbol.ContainingType; outer != null; outer = outer.ContainingType)
                chain.Insert(0, outer.MetadataName + "+");

            var ns = symbol.ContainingNamespace;
            return ns == null || ns.IsGlobalNamespace ? chain.ToString() : ns.ToDisplayString() + "." + chain;
        }

        /// <summary>
        /// A compilation whose references are exactly the assemblies the engine has loaded, read once. Building the
        /// symbol universe from anything else — the trusted-platform list, a hand-picked set — would let a row fail
        /// because one side could see a type the other could not, which is the one thing this suite must not be able
        /// to report.
        /// </summary>
        private static readonly Lazy<Compilation> Universe = new Lazy<Compilation>(BuildUniverse);

        private static Compilation BuildUniverse()
        {
            var references = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assembly in AssemblyHelper.GetAssemblies())
            {
                var location = LocationOf(assembly);
                if (location == null || !seen.Add(location))
                    continue;
                references.Add(MetadataReference.CreateFromFile(location));
            }

            return CSharpCompilation.Create("LadderSeamUniverse", Array.Empty<SyntaxTree>(), references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static string LocationOf(Assembly assembly)
        {
            try
            {
                var location = assembly.IsDynamic ? null : assembly.Location;
                return string.IsNullOrEmpty(location) || !File.Exists(location) ? null : location;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }
    }
}
