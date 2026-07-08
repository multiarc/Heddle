using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Phase 7 WI2/WI3 gate: the incremental generator discovers <c>.heddle</c> templates, parses them through the
    /// shared front end compiled into the analyzer (D4), surfaces template errors at their <c>.heddle</c> span
    /// (build-time validation), reads the compilation-wide options (D14), and emits the discovery attribute + typed
    /// manifest (D6). Driven through <see cref="Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver"/> (D19).
    /// </summary>
    public class HeddleGeneratorTests
    {
        [Fact]
        public void EmitsDiscoveryAttributeAndManifest()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Home.heddle", "@model(){{System.String}}@\\\nHello @(this)!\n")
            }, globalOptions: new Dictionary<string, string>
            {
                ["build_property.HeddleGeneratedNamespace"] = "SampleApp.HeddleTemplates"
            });

            var manifest = run.GeneratedSourceTexts.FirstOrDefault(s => s.Contains("__HeddleManifest"));
            Assert.NotNull(manifest);
            Assert.Contains("HeddleCompiledTemplates(", manifest);
            Assert.Contains("engineVersion: \"2.0.0\"", manifest);
            Assert.Contains("namespace SampleApp.HeddleTemplates", manifest);

            // No generator errors, and the generated manifest compiles cleanly against the runtime types.
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.DoesNotContain(run.OutputDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void SharedFrontEndParsesInGenerator_ValidTemplateHasNoDiagnostics()
        {
            // A definition + call — exercises the shared front end's definition/output-chain machinery in-generator.
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Card.heddle", "@%<greet>{{Hi, @(Name)}} :: dynamic%@\n@greet(User)\n")
            });
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void ReportsTemplateErrorAtHeddleSpan()
        {
            // Duplicate definition — a semantic front-end error that must surface at the .heddle position.
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Dup.heddle", "@%<a>{{x}} :: dynamic%@@%<a>{{y}} :: dynamic%@\n")
            });

            var error = run.GeneratorDiagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
            Assert.NotNull(error);
            Assert.Contains("Views/Dup.heddle", error.Location.GetLineSpan().Path);
            // A forwarded front-end error carrying no DiagnosticId is wrapped as HED7012 (D13).
            Assert.Equal("HED7012", error.Id);
        }

        [Fact]
        public void ImportsResolveFromAdditionalFilesNotDisk()
        {
            // @<< import served from the in-memory map — no disk access, and no "file not found" error. With no
            // HeddleTemplateRoot the key is the file name, so the import references it by that key (lib.heddle).
            var run = GeneratorHarness.Run(new[]
            {
                ("views/lib.heddle", "@%<footer>{{(c) Heddle}} :: dynamic%@"),
                ("views/home.heddle", "@<<{{lib.heddle}}@\\\n@footer(this)\n")
            });
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void UnparsableOptionReportsHed7009()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Home.heddle", "Hello\n")
            }, globalOptions: new Dictionary<string, string>
            {
                ["build_property.HeddleOutputProfile"] = "Xml"
            });

            var hed7009 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7009");
            Assert.NotNull(hed7009);
            Assert.Contains("Xml", hed7009.GetMessage());
        }

        [Fact]
        public void CaseOnlyKeyTwinReportsHed7003()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("a/Home.heddle", "one\n"),
                ("a/home.heddle", "two\n")
            });
            Assert.Contains(run.GeneratorDiagnostics, d => d.Id == "HED7003");
        }

        [Fact]
        public void DistinctKeysSanitizingToOneIdentifierReportHed7010()
        {
            // "a.b.heddle" -> "A_b" and "a-b.heddle" -> "A_b" collide on the entry-class identifier (D11).
            var run = GeneratorHarness.Run(new[]
            {
                ("a.b.heddle", "one\n"),
                ("a-b.heddle", "two\n")
            });
            Assert.Contains(run.GeneratorDiagnostics, d => d.Id == "HED7010");
        }

        [Fact]
        public void UnreadableTemplateReportsHed7001()
        {
            var run = GeneratorHarness.RunTexts(new AdditionalText[]
            {
                new UnreadableAdditionalText("Views/Broken.heddle")
            });

            var hed7001 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7001");
            Assert.NotNull(hed7001);
            Assert.Equal(DiagnosticSeverity.Error, hed7001.Severity);
            Assert.Contains("Views/Broken.heddle", hed7001.GetMessage());
        }

        [Fact]
        public void DuplicateNormalizedKeyReportsHed7002()
        {
            // Two files in different directories whose file names normalize to the same root-relative key (no root
            // configured → key = file name), a duplicate-key registration hazard the build must reject.
            var run = GeneratorHarness.Run(new[]
            {
                ("one/dup.heddle", "one\n"),
                ("two/dup.heddle", "two\n")
            });

            var hed7002 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7002");
            Assert.NotNull(hed7002);
            Assert.Contains("dup.heddle", hed7002.GetMessage());
        }

        [Fact]
        public void InvalidKeyMetadataReportsHed7004()
        {
            // An explicit Key metadata containing a '..' segment is rejected (TemplateKey normalization).
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Home.heddle", "Hello\n")
            }, perFileOptions: new Dictionary<string, Dictionary<string, string>>
            {
                ["Views/Home.heddle"] = new Dictionary<string, string>
                {
                    ["build_metadata.AdditionalFiles.Key"] = "../escape"
                }
            });

            var hed7004 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7004");
            Assert.NotNull(hed7004);
            Assert.Contains("../escape", hed7004.GetMessage());
        }

        [Fact]
        public void LoneSurrogateInStaticTextReportsHed7005AndSuppressesU8()
        {
            // A static piece with an unpaired high surrogate stays legal for string output but must warn (HED7005)
            // and suppress the "…"u8 twin — even with UTF-8 pieces enabled.
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Home.heddle", "before \uD800 after\n")
            }, globalOptions: new Dictionary<string, string>
            {
                ["build_property.HeddleEmitUtf8Pieces"] = "true"
            });

            var hed7005 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7005");
            Assert.NotNull(hed7005);
            Assert.Equal(DiagnosticSeverity.Warning, hed7005.Severity);
            Assert.Contains("Views/Home.heddle", hed7005.Location.GetLineSpan().Path);

            // The template still precompiles (string output), but no u8 twin was emitted for it.
            var body = run.GeneratedSourceTexts.FirstOrDefault(s => s.Contains("class Home"));
            Assert.NotNull(body);
            Assert.DoesNotContain("u8;", body);
        }

        [Fact]
        public void MissingImportReportsHed7011AtTheImportBlock()
        {
            // @<< referencing a path not among the compilation's .heddle AdditionalFiles.
            var run = GeneratorHarness.Run(new[]
            {
                ("home.heddle", "top\n@<<{{missing/lib.heddle}}@\\\nbottom\n")
            });

            var hed7011 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7011");
            Assert.NotNull(hed7011);
            Assert.Contains("missing/lib.heddle", hed7011.GetMessage());
            Assert.Contains("home.heddle", hed7011.Location.GetLineSpan().Path);
            // Reported at the @<< block on line 2 (0-based line 1), not the document start.
            Assert.Equal(1, hed7011.Location.GetLineSpan().StartLinePosition.Line);
        }

        [Fact]
        public void UnresolvableModelTypeReportsHed7007()
        {
            // Milestone 2 (D3): a declared @model type that resolves as no symbol and matches no type name anywhere
            // in the reference closure is a genuine unresolvable symbol.
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Home.heddle", "@model(){{Totally.Bogus.NonexistentModelZzz}}@\\\nHi @(this)\n")
            });

            var hed7007 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7007");
            Assert.NotNull(hed7007);
            Assert.Equal(DiagnosticSeverity.Error, hed7007.Severity);
            Assert.Contains("NonexistentModelZzz", hed7007.GetMessage());
            Assert.Contains("Views/Home.heddle", hed7007.Location.GetLineSpan().Path);
        }

        [Fact]
        public void ResolvableModelTypeDoesNotReportHed7007()
        {
            // A bare simple name the runtime resolves by assembly scan (System.String exists) must NOT error, even
            // without an @using — the reconciliation the milestone requires (no safe fallback becomes a hard error).
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Ok.heddle", "@model(){{String}}@\\\n@(this)\n")
            });
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7007");
        }

        [Fact]
        public void UnresolvableMemberReportsHed7008()
        {
            // Milestone 2 (D3): a member typo on a resolved, non-dynamic model — a native expression forces the
            // member-path writer, which reports the genuine property-not-found before the C# compiler sees the code.
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/M.heddle", "@model(){{System.String}}@\\\n@(Length + NoSuchMemberZzz)\n")
            });

            var hed7008 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7008");
            Assert.NotNull(hed7008);
            Assert.Equal(DiagnosticSeverity.Error, hed7008.Severity);
            Assert.Contains("NoSuchMemberZzz", hed7008.GetMessage());
            Assert.Contains("Views/M.heddle", hed7008.Location.GetLineSpan().Path);
            // Positioned on the expression line (line index 1), not the document start.
            Assert.Equal(1, hed7008.Location.GetLineSpan().StartLinePosition.Line);
        }

        [Fact]
        public void UnresolvableMemberInBarePathReportsHed7008()
        {
            // The bare dotted-path form (@(A.B)) — the model-parameter member path — also reports the genuine miss.
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/P.heddle", "@model(){{System.String}}@\\\n@(Length.NoSuchNestedZzz)\n")
            });

            var hed7008 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7008");
            Assert.NotNull(hed7008);
            Assert.Contains("NoSuchNestedZzz", hed7008.GetMessage());
        }

        [Fact]
        public void ResolvedMemberDoesNotReportHed7008()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/G.heddle", "@model(){{System.String}}@\\\n@(Length + 1)\n")
            });
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7008");
        }

        [Fact]
        public void GeneratorAssemblyDoesNotReferenceRuntimeHeddle()
        {
            // D4/D12 packaging constraint: the generator shares front-end SOURCE and references the ANTLR
            // Heddle.Language, but never the runtime Heddle assembly.
            var referenced = typeof(HeddleTemplateGenerator).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name).ToList();
            Assert.DoesNotContain("Heddle", referenced);
            Assert.Contains("Heddle.Language", referenced);
        }
    }
}
