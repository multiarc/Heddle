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
    /// The prop-layout manifest row and its gauntlet check. Prop layouts were the one wire-format
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
            // its subclasses inherit `[ExtensionName("fpbase")]` (the attribute is Inherited = true) and
            // registering them all would collide under the runtime's precedence.
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
            Assert.StartsWith(baseline, added);
        }

        [Fact]
        public void ARedeclarationThatKeepsTheLayoutKeepsTheFingerprint()
        {
            Assert.Equal(PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintBaseExtension)),
                PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintNarrowedExtension)));
        }

        [Fact]
        public void GauntletFallsBackWhenTheRecordedLayoutNoLongerMatchesTheLiveOne()
        {
            var live = typeof(FingerprintFixtures.FingerprintBaseExtension);
            var entry = Entry(new PrecompiledExtensionBinding("fpbase",
                PrecompiledGauntlet.AqnSansVersion(live),
                PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintAddedExtension))));

            var failure = PrecompiledGauntlet.Validate(entry, new TemplateOptions(),
                (binding, type) => true);

            Assert.NotNull(failure);
            Assert.Equal(PrecompiledFallbackReason.ExtensionBindingMismatch, failure.Value.Reason);
            // Assert the full detail string, not a substring (telemetry format is "<Thing> 'name': manifest=X live=Y").
            Assert.Equal(
                "Extension 'fpbase': prop layout " +
                "manifest=" + PropLayout.Fingerprint(typeof(FingerprintFixtures.FingerprintAddedExtension)) + " " +
                "live=" + PropLayout.Fingerprint(live),
                failure.Value.Detail);
        }

        [Fact]
        public void ALiveExtensionThatDroppedAllItsPropsReportsTheAbsentSentinel()
        {
            // When an extension drops all [Prop], the detail uses <none> sentinel (not null, which would break format).
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

        /// <summary>
        /// Rows without a fingerprint are checked vacuously, so the layout check does not invalidate pre-fingerprint rows.
        /// (This test does not prove old manifests still load — that is in <c>OldSchemaManifestRejectionTests</c>.)
        /// </summary>
        [Fact]
        public void ARowWithNoFingerprintIsCheckedVacuously()
        {
            var live = typeof(FingerprintFixtures.FingerprintBaseExtension);
            var entry = Entry(new PrecompiledExtensionBinding("fpbase", PrecompiledGauntlet.AqnSansVersion(live)));
            Assert.Null(entry.ExtensionBindings[0].PropLayoutFingerprint);

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

        /// <summary>Dummy strategy to mark the entry as precompiled; does not render.</summary>
        private sealed class NoOpStrategy : Heddle.Runtime.IProcessStrategy
        {
            internal static readonly NoOpStrategy Instance = new NoOpStrategy();

            public string Execute(in Scope scope) => string.Empty;

            public void Render(in Scope scope) { }
        }

        /// <summary>Normalizes the assembly name to <c>System.Private.CoreLib</c> so the assertion is platform-independent.</summary>
        private static string Normalize(string fingerprint) =>
            fingerprint?.Replace(", " + typeof(int).Assembly.GetName().Name, ", System.Private.CoreLib");
    }
}
