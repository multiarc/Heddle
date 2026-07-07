using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;

namespace Heddle.Samples.Sandboxed
{
    // Sample 4 — the trust boundary. User-supplied templates run under ExpressionMode.Native with a curated
    // FunctionRegistry: only whitelisted functions, operators, literals and member paths are reachable. Hostile
    // templates (C#-tier escapes, unregistered calls, method calls) are declined at compile with positioned
    // HED1xxx diagnostics and never execute — proven by a side-effect canary that must stay unfired.
    public static class Canary
    {
        public static bool Fired { get; private set; }

        // Reachable only through the C# tier, which is OFF in Native mode. If a hostile template ever executed
        // arbitrary code, this would flip — the capture asserts it never does.
        public static string Fire()
        {
            Fired = true;
            return "PWNED";
        }
    }

    internal sealed class UserModel
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            HeddleTemplate.Configure(typeof(Program).Assembly);

            // The host curates exactly what user templates may call — the trust boundary.
            var registry = new FunctionRegistry();
            registry.Register("shout", (Func<string, string>)(s => (s ?? string.Empty).ToUpperInvariant()));

            var model = new UserModel { Name = "world", Count = 3 };

            // Benign inputs: operators, literals, member paths, and the one whitelisted function.
            var benign = new (string Name, string Template)[]
            {
                ("arithmetic", "@(Count * 2 + 1)"),
                ("member-path", "Hello, @(Name)!"),
                ("whitelisted", "@(shout(Name))"),
                ("conditional", "@if(Count > 0){{has items}}@ifnot(Count > 0){{empty}}")
            };

            // Hostile inputs: each must be declined at compile and never render.
            var hostile = new (string Name, string Template)[]
            {
                ("csharp-escape", "@(@ Heddle.Samples.Sandboxed.Canary.Fire() )"),
                ("unregistered-call", "@(danger(Name))"),
                ("method-call", "@(Name.ToUpper())")
            };

            var rendered = new List<(string Name, string Output)>();
            var rejected = new List<string>();

            foreach (var (name, template) in benign)
            {
                var result = Compile(template, registry, out var output);
                if (result != null)
                    throw new InvalidOperationException($"benign template '{name}' unexpectedly rejected: {result}");
                rendered.Add((name, output));
            }

            foreach (var (name, template) in hostile)
            {
                var error = Compile(template, registry, out _);
                if (error == null)
                    throw new InvalidOperationException($"hostile template '{name}' was NOT rejected — sandbox breach!");
                rejected.Add($"{name}: {Describe(error)}");
            }

            if (Canary.Fired)
                throw new InvalidOperationException("CANARY FIRED — a rejected template executed. Sandbox breach!");

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                foreach (var (name, output) in rendered)
                    SampleCapture.Write(capture, $"rendered/{name}.txt", output);
                SampleCapture.Write(capture, "rejected.txt", string.Join("\n", rejected) + "\n");
                Console.WriteLine($"captured {rendered.Count} rendered/*.txt + rejected.txt; canary unfired");
                return 0;
            }

            Console.WriteLine("=== accepted templates ===");
            foreach (var (name, output) in rendered)
                Console.WriteLine($"[{name}] {output}");
            Console.WriteLine("=== rejected templates (positioned diagnostics) ===");
            foreach (var line in rejected)
                Console.WriteLine(line);
            Console.WriteLine($"canary fired: {Canary.Fired}");
            return 0;
        }

        // Returns null on success (output set), or the first compile error on rejection.
        private static HeddleCompileError Compile(string template, FunctionRegistry registry, out string output)
        {
            output = null;
            var options = new TemplateOptions
            {
                ExpressionMode = ExpressionMode.Native,   // the C# tier is unavailable — the sandbox
                Functions = registry
            };
            using var t = new HeddleTemplate(template, new CompileContext(options, typeof(UserModel)));
            if (!t.CompileResult.Success)
                return t.CompileResult.ErrorList.First();
            output = t.Generate(new UserModel { Name = "world", Count = 3 });
            return null;
        }

        private static string Describe(HeddleCompileError error)
        {
            var id = error.DiagnosticId ?? "—";
            return $"{id} @ {error.Position.StartIndex}:{error.Position.Length} {error.Error}";
        }
    }
}
