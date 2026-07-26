using System.Collections.Generic;
using Heddle.Language;
using Heddle.Language.Expressions;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The two slot rules, now shared. The five-way <see cref="SlotRules.HasOutValue"/> theory is the important
    /// half: the emitter used to approximate it as <c>!IsModelTypeParameter || first-segment-non-empty ||
    /// any-prop-arguments</c>, which agreed with the canonical test on every shape that exists <em>today</em> and
    /// would have stopped agreeing the moment a sixth carrier was added to <see cref="CallParameter"/>. The rows
    /// below enumerate all five carriers plus the "nothing" case, so the shared implementation is the thing under
    /// test rather than the agreement.
    /// </summary>
    public class SlotRulesTests
    {
        private static CallParameter Model(params string[] segments) =>
            new CallParameter { ModelParameter = segments };

        private static DefinitionItem Definition(string name, string slot = null,
            DefinitionItem baseDefinition = null)
        {
            var item = new DefinitionItem(name, "body", baseDefinition);
            item.SlotTypeName = slot;
            return item;
        }

        public static IEnumerable<object[]> OutValueShapes()
        {
            // native expression
            yield return new object[]
            {
                "native expression",
                new CallParameter { NativeExpression = new LiteralNode(1, new BlockPosition(0, 1)) }, true
            };
            // chain parameter
            yield return new object[]
            {
                "chain parameter",
                new CallParameter { ChainParameter = new List<OutputItem>() }, true
            };
            // C# expression
            yield return new object[]
            {
                "C# expression", new CallParameter { CSharpExpression = "1 + 1" }, true
            };
            // named prop arguments
            yield return new object[]
            {
                "prop arguments",
                new CallParameter { PropArguments = new List<NamedArgument>() }, true
            };
            // non-empty first model segment
            yield return new object[] { "model path", Model("Name"), true };
            yield return new object[] { "multi-hop model path", Model("A", "B"), true };
            // the "nothing" shapes
            yield return new object[] { "no parameter at all", new CallParameter(), false };
            yield return new object[] { "empty segment array", Model(), false };
            yield return new object[] { "empty first segment", Model(""), false };
            yield return new object[] { "null first segment", Model((string) null), false };
        }

        [Theory]
        [MemberData(nameof(OutValueShapes))]
        public void HasOutValueCoversEveryCarrier(string label, CallParameter parameter, bool expected)
        {
            Assert.Equal(expected, SlotRules.HasOutValue(parameter));
            Assert.NotNull(label);
        }

        /// <summary>An <em>empty but non-null</em> <c>PropArguments</c> list is the one row where the emitter's
        /// old approximation (<c>Count != 0</c>) disagreed with the canonical test (<c>!= null</c>). The runtime is
        /// normative, so the shared rule keeps the null check; this row pins the resolved direction.</summary>
        [Fact]
        public void EmptyButPresentPropArgumentsCountAsAValue()
        {
            Assert.True(SlotRules.HasOutValue(new CallParameter { PropArguments = new List<NamedArgument>() }));
        }

        [Fact]
        public void HasOutValueTreatsANullParameterAsNoValue()
        {
            Assert.False(SlotRules.HasOutValue(null));
        }

        [Fact]
        public void SlotTypeNameWalksTheBaseChainOutermostFirst()
        {
            var grandparent = Definition("a", slot: "System.Int32");
            var parent = Definition("b", baseDefinition: grandparent);
            var child = Definition("c", baseDefinition: parent);

            Assert.Equal("System.Int32", SlotRules.SlotTypeName(child));
            Assert.True(SlotRules.HasSlot(child));
        }

        [Fact]
        public void ARedeclaredSlotTypeWinsOverTheInheritedOne()
        {
            var baseDef = Definition("a", slot: "System.Int32");
            var child = Definition("b", slot: "System.String", baseDefinition: baseDef);

            Assert.Equal("System.String", SlotRules.SlotTypeName(child));
        }

        [Fact]
        public void ADefinitionChainWithNoSlotHasNone()
        {
            var baseDef = Definition("a");
            var child = Definition("b", baseDefinition: baseDef);

            Assert.Null(SlotRules.SlotTypeName(child));
            Assert.False(SlotRules.HasSlot(child));
            Assert.False(SlotRules.HasSlot(null));
        }

        /// <summary>An empty (not null) declared slot type is "no slot": the walk skips it and keeps going, which
        /// is what lets a middle layer declare nothing without hiding its base's slot.</summary>
        [Fact]
        public void AnEmptySlotTypeNameIsSkipped()
        {
            var baseDef = Definition("a", slot: "System.Int32");
            var child = Definition("b", slot: string.Empty, baseDefinition: baseDef);

            Assert.Equal("System.Int32", SlotRules.SlotTypeName(child));
        }
    }
}
