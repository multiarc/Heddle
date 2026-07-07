using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 7 D8: <see cref="PrecompiledMismatchPolicy"/> defaults to <see cref="PrecompiledMismatchPolicy.Fallback"/>,
    /// rides the copy constructor (also covered generically by <c>TemplateOptionsCompletenessTests</c>), and stays
    /// out of options identity — it changes failure handling, never output bytes, so it must not perturb the
    /// resolver cache key.
    /// </summary>
    public class PrecompiledMismatchPolicyTests
    {
        [Fact]
        public void DefaultsToFallback()
        {
            Assert.Equal(PrecompiledMismatchPolicy.Fallback, new TemplateOptions().PrecompiledMismatchPolicy);
            Assert.Equal(PrecompiledMismatchPolicy.Fallback, new TemplateOptions("t").PrecompiledMismatchPolicy);
        }

        [Fact]
        public void FallbackIsZero()
        {
            Assert.Equal(0, (int)PrecompiledMismatchPolicy.Fallback);
            Assert.Equal(1, (int)PrecompiledMismatchPolicy.Strict);
        }

        [Fact]
        public void CopyConstructorPreservesPolicy()
        {
            var source = new TemplateOptions { PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict };
            var copy = new TemplateOptions(source);
            Assert.Equal(PrecompiledMismatchPolicy.Strict, copy.PrecompiledMismatchPolicy);
        }

        [Fact]
        public void PolicyIsNotPartOfIdentity()
        {
            var a = new TemplateOptions { PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Fallback };
            var b = new TemplateOptions { PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict };

            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }
    }
}
