using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Helpers;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Conformance test: the runtime's null <c>InitStart</c> and the <c>[ZeroOutput]</c> attribute must stay in
    /// sync across all built-ins.
    /// </summary>
    public class ZeroOutputProtocolTests
    {
        /// <summary>Every zero-output built-in must be added here <em>and</em> carry the attribute; adding only
        /// one of the two reds this test.</summary>
        private static readonly HashSet<string> ZeroOutputNames =
            new HashSet<string>(StringComparer.Ordinal) { "model", "using", "import", "profile" };

        public static IEnumerable<object[]> RegisteredExtensions() =>
            TemplateFactory.RegisteredNames().OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => new object[] { n });

        [Theory]
        [MemberData(nameof(RegisteredExtensions))]
        public void AttributePresenceAgreesWithTheNullInitStartProtocol(string name)
        {
            Assert.True(TemplateFactory.TryGetExtensionType(name, out var type),
                "registered name '" + name + "' resolves to no type");

            bool declared = type.IsHaveAttribute<ZeroOutputAttribute>(true);
            bool expected = ZeroOutputNames.Contains(name);

            Assert.Equal(expected, declared);
        }

        /// <summary>The list cannot rot by naming an extension that no longer exists.</summary>
        [Fact]
        public void EveryZeroOutputNameIsRegistered()
        {
            var registered = new HashSet<string>(TemplateFactory.RegisteredNames(), StringComparer.Ordinal);
            Assert.Empty(ZeroOutputNames.Except(registered));
        }

        /// <summary>The attribute is <c>Inherited = true</c>: deriving extensions keep zero-output classification
        /// without re-declaring it.</summary>
        [Fact]
        public void TheAttributeIsInherited()
        {
            var usage = (AttributeUsageAttribute) Attribute.GetCustomAttribute(
                typeof(ZeroOutputAttribute), typeof(AttributeUsageAttribute));
            Assert.NotNull(usage);
            Assert.True(usage.Inherited);
            Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        }
    }
}
