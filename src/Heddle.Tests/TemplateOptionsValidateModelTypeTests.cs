using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 4 WI3 — <see cref="TemplateOptions.ValidateModelType"/> is deliberately excluded from
    /// <see cref="TemplateOptions.Equals(TemplateOptions)"/>/<see cref="TemplateOptions.GetHashCode"/>
    /// (the <see cref="TemplateOptions.RenderBudget"/>/<see cref="TemplateOptions.MaxRecursionCount"/>
    /// precedent): it changes failure handling, never the bytes of a successful render, so it must not
    /// fragment template caches. The copy-constructor round-trip is covered by
    /// <see cref="TemplateOptionsCompletenessTests"/> automatically.
    /// </summary>
    public class TemplateOptionsValidateModelTypeTests
    {
        /// <summary>Two options differing only by the flag are equal and hash identically.</summary>
        [Fact]
        public void EqualityAndHashIgnoreValidateModelType()
        {
            var off = new TemplateOptions { ValidateModelType = false };
            var on = new TemplateOptions { ValidateModelType = true };

            Assert.True(off.Equals(on));
            Assert.True(on.Equals(off));
            Assert.Equal(off.GetHashCode(), on.GetHashCode());
        }
    }
}
