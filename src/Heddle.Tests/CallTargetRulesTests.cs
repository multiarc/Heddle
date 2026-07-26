using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Attributes;
using Heddle.Language;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Tests the name-resolution precedence rules that both dispatch sites use. The final invariant verifies that
    /// default-function names never collide with built-in extension names.
    /// </summary>
    public class CallTargetRulesTests
    {
        private static readonly Func<string, bool> Never = _ => false;
        private static Func<string, bool> Only(params string[] names) =>
            n => names.Contains(n, StringComparer.Ordinal);

        private static CallParameter Path(string segment) =>
            new CallParameter { ModelParameter = new[] { segment } };

        [Fact]
        public void FillBeatsEverything()
        {
            Assert.Equal(CallTargetKind.Fill, CallTargetRules.ResolveCallTarget("x", Path("a"),
                Only("x"), Only("x"), Only("x"), Only("x")));
        }

        [Fact]
        public void DefinitionBeatsExtensionAndFunction()
        {
            Assert.Equal(CallTargetKind.Definition, CallTargetRules.ResolveCallTarget("x", Path("a"),
                Never, Only("x"), Only("x"), Only("x")));
        }

        /// <summary>A definition may shadow a branch keyword — the precedence, not a special case.</summary>
        [Fact]
        public void ADefinitionShadowsABranchKeyword()
        {
            Assert.Equal(CallTargetKind.Definition, CallTargetRules.ResolveCallTarget("if", Path("a"),
                Never, Only("if"), Only("if"), Never));
        }

        [Fact]
        public void ExtensionBeatsFunction()
        {
            Assert.Equal(CallTargetKind.Extension, CallTargetRules.ResolveCallTarget("x", Path("a"),
                Never, Never, Only("x"), Only("x")));
        }

        [Fact]
        public void ABodilessKnownFunctionResolvesAsAFunction()
        {
            Assert.Equal(CallTargetKind.Function, CallTargetRules.ResolveCallTarget("upper", Path("Name"),
                Never, Never, Never, Only("upper")));
        }

        [Fact]
        public void AChainParameterRefusesTheFunctionPath()
        {
            var chained = new CallParameter { ChainParameter = new List<OutputItem>() };
            Assert.Equal(CallTargetKind.FunctionShapeUnsupported,
                CallTargetRules.ResolveCallTarget("upper", chained, Never, Never, Never, Only("upper")));
            Assert.False(CallTargetRules.IsFunctionCompatibleShape(chained));
        }

        [Fact]
        public void ACSharpExpressionRefusesTheFunctionPath()
        {
            var csharp = new CallParameter { CSharpExpression = "1 + 1" };
            Assert.Equal(CallTargetKind.FunctionShapeUnsupported,
                CallTargetRules.ResolveCallTarget("upper", csharp, Never, Never, Never, Only("upper")));
        }

        /// <summary>The shape gate is only consulted for a name the function tier actually owns: an extension with
        /// a chain parameter is still an extension.</summary>
        [Fact]
        public void TheShapeGateDoesNotApplyToExtensions()
        {
            var chained = new CallParameter { ChainParameter = new List<OutputItem>() };
            Assert.Equal(CallTargetKind.Extension,
                CallTargetRules.ResolveCallTarget("list", chained, Never, Never, Only("list"), Never));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void TheUnnamedCarrierIsNeverClassifiedHere(string name)
        {
            Assert.Equal(CallTargetKind.Unknown,
                CallTargetRules.ResolveCallTarget(name, new CallParameter(), Only(""), Only(""), Only(""), Only("")));
        }

        [Fact]
        public void AnUnregisteredNameIsUnknown()
        {
            Assert.Equal(CallTargetKind.Unknown,
                CallTargetRules.ResolveCallTarget("nope", Path("a"), Never, Never, Never, Never));
        }

        /// <summary>
        /// Verifies that no default-function name collides with a built-in extension name; the extension-wins
        /// precedence rule would cause silent misrouting.
        /// </summary>
        [Fact]
        public void DefaultFunctionNamesDoNotCollideWithBuiltInExtensionNames()
        {
            var functions = new HashSet<string>(DefaultFunctionTable.Rows.Select(r => r.Name),
                StringComparer.Ordinal);
            var extensions = new HashSet<string>(TemplateFactory.RegisteredNames(), StringComparer.Ordinal);
            functions.IntersectWith(extensions);
            Assert.Empty(functions);
        }
    }
}
