extern alias gen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
// The build tier's own linked copies of the shared encoding rules (both assemblies declare them).
using RenderType = gen::Heddle.Data.RenderType;
using RenderTypeRules = gen::Heddle.Data.RenderTypeRules;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// [EncodeOutput] + [NotEncode] is not declarable (CS0592, different attribute targets).
    /// Pair observable only in forged metadata, where both tiers agree (RenderType.Raw).
    /// </summary>
    public class ContradictoryEncodingAttributeTests
    {
        /// <summary>The contradiction, written the way an extension author would write it. It is not valid C#.</summary>
        private const string ContradictorySource = @"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

[assembly: ExportExtensions]

namespace Contradiction
{
    [ExtensionName(""contradiction"")]
    [EncodeOutput]
    [NotEncode]
    public class ContradictoryExtension : AbstractExtension
    {
        public override void RenderData(in Scope scope) { }
        public override object ProcessData(in Scope scope) => null;
    }
}";

        /// <summary>The same declaration with the impossible attribute removed — the shape that does compile,
        /// so the CS0592 below is attributable to <c>[NotEncode]</c> and to nothing else about the fixture.</summary>
        private const string EncodeOnlySource = @"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

[assembly: ExportExtensions]

namespace Contradiction
{
    [ExtensionName(""contradiction"")]
    [EncodeOutput]
    public class ContradictoryExtension : AbstractExtension
    {
        public override void RenderData(in Scope scope) { }
        public override object ProcessData(in Scope scope) => null;
    }
}";

        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                // Heddle.Generator is analyzer-only; linked copies create CS0433 ambiguity with Heddle.dll.
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
            refs.Add(MetadataReference.CreateFromFile(
                typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly.Location));
            return refs;
        }

        private static CSharpCompilation Compile(string source) =>
            CSharpCompilation.Create("ContradictionProbe",
                new[] { CSharpSyntaxTree.ParseText(source) }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        /// <summary>CS0592 is the only error; removing [NotEncode] makes the declaration compile.</summary>
        [Fact]
        public void TheContradictoryDeclarationIsAlreadyACSharpCompilerError()
        {
            var errors = Compile(ContradictorySource).GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            var only = Assert.Single(errors);
            Assert.Equal("CS0592", only.Id);
            Assert.Contains("NotEncode", only.GetMessage());

            Assert.Empty(Compile(EncodeOnlySource).GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error));
        }

        /// <summary>Binder sees the pair only in failing compilations (CS0592); no use-site error is possible
        /// and both tiers agree on the verdict.</summary>
        [Fact]
        public void TheBinderOnlySeesThePairInAnAlreadyFailingCompilation()
        {
            var contradictory = Compile(ContradictorySource);
            Assert.Contains(contradictory.GetDiagnostics(),
                d => d.Id == "CS0592" && d.Severity == DiagnosticSeverity.Error);

            Assert.True(ExtensionBinder.Build(contradictory).TryResolve("contradiction", out var info));
            Assert.True(info.HasEncodeOutput);
            Assert.True(info.HasNotEncode);
            // Both tiers derive RenderType.Raw for that pair (same as neither attribute).
            Assert.Equal(RenderType.Raw, RenderTypeRules.Derive(info.HasEncodeOutput, info.HasNotEncode));
            Assert.Equal(RenderTypeRules.Derive(false, false),
                RenderTypeRules.Derive(info.HasEncodeOutput, info.HasNotEncode));

            // Declarable shape derives Encode; tests the veto, not a null-reading binder.
            Assert.True(ExtensionBinder.Build(Compile(EncodeOnlySource)).TryResolve("contradiction", out var sane));
            Assert.True(sane.HasEncodeOutput);
            Assert.False(sane.HasNotEncode);
            Assert.Equal(RenderType.Encode, RenderTypeRules.Derive(sane.HasEncodeOutput, sane.HasNotEncode));
        }
    }
}
