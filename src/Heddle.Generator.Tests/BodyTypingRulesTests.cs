extern alias gen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using BodyContext = gen::Heddle.Generator.Typing.BodyContext;
using BodyTypingMemo = gen::Heddle.Generator.Typing.BodyTypingMemo;
using BodyTypingRules = gen::Heddle.Generator.Typing.BodyTypingRules;
using ParseContext = gen::Heddle.Language.ParseContext;
using PathNode = gen::Heddle.Language.Expressions.PathNode;
using PropLayoutInfo = gen::Heddle.Generator.Typing.PropLayoutInfo;
using PropSlotInfo = gen::Heddle.Generator.Typing.PropSlotInfo;
using SymbolTypeResolver = gen::Heddle.Generator.Binding.SymbolTypeResolver;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Direct unit tests of the extracted typing rules — the point of the extraction: each rule is a pure function
    /// over a <see cref="BodyContext"/> and node facts, testable without running the generator. The emitted-bytes
    /// conformance of the same rules stays where it was, in <see cref="EmitterSharedRuleAdoptionTests"/> and the
    /// differential suite; this file covers the decisions themselves, including the shared-typing memo that gives a
    /// second call site the first one's typing.
    /// </summary>
    public class BodyTypingRulesTests
    {
        private static readonly CSharpCompilation Compilation = BuildCompilation();

        private static CSharpCompilation BuildCompilation()
        {
            const string source =
                "namespace TypingProbe { public class Person { public string Name { get; set; } } }";
            var references = HostAssemblies.TrustedOrLoaded().Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
                .ToList();
            return CSharpCompilation.Create("BodyTypingProbe", new[] { CSharpSyntaxTree.ParseText(source) },
                references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static ITypeSymbol Person => Compilation.GetTypeByMetadataName("TypingProbe.Person");
        private static ITypeSymbol StringType => Compilation.GetSpecialType(SpecialType.System_String);
        private static ITypeSymbol IntType => Compilation.GetSpecialType(SpecialType.System_Int32);
        private static ITypeSymbol DynamicType => Compilation.DynamicType;

        private static BodyContext TypedContext(ITypeSymbol model, PropLayoutInfo props = null) =>
            new BodyContext("(" + SymbolTypeResolver.FullyQualified(model) + ")", model, false, props: props,
                root: model);

        private static BodyContext DynamicContext() => new BodyContext(null, null, true);

        private static PropLayoutInfo Layout(params (string Name, ITypeSymbol Type)[] slots)
        {
            var layout = new PropLayoutInfo();
            foreach (var (name, type) in slots)
            {
                var slot = new PropSlotInfo { Name = name, Type = type, Index = layout.Slots.Count };
                layout.Slots.Add(slot);
                layout.ByName.Add(name, slot);
            }

            return layout;
        }

        // ---- TryNestedBodyContext: the BodyModelRules row decides the nested body's environment ----

        [Theory]
        [InlineData("if")]
        [InlineData("ifnot")]
        [InlineData("elif")]
        [InlineData("elseif")]
        [InlineData("else")]
        [InlineData("for")]
        public void AParentRowBodyKeepsTheEnclosingContext(string name)
        {
            var bctx = TypedContext(Person);
            Assert.True(BodyTypingRules.TryNestedBodyContext(name, bctx, null, out var nested));
            Assert.Same(bctx.ModelSymbol, nested.ModelSymbol);
            Assert.Equal(bctx.ModelCast, nested.ModelCast);
            Assert.False(nested.IsDynamic);
        }

        [Fact]
        public void AListBodyIsTypedByTheElementNotTheEnclosingModel()
        {
            var bctx = TypedContext(Person);
            Assert.True(BodyTypingRules.TryNestedBodyContext("list", bctx, StringType, out var nested));
            Assert.False(nested.IsDynamic);
            Assert.Same(StringType, nested.ModelSymbol);
            Assert.Equal("(" + SymbolTypeResolver.FullyQualified(StringType) + ")", nested.ModelCast);
            Assert.Same(StringType, nested.DynamicBodyModel);
            Assert.Same(bctx.Root, nested.Root);
        }

        [Fact]
        public void AListBodyOverADynamicElementIsUntypedAndCarriesTheElement()
        {
            Assert.True(BodyTypingRules.TryNestedBodyContext("list", TypedContext(Person), DynamicType, out var nested));
            Assert.True(nested.IsDynamic);
            Assert.Null(nested.ModelSymbol);
            Assert.Null(nested.ModelCast);
            Assert.Same(DynamicType, nested.DynamicBodyModel);
        }

        [Fact]
        public void AListBodyWithNoElementTypeIsUntypedWithNoModelBehindIt()
        {
            Assert.True(BodyTypingRules.TryNestedBodyContext("list", TypedContext(Person), null, out var nested));
            Assert.True(nested.IsDynamic);
            Assert.Null(nested.DynamicBodyModel);
        }

        [Fact]
        public void SlotModeAndPropsPropagateIntoAListBody()
        {
            var props = Layout(("title", StringType));
            var bctx = TypedContext(Person, props).AsSlot(StringType);
            Assert.True(BodyTypingRules.TryNestedBodyContext("list", bctx, IntType, out var nested));
            Assert.Same(StringType, nested.SlotType);
            Assert.Same(props, nested.Props);
        }

        [Fact]
        public void ANameWithNoPinnedRowRefusesTheNestedBody()
        {
            Assert.False(BodyTypingRules.TryNestedBodyContext("frobnicate", TypedContext(Person), null, out _));
        }

        // ---- TryDataValueBodyContext: a data-role body is typed by the call value (the engine's dataType) ----

        [Fact]
        public void ADataValueBodyIsTypedByTheCallValue()
        {
            Assert.True(BodyTypingRules.TryDataValueBodyContext(StringType, TypedContext(Person), out var nameCtx));
            Assert.False(nameCtx.IsDynamic);
            Assert.Same(StringType, nameCtx.ModelSymbol);
            Assert.Same(StringType, nameCtx.DynamicBodyModel);
        }

        [Fact]
        public void ADataValueBodyOverADynamicValueIsUntyped()
        {
            Assert.True(BodyTypingRules.TryDataValueBodyContext(DynamicType, TypedContext(Person), out var nameCtx));
            Assert.True(nameCtx.IsDynamic);
            Assert.Null(nameCtx.ModelSymbol);
            Assert.Same(DynamicType, nameCtx.DynamicBodyModel);
        }

        [Fact]
        public void ATypedCallerWhoseValueCannotBeTypedRefusesTheDataValueBody()
        {
            Assert.False(BodyTypingRules.TryDataValueBodyContext(null, TypedContext(Person), out _));
        }

        [Fact]
        public void ADynamicCallerWithNoValueTypeKeepsAnUntypedDataValueBody()
        {
            Assert.True(BodyTypingRules.TryDataValueBodyContext(null, DynamicContext(), out var nameCtx));
            Assert.True(nameCtx.IsDynamic);
        }

        [Fact]
        public void SlotModePropagatesIntoADataValueBody()
        {
            var bctx = TypedContext(Person).AsSlot(IntType);
            Assert.True(BodyTypingRules.TryDataValueBodyContext(StringType, bctx, out var nameCtx));
            Assert.Same(IntType, nameCtx.SlotType);
        }

        // ---- Prop shadowing: a prop wins over the model at segment 0 and never falls back ----

        [Fact]
        public void AFirstSegmentNamingAPropResolvesPropFirst()
        {
            var props = Layout(("Name", IntType));
            var path = new PathNode(false, new[] { "Name" }, null, default);
            Assert.True(BodyTypingRules.IsPropName(path, props));
            Assert.Same(props.ByName["Name"], BodyTypingRules.PropShadowSlot(props, "Name"));
        }

        [Fact]
        public void ARootedPathOrATargetedPathIsNeverAPropRead()
        {
            var props = Layout(("Name", IntType));
            var rooted = new PathNode(true, new[] { "Name" }, null, default);
            var targeted = new PathNode(false, new[] { "Name" },
                new PathNode(false, new[] { "Other" }, null, default), default);
            Assert.False(BodyTypingRules.IsPropName(rooted, props));
            Assert.False(BodyTypingRules.IsPropName(targeted, props));
        }

        [Fact]
        public void ANameTheLayoutDoesNotCarryReadsTheModel()
        {
            var props = Layout(("title", StringType));
            var path = new PathNode(false, new[] { "Name" }, null, default);
            Assert.False(BodyTypingRules.IsPropName(path, props));
            Assert.Null(BodyTypingRules.PropShadowSlot(props, "Name"));
            Assert.False(BodyTypingRules.IsPropName(path, null));
            Assert.Null(BodyTypingRules.PropShadowSlot(null, "Name"));
        }

        // ---- Slot typing: the HED5014 decision over the value's static type ----

        [Fact]
        public void AValueWithNoEstablishedTypeIsExemptFromTheSlotCheck()
        {
            Assert.True(BodyTypingRules.TrySlotValue(null, StringType, (s, t) => false, out var reason));
            Assert.Null(reason);
        }

        [Fact]
        public void ADynamicSlotValueIsRefusedBeforeTheConversionTableIsAsked()
        {
            Assert.False(BodyTypingRules.TrySlotValue(DynamicType, StringType,
                (s, t) => throw new InvalidOperationException("the conversion table must not be consulted"),
                out var reason));
            Assert.Equal("slot value is dynamic under a dynamic definition model", reason);
        }

        [Fact]
        public void AConvertibleSlotValuePasses()
        {
            Assert.True(BodyTypingRules.TrySlotValue(StringType, StringType, (s, t) => true, out var reason));
            Assert.Null(reason);
        }

        [Fact]
        public void AnInconvertibleSlotValueNamesBothTypesInItsReason()
        {
            Assert.False(BodyTypingRules.TrySlotValue(IntType, Person, (s, t) => false, out var reason));
            Assert.Contains(SymbolTypeResolver.FullyQualified(IntType), reason);
            Assert.Contains(SymbolTypeResolver.FullyQualified(Person), reason);
        }

        // ---- The shared-typing memo: one body per parse context, typed by whoever arrives first ----

        [Fact]
        public void ASecondCallSiteOfAnotherModelReusesTheFirstTyping()
        {
            var memo = new BodyTypingMemo();
            var key = memo.KeyOf(new ParseContext());

            var first = TypedContext(Person);
            Assert.True(memo.TryShareBodyTyping(key, ref first, out _));

            var second = TypedContext(StringType);
            Assert.True(memo.TryShareBodyTyping(key, ref second, out var reason));
            Assert.Null(reason);
            Assert.Same(Person, second.ModelSymbol);
            Assert.Equal(first.ModelCast, second.ModelCast);
        }

        [Fact]
        public void ACallSiteAgreeingWithTheFirstTypingKeepsItsOwnContext()
        {
            var memo = new BodyTypingMemo();
            var key = memo.KeyOf(new ParseContext());

            var first = TypedContext(Person);
            Assert.True(memo.TryShareBodyTyping(key, ref first, out _));

            var props = Layout(("title", StringType));
            var second = TypedContext(Person, props);
            Assert.True(memo.TryShareBodyTyping(key, ref second, out _));
            Assert.Same(props, second.Props);
        }

        [Fact]
        public void AnUntypedFirstBodyRefusesALaterCallSiteOfAnotherModel()
        {
            var memo = new BodyTypingMemo();
            var key = memo.KeyOf(new ParseContext());

            var first = DynamicContext();
            Assert.True(memo.TryShareBodyTyping(key, ref first, out _));

            var second = TypedContext(Person);
            Assert.False(memo.TryShareBodyTyping(key, ref second, out var reason));
            Assert.Equal("definition body already compiled untyped for a call site of another model", reason);
        }

        [Fact]
        public void TwoParseContextsShareNoTypingAndOneContextKeysStably()
        {
            var memo = new BodyTypingMemo();
            var contextA = new ParseContext();
            var keyA = memo.KeyOf(contextA);
            var keyB = memo.KeyOf(new ParseContext());
            Assert.NotEqual(keyA, keyB);
            Assert.Equal(keyA, memo.KeyOf(contextA));
            Assert.Equal("0", memo.KeyOf(null));
        }
    }
}
