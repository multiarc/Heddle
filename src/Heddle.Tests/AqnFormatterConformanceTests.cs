using System;
using System.Collections.Generic;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Conformance gate for the shared <see cref="AqnFormatter"/>: for every shape in the corpus the
    /// reflection adapter's output must be byte-equal to live <c>Type.FullName + ", " + assembly simple name</c>.
    /// <para>This is the pin the build tier's Roslyn adapter is then held to — the two sides no longer produce the
    /// identity string with unrelated formatters, so a nested type can no longer be <c>Ns.Outer.Inner</c> on one
    /// tier and <c>Ns.Outer+Inner</c> on the other.</para>
    /// </summary>
    public class AqnFormatterConformanceTests
    {
        public class Plain { }

        public class Outer
        {
            public class Inner
            {
                public class Innermost { }
            }

            public class GenericInner<T> { }
        }

        public class Generic<T>
        {
            public class NestedInGeneric { }
        }

        public static IEnumerable<object[]> CorpusTypes()
        {
            yield return new object[] { typeof(AqnFormatterConformanceTests) };
            yield return new object[] { typeof(Plain) };
            yield return new object[] { typeof(Outer) };
            yield return new object[] { typeof(Outer.Inner) };
            yield return new object[] { typeof(Outer.Inner.Innermost) };
            yield return new object[] { typeof(Outer.GenericInner<>) };
            yield return new object[] { typeof(Generic<>) };
            yield return new object[] { typeof(Generic<>.NestedInGeneric) };
            yield return new object[] { typeof(GlobalNamespaceProbe) };
            yield return new object[] { typeof(string) };
            yield return new object[] { typeof(Dictionary<,>) };
            yield return new object[] { typeof(Heddle.Extensions.IfExtension) };
        }

        [Theory]
        [MemberData(nameof(CorpusTypes))]
        public void ReflectionAdapterMatchesLiveFullNamePlusAssembly(Type type)
        {
            var expected = type.FullName + ", " + type.Assembly.GetName().Name;
            Assert.Equal(expected, PrecompiledGauntlet.AqnSansVersion(type));
        }

        [Fact]
        public void NullTypeYieldsTheUnknownMarker()
        {
            Assert.Equal("<unknown>", PrecompiledGauntlet.AqnSansVersion(null));
            Assert.Equal(AqnFormatter.Unknown, PrecompiledGauntlet.AqnSansVersion(null));
        }

        [Fact]
        public void GlobalNamespaceTypeCarriesNoLeadingDot()
        {
            Assert.Equal("GlobalNamespaceProbe", AqnFormatter.FormatFullName(null, new[] { "GlobalNamespaceProbe" }));
            Assert.Equal("GlobalNamespaceProbe", AqnFormatter.FormatFullName("", new[] { "GlobalNamespaceProbe" }));
        }

        /// <summary>The join rules, stated as data so a change to <see cref="AqnFormatter"/> is visible here first.</summary>
        [Theory]
        [InlineData("Ns", new[] { "Type" }, "Asm", "Ns.Type, Asm")]
        [InlineData("Ns", new[] { "Outer", "Inner" }, "Asm", "Ns.Outer+Inner, Asm")]
        [InlineData("Ns.Sub", new[] { "A", "B", "C" }, "Asm", "Ns.Sub.A+B+C, Asm")]
        [InlineData("Ns", new[] { "C`1" }, "Asm", "Ns.C`1, Asm")]
        [InlineData("Ns", new[] { "Outer`1", "Inner`1" }, "Asm", "Ns.Outer`1+Inner`1, Asm")]
        [InlineData(null, new[] { "Type" }, "Asm", "Type, Asm")]
        public void JoinRules(string ns, string[] chain, string assembly, string expected)
        {
            Assert.Equal(expected, AqnFormatter.Format(ns, chain, assembly));
        }

        [Fact]
        public void EmptyChainIsUnknown()
        {
            Assert.Equal(AqnFormatter.Unknown, AqnFormatter.Format("Ns", new string[0], "Asm"));
            Assert.Null(AqnFormatter.FormatFullName("Ns", null));
        }
    }
}

/// <summary>Global-namespace probe for the AQN corpus — <c>Type.Namespace</c> is <c>null</c> here, and the
/// formatter must not emit a leading dot.</summary>
public class GlobalNamespaceProbe
{
}
