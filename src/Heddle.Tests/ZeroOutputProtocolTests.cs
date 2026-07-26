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
    /// Generator plan phase 1 WI9 (D10) — the zero-output protocol. The runtime's rule is behavioral: a directive's
    /// <c>InitStart</c> returns <c>null</c>, and that is what makes the compiler drop the block. A build-time
    /// generator can only read symbols, so <c>[ZeroOutput]</c> is the declarative form of the same fact — and this
    /// conformance test is what keeps the two from drifting apart, for every built-in at once.
    /// </summary>
    public class ZeroOutputProtocolTests
    {
        /// <summary>The four built-ins whose <c>InitStart</c> returns null — the runtime protocol, stated here as
        /// the expected side of the conformance. A fifth zero-output built-in must be added here <em>and</em>
        /// carry the attribute; adding only one of the two reds this test.</summary>
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

        /// <summary>Every name the protocol list claims is actually registered — so the list cannot rot into
        /// vacuity by naming an extension that no longer exists.</summary>
        [Fact]
        public void EveryZeroOutputNameIsRegistered()
        {
            var registered = new HashSet<string>(TemplateFactory.RegisteredNames(), StringComparer.Ordinal);
            Assert.Empty(ZeroOutputNames.Except(registered));
        }

        /// <summary>The attribute is <c>Inherited = true</c>, like <c>[ScopeChannel]</c>: a host extension deriving
        /// a directive keeps zero-output classification without re-declaring it. Both tiers read it that way — the
        /// runtime through <c>IsHaveAttribute(inherit: true)</c>, the generator through its base-chain walk.</summary>
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
