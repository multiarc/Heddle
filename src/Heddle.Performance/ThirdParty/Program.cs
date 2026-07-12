using System;
using System.Globalization;
using BenchmarkDotNet.Running;

namespace Heddle.Performance.ThirdParty
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            // Fast, host-free parity self-test (D3-R3): confirms the Heddle entry renders the scenario's
            // expected output (matched against Fluid rendering the unmodified product.liquid, and against
            // every engine's CheckBenchmark oracle) without spinning up the full BenchmarkDotNet run.
            //   dotnet run -c Release --project src/Heddle.Performance/ThirdParty -- parity
            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "parity", StringComparison.OrdinalIgnoreCase))
            {
                var (pass, text) = Parity.Report();
                Console.Write(text);
                return pass ? 0 : 1;
            }

            // Prints the Heddle entry's rendered output (for eyeballing the scenario shape).
            //   dotnet run -c Release --project src/Heddle.Performance/ThirdParty -- dump
            if (args != null && args.Length > 0 &&
                string.Equals(args[0], "dump", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write(new HeddleBenchmarks().Render());
                return 0;
            }

            // Otherwise defer to BenchmarkDotNet, e.g.:
            //   dotnet run -c Release --project src/Heddle.Performance/ThirdParty -- --filter *HeddleComparisonBenchmarks*
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            return 0;
        }
    }
}
