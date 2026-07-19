using System;
using System.IO;

namespace Heddle.Tool
{
    /// <summary>
    /// The <c>heddle</c> CLI entry point (WI12). Usage:
    /// <code>heddle render &lt;template&gt; [--model-json &lt;file&gt;] [--out &lt;file&gt;] [--root &lt;dir&gt;]</code>
    /// Hosts the full dynamic engine (the T4-successor story): invoked from an MSBuild <c>Exec</c> step it turns data
    /// into generated source, and the resulting artifact carries no runtime Heddle dependency.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

        /// <summary>Runs the CLI with injectable streams (testable). Returns a process exit code.</summary>
        public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            if (args == null || args.Length == 0 || IsHelp(args[0]))
            {
                stdout.WriteLine(Usage);
                return args != null && args.Length != 0 && IsHelp(args[0]) ? 0 : 1;
            }

            if (!string.Equals(args[0], "render", StringComparison.Ordinal))
            {
                stderr.WriteLine("Unknown command '" + args[0] + "'.");
                stderr.WriteLine(Usage);
                return 2;
            }

            string template = null, modelJsonPath = null, outPath = null, root = null;
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--model-json": modelJsonPath = Next(args, ref i); break;
                    case "--out": outPath = Next(args, ref i); break;
                    case "--root": root = Next(args, ref i); break;
                    default:
                        if (args[i].StartsWith("-", StringComparison.Ordinal))
                        {
                            stderr.WriteLine("Unknown option '" + args[i] + "'.");
                            return 2;
                        }

                        if (template != null)
                        {
                            stderr.WriteLine("Unexpected argument '" + args[i] + "'.");
                            return 2;
                        }

                        template = args[i];
                        break;
                }
            }

            if (string.IsNullOrEmpty(template))
            {
                stderr.WriteLine("A template path is required.");
                stderr.WriteLine(Usage);
                return 2;
            }

            try
            {
                if (!File.Exists(template))
                {
                    stderr.WriteLine("Template file not found: " + template);
                    return 3;
                }

                var templateText = File.ReadAllText(template);
                var modelJson = modelJsonPath != null ? File.ReadAllText(modelJsonPath) : null;
                var effectiveRoot = root ?? Path.GetDirectoryName(Path.GetFullPath(template));

                var output = HeddleRenderer.Render(templateText, modelJson, effectiveRoot);

                if (outPath != null)
                    File.WriteAllText(outPath, output);
                else
                    stdout.Write(output);
                return 0;
            }
            catch (Exception ex)
            {
                stderr.WriteLine("heddle: " + ex.Message);
                return 4;
            }
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
                throw new ArgumentException("Missing value for option '" + args[i] + "'.");
            return args[++i];
        }

        private static bool IsHelp(string arg) =>
            arg == "-h" || arg == "--help" || arg == "help" || arg == "-?";

        private const string Usage =
            "heddle — render a Heddle template against a JSON model (T4-successor codegen).\n\n" +
            "Usage:\n" +
            "  heddle render <template> [--model-json <file>] [--out <file>] [--root <dir>]\n\n" +
            "Options:\n" +
            "  --model-json <file>  JSON file whose object is the template model (objects -> members,\n" +
            "                       arrays -> @list/@for, scalars -> primitives). Omit for a model-less template.\n" +
            "  --out <file>         Write output to this file instead of stdout.\n" +
            "  --root <dir>         Resolver root for @partial/@<< imports (default: the template's directory).";
    }
}
