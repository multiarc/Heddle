using System;
using System.Reflection;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Allocation-identity proof for the per-node escapes, in the differential style of the engine's
    /// BranchAllocationTests: two templates rendering the same bytes — one through the direct typed plan, one
    /// through the engine accessor — are measured over the same render loop, so every shared cost cancels and the
    /// delta isolates the escape's own per-render price, which must be zero. The accessor is built once at
    /// type-init; each render is one static delegate call returning an existing string.
    /// </summary>
    public class EngineAccessorAllocationTests
    {
        private const string Model =
            "@model(){{Heddle.Generator.IntegrationTests.Fixtures.AccessorAllocationModel}}@\\\n";

        private static IProcessStrategy Root(DifferentialHarness.GenResult gen, string key)
        {
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var entry = DifferentialHarness.FindEntryTypeByKey(gen.Assembly, key);
            Assert.NotNull(entry);
            return (IProcessStrategy) entry
                .GetField("Root", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);
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

        private static void AssertEscapeAddsNoPerRenderAllocation(string directTemplate, string escapedTemplate)
        {
            const int n = 2000;
            const string directKey = "views/alloc-direct.heddle";
            const string escapedKey = "views/alloc-escaped.heddle";
            var gen = DifferentialHarness.Generate(new[]
            {
                (directKey, Model + directTemplate),
                (escapedKey, Model + escapedTemplate)
            });
            var direct = Root(gen, directKey);
            var escaped = Root(gen, escapedKey);
            var model = new AccessorAllocationModel();

            // Identical output, so every shared allocation (renderer, scope, result string) cancels.
            Assert.Equal(PrecompiledRuntime.GenerateString(direct, model, null, null),
                PrecompiledRuntime.GenerateString(escaped, model, null, null));

            for (int i = 0; i < 200; i++)
            {
                PrecompiledRuntime.GenerateString(direct, model, null, null);
                PrecompiledRuntime.GenerateString(escaped, model, null, null);
            }

            long directAlloc = Measure(() =>
            {
                for (int i = 0; i < n; i++)
                    PrecompiledRuntime.GenerateString(direct, model, null, null);
            });
            long escapedAlloc = Measure(() =>
            {
                for (int i = 0; i < n; i++)
                    PrecompiledRuntime.GenerateString(escaped, model, null, null);
            });
            long delta = escapedAlloc - directAlloc;

            // Zero per render: the delta must not grow with n. The band absorbs one-off runtime artifacts
            // (tiering, GC bookkeeping) without admitting even a single byte per render.
            Assert.True(delta <= n && delta >= -n,
                $"escape delta {delta} bytes over {n} renders (direct {directAlloc}, escaped {escapedAlloc}) — " +
                "expected no per-render allocation from the accessor");
        }

        /// <summary>The member-tier escape (<c>MemberAccessor</c>): a hidden member read allocates exactly what
        /// the direct typed read allocates.</summary>
        [Fact]
        public void TheMemberAccessorEscapeAddsNoPerRenderAllocation() =>
            AssertEscapeAddsNoPerRenderAllocation("[@(Direct)]\n", "[@(Hidden)]\n");

        /// <summary>The native-tier escape (<c>NativeAccessor</c>, the three-channel delegate): same proof over
        /// the <c>this.</c>-rooted expression shape.</summary>
        [Fact]
        public void TheNativeAccessorEscapeAddsNoPerRenderAllocation() =>
            AssertEscapeAddsNoPerRenderAllocation("[@(this.Direct)]\n", "[@(this.Hidden)]\n");
    }
}
