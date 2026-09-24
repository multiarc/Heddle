using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Heddle.Data;
using Heddle.Precompiled;

namespace Heddle.Samples.Codegen
{
    // Sample 8 — Heddle as a build-time code/text generator (a T4 successor). templates/report.heddle is compiled
    // at BUILD time by Heddle.Build (the out-of-process host) into a typed entry point; Program.cs calls it.
    // The build host itself is a build-time-only dependency — it is not present in the runtime output (asserted).
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

            // The build pins Text + untrimmed directive lines for code generation; the typed entry
            // validates ExpressionMode and TrimDirectiveLines against these process-wide defaults.
            PrecompiledTemplates.DefaultOptions = new TemplateOptions { TrimDirectiveLines = false };

            // The generated typed entry point — no runtime parse or compile of the template.
            var rendered = global::Heddle.Generated.Templates_Report.Generate(model);

            // Structural check: the build host is a build-time tool, so its assemblies must NOT ship at runtime.
            var binDir = AppContext.BaseDirectory;
            bool generatorPresent = File.Exists(Path.Combine(binDir, "Heddle.Build.dll"));
            bool languageGeneratorPresent = Directory.EnumerateFiles(binDir, "Heddle.Build*.dll").Any();
            bool enginePresent = File.Exists(Path.Combine(binDir, "Heddle.dll"));
            if (generatorPresent || languageGeneratorPresent)
                throw new InvalidOperationException("STRUCTURAL FAIL: Heddle.Build shipped in the runtime output.");

            var report = new StringBuilder();
            report.Append("Heddle.Build.dll (build-time host) present at runtime: ")
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
            // The build host emits one Heddle.CompiledForm.g.cs under obj/ (artifact class plus one
            // typed entry class per template).
            var searchRoot = SampleCapture.SampleRoot();
            var file = Directory.EnumerateFiles(searchRoot, "Heddle.CompiledForm.g.cs", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
            if (file == null)
                return "// generated entry point not found on disk\n";
            var source = File.ReadAllText(file).Replace("\r\n", "\n");
            // Mask the engine version and every content digest so the golden is stable across builds.
            source = Regex.Replace(source, @"\b\d+\.\d+\.\d+\b", "<version>");
            source = Regex.Replace(source, @"[0-9a-f]{64}", "<digest>");
            return source;
        }
    }
}
