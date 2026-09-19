using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Heddle.Data;
using Heddle.Language;
using Heddle.Native;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The degrade paths that exist for .NET Framework's sake, measured on the one leg that can reach them — the
    /// <c>net48</c> target this project builds on Windows. On every other target the framework halves of these are
    /// dead code, and for years the guards they pin were reasoning rather than evidence.
    /// </summary>
    public class NetFrameworkDegradePathTests
    {
#if NETFRAMEWORK
        /// <summary>
        /// <c>Path.Combine</c> refuses '&lt;', '&gt;' and '|' on .NET Framework and a template is free to contain
        /// them in an import spelling. The premise is asserted first: if a framework servicing ever stops
        /// throwing, this reddens and the guard gets re-measured instead of quietly outliving its reason. The
        /// parse must survive on the raw-spelling fallback, and the identity must be that raw spelling.
        /// </summary>
        [Fact]
        public void AnImportPathWithCharactersTheFrameworkRejectsStillParses()
        {
            Assert.ThrowsAny<ArgumentException>(() => Path.Combine("dir", "a<b.heddle"));

            var library = new Dictionary<string, string> { ["ok.heddle"] = "imported" };
            var settings = new ParserSettings
            {
                RootPath = "dir",
                ImportReader = path => library.TryGetValue(path, out var text) ? text : string.Empty
            };

            var context = DocumentParser.Parse("@<<{{bad<name.heddle}}@\\\n@<<{{ok.heddle}}@\\\nroot",
                settings, out _);

            Assert.DoesNotContain(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportCycle);
            Assert.DoesNotContain(context.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.TemplateNestedTooDeeply);
            Assert.Equal("a<b.heddle", new ParserSettings { RootPath = "dir" }.ImportIdentity("a<b.heddle"));
        }
#endif

        /// <summary>
        /// An assembly with no file behind it: on targets with <c>TryGetRawMetadata</c> its loaded image is read
        /// directly; the <c>netstandard2.0</c> build consumed by .NET Framework has no such API, so the assembly
        /// silently yields no reference and expressions naming its types fail to compile. Both arms are written
        /// out, and the file-backed engine assembly rides along as the positive control — a provider that refuses
        /// everything does not pass the framework arm.
        /// </summary>
        [Fact]
        public void AnAssemblyWithNoFileYieldsAReferenceOnlyWhereTheRuntimeExposesItsMetadata()
        {
            var byteLoaded = CompileToBytesAndLoad();
            Assert.Empty(byteLoaded.Location);
            var fileBacked = typeof(HeddleTemplate).Assembly;

            var references = RoslynReferenceProvider.Build(new[] { byteLoaded, fileBacked });

#if NETFRAMEWORK
            var reference = Assert.Single(references);
            Assert.Contains(fileBacked.GetName().Name, reference.Display);
#else
            Assert.Equal(2, references.Count);
#endif
        }

        private static Assembly CompileToBytesAndLoad()
        {
            var compilation = CSharpCompilation.Create(
                "NoFileProbe" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText("namespace NoFileProbe { public class Marker { } }") },
                AssemblyHelper.GetApplicationReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success,
                string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())));

            return Assembly.Load(stream.ToArray());
        }
    }
}
