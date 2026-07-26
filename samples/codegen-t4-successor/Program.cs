using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Heddle.Samples.Codegen
{
    // Sample 8 — Heddle as a build-time code/text generator (a T4 successor). templates/report.heddle is compiled
    // at BUILD time by Heddle.Generator into a typed entry point; Program.cs calls it. The code generator itself
    // (Heddle.Generator) is a build-time-only dependency — it is not present in the runtime output (asserted).
    public sealed class BuildInfo
    {
        public string Project { get; set; }
        public string Version { get; set; }
        public string Commit { get; set; }
    }

    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var model = new BuildInfo { Project = "Heddle", Version = "2.0.0", Commit = "deadbeef" };

            // The generated typed entry point — no runtime parse or compile of the template.
            var rendered = global::Heddle.Generated.Templates_Report.Generate(model);

            // Structural check: the code generator is a build-time tool, so its assembly must NOT ship at runtime.
            var binDir = AppContext.BaseDirectory;
            bool generatorPresent = File.Exists(Path.Combine(binDir, "Heddle.Generator.dll"));
            bool languageGeneratorPresent = Directory.EnumerateFiles(binDir, "Heddle.Generator*.dll").Any();
            bool enginePresent = File.Exists(Path.Combine(binDir, "Heddle.dll"));
            if (generatorPresent || languageGeneratorPresent)
                throw new InvalidOperationException("STRUCTURAL FAIL: Heddle.Generator shipped in the runtime output.");

            var report = new StringBuilder();
            report.Append("Heddle.Generator.dll (build-time code generator) present at runtime: ")
                  .Append(generatorPresent).Append('\n');
            report.Append("Heddle.dll (precompiled render runtime) present at runtime: ")
                  .Append(enginePresent).Append('\n');

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                SampleCapture.Write(capture, "codegen-output.txt", rendered);
                SampleCapture.Write(capture, "dependency-report.txt", report.ToString());
                SampleCapture.Write(capture, "generated-source.cs.txt", SanitizedEntryPoint());
                Console.WriteLine("captured codegen-output.txt, dependency-report.txt, generated-source.cs.txt");
                return 0;
            }

            Console.WriteLine("=== generated report (rendered via the build-time entry point) ===\n" + rendered);
            Console.WriteLine("=== dependency report ===\n" + report);
            return 0;
        }

        // The emitted entry-point source, with the non-deterministic manifest details (engine version, content
        // hashes) left out — the golden pins the STABLE shape of the generated code (a reviewed spec artifact).
        private static string SanitizedEntryPoint()
        {
            // The generator emits under obj/…/generated when no explicit output path is set.
            var searchRoot = SampleCapture.SampleRoot();
            var file = Directory.EnumerateFiles(searchRoot, "Templates_Report.g.cs", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (file == null)
                return "// generated entry point not found on disk\n";
            var source = File.ReadAllText(file).Replace("\r\n", "\n");
            // Drop any content-hash/version metadata lines so the golden is stable across builds.
            source = Regex.Replace(source, @"(?m)^.*(ContentHash|contentHash|engineVersion|schemaVersion|Version =).*$\n?", string.Empty);
            return source;
        }
    }
}
