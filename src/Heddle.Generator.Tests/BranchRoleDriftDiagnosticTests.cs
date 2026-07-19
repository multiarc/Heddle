using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Heddle.Generator.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// WI7 / D-ROLE-5 (§6.5): the optional generator drift diagnostic <c>HED7016</c> fires for a branch
    /// <c>Continuation</c>/<c>Terminal</c> that omits <c>[ScopeChannel]</c> (it can never read the branch state at
    /// render time, R11), and never fires for the compliant engine built-ins or a compliant custom trio. The
    /// diagnostic is additive: emitting it does not change any generated source or existing diagnostics.
    /// </summary>
    public class BranchRoleDriftDiagnosticTests
    {
        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .ToList();
            refs.Add(MetadataReference.CreateFromFile(
                typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly.Location));
            return refs;
        }

        // A continuation and a terminal that FAIL the R11 contract (no [ScopeChannel]); plus one compliant
        // continuation, to prove the drift set is exactly the offenders.
        private const string DriftSource = @"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace DriftBranch
{
    [ExtensionName(""begin"")]
    [BranchRole(BranchRole.Opener)]
    public class BeginExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    // Continuation WITHOUT [ScopeChannel] — drift (HED7016).
    [ExtensionName(""driftbetween"")]
    [BranchRole(BranchRole.Continuation)]
    public class DriftBetweenExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    // Terminal WITHOUT [ScopeChannel] — drift (HED7016).
    [ExtensionName(""driftfinish"")]
    [BranchRole(BranchRole.Terminal)]
    public class DriftFinishExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    // Compliant continuation — NOT drift.
    [ExtensionName(""okbetween"")]
    [ScopeChannel]
    [BranchRole(BranchRole.Continuation)]
    public class OkBetweenExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }
}";

        private const string CompliantTrioSource = @"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace OkBranch
{
    [ExtensionName(""begin"")]
    [BranchRole(BranchRole.Opener)]
    public class BeginExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    [ExtensionName(""between"")]
    [ScopeChannel]
    [BranchRole(BranchRole.Continuation)]
    public class BetweenExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    [ExtensionName(""finish"")]
    [ScopeChannel]
    [BranchRole(BranchRole.Terminal)]
    public class FinishExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }
}";

        private sealed class TemplateText : AdditionalText
        {
            private readonly SourceText _text;
            public TemplateText(string path, string content)
            {
                Path = path;
                _text = SourceText.From(content, Encoding.UTF8);
            }
            public override string Path { get; }
            public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
        }

        private sealed class Options : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value) { value = null; return false; }
        }

        private sealed class OptionsProvider : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
        {
            public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions => new Options();
            public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options();
            public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options();
        }

        private static ImmutableArray<Diagnostic> RunGenerator(string csharpSource)
        {
            var trees = csharpSource == null
                ? Array.Empty<SyntaxTree>()
                : new[] { CSharpSyntaxTree.ParseText(csharpSource) };
            var compilation = CSharpCompilation.Create("DriftGenTest", trees, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var texts = ImmutableArray.Create<AdditionalText>(new TemplateText("Home.heddle", "hello"));

            var driver = CSharpGeneratorDriver.Create(
                new[] { new HeddleTemplateGenerator().AsSourceGenerator() },
                texts,
                parseOptions: CSharpParseOptions.Default,
                optionsProvider: new OptionsProvider());

            driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
            return diagnostics;
        }

        [Fact]
        public void Hed7016FiresForContinuationAndTerminalMissingScopeChannel()
        {
            var diagnostics = RunGenerator(DriftSource);

            var drift = diagnostics.Where(d => d.Id == "HED7016").ToList();
            Assert.Equal(2, drift.Count);
            Assert.All(drift, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));

            var messages = drift.Select(d => d.GetMessage()).ToList();
            Assert.Contains(messages, m => m.Contains("DriftBetweenExtension") &&
                m.Contains("does not carry [ScopeChannel]"));
            Assert.Contains(messages, m => m.Contains("DriftFinishExtension"));
            // The compliant continuation is not reported.
            Assert.DoesNotContain(messages, m => m.Contains("OkBetweenExtension"));
        }

        [Fact]
        public void Hed7016DoesNotFireForCompliantTrioOrBuiltIns()
        {
            Assert.Empty(RunGenerator(CompliantTrioSource).Where(d => d.Id == "HED7016"));
            // No custom source at all — only the engine built-ins (all R11-compliant).
            Assert.Empty(RunGenerator(null).Where(d => d.Id == "HED7016"));
        }

        [Fact]
        public void BinderRecordsDriftTypesExactly()
        {
            var compilation = CSharpCompilation.Create("DriftBinderTest",
                new[] { CSharpSyntaxTree.ParseText(DriftSource) }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var drift = ExtensionBinder.Build(compilation).DriftTypes;
            Assert.Equal(2, drift.Count);
            Assert.Contains(drift, t => t.Contains("DriftBetweenExtension"));
            Assert.Contains(drift, t => t.Contains("DriftFinishExtension"));
            Assert.DoesNotContain(drift, t => t.Contains("OkBetweenExtension"));
        }
    }
}
