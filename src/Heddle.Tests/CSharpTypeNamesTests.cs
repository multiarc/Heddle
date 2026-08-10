using System;
using System.Linq;
using Heddle.Helpers;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The C# alias table exists once. Five tables across four projects used to differ, so adding an alias
    /// could change what a template may write without changing what the build tier binds. These pin the shared table
    /// and the two projections taken from it.
    /// </summary>
    public class CSharpTypeNamesTests
    {
        /// <summary>The key list and the alias map are two views of one dictionary, so a consumer keying off
        /// <see cref="CSharpTypeNames.AliasNames"/> (the build tier's symbol adapter, the spelling parser)
        /// can never see a set the resolver does not resolve.</summary>
        [Fact]
        public void TheKeyListAndTheAliasMapAreTheSameSet()
        {
            Assert.Equal(
                CSharpTypeNames.Aliases.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                CSharpTypeNames.AliasNames.OrderBy(k => k, StringComparer.Ordinal).ToList());
        }

        [Theory]
        [InlineData("int", typeof(int))]
        [InlineData("uint", typeof(uint))]
        [InlineData("nint", null)]
        [InlineData("dynamic", typeof(object))]
        [InlineData("Int32", null)]
        public void TryGetTypeResolvesExactlyTheAliasSet(string alias, Type expected)
        {
            Assert.Equal(expected != null, CSharpTypeNames.TryGetType(alias, out var actual));
            Assert.Equal(expected, actual);
        }

        /// <summary>The display direction covers the same key set, plus the single-rank array shape a function
        /// signature prints for a <c>params object[]</c>. <c>dynamic</c> is not a display spelling: it shares
        /// <see cref="object"/>'s CLR type, and <c>object</c> is what a reader expects to see.</summary>
        [Theory]
        [InlineData(typeof(int), "int")]
        [InlineData(typeof(ushort), "ushort")]
        [InlineData(typeof(string), "string")]
        [InlineData(typeof(object), "object")]
        [InlineData(typeof(object[]), "object[]")]
        [InlineData(typeof(int[]), "int[]")]
        public void TryGetDisplayNameReturnsTheAlias(Type type, string expected)
        {
            Assert.True(CSharpTypeNames.TryGetDisplayName(type, out var name));
            Assert.Equal(expected, name);
        }

        [Theory]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(int[,]))]
        [InlineData(typeof(DateTime[]))]
        [InlineData(null)]
        public void TryGetDisplayNameLeavesNonAliasTypesToTheCallersFallback(Type type)
        {
            Assert.False(CSharpTypeNames.TryGetDisplayName(type, out var name));
            Assert.Null(name);
        }

        /// <summary>Every alias displays back to itself — the two projections cannot drift apart, because they
        /// are built from one dictionary.</summary>
        [Fact]
        public void TheTwoProjectionsRoundTrip()
        {
            foreach (var alias in CSharpTypeNames.AliasNames.Where(a => a != CSharpTypeNames.DynamicAlias))
            {
                Assert.True(CSharpTypeNames.TryGetType(alias, out var type));
                Assert.True(CSharpTypeNames.TryGetDisplayName(type, out var name));
                Assert.Equal(alias, name);
            }
        }
    }
}
