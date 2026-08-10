using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary><see cref="TemplateOptions.ValidateModelType"/> is deliberately excluded from
    /// Equals/GetHashCode: it changes failure handling, not render bytes, so must not fragment caches.</summary>
    public class TemplateOptionsValidateModelTypeTests
    {
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
