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
        /// Every non-dynamic assembly offered must come back as a reference, whether or not it has a file. Pins the
        /// population rather than one probe: what killed the tier was not a single dropped assembly but a rule that
        /// silently thinned the whole set, and a set short by one framework assembly fails just as total.
        /// </summary>
        [Fact]
        public void NoAssemblyIsDroppedFromTheReferenceSet()
        {
            var observed = AssemblyHelper.GetAssemblies();
            List<Assembly> offered;
            lock (observed)
            {
                offered = observed.Where(a => a != null && !a.IsDynamic).ToList();
            }

            Assert.NotEmpty(offered);
            offered.Add(Assembly.Load(EmitProbe()));

            Assert.Equal(offered.Count, RoslynReferenceProvider.Build(offered).Count);
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
        /// Compiling the same broken template twice in one process must report the same thing twice — a long-running
        /// host, and an editor recompiling on each keystroke, get the second answer far more often than the first.
        /// <para><b>This is the end-to-end divergence guard, not the mechanism pin.</b> Both the replay path and a
        /// recompile produce identical text here, so removing the replay leaves this green; the replay itself — and
        /// each caller receiving its own position — is pinned at the unit level by
        /// <see cref="PreparseDiagnosticReplayTests"/>, which reddens on either reversion. This one is kept because
        /// the property it states is the one the two paths must keep agreeing on.</para>
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

        /// <summary>
        /// A cached <b>failure</b> must not outlive the assembly set that produced it. An expression naming a type
        /// the host had not registered yet fails; once registered it has to succeed. Caching the failure forever
        /// turned a fault that healed on the next compile into a permanent one decided by load order — the property
        /// the observation gate exists to prevent.
        /// </summary>
        [Fact]
        public void AFailureCachedBeforeRegistrationDoesNotSurviveIt()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var source = $@"namespace LateNs{suffix} {{ public static class Late{suffix} {{ public static string V() => ""late""; }} }}";
            var bytes = EmitNamed(source, out var assemblyName);
            var path = ProbeAssemblyFiles.WriteBesideTestAssembly(bytes, assemblyName);

            var document = $"@model(){{{{dynamic}}}}@(@ LateNs{suffix}.Late{suffix}.V() )";
            Assert.False(Compiles(document), "the type is not loaded yet, so this must fail first");

            HeddleTemplate.Register(Assembly.LoadFrom(path));

            Assert.True(Compiles(document),
                "a failure cached before the assembly was registered outlived the registration");
        }

        private static bool Compiles(string document)
        {
            using var template = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp }));
            return template.CompileResult.Success;
        }

        private static byte[] EmitNamed(string source, out string assemblyName)
        {
            assemblyName = "LateProbe" + Guid.NewGuid().ToString("N");
            var compilation = CSharpCompilation.Create(assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source) },
                AssemblyHelper.GetApplicationReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            Assert.True(compilation.Emit(stream).Success);
            return stream.ToArray();
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
