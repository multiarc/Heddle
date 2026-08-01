using System.Collections.Generic;
using System.Linq;
using Heddle.Language.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The reading of a <c>@using</c> body that type resolution needs — is it a namespace import, an alias, or a
    /// static import — checked against the compiler that owns the grammar rather than against a recollection of it.
    /// <para>The engine's own classifier cannot call Roslyn: it sits on the type-resolution path, which a trimmed
    /// publish keeps after the whole <c>Microsoft.CodeAnalysis</c> graph has been linked away with the C# tier. So
    /// the grammar question is asked here instead, as a gate, over the spellings that separate the three forms —
    /// whitespace, a verbatim identifier, a <c>global::</c> target, a namespace whose first segment merely starts
    /// with the <c>static</c> keyword, and the malformed halves of each form.</para>
    /// <para>Only the two new classifications are asserted. Whether a body is a *valid* directive at all is a
    /// different question with an owner already — the engine answers it by compiling the body into the unit it
    /// builds for an embedded expression, and the build tier by compiling it into this compilation.</para>
    /// </summary>
    public class UsingDirectiveClassificationTests
    {
        public static IEnumerable<object[]> Bodies() => new[]
        {
            "System.Linq",
            "System",
            "global::System.Linq",
            "System . Linq",
            "X = System.Linq",
            "X=System.Linq",
            "   X   =   System.Linq   ",
            "@X = System.Linq",
            "X = global::System.Linq",
            "X = int",
            "static System.Math",
            "static  global::System.Math",
            "static\tSystem.Math",
            // Six letters and then more of the same identifier: a namespace, not the keyword.
            "staticSystem.Math",
            "static",
            "X =",
            "= System.Linq",
            "X.Y = System.Linq",
            "1 + 2",
            "Zork.Nope",
        }.Select(body => new object[] { body });

        [Theory]
        [MemberData(nameof(Bodies))]
        public void TheClassifierReadsABodyTheWayTheCompilerReadsIt(string body)
        {
            var source = "using " + body + ";";
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();
            var directive = root.Usings.Count == 1 && !tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)
                ? root.Usings[0]
                : null;

            var compilerSaysStatic = directive != null &&
                                     directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword);
            var compilerAliasName = compilerSaysStatic ? null : directive?.Alias?.Name.Identifier.ValueText;

            Assert.Equal(compilerSaysStatic, UsingDirectives.TryReadStaticTarget(body, out var staticTarget));
            if (compilerSaysStatic)
                Assert.Equal(directive.Name.ToString(), staticTarget);

            Assert.Equal(compilerAliasName != null, UsingDirectives.TryReadAlias(body, out var name, out var target));
            if (compilerAliasName != null)
            {
                Assert.Equal(compilerAliasName, name);
                // `Name` is null exactly where the target is not a name — `using X = int;`, whose target the
                // classifier hands to the keyword table instead. Asked through the typed property so this stays
                // readable on the oldest Roslyn any target framework here resolves.
                if (directive.Name != null)
                    Assert.Equal(directive.Name.ToString(), target);
                else
                    Assert.Equal("int", target);
            }
        }

        /// <summary>A name aliased twice to different targets is dropped rather than picked between: C# refuses the
        /// duplicate outright, and choosing by collection order is the order-dependent bind this resolver already
        /// ruled out for short names. Aliased twice to the <em>same</em> target there is nothing to choose.</summary>
        [Fact]
        public void ANameAliasedTwiceToDifferentTargetsBindsNothing()
        {
            var conflicting = UsingDirectives.Parse(new[] { "X = System.Linq", "X = System.Text" });
            Assert.False(conflicting.Aliases.ContainsKey("X"));

            var agreeing = UsingDirectives.Parse(new[] { "X = System.Linq", "X = System.Linq" });
            Assert.Equal("System.Linq", agreeing.Aliases["X"]);
        }

        /// <summary>A body list with neither form in it carries no directive structure at all, so the resolution
        /// arms that read one are skipped outright for every template that has only namespace imports.</summary>
        [Fact]
        public void PlainNamespaceBodiesProduceNoDirectives()
        {
            Assert.True(UsingDirectives.Parse(new[] { "System", "System.Linq", "Zork.Nope" }).IsEmpty);
            Assert.True(UsingDirectives.Parse(null).IsEmpty);
        }

        [Theory]
        [InlineData("global::System.Linq", true, "System.Linq")]
        [InlineData("global::A", true, "A")]
        [InlineData("System.Linq", false, "System.Linq")]
        [InlineData("global::", false, "global::")]
        [InlineData("globalSomething", false, "globalSomething")]
        public void TheGlobalQualifierComesOffOnlyWhenItIsThere(string spelling, bool stripped, string rest)
        {
            Assert.Equal(stripped, UsingDirectives.TryStripGlobalQualifier(spelling, out var actual));
            Assert.Equal(rest, actual);
        }
    }
}
