using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using Heddle.Tool.Compile;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Tool.Tests
{
    /// <summary>Consumer code runs during a build — an extension's hooks, a static constructor — and it may read a
    /// satellite resource or call into a native library that sits beside its assembly. Pins that an image the
    /// host loads still finds both, in the two ways the host loads one: from bytes when the caller stays alive,
    /// and from its path in the one-shot host process. The regression was the first of these, which gave the
    /// image no location: French came back as the neutral text and the import threw.</summary>
    public class ImageSatelliteAndNativeTests : IDisposable
    {
        private readonly string _dir;

        public ImageSatelliteAndNativeTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-satnative-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        private static byte[] Resources(string value)
        {
            var stream = new MemoryStream();
            var writer = new ResourceWriter(stream);
            writer.AddResource("Hello", value);
            writer.Generate();
            return stream.ToArray();
        }

        private static List<MetadataReference> References()
        {
            var references = new List<MetadataReference>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }

            return references;
        }

        /// <summary>The runtime's own compression shim under a name nothing else resolves, so the import can only
        /// be answered from beside the image. Which file that is depends on the platform the test runs on.</summary>
        private static string NativeFileName(out string source)
        {
            string runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                source = Path.Combine(runtime, "System.IO.Compression.Native.dll");
                return "heddlescratchnative.dll";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                source = Path.Combine(runtime, "libSystem.IO.Compression.Native.dylib");
                return "libheddlescratchnative.dylib";
            }

            source = Path.Combine(runtime, "libSystem.IO.Compression.Native.so");
            return "libheddlescratchnative.so";
        }

        /// <summary>Emits the image with a French satellite and the native library beside it. With
        /// <paramref name="reportTo"/>, the image also exports an extension whose compile-time hook writes what
        /// it found to that file — consumer code running during the build.</summary>
        private string EmitImage(string name, string reportTo)
        {
            string nativeName = NativeFileName(out string nativeSource);
            Assert.True(File.Exists(nativeSource), "the runtime's compression shim was not found at " + nativeSource);
            File.Copy(nativeSource, Path.Combine(_dir, nativeName));

            string source =
                "[assembly: System.Resources.NeutralResourcesLanguage(\"en\")]\n" +
                "namespace " + name + " { public static class Probe { " +
                "public static string Hello(string culture) => new System.Resources.ResourceManager(\"" + name +
                ".Strings\", typeof(Probe).Assembly).GetString(\"Hello\", new System.Globalization.CultureInfo(culture)); " +
                "[System.Runtime.InteropServices.DllImport(\"heddlescratchnative\")] static extern unsafe uint CompressionNative_Crc32(uint crc, byte* buffer, int len); " +
                "public static unsafe uint Crc() { byte b = 1; return CompressionNative_Crc32(0, &b, 1); } } }\n";
            if (reportTo != null)
            {
                source =
                    "[assembly: Heddle.Attributes.ExportExtensions(typeof(" + name + ".ProbeExtension))]\n" + source +
                    "namespace " + name + " { [Heddle.Attributes.ExtensionName(\"probe\")] public class ProbeExtension : Heddle.Core.AbstractExtension { " +
                    "public override Heddle.Data.ExType InitStart(Heddle.Core.InitContext c, Heddle.Data.ExType d, Heddle.Data.ExType ch, Heddle.Data.ExType p) { " +
                    "string crc; try { crc = Probe.Crc().ToString(\"x\"); } catch (System.Exception e) { crc = e.GetType().Name; } " +
                    "System.IO.File.WriteAllText(@\"" + reportTo + "\", Probe.Hello(\"fr\") + \"|\" + crc); return base.InitStart(c, d, ch, p); } " +
                    "public override object ProcessData(in Heddle.Data.Scope scope) => null; public override void RenderData(in Heddle.Data.Scope scope) { } } }\n";
            }

            var references = References();
            var main = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(source) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            string path = Path.Combine(_dir, name + ".dll");
            var emitted = main.Emit(path, manifestResources: new[]
            {
                new ResourceDescription(name + ".Strings.resources", () => new MemoryStream(Resources("hello")), true)
            });
            Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));

            foreach (var (culture, text) in new[] { ("fr", "bonjour"), ("es", "hola") })
            {
                Directory.CreateDirectory(Path.Combine(_dir, culture));
                var satellite = CSharpCompilation.Create(name + ".resources",
                    new[] { CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyCulture(\"" + culture + "\")]") },
                    references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                using (var file = File.Create(Path.Combine(_dir, culture, name + ".resources.dll")))
                {
                    var result = satellite.Emit(file, manifestResources: new[]
                    {
                        new ResourceDescription(name + ".Strings." + culture + ".resources",
                            () => new MemoryStream(Resources(text)), true)
                    });
                    Assert.True(result.Success, string.Join("\n", result.Diagnostics));
                }
            }

            return path;
        }

        [Fact]
        public void AnImageLoadedFromBytesFindsItsSatelliteAndItsNativeLibrary()
        {
            string name = "SatBytes" + Guid.NewGuid().ToString("N");
            string path = EmitImage(name, null);
            using (var images = new ImageLoadContext())
            {
                var probe = images.LoadImage(path).GetType(name + ".Probe", true);
                Assert.Equal("bonjour", (string) probe.GetMethod("Hello").Invoke(null, new object[] { "fr" }));
                Assert.Equal("hello", (string) probe.GetMethod("Hello").Invoke(null, new object[] { "de" }));
                Assert.NotEqual(0u, (uint) probe.GetMethod("Crc").Invoke(null, null));
            }

            // The managed files stay free; the native one is mapped for as long as this process lives.
            File.Delete(path);
            File.Delete(Path.Combine(_dir, "fr", name + ".resources.dll"));
        }

        /// <summary>A caller that stays alive compiles more than once, and a second invocation over an image the
        /// first one loaded is handed that same image back. What it first asks for then — another culture, the
        /// native library — is still asked of the first invocation's context, which had stopped answering when
        /// the invocation ended: Spanish came back as the neutral text and the import threw.</summary>
        [Fact]
        public void ASecondInvocationOverTheSameImageStillFindsItsSatellitesAndItsNativeLibrary()
        {
            string name = "SatTwice" + Guid.NewGuid().ToString("N");
            string path = EmitImage(name, null);
            Assembly first;
            using (var images = new ImageLoadContext())
            {
                first = images.LoadImage(path);
                var probe = first.GetType(name + ".Probe", true);
                Assert.Equal("bonjour", (string) probe.GetMethod("Hello").Invoke(null, new object[] { "fr" }));
            }

            using (var images = new ImageLoadContext())
            {
                var second = images.LoadImage(path);
                Assert.Same(first, second);
                var probe = second.GetType(name + ".Probe", true);
                Assert.Equal("hola", (string) probe.GetMethod("Hello").Invoke(null, new object[] { "es" }));
                Assert.NotEqual(0u, (uint) probe.GetMethod("Crc").Invoke(null, null));
            }
        }

        [Fact]
        public void TheHostProcessFindsAnImagesSatelliteAndNativeLibraryWhileItsHooksRun()
        {
            string name = "SatHost" + Guid.NewGuid().ToString("N");
            string report = Path.Combine(_dir, "report.txt");
            string image = EmitImage(name, report);
            string template = Path.Combine(_dir, "probe.heddle");
            File.WriteAllText(template, "Probe: @probe()\n");
            string rsp = Path.Combine(_dir, "probe.rsp");
            File.WriteAllLines(rsp, new[]
            {
                "--project", Path.Combine(_dir, "proj.csproj"), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "Native",
                "--reference", image,
                "--template", template + "|probe|||",
                "--artifact-out", Path.Combine(_dir, "probe.bin"),
                "--source-out", Path.Combine(_dir, "probe.g.cs"),
                "--stamp", Path.Combine(_dir, "probe.txt")
            });

            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
            start.ArgumentList.Add("compile");
            start.ArgumentList.Add("@" + rsp);
            string output;
            using (var host = Process.Start(start))
            {
                output = host.StandardOutput.ReadToEnd() + host.StandardError.ReadToEnd();
                Assert.True(host.WaitForExit(120000), "the host did not exit.");
                Assert.True(host.ExitCode == 0, "exit " + host.ExitCode + "\n" + output);
            }

            Assert.True(File.Exists(report), "the extension's hook did not run.\n" + output);
            string[] found = File.ReadAllText(report).Split('|');
            Assert.Equal("bonjour", found[0]);
            Assert.True(uint.TryParse(found[1], System.Globalization.NumberStyles.HexNumber, null, out uint crc) && crc != 0,
                "the native import failed in the host: " + found[1]);
        }
    }
}
