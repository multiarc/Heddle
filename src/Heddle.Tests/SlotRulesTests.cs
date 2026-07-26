using System.Collections.Generic;
using Heddle.Language;
using Heddle.Language.Expressions;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Pins the shared slot rules, particularly <see cref="SlotRules.HasOutValue"/>, which enumerates all five carriers of <see cref="CallParameter"/> plus the "nothing" case.
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
            yield return new object[]
            {
                "native expression",
                new CallParameter { NativeExpression = new LiteralNode(1, new BlockPosition(0, 1)) }, true
            };
            yield return new object[]
            {
                "chain parameter",
                new CallParameter { ChainParameter = new List<OutputItem>() }, true
            };
            yield return new object[]
            {
                "C# expression", new CallParameter { CSharpExpression = "1 + 1" }, true
            };
            yield return new object[]
            {
                "prop arguments",
                new CallParameter { PropArguments = new List<NamedArgument>() }, true
            };
            yield return new object[] { "model path", Model("Name"), true };
            yield return new object[] { "multi-hop model path", Model("A", "B"), true };
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

        /// <summary>Empty but non-null <c>PropArguments</c> counts as a value (checks null, not count).</summary>
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

        /// <summary>Empty (non-null) declared slot type is skipped; lets a middle layer declare nothing without hiding its base's slot.</summary>
        [Fact]
        public void AnEmptySlotTypeNameIsSkipped()
        {
            var baseDef = Definition("a", slot: "System.Int32");
            var child = Definition("b", slot: string.Empty, baseDefinition: baseDef);

            Assert.Equal("System.Int32", SlotRules.SlotTypeName(child));
        }
    }
}
