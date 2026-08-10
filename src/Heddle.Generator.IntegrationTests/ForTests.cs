using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>Tests the <c>@for</c> sugar across both backends: counted forms (<c>@for(n)</c>, <c>@for(Count)</c>)
    /// and range expressions, verifying byte-identical output.</summary>
    public class ForTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Carts()
        {
            yield return new object[] { new Cart { Count = 3 } };
            yield return new object[] { new Cart { Count = 0 } };
            yield return new object[] { new Cart { Count = 5 } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Carts))]
        public void CountedLiteral(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n<ul>@for(3){{<li>@out()</li>}}</ul>\n";
            AssertParity("views/for-literal.heddle", t, typeof(Cart), model);
        }

        [Theory]
        [MemberData(nameof(Carts))]
        public void CountedMember(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n@for(Count){{[@out()]}}\n";
            AssertParity("views/for-member.heddle", t, typeof(Cart), model);
        }

        [Theory]
        [MemberData(nameof(Carts))]
        public void RangeForm(Cart model)
        {
            var t = "@model(){{" + CartType + "}}@\\\n@for(range(2, 8, 3)){{<i>@out()</i>}}\n";
            AssertParity("views/for-range.heddle", t, typeof(Cart), model);
        }

        // Tests iteration, the empty range, and the str(range(...)) stringification to verify PrecompiledFunctions.Str
        // matches the dynamic version byte-for-byte.
        [Fact]
        public void RangeForFixtureIsByteIdenticalAcrossBackends()
        {
            var t = "@model(){{" + CartType + "}}@\\\n" +
                    "@for(range(2, 10, 2)){{@out()}}\n" +
                    "@for(range(2, 8, 3)){{[@out()]}}\n" +
                    "@for(range(5, 2)){{x}}\n" +
                    "@(str(range(2, 10, 2)))\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/range-for.heddle", t, typeof(Cart), new Cart());
            Assert.Equal(dyn, precompiled);
            Assert.Contains("2468", precompiled);
            Assert.Contains("[2][5]", precompiled);
            Assert.Contains("range(2, 10, 2)", precompiled);
        }

        /// <summary>
        /// <c>@for</c> declares the types it accepts — a <c>Range</c> or an <c>int</c> — exactly the way <c>@list</c>
        /// declares <c>IEnumerable</c>, and the engine checks the call value against them before it compiles the
        /// call. The emitter checked only <c>@list</c>'s, so a <c>@for</c> over a value of any other type
        /// precompiled and rendered a page the engine will not compile: a chain value, which reaches the extension
        /// as the carrier's rendered <b>text</b>, iterated nothing at all here while the parent commit iterated the
        /// producer's number.
        /// <para>The check is read off the <c>[DataType]</c> attribute now rather than written out for one
        /// extension, so <c>@for</c> is covered by the same code as <c>@list</c> and any host extension declaring
        /// one is covered without a list to keep in step.</para>
        /// </summary>
        [Theory]
        [InlineData("paren-chain", "(Count)")]
        [InlineData("function-chain", "len(Name)")]
        [InlineData("string-member", "Name")]
        [InlineData("decimal-member", "Price")]
        public void AForOverAValueItDoesNotAcceptIsRefusedByBothTiers(string name, string value)
        {
            var key = "views/for-unaccepted-" + name + ".heddle";
            var t = "@model(){{" + CartType + "}}@\\\n@for(" + value + "){{<@out()>}}\n";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, t) }), key);

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(Cart)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains("but any of [Heddle.Models.Range, System.Int32] expected",
                dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);
        }

        /// <summary>The near neighbours: every value <c>@for</c> does accept still precompiles and still renders the
        /// engine's bytes — the literal, an <c>int</c> member, a <c>Range</c> from the built-in, and an <c>int</c>
        /// native expression.</summary>
        [Theory]
        [InlineData("literal", "2")]
        [InlineData("member", "Count")]
        [InlineData("range-call", "range(0, 2)")]
        [InlineData("arithmetic", "Count + 1")]
        // The two spellings that turn a refused chain into an accepted value, which is what the language
        // reference tells a reader to write: keep the call inside a native expression, or wrap it in `range`.
        [InlineData("call-arithmetic", "len(Name) + 0")]
        [InlineData("range-of-call", "range(0, len(Name))")]
        public void AForOverAnAcceptedValueStillPrecompiles(string name, string value)
        {
            var key = "views/for-accepted-" + name + ".heddle";
            var t = "@model(){{" + CartType + "}}@\\\n@for(" + value + "){{<@out()>}}\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(Cart),
                new Cart { Count = 1, Name = "ab" });
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A nullable <c>int</c>, which the engine unwraps before it checks the accepted types — so this is
        /// a value <c>@for</c> takes, not one it refuses, and the emitter has to unwrap it too.</summary>
        [Fact]
        public void AForOverANullableIntStillPrecompiles()
        {
            const string key = "views/for-accepted-nullable.heddle";
            const string t = "@model(){{Heddle.Generator.IntegrationTests.Fixtures.NullableHolder}}@\\\n" +
                             "@for(Maybe){{<@out()>}}\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, t, typeof(NullableHolder),
                new NullableHolder { Maybe = 2 });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("<0><1>\n", dyn);
        }
    }
}
