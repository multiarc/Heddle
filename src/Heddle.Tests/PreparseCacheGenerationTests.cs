using System;
using System.Threading;
using Heddle.Data;
using Heddle.Native;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The C#-tier preparse cache holds one assembly generation at a time, and that one rule has to cover both jobs:
    /// never answer with a result computed against a set that has moved on, and never <i>hold</i> one either — an
    /// entry carries the expression's result <c>Type</c>, which for a workspace model type belongs to a collectible
    /// load context the host is trying to unload.
    /// <para>The difficult case throughout is the compile that was already running: it stores its result after the
    /// unregistration has finished, so an emptied map is not the same thing as no stale entry.</para>
    /// </summary>
    public class PreparseCacheGenerationTests : IDisposable
    {
        /// <summary>The cache is process-wide and refuses anything below the generation it holds, so each test takes
        /// a band of its own and starts by putting the cache in it. Sharing one would make these tests order-dependent
        /// on each other rather than on the rule under test.</summary>
        private static int _band = 1000;

        /// <summary>
        /// Puts the process-global cache back on the generation the process is actually at.
        /// <para>The bands above are far ahead of any real generation, and the cache refuses everything below the one
        /// it holds — so a test that walked away leaving it up there left the C# tier's preparse cache dead for the
        /// rest of the run: nothing served, nothing retired, every store refused. Nothing failed because of it, which
        /// is the whole problem: a cache that has quietly stopped working looks exactly like one that is working.</para>
        /// </summary>
        public void Dispose() => PreparseCache.Retarget(AssemblyHelper.Generation);

        private static int FreshGeneration()
        {
            var generation = Interlocked.Add(ref _band, 10);
            PreparseCache.Retarget(generation);
            return generation;
        }

        private static PreparseResult Result(int generation) =>
            new PreparseResult(new OptionalValue<object>(null, false), typeof(object), new string[0], generation);

        [Fact]
        public void AnEntryAtTheGenerationTheMapHoldsIsServed()
        {
            const string key = "preparse-test/ordinary";
            var generation = FreshGeneration();
            PreparseCache.Store(key, Result(generation));

            Assert.True(PreparseCache.TryGet(key, generation, out var served));
            Assert.NotNull(served);
        }

        /// <summary>A success is bound to its generation as tightly as a failure: a later registration can make a
        /// name ambiguous, or introduce a better overload, so "it compiled once" is not an answer about a larger
        /// assembly set.</summary>
        [Fact]
        public void AnEntryIsNotServedOnceTheAssemblySetHasMovedOn()
        {
            const string key = "preparse-test/moved-on";
            var generation = FreshGeneration();
            PreparseCache.Store(key, Result(generation));

            Assert.False(PreparseCache.TryGet(key, generation + 1, out var served));
            Assert.Null(served);
        }

        /// <summary>The in-flight compile, as a unit: its result arrives after the set it was computed against has
        /// been retired, and must not be admitted at all.</summary>
        [Fact]
        public void AResultComputedAgainstASupersededSetIsRefusedAdmission()
        {
            const string key = "preparse-test/in-flight";
            var generation = FreshGeneration();
            PreparseCache.Retarget(generation + 1);
            PreparseCache.Store(key, Result(generation));

            Assert.False(PreparseCache.TryGet(key, generation + 1, out _));
            Assert.False(PreparseCache.TryGet(key, generation, out _));
        }

        /// <summary>The map is homogeneous by construction: the first arrival from a newer set retires everything
        /// older instead of the two generations coexisting and the reader having to sort them out.</summary>
        [Fact]
        public void AResultFromANewerSetRetiresTheOlderEntries()
        {
            const string older = "preparse-test/older";
            const string newer = "preparse-test/newer";
            var generation = FreshGeneration();
            PreparseCache.Store(older, Result(generation));
            PreparseCache.Store(newer, Result(generation + 1));

            Assert.True(PreparseCache.TryGet(newer, generation + 1, out _));
            Assert.False(PreparseCache.TryGet(older, generation, out _));
        }

        /// <summary>
        /// The same arrival, seen from the entries it displaces. Moving the map's generation forward without
        /// emptying it leaves every older entry in place and now <i>readable at the new generation</i> — the map
        /// stops being one generation's worth of answers and starts being a pile of them wearing the newest label,
        /// which is both the wrong answer and the load context still pinned.
        /// </summary>
        [Fact]
        public void AResultFromANewerSetEvictsTheOlderEntriesRatherThanRelabellingThem()
        {
            const string older = "preparse-test/relabelled-older";
            const string newer = "preparse-test/relabelled-newer";
            var generation = FreshGeneration();
            PreparseCache.Store(older, Result(generation));
            PreparseCache.Store(newer, Result(generation + 1));

            Assert.True(PreparseCache.TryGet(newer, generation + 1, out _));
            Assert.False(PreparseCache.TryGet(older, generation + 1, out _));
        }

        /// <summary>
        /// A reader that read the generation before an unregistration and reached the map after it. It has nothing to
        /// be served — the map holds one generation and it is not this one — but it must not drag the map back to
        /// the generation it asked at. Doing so throws away the current set's entries, and worse, opens the map to a
        /// store from the same superseded compile: a result naming types that are being unloaded, admitted into a
        /// map that had just been emptied to let go of them.
        /// </summary>
        [Fact]
        public void AReaderFromASupersededSetDoesNotDragTheMapBackToIt()
        {
            const string current = "preparse-test/current";
            const string superseded = "preparse-test/superseded";
            var generation = FreshGeneration();
            PreparseCache.Store(current, Result(generation));

            Assert.False(PreparseCache.TryGet(current, generation - 1, out _));

            Assert.True(PreparseCache.TryGet(current, generation, out _));
            PreparseCache.Store(superseded, Result(generation - 1));
            Assert.False(PreparseCache.TryGet(superseded, generation - 1, out _));
        }

        /// <summary>
        /// This suite hands the cache back. It is the only suite that moves the process-global generation for its own
        /// purposes, so it is the only one that can leave the cache unusable for everything that runs after it — and
        /// nothing downstream fails when it does, it merely stops caching. Constructed and disposed by hand here
        /// because that is the whole assertion: what the suite leaves behind.
        /// </summary>
        [Fact]
        public void TheSuiteLeavesTheCacheUsableAtTheLiveGeneration()
        {
            var suite = new PreparseCacheGenerationTests();
            suite.AnEntryAtTheGenerationTheMapHoldsIsServed();
            suite.Dispose();

            const string key = "preparse-test/after-the-suite";
            var live = AssemblyHelper.Generation;
            PreparseCache.Store(key, Result(live));
            Assert.True(PreparseCache.TryGet(key, live, out _));
        }

        /// <summary>
        /// Refusing to serve a spent entry is not the same as letting go of it: one that is kept but never read pins
        /// its load context exactly as hard as one that is served. Asking again at the original generation is how
        /// that shows — an entry merely fenced off would answer here, an evicted one cannot.
        /// </summary>
        [Fact]
        public void RetargetingDropsEntriesRatherThanLeavingThemUnread()
        {
            const string key = "preparse-test/eviction";
            var generation = FreshGeneration();
            PreparseCache.Store(key, Result(generation));
            PreparseCache.Retarget(generation + 1);

            PreparseCache.Retarget(generation);
            Assert.False(PreparseCache.TryGet(key, generation, out _));
        }

        /// <summary>
        /// End to end over the real counter. A result stamped before an unregistration is refused after it, which is
        /// what the generation bump and the cache retarget being one expression buys: the bump cannot happen without
        /// the retarget, and the retarget cannot be handed a value other than the one the bump produced.
        /// <para>The assertion asks at the <i>pre-unregistration</i> generation on purpose. Asking at the current one
        /// would pass whether or not the store was refused — a mismatched generation answers nothing either way — so
        /// it would pin nothing.</para>
        /// </summary>
        [Fact]
        public void AResultComputedBeforeAnUnregistrationIsRefusedAfterIt()
        {
            const string key = "preparse-test/unregistration";
            var beforeUnregistration = AssemblyHelper.Generation;
            PreparseCache.Retarget(beforeUnregistration);

            AssemblyHelper.UnregisterModelAssemblies();
            PreparseCache.Store(key, Result(beforeUnregistration));

            Assert.NotEqual(beforeUnregistration, AssemblyHelper.Generation);
            Assert.False(PreparseCache.TryGet(key, beforeUnregistration, out _));
        }

        // What is NOT pinned here, and cannot be from inside this process without a seam in production code: the
        // interleaving. That a store cannot land between an unregistration deciding the generation has moved and the
        // map being emptied is true because both run under the cache's monitor, not because a test observed it — a
        // test that races to observe it passes by luck when the code is wrong. The same goes for the assembly set and
        // the generation being read together in AssemblyHelper.GetApplicationReferences. Both are arguments about
        // what no concurrent caller can observe, and they are made where the code makes them impossible.
    }
}
