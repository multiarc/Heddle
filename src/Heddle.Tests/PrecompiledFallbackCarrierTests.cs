using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para><b>Q8.33: one field must not carry two kinds of key.</b> Through 2.0 the fallback event had a single
    /// <c>Key</c> property which held a <em>template key</em> for the per-request reasons and an <em>assembly
    /// name</em> for the registration-time ones (<c>SchemaVersionUnsupported</c>,
    /// <c>EngineVersionIncompatible</c>, and — as of Q8.30 — <c>RegisteredNameUnavailable</c>). A host could only
    /// tell which of the two it had by switching on <c>Reason</c>, i.e. by re-deriving a fact the event already
    /// knew, and the moment `HED7104` made the value actionable that inference became load-bearing. The event now
    /// carries <see cref="PrecompiledFallbackEvent.TemplateKey"/> and
    /// <see cref="PrecompiledFallbackEvent.AssemblyName"/> as separate properties, exactly one of which is
    /// populated.</para>
    ///
    /// <para><b>Why the union property was removed rather than narrowed.</b> Keeping <c>Key</c> and quietly
    /// restricting it to template keys is a behavioural break with no compile-time signal — a 2.0 host that reads
    /// <c>Key</c> to log which assembly was rejected would start logging null and never be told. Removing it is a
    /// binary break, which the compiler reports at the one site that has to change. The 2.1 disposition is recorded
    /// in <c>docs/spec/common/breaking-windows.md</c>.</para>
    ///
    /// <para><b>The mapping is pinned from both sides, which is the point of this fixture.</b> The code side is the
    /// two factories: each refuses a reason belonging to the other carrier, and the classifier behind them is an
    /// exhaustive switch that refuses a reason it does not know at all — so a reason added later cannot be raised
    /// without being classified. The declaration side is <see cref="AssemblyScoped"/> below, checked against the
    /// whole enum by <see cref="EveryReasonPopulatesExactlyOneCarrier"/>: a new reason absent from the table fails,
    /// and a table row the code disagrees with fails. Neither direction can be made green by editing one place.</para>
    /// </summary>
    public class PrecompiledFallbackCarrierTests
    {
        /// <summary>The declaration side of the reason→carrier pin: every reason <em>not</em> listed here is about one
        /// template and must carry a template key. A registration-time reason is about an assembly and has no one
        /// template to name, which is why the two carriers exist rather than one nullable one.</summary>
        private static readonly PrecompiledFallbackReason[] AssemblyScoped =
        {
            PrecompiledFallbackReason.SchemaVersionUnsupported,
            PrecompiledFallbackReason.EngineVersionIncompatible,
            PrecompiledFallbackReason.RegisteredNameUnavailable
        };

        public static IEnumerable<object[]> AllReasons() =>
            Enum.GetValues(typeof(PrecompiledFallbackReason))
                .Cast<PrecompiledFallbackReason>()
                .Select(r => new object[] { r });

        /// <summary>
        /// <para>Every declared reason is constructible through exactly one factory and refused by the other, and the
        /// carrier it does not use is null. Asserted over <c>Enum.GetValues</c> rather than over a hand-listed set so
        /// that adding a reason without deciding its carrier is a red test rather than a runtime surprise.</para>
        /// <para>Both failure directions are covered: a reason the code leaves unclassified is refused by <em>both</em>
        /// factories (so whichever branch this theory takes, it fails), and a reason the code classifies differently
        /// from <see cref="AssemblyScoped"/> fails on the branch the table chose.</para>
        /// </summary>
        [Theory]
        [MemberData(nameof(AllReasons))]
        public void EveryReasonPopulatesExactlyOneCarrier(PrecompiledFallbackReason reason)
        {
            if (AssemblyScoped.Contains(reason))
            {
                var evt = PrecompiledFallbackEvent.ForAssembly("Some.Assembly", reason, "detail", "HED7102");
                Assert.Equal("Some.Assembly", evt.AssemblyName);
                Assert.Null(evt.TemplateKey);
                Assert.Equal(reason, evt.Reason);
                Assert.Throws<ArgumentException>(() =>
                    PrecompiledFallbackEvent.ForTemplate("some/key.heddle", reason, "detail", "HED7101"));
            }
            else
            {
                var evt = PrecompiledFallbackEvent.ForTemplate("some/key.heddle", reason, "detail", "HED7101");
                Assert.Equal("some/key.heddle", evt.TemplateKey);
                Assert.Null(evt.AssemblyName);
                Assert.Equal(reason, evt.Reason);
                Assert.Throws<ArgumentException>(() =>
                    PrecompiledFallbackEvent.ForAssembly("Some.Assembly", reason, "detail", "HED7102"));
            }
        }

        /// <summary>An unclassified reason — the shape a future enum member has before anyone decides what it is about
        /// — is refused by both factories rather than silently landing in one carrier. This is the half of the pin
        /// that the theory above cannot express, because an undeclared value cannot appear in
        /// <c>Enum.GetValues</c>.</summary>
        [Fact]
        public void AnUnclassifiedReasonIsRefusedByBothFactories()
        {
            var unknown = (PrecompiledFallbackReason)9999;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PrecompiledFallbackEvent.ForTemplate("some/key.heddle", unknown, "detail", "HED7101"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PrecompiledFallbackEvent.ForAssembly("Some.Assembly", unknown, "detail", "HED7102"));
        }

        /// <summary>The carrier a reason does use must actually be populated: an empty string is the null the split
        /// exists to prevent, one indirection along. Without this a caller could satisfy the factory and still hand a
        /// host an event naming nothing.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ACarrierlessEventCannotBeConstructed(string blank)
        {
            Assert.Throws<ArgumentException>(() => PrecompiledFallbackEvent.ForTemplate(
                blank, PrecompiledFallbackReason.OptionsMismatch, "detail", "HED7101"));
            Assert.Throws<ArgumentException>(() => PrecompiledFallbackEvent.ForAssembly(
                blank, PrecompiledFallbackReason.SchemaVersionUnsupported, "detail", "HED7102"));
        }

        /// <summary>The union carrier is gone from the surface, and gone in the way that produces a compile error at a
        /// 2.0 host rather than a null at run time: no <c>Key</c> member, and no public constructor that could take a
        /// string meaning either thing. Asserted by reflection because the point is the <em>absence</em> of a member,
        /// which no ordinary call site can express — and because a well-meaning re-addition of a convenience
        /// <c>Key</c> would re-create the exact ambiguity Q8.33 removed.</summary>
        [Fact]
        public void TheUnionKeyCarrierIsGoneFromTheSurface()
        {
            var type = typeof(PrecompiledFallbackEvent);
            Assert.Empty(type.GetMember("Key", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        }

        /// <summary>A per-request failure names the template and no assembly, taken from the real gauntlet rather than
        /// from a hand-built event: the carriers are only worth anything if the code that raises events uses the right
        /// one.</summary>
        [Fact]
        public void APerRequestGauntletFailureCarriesTheTemplateKey()
        {
            var entry = TextEntry("views/home.heddle");
            var options = new TemplateOptions("x") { OutputProfile = OutputProfile.Html };

            var failure = PrecompiledTemplates.Validate(entry, options);

            Assert.NotNull(failure);
            Assert.Equal(PrecompiledFallbackReason.OptionsMismatch, failure.Value.Reason);
            Assert.Equal("views/home.heddle", failure.Value.TemplateKey);
            Assert.Null(failure.Value.AssemblyName);
        }

        private sealed class CarrierFakeStrategy : IProcessStrategy
        {
            public string Execute(in Scope scope) => string.Empty;
            public void Render(in Scope scope) { }
        }

        private static PrecompiledTemplateInfo TextEntry(string key) =>
            new PrecompiledTemplateInfo(
                key, typeof(object), null, false, "0",
                Array.Empty<PrecompiledImport>(),
                new PrecompiledOptionsFingerprint(OutputProfile.Text, ExpressionMode.Native, false),
                Array.Empty<PrecompiledExtensionBinding>(),
                Array.Empty<PrecompiledFunctionBinding>(),
                PrecompiledCapabilities.StringOutput, new CarrierFakeStrategy(),
                registeredName: null,
                linePathForm: PrecompiledLinePathForm.RootRelative);
    }
}
