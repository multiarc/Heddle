extern alias gen;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using EmbeddedCSharpFragment = gen::Heddle.Generator.Binding.EmbeddedCSharpFragment;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The probe pair, held together structurally: the typer's probe and the emitted fragment must be the same
    /// compilation unit — same namespace, same using set, same parameter spelling — or the probe's verdict stops
    /// predicting what the consumer's compiler does with the fragment. So the wrapper shape has exactly one
    /// writer, <c>EmbeddedCSharpFragment.Build</c>, and these tests pin (a) the shape itself against the engine's
    /// templates, (b) that both consumers route through the one writer rather than keeping a private copy, and
    /// (c) that a generated file's fragment block is the builder's own text for the same inputs.
    /// </summary>
    public class EmbeddedCSharpFragmentShapeTests
    {
        /// <summary>The engine's method shape (CSharpClassTemplate.tcs / CSharpPreparseTemplate.tcs): the
        /// namespace enclosure that participates in name lookup, <c>dynamic</c> for chained and every untyped
        /// slot, the space before the parameter list, and the syntactic <c>unchecked</c> around the pasted
        /// expression.</summary>
        [Fact]
        public void TheBuilderWritesTheEnginesWrapperShape()
        {
            var text = EmbeddedCSharpFragment.Build(
                new[] { "System.Linq", "System" },
                new[] { new EmbeddedCSharpFragment.Method("__cs0", "global::Acme.Order", "dynamic", "model.Total + 1") },
                "__CSharp_Views_Page");

            Assert.StartsWith("namespace Heddle.Runtime\n{\n", text, StringComparison.Ordinal);
            Assert.Contains("    using System.Linq;\n    using System;\n", text);
            Assert.Contains("internal static class __CSharp_Views_Page", text);
            Assert.Contains(
                "internal static object __cs0 (global::Acme.Order model, dynamic chained, dynamic root)", text);
            Assert.Contains("return unchecked(model.Total + 1);", text);
        }

        /// <summary>The engine's using set is exact-string deduped (one <c>HashSet&lt;string&gt;</c>), first
        /// spelling wins — so is the builder's.</summary>
        [Fact]
        public void UsingsDedupeByExactStringFirstSpellingWins()
        {
            var text = EmbeddedCSharpFragment.Build(
                new[] { "System", "System . Linq", "System.Linq", "System" },
                new[] { new EmbeddedCSharpFragment.Method("m", "dynamic", "dynamic", "1") },
                "C");

            Assert.Equal(1, Count(text, "    using System;\n"));
            Assert.Equal(1, Count(text, "    using System . Linq;\n"));
            Assert.Equal(1, Count(text, "    using System.Linq;\n"));
        }

        /// <summary>Both consumers of the shape route through the one builder, pinned by source text so a private
        /// re-implementation cannot hide behind a rename: each file calls <c>EmbeddedCSharpFragment.Build</c>, and
        /// neither spells the enclosing namespace or the wrapper's <c>unchecked(</c> return itself.</summary>
        [Theory]
        [InlineData("Emit/TemplateEmitter.cs")]
        [InlineData("Binding/CSharpExpressionTyper.cs")]
        public void TheWrapperShapeHasOneWriterAndBothConsumersUseIt(string relativePath)
        {
            var source = GeneratorSource(relativePath);
            Assert.Contains("EmbeddedCSharpFragment.Build(", source);

            // Code only — a doc comment may name the namespace block it is describing.
            var code = string.Join("\n", source.Replace("\r\n", "\n").Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            Assert.DoesNotContain("namespace Heddle.Runtime", code.Replace("global::Heddle.Runtime.", ""));
            Assert.DoesNotContain("return unchecked(", code);
        }

        /// <summary>The functional half of the pair: a generated file's fragment block is byte-for-byte the
        /// builder's output for the same inputs — the collected namespaces (here the model's <c>System</c>, which
        /// is also the chained type's) and the expression, under the file's own fragment class name. If this ever
        /// reddens, the probe compiled one text and the consumer another.</summary>
        [Fact]
        public void AGeneratedFragmentBlockIsTheBuildersTextForTheSameInputs()
        {
            var run = GeneratorHarness.Run(
                new[] { ("views/embed.heddle", "@model(){{System.String}}@\\\nlen=@(@model.Length)\n") },
                new Dictionary<string, string> { ["build_property.HeddleExpressionMode"] = "FullCSharp" });
            var source = run.RunResult.Results.SelectMany(r => r.GeneratedSources)
                .Single(s => s.HintName.Contains("Embed")).SourceText.ToString();

            var expected = EmbeddedCSharpFragment.Build(
                new[] { "System" },
                new[] { new EmbeddedCSharpFragment.Method("__cs0", "string", "string", "model.Length") },
                "__CSharp_Embed");
            Assert.Contains(expected.TrimEnd('\n'), source.Replace("\r\n", "\n"));
        }

        private static int Count(string text, string token)
        {
            int count = 0, at = 0;
            while ((at = text.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += token.Length;
            }

            return count;
        }

        private static string GeneratorSource(string relativePath)
        {
            var dir = System.IO.Path.GetDirectoryName(typeof(EmbeddedCSharpFragmentShapeTests).Assembly.Location);
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++, dir = System.IO.Path.GetDirectoryName(dir))
            {
                var candidate = System.IO.Path.Combine(dir, "Heddle.Generator",
                    relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(candidate))
                    return System.IO.File.ReadAllText(candidate);
            }

            Assert.Fail(relativePath + " was not found by walking up from " +
                        typeof(EmbeddedCSharpFragmentShapeTests).Assembly.Location +
                        ". This gate reads the generator's source and must not be skipped.");
            return null;
        }
    }
}
