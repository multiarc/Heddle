using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests.FingerprintFixtures
{
    [ExtensionName("fpbase")]
    [Prop("a", typeof(int))]
    [Prop("b", typeof(string))]
    public class FingerprintBaseExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    /// <summary>Adds a slot at the end — the layout the manifest recorded is now a prefix of the live one.</summary>
    [ExtensionName("fpadded")]
    [Prop("c", typeof(bool))]
    public sealed class FingerprintAddedExtension : FingerprintBaseExtension
    {
    }

    /// <summary>Narrows an inherited slot's type — same names, same indices, different slot type.</summary>
    [ExtensionName("fpnarrowed")]
    [Prop("b", typeof(string))]
    public sealed class FingerprintNarrowedExtension : FingerprintBaseExtension
    {
    }

    [ExtensionName("fpnone")]
    public sealed class NoPropsExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }
}

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 3 (OQ4) — the prop-layout manifest row and its gauntlet check. Prop layouts were the one wire-format
    /// contract with <b>no</b> gauntlet coverage: the gauntlet checked options, extension identity, functions and
    /// staleness, so a layout disagreement between the generator's frozen <c>object[]</c> prototype and the
    /// runtime's <c>ExtensionParameterCarrier</c> was silent wrong rendered output rather than a fallback.
    /// </summary>
    public class PropLayoutFingerprintTests
    {
        static PropLayoutFingerprintTests()
        {
            // The fixtures are not exported through [assembly: ExportExtensions]; the gauntlet's extension check
            // needs `fpbase` in the live registry, so it is registered explicitly. Only the base is registered:
            // its subclasses inherit `[ExtensionName("fpbase")]` (the attribute is Inherited = true — the very
            // rule F3 is about) and registering them all would collide under the runtime's precedence.
            Heddle.Runtime.TemplateFactory.AddExtensions(Heddle.Runtime.TemplateFactory.LoadExtensions(new[]
            {
                typeof(FingerprintFixtures.FingerprintBaseExtension),
                typeof(FingerprintFixtures.NoPropsExtension)
            }));
        }

        [Fact]
        public void FingerprintIsOrderedNameAndSlotTypeAqn()
        {
            Assert.Equal(
                "a:System.Int32, System.Private.CoreLib|b:System.String, System.Private.CoreLib",
                Normalize(PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintBaseExtension))));
        }

        [Fact]
        public void AParameterLessExtensionHasNoFingerprintSoTheCheckIsVacuous()
        {
            Assert.Null(PropLayout.Fingerprint(typeof(FingerprintFixtures.NoPropsExtension)));
        }

        [Fact]
        public void AnAddedSlotChangesTheFingerprint()
        {
            var baseline = PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintBaseExtension));
            var added = PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintAddedExtension));

            Assert.NotEqual(baseline, added);
            Assert.StartsWith(baseline, added);   // base slots keep their indices; the new one appends
        }

        [Fact]
        public void ARedeclarationThatKeepsTheLayoutKeepsTheFingerprint()
        {
            // Re-declaring `b` as the same type keeps both index and slot type, so a compatible extension update
            // does NOT invalidate every precompiled template that binds it.
            Assert.Equal(PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintBaseExtension)),
                PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintNarrowedExtension)));
        }

        [Fact]
        public void GauntletFallsBackWhenTheRecordedLayoutNoLongerMatchesTheLiveOne()
        {
            var live = typeof(FingerprintFixtures.FingerprintBaseExtension);
            var entry = Entry(new PrecompiledExtensionBinding("fpbase",
                PrecompiledGauntlet.AqnSansVersion(live),
                // A stale fingerprint: what the manifest would carry if the extension package had since gained a
                // slot. Before this row the render simply wrote values into the wrong slots.
                PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintAddedExtension))));

            var failure = PrecompiledGauntlet.Validate(entry, new TemplateOptions(),
                (binding, type) => true);

            Assert.NotNull(failure);
            Assert.Equal(PrecompiledFallbackReason.ExtensionBindingMismatch, failure.Value.Reason);
            // The detail string is pinned in full, not probed for a substring. Every Fail() in the gauntlet spells
            // its detail "<Thing> 'name': manifest=X live=Y", and telemetry consumers read these; a substring
            // assertion let a hand-restored version of this check drift to a different shape unnoticed.
            Assert.Equal(
                "Extension 'fpbase': prop layout " +
                "manifest=" + PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintAddedExtension)) + " " +
                "live=" + PropLayout.Fingerprint(live),
                failure.Value.Detail);
        }

        [Fact]
        public void ALiveExtensionThatDroppedAllItsPropsReportsTheAbsentSentinel()
        {
            // The live fingerprint is null when the extension no longer declares any [Prop] at all — the package
            // removed them. Interpolating a null there would render "live=" and read as a formatting bug on the
            // single most diagnostic case, so it uses the same angle-bracket sentinel as this file's
            // <unresolved>/<missing>/<delegate>/<overloads added> details.
            var live = typeof(FingerprintFixtures.NoPropsExtension);
            Assert.Null(PropLayout.Fingerprint(live));

            var entry = Entry(new PrecompiledExtensionBinding("fpnone",
                PrecompiledGauntlet.AqnSansVersion(live),
                PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintBaseExtension))));

            var failure = PrecompiledGauntlet.Validate(entry, new TemplateOptions(), (binding, type) => true);

            Assert.NotNull(failure);
            Assert.EndsWith("live=<none>", failure.Value.Detail);
        }

        [Fact]
        public void GauntletPassesWhenTheLayoutMatches()
        {
            var live = typeof(FingerprintFixtures.FingerprintBaseExtension);
            var entry = Entry(new PrecompiledExtensionBinding("fpbase",
                PrecompiledGauntlet.AqnSansVersion(live), PropLayout.Fingerprint(live)));

            Assert.Null(PrecompiledGauntlet.Validate(entry, new TemplateOptions(), (binding, type) => true));
        }

        [Fact]
        public void AManifestPredatingTheRowStillPasses()
        {
            // The additive-schema contract: a binding row with no fingerprint (schema 1–3) is checked vacuously,
            // so no existing precompiled assembly is forced to re-precompile by the row's arrival.
            var live = typeof(FingerprintFixtures.FingerprintBaseExtension);
            var entry = Entry(new PrecompiledExtensionBinding("fpbase", PrecompiledGauntlet.AqnSansVersion(live)));

            Assert.Null(PrecompiledGauntlet.Validate(entry, new TemplateOptions(), (binding, type) => true));
        }

        private static PrecompiledTemplateInfo Entry(PrecompiledExtensionBinding binding)
        {
            var options = new TemplateOptions();
            return new PrecompiledTemplateInfo("fp.heddle", typeof(PropLayoutFingerprintTests), null, false,
                "hash", null,
                new PrecompiledOptionsFingerprint(options.OutputProfile, options.ExpressionMode,
                    options.TrimDirectiveLines),
                new[] { binding }, null, default, NoOpStrategy.Instance);
        }

        /// <summary>Any non-null strategy makes the entry "precompiled" for the gauntlet's step-0 short-circuit;
        /// nothing here renders.</summary>
        private sealed class NoOpStrategy : Heddle.Runtime.IProcessStrategy
        {
            internal static readonly NoOpStrategy Instance = new NoOpStrategy();

            public string Execute(in Scope scope) => string.Empty;

            public void Render(in Scope scope) { }
        }

        /// <summary>The corlib assembly name differs between TFMs; the assertion is about shape, not the BCL's
        /// packaging, so the assembly half is normalised.</summary>
        private static string Normalize(string fingerprint) =>
            fingerprint?.Replace(", " + typeof(int).Assembly.GetName().Name, ", System.Private.CoreLib");
    }
}
