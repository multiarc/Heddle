using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
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
            Assert.Contains("engineVersion: \"2.1.0\"", manifest);
            Assert.Contains("namespace SampleApp.HeddleTemplates", manifest);
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.DoesNotContain(run.OutputDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void SharedFrontEndParsesInGenerator_ValidTemplateHasNoDiagnostics()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Card.heddle", "@%<greet>{{Hi, @(Name)}} :: dynamic%@\n@greet(User)\n")
            });
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void ReportsTemplateErrorAtHeddleSpan()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Dup.heddle", "@%<a>{{x}} :: dynamic%@@%<a>{{y}} :: dynamic%@\n")
            });

            var error = run.GeneratorDiagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
            Assert.NotNull(error);
            Assert.Contains("Views/Dup.heddle", error.Location.GetLineSpan().Path);
            Assert.Equal("HED7012", error.Id);
        }

        [Fact]
        public void ImportsResolveFromAdditionalFilesNotDisk()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("views/lib.heddle", "@%<footer>{{(c) Heddle}} :: dynamic%@"),
                ("views/home.heddle", "@<<{{lib.heddle}}@\\\n@footer(this)\n")
            });
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void LegacyImportEmitsHed4003AtTheCallSite()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Home.heddle", "@import(){{lib.heddle}}\nHello\n")
            });

            var single = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED4003"));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            Assert.Contains("has been removed", single.GetMessage());
            Assert.Contains("docs/language-reference.md#imports--", single.GetMessage());
            Assert.Contains("Views/Home.heddle", single.Location.GetLineSpan().Path);
            // Positioned at the @import call on the first line, not the default document start of a forwarded miss.
            Assert.Equal(0, single.Location.GetLineSpan().StartLinePosition.Line);
        }

        [Fact]
        public void LegacyImportNestedInIfEmitsHed4003()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Home.heddle", "@if(true){{ @import(){{lib.heddle}} }}\n")
            });

            var single = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED4003"));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            Assert.Contains("has been removed", single.GetMessage());
        }

        [Fact]
        public void ComposeImportEmitsNoHed4003InGenerator()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("views/lib.heddle", "@%<footer>{{(c) Heddle}} :: dynamic%@"),
                ("views/home.heddle", "@<<{{lib.heddle}}@\\\n@footer(this)\n")
            });
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED4003");
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
            // "a.b.heddle" -> "A_b" and "a-b.heddle" -> "A_b" collide on the entry-class identifier.
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
            var body = run.GeneratedSourceTexts.FirstOrDefault(s => s.Contains("class Home"));
            Assert.NotNull(body);
            Assert.DoesNotContain("u8;", body);
        }

        [Fact]
        public void MissingImportReportsHed7011AtTheImportBlock()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("home.heddle", "top\n@<<{{missing/lib.heddle}}@\\\nbottom\n")
            });

            var hed7011 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7011");
            Assert.NotNull(hed7011);
            Assert.Contains("missing/lib.heddle", hed7011.GetMessage());
            Assert.Contains("home.heddle", hed7011.Location.GetLineSpan().Path);
            Assert.Equal(1, hed7011.Location.GetLineSpan().StartLinePosition.Line);
        }

        [Fact]
        public void UnresolvableModelTypeReportsHed7007()
        {
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
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/Ok.heddle", "@model(){{String}}@\\\n@(this)\n")
            });
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7007");
        }

        [Fact]
        public void UnresolvableMemberReportsHed7008()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("Views/M.heddle", "@model(){{System.String}}@\\\n@(Length + NoSuchMemberZzz)\n")
            });

            var hed7008 = run.GeneratorDiagnostics.FirstOrDefault(d => d.Id == "HED7008");
            Assert.NotNull(hed7008);
            Assert.Equal(DiagnosticSeverity.Error, hed7008.Severity);
            Assert.Contains("NoSuchMemberZzz", hed7008.GetMessage());
            Assert.Contains("Views/M.heddle", hed7008.Location.GetLineSpan().Path);
            Assert.Equal(1, hed7008.Location.GetLineSpan().StartLinePosition.Line);
        }

        [Fact]
        public void UnresolvableMemberInBarePathReportsHed7008()
        {
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
            var referenced = typeof(HeddleTemplateGenerator).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name).ToList();
            Assert.DoesNotContain("Heddle", referenced);
            Assert.Contains("Heddle.Language", referenced);
        }
    }
}
