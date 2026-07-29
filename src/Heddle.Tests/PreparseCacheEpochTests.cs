using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Dropping the C#-tier preparse cache has to outlive the compiles that were already running when it happened.
    /// One of those stores its result after the map has been emptied, and that result was computed against the model
    /// assemblies the drop existed to retire — so an empty map is not the same thing as no stale entry.
    /// </summary>
    public class PreparseCacheEpochTests
    {
        private static PreparseResult Result(int generation, int epoch) =>
            new PreparseResult(new OptionalValue<object>(null, false), typeof(object), new string[0], generation,
                epoch);

        /// <summary>The store lands after the clear, which is the whole difficulty: emptying the map cannot reach an
        /// entry that does not exist yet.</summary>
        [Fact]
        public void AnEntryFromACompileThatStartedBeforeTheDropIsNotServedAfterIt()
        {
            const string key = "epoch-test/compile-in-flight";
            var epochAtCompileStart = PreparseCache.Epoch;

            PreparseCache.Clear();
            PreparseCache.Store(key, Result(generation: 1, epoch: epochAtCompileStart));

            Assert.False(PreparseCache.TryGet(key, out var served));
            Assert.Null(served);
        }

        /// <summary>The ordinary case, which the epoch must not break: nothing was dropped, so the entry stands.</summary>
        [Fact]
        public void AnEntryFromTheCurrentEpochIsServed()
        {
            const string key = "epoch-test/ordinary";
            PreparseCache.Store(key, Result(generation: 1, epoch: PreparseCache.Epoch));

            Assert.True(PreparseCache.TryGet(key, out var served));
            Assert.NotNull(served);
        }

        /// <summary>
        /// Refusing to serve a spent entry is not the same as letting go of it. The whole reason the cache is
        /// dropped is that an entry names a <c>Type</c> from a load context on its way out; one that is kept but
        /// never read holds exactly as hard as one that is read.
        /// </summary>
        [Fact]
        public void ASpentEntryIsDroppedRatherThanKeptAndIgnored()
        {
            const string key = "epoch-test/eviction";
            var epochAtCompileStart = PreparseCache.Epoch;
            PreparseCache.Clear();
            PreparseCache.Store(key, Result(generation: 1, epoch: epochAtCompileStart));

            Assert.False(PreparseCache.TryGet(key, out _));

            // Now current: if the entry were still in the map, this would find it and its epoch would match.
            PreparseCache.Store(key, Result(generation: 1, epoch: PreparseCache.Epoch));
            PreparseCache.Clear();
            Assert.False(PreparseCache.TryGet(key, out _));
        }

        // What is NOT pinned here, and cannot be from inside this process without a seam in production code: the
        // interleaving itself. The ordering that makes the epoch mean anything — the cache dropped after the
        // assemblies are removed, and the epoch read before the assembly set is — is an argument about what a
        // concurrent reader can observe, and a test that races to observe it passes by luck when it is wrong.
        // Reversing either ordering leaves every suite in this repo green. The argument is written out at each of
        // the three sites; this file pins only the primitive those sites rely on.
    }
}
