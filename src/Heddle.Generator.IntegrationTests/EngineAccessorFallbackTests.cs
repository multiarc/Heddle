extern alias generator;
using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Generator.IntegrationTests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The per-node engine-accessor fallback: a member-path VALUE node the engine resolves but generated C# cannot
    /// spell no longer de-precompiles the whole file — it is computed by the engine's own accessor
    /// (<c>PrecompiledRuntime.MemberAccessor</c> / <c>NativeAccessor</c>, both built once at type-init from the
    /// engine's member resolution), so byte parity holds by construction. Every case here pins the tier: an
    /// unpinned parity test proves nothing, since the degrade is byte-identical by design. The escape is for
    /// values only — bodies, branches, lists and definition invocations still degrade whole-template — and
    /// <c>HeddleNodeFallback=false</c> restores the pre-fallback degrade, pinned by category.
    /// </summary>
    public class EngineAccessorFallbackTests
    {
        private const string Fixtures = "Heddle.Generator.IntegrationTests.Fixtures.";

        private static readonly Dictionary<string, string> FallbackOff =
            new Dictionary<string, string> { ["build_property.HeddleNodeFallback"] = "false" };

        private static Type Fixture(string fullName) =>
            typeof(EngineAccessorFallbackTests).Assembly.GetType(fullName, throwOnError: true);

        /// <summary>
        /// The member-level faults that used to sit in UnnameableModelSymbolTests' hostile table: the model type
        /// itself is nameable (so the entry point compiles), the path is not (an internal member, an error-obsolete
        /// getter, an error-obsolete type mid-chain) — and the engine reads every one of them by reflection, which
        /// asks neither accessibility nor <c>[Obsolete]</c>. Each precompiles byte-identically with no HED7030.
        /// </summary>
        [Theory]
        [InlineData("internal-member", Fixtures + "InternalMemberModel", "@(Secret)\n", "s3cret\n")]
        [InlineData("error-obsolete-getter", Fixtures + "ObsoleteMemberModel", "@(Bad)\n", "bad\n")]
        [InlineData("error-obsolete-property-type", Fixtures + "ObsoletePropertyTypeModel", "@(Balance.Amount)\n",
            "0\n")]
        [InlineData("error-obsolete-argument-of-containing-type", Fixtures + "ObsoleteContainerArgumentModel",
            "@(Balance.Amount)\n", "0\n")]
        [InlineData("internal-mid-chain-type", Fixtures + "ChainThroughInternalModel", "@(Link.Deep)\n", "deep\n")]
        public void AMemberPathTheEngineResolvesButCSharpCannotSpellPrecompilesByteIdentically(
            string name, string modelSpelling, string body, string expected)
        {
            var key = "views/accessor-" + name + ".heddle";
            var template = "@model(){{" + modelSpelling + "}}@\\\n" + body;
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7030");
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var modelType = Fixture(modelSpelling);
            var (precompiled, dyn) = DifferentialHarness.Render(key, template, modelType,
                Activator.CreateInstance(modelType));
            Assert.Equal(expected, dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The same rows with the fallback off: the whole-template degrade returns, pinned by the
        /// <c>MemberAccess</c> category — <c>Failed</c> where the metadata view hides the member outright,
        /// <c>Inaccessible</c> where the symbol model shows it and only nameability refuses it.</summary>
        [Theory]
        [InlineData("internal-member", Fixtures + "InternalMemberModel", "@(Secret)\n", "member path (Failed)")]
        [InlineData("error-obsolete-getter", Fixtures + "ObsoleteMemberModel", "@(Bad)\n",
            "member path (Inaccessible)")]
        [InlineData("internal-mid-chain-type", Fixtures + "ChainThroughInternalModel", "@(Link.Deep)\n",
            "member path (Failed)")]
        public void WithNodeFallbackOffTheWholeTemplateDegradeReturns(
            string name, string modelSpelling, string body, string reason)
        {
            var key = "views/accessor-optout-" + name + ".heddle";
            var template = "@model(){{" + modelSpelling + "}}@\\\n" + body;
            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FallbackOff);

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectDegrade(gen, key,
                generator::Heddle.Generator.Emit.RefusalCategory.MemberAccess, reason);
        }

        /// <summary>A <c>::</c>-rooted path to the internal member — the root-member-path site escapes over the
        /// root channel (<c>RootModel(in scope)</c>) instead of the model channel.</summary>
        [Fact]
        public void ARootRootedPathToAnInternalMemberPrecompilesByteIdentically()
        {
            const string key = "views/accessor-rootref.heddle";
            const string template = "@model(){{" + Fixtures + "InternalMemberModel}}@\\\n[@(::Secret)]\n";
            var (precompiled, dyn) = DifferentialHarness.Render(key, template,
                typeof(InternalMemberModel), new InternalMemberModel());
            Assert.Equal("[s3cret]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A <c>this.</c>-rooted path is the native expression tier — the escape is the engine's own
        /// three-channel expression compilation (<c>NativeAccessor</c>), not the member tier's accessor.</summary>
        [Fact]
        public void ANativeThisRootedPathToAnInternalMemberPrecompilesByteIdentically()
        {
            const string key = "views/accessor-native-this.heddle";
            const string template = "@model(){{" + Fixtures + "InternalMemberModel}}@\\\n[@(this.Secret)]\n";
            var (precompiled, dyn) = DifferentialHarness.Render(key, template,
                typeof(InternalMemberModel), new InternalMemberModel());
            Assert.Equal("[s3cret]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The native escape's opt-out, pinned with the writer's own refusal wording so the construct
        /// cannot silently start degrading for a different reason.</summary>
        [Fact]
        public void WithNodeFallbackOffTheNativePathDegradesWithTheWriterReason()
        {
            const string key = "views/accessor-native-this-optout.heddle";
            const string template = "@model(){{" + Fixtures + "InternalMemberModel}}@\\\n[@(this.Secret)]\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FallbackOff);

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectDegrade(gen, key,
                generator::Heddle.Generator.Emit.RefusalCategory.MemberAccess,
                "a member path that does not resolve statically");
        }

        /// <summary>A prop-rooted multi-hop whose tail the compilation cannot name: the accessor is rooted at the
        /// prop slot's declared type over the boxed prop value, the engine's own first-hop conversion. The prop
        /// arg itself is an ordinary nameable path — only the hop taken off the prop is hidden.</summary>
        [Fact]
        public void APropMultiHopToAnInternalMemberPrecompilesByteIdentically()
        {
            const string key = "views/accessor-prop-hop.heddle";
            const string template =
                "@model(){{" + Fixtures + "AccessorPropRootModel}}@\\\n" +
                "@%<w(x: " + Fixtures + "InternalMemberModel)>{{[@(x.Secret)]}} :: dynamic%@\\\n" +
                "@w(x: Inner)\n";
            var (precompiled, dyn) = DifferentialHarness.Render(key, template,
                typeof(AccessorPropRootModel), new AccessorPropRootModel());
            Assert.Equal("\\\n[s3cret]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>Encoding parity: the escaped value re-enters the rendered-value path exactly where a direct
        /// value would, so the Html profile encodes it identically on both tiers — the accessor hands the carrier a
        /// boxed value, never pre-rendered text.</summary>
        [Fact]
        public void AnEscapedValueIsEncodedExactlyAsTheEngineEncodesIt()
        {
            const string key = "views/accessor-encoded.heddle";
            const string template = "@model(){{" + Fixtures + "InternalMarkupModel}}@\\\n[@(Markup)]\n";
            var (precompiled, dyn) = DifferentialHarness.Render(key, template,
                typeof(InternalMarkupModel), new InternalMarkupModel());
            Assert.Equal("[&lt;b&gt;&amp;&quot;q&quot;&lt;/b&gt;]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A single-item chain around the hidden member — the carrier arm reduces to the inner member
        /// path, so the same escape serves it and the carrier's own text protocol is preserved.</summary>
        [Fact]
        public void AChainWrappedInternalMemberPrecompilesByteIdentically()
        {
            const string key = "views/accessor-chain.heddle";
            const string template = "@model(){{" + Fixtures + "InternalMemberModel}}@\\\n[@((Secret))]\n";
            var (precompiled, dyn) = DifferentialHarness.Render(key, template,
                typeof(InternalMemberModel), new InternalMemberModel());
            Assert.Equal("[s3cret]\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The boundary the escape must not cross: a path the ENGINE does not resolve either (a typo)
        /// keeps its HED7008 error — recovery is claimed only where the engine succeeds, so the tiers still agree
        /// in refusal.</summary>
        [Fact]
        public void APathTheEngineDoesNotResolveEitherStaysAnError()
        {
            const string key = "views/accessor-typo.heddle";
            const string template = "@model(){{" + Fixtures + "InternalMemberModel}}@\\\n@(Secrett)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            var hed7008 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7008"));
            Assert.Equal(DiagnosticSeverity.Error, hed7008.Severity);
        }

        /// <summary>Two reads of the same hidden path share one accessor field — one type-init construction, not
        /// one per call site.</summary>
        [Fact]
        public void RepeatedReadsOfOnePathShareOneAccessorField()
        {
            const string key = "views/accessor-shared.heddle";
            const string template = "@model(){{" + Fixtures + "InternalMemberModel}}@\\\n@(Secret)@(Secret)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var source = gen.TemplateSources.Values.Single(s => s.Contains("MemberAccessor"));
            Assert.Contains("__acc0", source);
            Assert.DoesNotContain("__acc1", source);
        }
    }
}
