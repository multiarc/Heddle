using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Phase 7 D19 — snapshot goldens for the seven <c>generated-code.md</c> example families: static text + typed
    /// member paths, native expressions, embedded C# verbatim, <c>:: dynamic</c>, a phase 5 props definition, a
    /// phase 3 branch set, and function calls (shim-bound built-in + the OQ1 unresolvable negative). Each snapshots
    /// the generator's actual output (every generated source, in hint-name order, plus the reported diagnostics) so
    /// the emitted shape is pinned; a shape regression fails the snapshot. Rendered through
    /// <c>Verify</c>/<c>Verify.SourceGenerators</c> (D19) over <c>CSharpGeneratorDriver</c>. The generated text is
    /// deterministic and produced by the netstandard2.0 generator, so one golden serves every test TFM.
    /// </summary>
    public class GeneratorSnapshotTests
    {
        private static Task VerifyTemplate(string path, string content,
            Dictionary<string, string> globalOptions = null)
        {
            var run = GeneratorHarness.Run(new[] { (path, content) }, globalOptions);
            return Verifier.Verify(Render(run)).UseDirectory("Snapshots");
        }

        /// <summary>A deterministic textual snapshot of the generator run: every generated source in hint-name order
        /// followed by the reported diagnostics (id, severity, mapped span, message) in a stable order.</summary>
        private static string Render(GeneratorRun run)
        {
            var sb = new StringBuilder();
            var sources = run.RunResult.Results
                .SelectMany(r => r.GeneratedSources)
                .OrderBy(s => s.HintName, StringComparer.Ordinal);
            foreach (var s in sources)
            {
                sb.Append("// ==== ").Append(s.HintName).Append(" ====\n");
                sb.Append(s.SourceText.ToString().Replace("\r\n", "\n").TrimEnd('\n'));
                sb.Append("\n\n");
            }

            var diagnostics = run.GeneratorDiagnostics
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .ThenBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
                .ThenBy(d => d.GetMessage(), StringComparer.Ordinal);
            sb.Append("// ==== diagnostics ====\n");
            bool any = false;
            foreach (var d in diagnostics)
            {
                any = true;
                var span = d.Location.GetLineSpan();
                var pos = d.Location == Location.None
                    ? "(none)"
                    : $"({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1})";
                sb.Append(d.Id).Append(' ').Append(d.Severity).Append(' ').Append(pos).Append(": ")
                    .Append(d.GetMessage()).Append('\n');
            }

            if (!any)
                sb.Append("(none)\n");
            return sb.ToString();
        }

        [Fact]
        public Task Example1_StaticTextAndMemberPaths() =>
            VerifyTemplate("views/version.heddle",
                "@model(){{System.Version}}@\\\n<p>v@(Major).@(Minor)</p>\n");

        [Fact]
        public Task Example2_NativeExpression() =>
            VerifyTemplate("views/sum.heddle",
                "@model(){{System.Version}}@\\\nsum=@(Major + Minor)\n");

        [Fact]
        public Task Example3_EmbeddedCSharpVerbatim() =>
            VerifyTemplate("views/embed.heddle",
                "@model(){{System.String}}@\\\nlen=@(@model.Length)\n",
                new Dictionary<string, string>
                {
                    ["build_property.HeddleExpressionMode"] = "FullCSharp"
                });

        [Fact]
        public Task Example4_DynamicTemplate() =>
            VerifyTemplate("views/dyn.heddle",
                "@model(){{dynamic}}@\\\n@(Name)\n");

        [Fact]
        public Task Example5_DefinitionWithProps() =>
            VerifyTemplate("views/tag.heddle",
                "@%<tag(label: string = \"note\")>{{[@(label)]}} :: System.String%@\n@tag(\"hi\")\n");

        [Fact]
        public Task Example6_BranchSet() =>
            VerifyTemplate("views/branch.heddle",
                "@model(){{System.Version}}@\\\n@if(Major > 0){{big}}@else(){{small}}\n");

        [Fact]
        public Task Example7_FunctionShimCall() =>
            VerifyTemplate("views/shout.heddle",
                "@model(){{System.String}}@\\\n@(upper(this))\n");

        [Fact]
        public Task Example7b_UnresolvableFunctionMarker() =>
            VerifyTemplate("views/unresolved.heddle",
                "@model(){{System.String}}@\\\n@(nosuchfn9000(this))\n");
    }
}
