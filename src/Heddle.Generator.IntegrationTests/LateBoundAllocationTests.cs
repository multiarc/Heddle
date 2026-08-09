using System;
using System.Reflection;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Allocation-identity proof for the <c>LateBound</c> plan, in the differential style of
    /// <see cref="EngineAccessorAllocationTests"/>: two templates rendering the same bytes — one calling a
    /// built-in bound at build time, one calling a host registration bound at first render — are measured over
    /// the same render loop, so every shared cost cancels and the delta isolates the late-bound site's own
    /// per-render price, which must be zero once the first render has bound.
    /// <para>What could have cost per render, and does not: the bind is cached on the site, the registry check is
    /// a reference comparison against a thread-static read, and the argument-type vector the bind needs is a
    /// static field of a generic holder rather than a params array built at the call.</para>
    /// </summary>
    public class LateBoundAllocationTests
    {
        private const string Model =
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.LateBoundModel}}@\\\n";

        private static IProcessStrategy Root(DifferentialHarness.GenResult gen, string key)
        {
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var entry = DifferentialHarness.FindEntryTypeByKey(gen.Assembly, key);
            Assert.NotNull(entry);
            return (IProcessStrategy) entry
                .GetField("Root", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
        }

        private static long Measure(Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public void TheLateBoundSiteAddsNoPerRenderAllocationOnceItHasBound()
        {
            const int n = 2000;
            const string directKey = "views/late-alloc-direct.heddle";
            const string lateKey = "views/late-alloc-late.heddle";

            // upper() is a built-in the build binds outright; hostupper is the same transform reached only through
            // a run-time registration, so the two documents render identical bytes by different plans.
            var gen = DifferentialHarness.Generate(new[]
            {
                (directKey, Model + "[@(upper(Name))]\n"),
                (lateKey, Model + "[@(hostupper(Name))]\n")
            });
            var direct = Root(gen, directKey);
            var late = Root(gen, lateKey);

            var registry = new FunctionRegistry();
            registry.Register("hostupper",
                new Func<string, string>(s => (s ?? string.Empty).ToUpperInvariant()));
            var options = new TemplateOptions { Functions = registry };
            var model = new LateBoundModel { Name = "widget", Stock = 7 };

            Assert.Equal(PrecompiledRuntime.GenerateString(direct, model, null, null, options),
                PrecompiledRuntime.GenerateString(late, model, null, null, options));

            for (int i = 0; i < 200; i++)
            {
                PrecompiledRuntime.GenerateString(direct, model, null, null, options);
                PrecompiledRuntime.GenerateString(late, model, null, null, options);
            }

            long directAlloc = Measure(() =>
            {
                for (int i = 0; i < n; i++)
                    PrecompiledRuntime.GenerateString(direct, model, null, null, options);
            });
            long lateAlloc = Measure(() =>
            {
                for (int i = 0; i < n; i++)
                    PrecompiledRuntime.GenerateString(late, model, null, null, options);
            });
            long delta = lateAlloc - directAlloc;

            // Zero per render: the delta must not grow with n. The band absorbs one-off runtime artifacts
            // (tiering, GC bookkeeping) without admitting even a single byte per render.
            Assert.True(delta <= n && delta >= -n,
                $"late-bound delta {delta} bytes over {n} renders (direct {directAlloc}, late {lateAlloc}) — " +
                "expected no per-render allocation once the site has bound");
        }
    }
}
