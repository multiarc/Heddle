using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using Heddle.Benchmarks.Dotnet.Engines;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// Steady-state view resolution: what a hosted caller pays on the SECOND and every later request
    /// for a view it has already resolved once. <see cref="StartupBenchmarks"/> measures first use
    /// (register, bind, first render); nothing measured repeat resolution, which is where an MVC-style
    /// host spends one call per HTTP request for the life of the process.
    ///
    /// <para>The rows are a contrast, not a ranking. <see cref="ResolvePrecompiledHit"/> is a registry
    /// hit — the tier the artifact exists for; <see cref="ResolveDynamicCacheHit"/> is the same call
    /// answered by the resolver's own template cache, i.e. a dictionary lookup, and is the floor the
    /// precompiled row should be approaching; <see cref="ResolveHostedViewLadder"/> is the hosted
    /// <c>View</c> arm, whose probe ladder formats and options-builds one candidate per location
    /// before any tier has answered.</para>
    ///
    /// <para>No render happens here on purpose: <c>GetTemplate</c> is the measured unit, so a render's
    /// cost cannot mask the resolution's.</para>
    /// </summary>
    [MemoryDiagnoser]
    public class ResolverBenchmarks
    {
        private const string PrecompiledWorkload = "composed-page";

        private TemplateResolver _precompiledResolver;
        private CompileContext _precompiledContext;
        private string _precompiledView;

        private TemplateResolver _dynamicResolver;
        private CompileContext _dynamicContext;
        private string _dynamicView;

        private TemplateResolver _hostedResolver;
        private CompileContext _hostedContext;
        private string _hostedView;

        private string _scratch;

        [GlobalSetup]
        public void Setup()
        {
            var entries = Engines.Precompiled.Entries();
            var key = PrecompiledWorkload + ".heddle";
            if (!entries.TryGetValue(key, out var entry))
                throw new InvalidOperationException(
                    "no precompiled entry for '" + key + "' — the Heddle.Build tier should have compiled it.");

            _precompiledView = key;
            var heddleRoot = Path.Combine(Templates.Root(), "controlled", "heddle");
            // The request shape the entry was built under, so the gauntlet's options step passes and the
            // row measures a registry HIT rather than a fallback.
            var precompiledOptions = new TemplateOptions(PrecompiledWorkload)
            {
                FileNamePostfix = ".heddle",
                RootPath = heddleRoot,
                OutputProfile = entry.OptionsFingerprint.Profile,
                ExpressionMode = entry.OptionsFingerprint.ExpressionMode,
                TrimDirectiveLines = entry.OptionsFingerprint.TrimDirectiveLines,
                ProvideLanguageFeatures = false,
            };
            _precompiledContext = new CompileContext(precompiledOptions,
                new ExType(entry.ModelType ?? typeof(object)));
            _precompiledResolver = new TemplateResolver(Path.Combine(heddleRoot, "any.heddle"), false,
                entry.OptionsFingerprint.Profile, entry.OptionsFingerprint.TrimDirectiveLines);

            // The dynamic contrast and the hosted ladder both need templates the registry does NOT
            // carry, so they go in a scratch tree under a name no artifact key spells.
            _scratch = Path.Combine(Path.GetTempPath(), "heddle-resolver-bench-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_scratch, "views", "bench"));
            _dynamicView = "resolver-bench-dynamic.heddle";
            File.WriteAllText(Path.Combine(_scratch, _dynamicView), "resolver bench dynamic row\n");
            _hostedView = "resolver-bench-hosted";
            File.WriteAllText(Path.Combine(_scratch, "views", "bench", _hostedView + ".heddle"),
                "resolver bench hosted row\n");

            var dynamicOptions = new TemplateOptions("resolver-bench-dynamic")
            {
                FileNamePostfix = ".heddle",
                RootPath = _scratch,
                OutputProfile = OutputProfile.Text,
                ExpressionMode = ExpressionMode.Native,
                ProvideLanguageFeatures = false,
            };
            _dynamicContext = new CompileContext(dynamicOptions, new ExType(typeof(object)));
            _dynamicResolver = new TemplateResolver(Path.Combine(_scratch, "any.heddle"), false,
                OutputProfile.Text, true);

            // The View arm builds its own options and ignores the caller's context's, so this one only
            // supplies the profile/trim the resolver was constructed with.
            var hostedOptions = new TemplateOptions("resolver-bench-hosted")
            {
                FileNamePostfix = ".heddle",
                RootPath = _scratch,
                OutputProfile = OutputProfile.Text,
                ExpressionMode = ExpressionMode.FullCSharp,
                ProvideLanguageFeatures = false,
            };
            _hostedContext = new CompileContext(hostedOptions, new ExType(typeof(object)));
            _hostedResolver = new TemplateResolver(Path.Combine(_scratch, "any.heddle"), false,
                OutputProfile.Text, true);

            // Warm every row once, outside the timed region: the measurement is the REPEAT resolution,
            // and a first call would fold a parse and a compile into it.
            for (int i = 0; i < 3; i++)
            {
                if (ResolvePrecompiledHit() == null)
                    throw new InvalidOperationException("the precompiled row resolved nothing.");
                if (ResolveDynamicCacheHit() == null)
                    throw new InvalidOperationException("the dynamic row resolved nothing.");
                if (ResolveHostedViewLadder() == null)
                    throw new InvalidOperationException("the hosted row resolved nothing.");
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            try
            {
                if (_scratch != null && Directory.Exists(_scratch))
                    Directory.Delete(_scratch, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>A registered precompiled entry, resolved again: registry lookup, per-request
        /// gauntlet, memoized materialization, adapter template.</summary>
        [Benchmark(Baseline = true)]
        public HeddleTemplate ResolvePrecompiledHit() =>
            _precompiledResolver.GetTemplate(_precompiledView, "bench", out _, _precompiledContext,
                TemplatePathType.None);

        /// <summary>The same call for a template the registry does not carry: registry miss, then the
        /// resolver's own cache — the floor the precompiled row is measured against.</summary>
        [Benchmark]
        public HeddleTemplate ResolveDynamicCacheHit() =>
            _dynamicResolver.GetTemplate(_dynamicView, "bench", out _, _dynamicContext,
                TemplatePathType.None);

        /// <summary>The hosted <c>View</c> probe ladder: every location formatted and options-built for
        /// the registry pass, then formatted again for the cache pass that answers.</summary>
        [Benchmark]
        public HeddleTemplate ResolveHostedViewLadder() =>
            _hostedResolver.GetTemplate(_hostedView, "bench", out _, _hostedContext,
                TemplatePathType.View);
    }
}
