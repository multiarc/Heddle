using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Native;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// What the C# tier compiles against, and what it reports when that compile fails. Both were decided by
    /// accidents — whether an assembly happened to be on disk, and whether a given expression happened to be the
    /// first of its kind in the process.
    /// </summary>
    public class CSharpTierMetadataTests
    {
        /// <summary>
        /// An assembly with no file behind it must still yield metadata to compile against. This is not an edge
        /// case: in a single-file or WASM publish <b>no</b> assembly has a location, so a provider that requires one
        /// hands the C# tier an empty reference set and every expression fails with "Predefined type 'System.Object'
        /// is not defined" — naming nothing the template author wrote.
        /// </summary>
        [Fact]
        public void AnAssemblyWithNoFileBehindItStillYieldsMetadata()
        {
            var inMemory = Assembly.Load(EmitProbe());

            Assert.True(string.IsNullOrEmpty(inMemory.Location),
                "the probe must have no location, or it proves nothing about the single-file shape");

            var references = RoslynReferenceProvider.Build(new[] { inMemory });

            Assert.Single(references);
        }

        /// <summary>
        /// The whole reference set, taken the way the C#-tier compile path takes it, must not thin out when the
        /// assemblies behind it are file-less. Pins the population rather than one probe: a provider that special-cases
        /// a single assembly but still drops framework metadata would leave the tier just as dead.
        /// </summary>
        [Fact]
        public void NoObservedAssemblyIsDroppedFromTheReferenceSet()
        {
            var observed = AssemblyHelper.GetAssemblies();
            int expected;
            lock (observed)
            {
                expected = observed.Count(a => a != null && !a.IsDynamic);
            }

            Assert.Equal(expected, AssemblyHelper.GetApplicationReferences().Count);
        }

        /// <summary>
        /// A byte value below 0x10 must keep its leading zero. Formatting one hex digit per byte shortens the string
        /// and shifts every digit after it, which corrupted the public keys the C#-tier code generator emits into
        /// <c>InternalsVisibleTo</c> — the compiler then rejected each grant and the rejections surfaced in the host's
        /// error list as references to keys nobody had written.
        /// </summary>
        [Fact]
        public void EveryByteFormatsToTwoHexDigits()
        {
            var all = Enumerable.Range(0, 256).Select(b => (byte)b).ToArray();

            var hex = all.ToHexString();

            Assert.Equal(512, hex.Length);
            Assert.Equal(all, Enumerable.Range(0, 256)
                .Select(i => byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber)));
        }

        /// <summary>
        /// The real signing key, formatted, must be exactly what the runtime reports — the concrete value the
        /// corruption was found in, and the one an <c>InternalsVisibleTo</c> grant has to match to be honoured.
        /// </summary>
        [Fact]
        public void TheEnginesOwnPublicKeyFormatsToItsFullLength()
        {
            var key = typeof(HeddleTemplate).Assembly.GetName().GetPublicKey();
            Assert.NotEmpty(key);

            var hex = key.ToHexString();

            Assert.Equal(key.Length * 2, hex.Length);
            Assert.StartsWith("00", hex, StringComparison.Ordinal);
        }

        /// <summary>
        /// Compiling the same broken template twice in one process must report the same thing twice. The C#-tier
        /// preparse cache used to store the value and drop the diagnostics, so the first caller received the errors
        /// and every later one received none, fell through to a different compile path, and got a different list —
        /// diagnostics decided by process history, which is exactly what a long-running host or an editor recompiling
        /// on each keystroke experiences.
        /// </summary>
        [Fact]
        public void TheSameFailingExpressionReportsTheSameDiagnosticsEveryTime()
        {
            const string document = "@(@ no_such_symbol_in_scope )";

            var first = CompileErrors(document);
            var second = CompileErrors(document);
            var third = CompileErrors(document);

            Assert.NotEmpty(first);
            Assert.Equal(first, second);
            Assert.Equal(first, third);
        }

        private static IReadOnlyList<string> CompileErrors(string document)
        {
            using var template = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp }));

            Assert.False(template.CompileResult.Success);
            return template.CompileResult.ErrorList.Select(e => e.Error).OrderBy(e => e, StringComparer.Ordinal)
                .ToArray();
        }

        private static byte[] EmitProbe()
        {
            var compilation = CSharpCompilation.Create(
                "MetadataProbe" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText("namespace Probe { public class Marker { } }") },
                AssemblyHelper.GetApplicationReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success,
                string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())));

            return stream.ToArray();
        }
    }
}
