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
    /// Q8.14 — the build-tier half of the finding that <c>[EncodeOutput]</c> + <c>[NotEncode]</c> on one extension
    /// is <b>not a declarable state</b>. <c>NotEncodeAttribute</c> is <c>AttributeTargets.Property</c>, so
    /// co-declaring it with the class-targeted <c>[EncodeOutput]</c> is <b>CS0592</b> — a C# compiler <em>error</em>,
    /// raised in the extension author's own project, which is exactly the surface and stronger than the severity a
    /// declaration-side analyzer warning was to occupy. The one state the pair can be observed in is forged/IL-authored
    /// metadata, and there both tiers already agree (the shared <see cref="RenderTypeRules.Derive"/> answers
    /// <see cref="RenderType.Raw"/> — indistinguishable from carrying neither attribute), so there is no
    /// tier divergence for a use-site error to close either.
    /// <para>These are the executable form of that finding. If either goes red because
    /// <c>NotEncodeAttribute</c>'s targets widened, the contradiction becomes declarable and Q8.14's two
    /// diagnostics become implementable and required — re-open the register entry.</para>
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
                // Heddle.Generator is an analyzer, never a reference; it also carries linked copies of runtime
                // types, which would make those names ambiguous (CS0433) beside Heddle.dll. Same filter the
                // other symbol suites apply.
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

        /// <summary>The declaration-side surface Q8.14's HED7027 was to occupy is already occupied, by the C#
        /// compiler, at error severity: CS0592 is the <b>only</b> error the contradictory declaration produces, and
        /// removing the impossible attribute makes the same declaration compile clean.</summary>
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

        /// <summary>The binder's symbol-side read of the pair, for the one state it is observable in: Roslyn records
        /// an invalidly-applied attribute on the symbol, so <c>HasNotEncode</c> can be true — but only in a
        /// compilation that is already failing CS0592. So a use-site error could never be the diagnostic standing
        /// between an author and a green build, and the verdict it would fire over is one both tiers already share.
        /// </summary>
        [Fact]
        public void TheBinderOnlySeesThePairInAnAlreadyFailingCompilation()
        {
            var contradictory = Compile(ContradictorySource);
            Assert.Contains(contradictory.GetDiagnostics(),
                d => d.Id == "CS0592" && d.Severity == DiagnosticSeverity.Error);

            Assert.True(ExtensionBinder.Build(contradictory).TryResolve("contradiction", out var info));
            Assert.True(info.HasEncodeOutput);
            Assert.True(info.HasNotEncode);
            // The shared truth table over that pair — the same function the run tier evaluates over the reflected
            // flags. Raw, i.e. exactly what an extension carrying neither attribute derives: nothing diverges.
            Assert.Equal(RenderType.Raw, RenderTypeRules.Derive(info.HasEncodeOutput, info.HasNotEncode));
            Assert.Equal(RenderTypeRules.Derive(false, false),
                RenderTypeRules.Derive(info.HasEncodeOutput, info.HasNotEncode));

            // The declarable shape derives Encode, so the assertion above is about the veto and not about a
            // binder that reads no attributes at all.
            Assert.True(ExtensionBinder.Build(Compile(EncodeOnlySource)).TryResolve("contradiction", out var sane));
            Assert.True(sane.HasEncodeOutput);
            Assert.False(sane.HasNotEncode);
            Assert.Equal(RenderType.Encode, RenderTypeRules.Derive(sane.HasEncodeOutput, sane.HasNotEncode));
        }
    }
}
