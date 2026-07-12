using System;
using System.Reflection;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Guards the B1 deprecation contract: <see cref="TemplateOptions.AllowCSharp"/> stays marked
    /// <see cref="ObsoleteAttribute"/> as a warning (never escalated to an error and never removed),
    /// so first-party code migrates to <c>ExpressionMode</c> while the bridge keeps compiling for hosts.
    /// Read via reflection so the assertion itself does not trip CS0618.
    /// </summary>
    public class AllowCSharpObsoleteTests
    {
        [Fact]
        public void AllowCSharpCarriesObsoleteWarningNotError()
        {
            // String name, not nameof(TemplateOptions.AllowCSharp) — nameof references the member and would trip CS0618.
            var property = typeof(TemplateOptions).GetProperty(
                "AllowCSharp",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);

            var obsolete = property.GetCustomAttribute<ObsoleteAttribute>();
            Assert.NotNull(obsolete);
            Assert.False(obsolete.IsError);
        }
    }
}
