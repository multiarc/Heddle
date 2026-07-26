using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The retention contract of the per-<see cref="Compilation"/> type index. In IDE scenarios where compilations
    /// accumulate per keystroke-batch and are never handed back, a bare static cache would retain every entry. These
    /// tests pin the three parts of the solution: an operation API, observable occupancy bounded by capacity, and
    /// eviction that counts <i>age</i>, not only size. The invariant enabling all of it: <b>what the cache holds can
    /// never change what resolution answers</b>.
    /// </summary>
    public class SymbolTypeIndexCacheTests
    {
        private static readonly IReadOnlyList<MetadataReference> References = LoadReferences();

        private static IReadOnlyList<MetadataReference> LoadReferences()
        {
            var tpa = (string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            return tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
        }

        /// <summary>A fresh compilation carrying one uniquely named probe type, so an index built from it is
        /// distinguishable from an index built from any other.</summary>
        private static Compilation Probe(string marker) =>
            CSharpCompilation.Create("CacheProbe_" + marker,
                new[] { CSharpSyntaxTree.ParseText("namespace CacheProbe { public class P" + marker + " { } }") },
                References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        [Fact]
        public void RepeatedAsksForOneCompilationShareTheBuiltIndex()
        {
            var cache = new SymbolTypeIndexCache(capacity: 4, maxIdleGenerations: 4);
            var compilation = Probe("A");

            var first = cache.Get(compilation);
            var second = cache.Get(compilation);

            Assert.Same(first, second);
            Assert.Equal(1, cache.Count);
            Assert.True(cache.Contains(compilation));
        }

        [Fact]
        public void OccupancyIsObservableAndNeverExceedsTheStatedCapacity()
        {
            var cache = new SymbolTypeIndexCache(capacity: 3, maxIdleGenerations: 1000);
            Assert.Equal(3, cache.Capacity);

            for (int i = 0; i < 12; i++)
            {
                cache.Get(Probe("Cap" + i));
                Assert.True(cache.Count <= cache.Capacity,
                    "occupancy " + cache.Count + " exceeded capacity " + cache.Capacity + " after " + (i + 1) +
                    " compilations");
            }

            Assert.Equal(3, cache.Count);
        }

        [Fact]
        public void AnEntryIdleAcrossTooManyNewCompilationsIsEvictedWhileTheCacheIsStillNearlyEmpty()
        {
            // Capacity is never approached here: the only thing that can drop the entry is its age.
            var cache = new SymbolTypeIndexCache(capacity: 64, maxIdleGenerations: 2);
            var old = Probe("Old");
            cache.Get(old);
            Assert.True(cache.Contains(old));

            cache.Get(Probe("Edit1"));
            Assert.True(cache.Contains(old), "one newer compilation is not yet staleness");

            cache.Get(Probe("Edit2"));
            Assert.True(cache.Contains(old),
                "idle for exactly MaxIdleGenerations generations is the last live generation, not the first stale one");

            cache.Get(Probe("Edit3"));

            Assert.False(cache.Contains(old), "an entry untouched across 3 newer compilations is stale");
            Assert.True(cache.Count < cache.Capacity, "the eviction was age-driven, not capacity-driven");
        }

        [Fact]
        public void UseRefreshesAnEntrySoALiveCompilationIsNotAgedOut()
        {
            var cache = new SymbolTypeIndexCache(capacity: 64, maxIdleGenerations: 2);
            var live = Probe("Live");
            var index = cache.Get(live);

            for (int i = 0; i < 10; i++)
            {
                cache.Get(Probe("Neighbour" + i));
                cache.Get(live);
            }

            Assert.True(cache.Contains(live), "a compilation asked about every generation is not stale");
            // Instance identity, not mere presence: an entry that was aged out and rebuilt would also be present,
            // and a refresh that does not refresh would be invisible without this.
            Assert.Same(index, cache.Get(live));
        }

        /// <summary>Capacity pressure drops the <i>least recently used</i> entry — the compilation still being
        /// asked about survives, which is the whole point of bounding rather than flushing.</summary>
        [Fact]
        public void CapacityPressureDropsTheLeastRecentlyUsedEntry()
        {
            var cache = new SymbolTypeIndexCache(capacity: 2, maxIdleGenerations: 1000);
            var kept = Probe("Kept");
            var idle = Probe("Idle");
            cache.Get(kept);
            cache.Get(idle);
            cache.Get(kept);

            cache.Get(Probe("New"));

            Assert.True(cache.Contains(kept), "the recently used entry was evicted");
            Assert.False(cache.Contains(idle), "the least recently used entry was kept");
        }

        [Fact]
        public void ClearDropsEverything()
        {
            var cache = new SymbolTypeIndexCache(capacity: 4, maxIdleGenerations: 4);
            cache.Get(Probe("X"));
            cache.Get(Probe("Y"));
            Assert.True(cache.Count > 0);

            cache.Clear();

            Assert.Equal(0, cache.Count);

            // Clear must leave eviction bookkeeping consistent with entries.
            var after = Probe("Z");
            Assert.NotNull(cache.Get(after));
            Assert.True(cache.Contains(after));
            Assert.Equal(1, cache.Count);
        }

        /// <summary>
        /// The determinism pin: a cache that evicts on every single insert (capacity 1, zero idle tolerance) and a
        /// cache that never evicts must answer <b>identically</b> for the same compilation and the same spellings.
        /// Generation output may depend on the compilation; it may not depend on cache state.
        /// </summary>
        [Fact]
        public void EvictionCannotChangeWhatTheIndexAnswers()
        {
            var thrashing = new SymbolTypeIndexCache(capacity: 1, maxIdleGenerations: 0);
            var roomy = new SymbolTypeIndexCache(capacity: 64, maxIdleGenerations: 1000);
            var subject = Probe("Subject");
            var noise = Probe("Noise");
            var imports = new[] { "CacheProbe", "System.Collections.Generic" };
            var spellings = new[] { "PSubject", "List`1", "System.String", "PNoise", "NoSuchType" };

            foreach (var spelling in spellings)
            {
                var expected = Describe(roomy.Get(subject), spelling, imports);

                // Interleave a second compilation so every ask of `subject` on the thrashing cache is a rebuild.
                thrashing.Get(noise);
                var actual = Describe(thrashing.Get(subject), spelling, imports);

                Assert.Equal(expected, actual);
            }

            Assert.True(thrashing.Count <= 1);
        }

        /// <summary>
        /// The production entry point is the shared cache and nothing else, so the bounds above are the bounds the
        /// generator actually runs under. Asserted through the generation counter and the occupancy invariant
        /// rather than through instance identity: other test classes resolve model types concurrently on this same
        /// shared cache and may evict this compilation between two asks — which is the invariant, not a defect.
        /// </summary>
        [Fact]
        public void TheProductionEntryPointServesFromTheSharedCacheAndTheSharedCacheIsBounded()
        {
            var shared = SymbolTypeIndexCache.Shared;
            Assert.True(shared.Capacity > 0);
            Assert.True(shared.MaxIdleGenerations > 0);

            var before = shared.Generation;
            var index = SymbolTypeIndex.For(Probe("Shared"));

            Assert.NotNull(index);
            Assert.True(shared.Generation > before, "For() admitted nothing to the shared cache");
            Assert.True(shared.Count <= shared.Capacity,
                "shared occupancy " + shared.Count + " exceeded capacity " + shared.Capacity);
        }

        [Fact]
        public void ANullCompilationResolvesNothingAndIsNotCached()
        {
            var cache = new SymbolTypeIndexCache(capacity: 4, maxIdleGenerations: 4);

            var index = cache.Get(null);

            Assert.NotNull(index);
            Assert.False(index.NameExistsAnywhere("PSubject"));
            Assert.Equal(0, cache.Count);
        }

        private static string Describe(SymbolTypeIndex index, string spelling, IReadOnlyList<string> imports)
        {
            if (index.TryResolve(spelling, imports, out var type, out var fault))
                return type.ToDisplayString();
            return "fault:" + fault + "|anywhere:" + index.NameExistsAnywhere(spelling);
        }
    }
}
