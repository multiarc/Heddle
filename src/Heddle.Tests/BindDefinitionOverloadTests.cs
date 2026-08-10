using System.Reflection;
using Heddle.Core;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The additive <c>PrecompiledRuntime.BindDefinition</c> overload that takes a per-carrier
    /// <c>needsLocals</c>.
    /// <para>Two claims are load-bearing: the existing overloads are behaviourally unchanged (the public-API
    /// golden covers their <em>signatures</em>, not their behavior), and the 10-arg one forwards with both flags
    /// equal, so it is byte-identical to before. Assemblies emitted by older generator versions call those
    /// overloads, so a forwarding mistake is a silent behavior change for already-shipped output.</para>
    /// <para>Frame provisioning is read off each carrier by reflection because that is the only observable the
    /// binding produces — <c>AbstractExtension._needsLocals</c> is what <c>GetInnerResult</c> reads to decide between
    /// a fresh frame, a cleared one and a passthrough.</para>
    /// </summary>
    public class BindDefinitionOverloadTests
    {
        private sealed class NullStrategy : IProcessStrategy
        {
            public string Execute(in Scope scope) => string.Empty;

            public void Render(in Scope scope)
            {
            }
        }

        private static bool NeedsLocals(AbstractExtension extension)
        {
            var field = typeof(AbstractExtension).GetField("_needsLocals",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (bool) field.GetValue(extension);
        }

        /// <summary>The inner carrier holds the definition body, accessible via <c>DefinitionParameterTemplate</c>.</summary>
        private static AbstractExtension Inner(AbstractExtension outer)
        {
            var property = outer.GetType().GetProperty("DefinitionParameterTemplate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property);
            return (AbstractExtension) property.GetValue(outer);
        }

        private static (bool Body, bool CallerContent) PerCarrier(bool bodyNeedsLocals,
            bool callerContentNeedsLocals)
        {
            var outer = PrecompiledRuntime.BindDefinition(new NullStrategy(), new NullStrategy(), null, null,
                RenderType.Raw, bodyNeedsLocals, callerContentNeedsLocals, false, 10, 1, 1);
            return (NeedsLocals(Inner(outer)), NeedsLocals(outer));
        }

        /// <summary>The overload's own semantics: each carrier gets its own flag, all four pairs.</summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void EachCarrierGetsItsOwnFlag(bool body, bool callerContent)
        {
            Assert.Equal((body, callerContent), PerCarrier(body, callerContent));
        }

        /// <summary>The 10-argument legacy overload applies one flag to both carriers, preserving original behavior.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheSlotAwareLegacyOverloadStillAppliesOneFlagToBothCarriers(bool needsLocals)
        {
            var outer = PrecompiledRuntime.BindDefinition(new NullStrategy(), new NullStrategy(), null, null,
                RenderType.Raw, needsLocals, false, 10, 1, 1);

            Assert.Equal(needsLocals, NeedsLocals(outer));
            Assert.Equal(needsLocals, NeedsLocals(Inner(outer)));
            Assert.Equal(PerCarrier(needsLocals, needsLocals), (NeedsLocals(Inner(outer)), NeedsLocals(outer)));
        }

        /// <summary>The 9-argument legacy overload forwards through the 10-argument one.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheOldestLegacyOverloadStillAppliesOneFlagToBothCarriers(bool needsLocals)
        {
            var outer = PrecompiledRuntime.BindDefinition(new NullStrategy(), new NullStrategy(), null, null,
                RenderType.Raw, needsLocals, 10, 1, 1);

            Assert.Equal(PerCarrier(needsLocals, needsLocals), (NeedsLocals(Inner(outer)), NeedsLocals(outer)));
        }

        /// <summary>The per-carrier overload must respect <c>slotMode</c> independently of the split.</summary>
        [Fact]
        public void SlotModeIsIndependentOfThePerCarrierFlags()
        {
            var outer = PrecompiledRuntime.BindDefinition(new NullStrategy(), new NullStrategy(), null, null,
                RenderType.Raw, true, false, true, 10, 1, 1);

            var slotMode = outer.GetType().GetProperty("SlotMode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(slotMode);
            Assert.True((bool) slotMode.GetValue(outer));
            Assert.True(NeedsLocals(Inner(outer)));
            Assert.False(NeedsLocals(outer));
        }
    }
}
