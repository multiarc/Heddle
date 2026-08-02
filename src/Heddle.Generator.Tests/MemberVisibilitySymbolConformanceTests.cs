using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Validates symbol-level member-visibility against the shared <c>MemberVisibility</c> decision table.
    /// Same corpus as run-tier tests prevents divergent policies.
    /// </summary>
    public class MemberVisibilitySymbolConformanceTests
    {
        private const string Source = @"
namespace Foreign
{
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class HiddenAttribute : System.Attribute { }
}

namespace Probe
{
    public interface IBaseFacet { string FromBaseInterface { get; } }
    public interface IDerivedFacet : IBaseFacet { string FromDerivedInterface { get; } }

    public class VisibilityBase
    {
        public string PublicOnBase { get; set; }
        internal string InternalOnBase { get; set; }
        protected string ProtectedOnBase { get; set; }
        public virtual string Shadowed { get; set; }
    }

    public class VisibilityModel : VisibilityBase
    {
        public string PublicHere { get; set; }
        internal string InternalHere { get; set; }
        protected internal string ProtectedInternalHere { get; set; }
        protected string ProtectedHere { get; set; }
        private string PrivateHere { get; set; }
        public string WriteOnly { set { } }
        [Heddle.Attributes.Hidden] public string HiddenHere { get; set; }
        [Foreign.Hidden] public string ForeignHiddenHere { get; set; }
        public static string StaticHere { get; set; }
        public new string Shadowed { get; set; }
        public string PublicWithPrivateGetter { private get; set; }
        public string Unused => PrivateHere;
    }
}";

        private static readonly (SymbolTypeResolver Resolver, INamedTypeSymbol Model, INamedTypeSymbol DerivedFacet)
            Probe = Build();

        private static (SymbolTypeResolver, INamedTypeSymbol, INamedTypeSymbol) Build()
        {
            var tpa = Heddle.Generator.Tests.HostAssemblies.TrustedOrLoaded();
            var references = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
            references.Add(MetadataReference.CreateFromFile(
                typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly.Location));

            var compilation = CSharpCompilation.Create("MemberVisibilityProbe",
                new[] { CSharpSyntaxTree.ParseText(Source) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errors);

            return (new SymbolTypeResolver(compilation),
                compilation.GetTypeByMetadataName("Probe.VisibilityModel"),
                compilation.GetTypeByMetadataName("Probe.IDerivedFacet"));
        }

        public static IEnumerable<object[]> Rows()
        {
            yield return new object[] { "PublicHere", true, "public getter on the receiver" };
            yield return new object[] { "InternalHere", true, "internal getter on the receiver" };
            yield return new object[] { "PublicOnBase", true, "public getter inherited from a base class" };
            yield return new object[] { "InternalOnBase", false,
                "inherited non-public members are not surfaced (Type.GetProperty's behavior, runtime-normative)" };
            yield return new object[] { "ProtectedOnBase", false, "protected is outside the sandbox" };
            yield return new object[] { "ProtectedInternalHere", false,
                "the generator used to accept ProtectedOrInternal; the runtime rejects it" };
            yield return new object[] { "ProtectedHere", false, "protected is outside the sandbox" };
            yield return new object[] { "PrivateHere", false, "private is outside the sandbox" };
            yield return new object[] { "WriteOnly", false, "not readable" };
            yield return new object[] { "HiddenHere", false, "[Hidden] by the real attribute type" };
            yield return new object[] { "ForeignHiddenHere", true,
                "a foreign *.HiddenAttribute hides nothing — the match is on the full metadata name" };
            yield return new object[] { "StaticHere", false, "statics are not member-path reachable" };
            yield return new object[] { "Shadowed", true, "new-shadowed: the most-derived accessible one" };
            yield return new object[] { "PublicWithPrivateGetter", false, "accessibility is the getter's" };
            yield return new object[] { "Missing", false, "no such member" };
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void SymbolAdapterMatchesTheConformanceRow(string member, bool accessible, string why)
        {
            var resolution = Probe.Resolver.ResolvePath(Probe.Model, new[] { member });
            Assert.True(accessible == (resolution.Kind == SymbolTypeResolver.PathKind.Resolved), why);
        }

        [Fact]
        public void BaseInterfaceMembersAreNotSurfacedFromAnInterfaceRoot()
        {
            // Symbol build (FindProperty) walks AllInterfaces; runtime Type.GetProperty does not.
            Assert.Equal(SymbolTypeResolver.PathKind.Resolved,
                Probe.Resolver.ResolvePath(Probe.DerivedFacet, new[] { "FromDerivedInterface" }).Kind);
            Assert.Equal(SymbolTypeResolver.PathKind.Failed,
                Probe.Resolver.ResolvePath(Probe.DerivedFacet, new[] { "FromBaseInterface" }).Kind);
        }
    }
}
