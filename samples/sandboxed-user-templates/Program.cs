using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Exceptions;
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

            // Well-formed but hostile: each compiles (no C#-tier escape, no unregistered call), so the compile-time
            // trust boundary lets it through — but a runaway loop would burn CPU/memory unbounded. A RenderBudget is
            // the availability leg: it stops the render at the seam and throws TemplateRenderBudgetException. One
            // scenario per budget kind proves each dimension independently caps a runaway template.
            const string runaway = "@for(1000000000){{x}}";
            var budgetRuns = new (string Name, RenderBudget Budget)[]
            {
                ("output-chars", new RenderBudget { MaxOutputChars = 1000 }),
                ("render-ops",   new RenderBudget { MaxRenderOps = 1000 }),
                ("render-time",  new RenderBudget { MaxRenderTime = TimeSpan.FromMilliseconds(50) }),
            };

            var rendered = new List<(string Name, string Output)>();
            var rejected = new List<string>();
            var budgeted = new List<string>();

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

            foreach (var (name, budget) in budgetRuns)
            {
                var kind = RunWithBudget(runaway, registry, budget);
                if (kind == null)
                    throw new InvalidOperationException($"budget '{name}' did NOT stop the runaway template — availability breach!");
                budgeted.Add($"{name}: stopped by {kind}");
            }

            if (Canary.Fired)
                throw new InvalidOperationException("CANARY FIRED — a rejected template executed. Sandbox breach!");

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                foreach (var (name, output) in rendered)
                    SampleCapture.Write(capture, $"rendered/{name}.txt", output);
                SampleCapture.Write(capture, "rejected.txt", string.Join("\n", rejected) + "\n");
                SampleCapture.Write(capture, "budgets.txt", string.Join("\n", budgeted) + "\n");
                Console.WriteLine($"captured {rendered.Count} rendered/*.txt + rejected.txt + budgets.txt; canary unfired");
                return 0;
            }

            Console.WriteLine("=== accepted templates ===");
            foreach (var (name, output) in rendered)
                Console.WriteLine($"[{name}] {output}");
            Console.WriteLine("=== rejected templates (positioned diagnostics) ===");
            foreach (var line in rejected)
                Console.WriteLine(line);
            Console.WriteLine("=== runaway templates stopped by render budgets ===");
            foreach (var line in budgeted)
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

        // Renders a well-formed but runaway template under a budget; returns the RenderBudgetKind that stopped it,
        // or null if it completed (which would be an availability breach for the runaway loop above).
        private static RenderBudgetKind? RunWithBudget(string template, FunctionRegistry registry, RenderBudget budget)
        {
            var options = new TemplateOptions
            {
                ExpressionMode = ExpressionMode.Native,
                Functions = registry,
                RenderBudget = budget,
            };
            using var t = new HeddleTemplate(template, new CompileContext(options, typeof(UserModel)));
            if (!t.CompileResult.Success)
                throw new InvalidOperationException("runaway template unexpectedly rejected at compile: " +
                    t.CompileResult.ErrorList.First());
            try
            {
                t.Generate(new UserModel { Name = "world", Count = 3 });
                return null;
            }
            catch (TemplateRenderBudgetException ex)
            {
                return ex.Kind;
            }
        }

        private static string Describe(HeddleCompileError error)
        {
            var id = error.DiagnosticId ?? "—";
            return $"{id} @ {error.Position.StartIndex}:{error.Position.Length} {error.Error}";
        }
    }
}
