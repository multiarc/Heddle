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
    }
}
