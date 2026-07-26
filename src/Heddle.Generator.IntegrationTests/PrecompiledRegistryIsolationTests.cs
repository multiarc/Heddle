using System;
using Heddle.Data;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Base for gauntlet-crossing suites: clears process-global registry before/after each test to prevent key leakage.
    /// Requires <c>[Collection("PrecompiledRegistry")]</c> serialization to avoid reset races.
    /// </summary>
    public abstract class PrecompiledRegistryTestBase : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        protected PrecompiledRegistryTestBase()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
        }

        public virtual void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }
    }

    /// <summary>
    /// Leakage canary: two tests registering the same key must each see only their own entry (detects reset failures).
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class PrecompiledRegistryLeakageCanaryTests : PrecompiledRegistryTestBase
    {
        private const string CanaryKey = "views/phase0-canary.heddle";
        private const string CanaryTemplate = "@model(){{System.String}}@\\\ncanary @(this)\n";

        private static void RegisterCanary()
        {
            var gen = DifferentialHarness.Generate(new[] { (CanaryKey, CanaryTemplate) });
            Assert.NotNull(gen.Assembly);
            DifferentialHarness.ExpectPrecompiled(gen, CanaryKey);
            PrecompiledTemplates.Register(gen.Assembly);
        }

        [Fact]
        public void CanaryA_RegistersTheKeyAndIsAlone()
        {
            Assert.Empty(PrecompiledTemplates.Entries);
            RegisterCanary();
            Assert.True(PrecompiledTemplates.TryGet(CanaryKey, out var entry));
            Assert.True(entry.IsPrecompiled);
            Assert.Single(PrecompiledTemplates.Entries);
        }

        [Fact]
        public void CanaryB_StartsCleanAndCanRegisterTheSameKeyAgain()
        {
            // Would throw if CanaryA's registration had leaked.
            Assert.Empty(PrecompiledTemplates.Entries);
            Assert.False(PrecompiledTemplates.TryGet(CanaryKey, out _));
            RegisterCanary();
            Assert.Single(PrecompiledTemplates.Entries);
        }

        [Fact]
        public void ResetForTestsIsReachableFromTheIntegrationSuite()
        {
            RegisterCanary();
            Assert.NotEmpty(PrecompiledTemplates.Entries);
            PrecompiledTemplates.ResetForTests();
            Assert.Empty(PrecompiledTemplates.Entries);
        }
    }

    /// <summary>
    /// Unit tests for <see cref="FallbackGuard"/>: save/restore, expected/unexpected split, and <see cref="FallbackGuard.GuardedOptions"/>.
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class FallbackGuardTests : PrecompiledRegistryTestBase
    {
        private static void Raise(string key, PrecompiledFallbackReason reason) =>
            PrecompiledTemplates.OnFallback?.Invoke(
                PrecompiledFallbackEvent.ForTemplate(key, reason, "detail", "HED7101"));

        [Fact]
        public void InstallAndDispose_RestoresThePreviousHook()
        {
            Action<PrecompiledFallbackEvent> sentinel = _ => { };
            PrecompiledTemplates.OnFallback = sentinel;
            using (var guard = FallbackGuard.Install())
            {
                Assert.NotSame(sentinel, PrecompiledTemplates.OnFallback);
                guard.Verify();
            }

            Assert.Same(sentinel, PrecompiledTemplates.OnFallback);
        }

        [Fact]
        public void NoEvents_VerifyPasses()
        {
            using var guard = FallbackGuard.Install();
            guard.Verify();
            Assert.Empty(guard.Events);
        }

        [Fact]
        public void UndeclaredEvent_VerifyFails()
        {
            using var guard = FallbackGuard.Install();
            Raise("views/x.heddle", PrecompiledFallbackReason.StaleContent);
            var ex = Assert.Throws<FallbackGuardException>(() => guard.Verify());
            Assert.Contains("views/x.heddle", ex.Message);
            Assert.Contains("StaleContent", ex.Message);
        }

        [Fact]
        public void DeclaredEvent_VerifyPasses()
        {
            using var guard = FallbackGuard.Install();
            guard.Expect("views/x.heddle", PrecompiledFallbackReason.StaleContent);
            Raise("views/x.heddle", PrecompiledFallbackReason.StaleContent);
            guard.Verify();
            Assert.Single(guard.Events);
        }

        [Fact]
        public void DeclaredEventThatNeverFires_VerifyFails()
        {
            using var guard = FallbackGuard.Install();
            guard.Expect("views/x.heddle", PrecompiledFallbackReason.StaleContent);
            var ex = Assert.Throws<FallbackGuardException>(() => guard.Verify());
            Assert.Contains("never fired", ex.Message);
        }

        [Fact]
        public void ExpectationIsConsumedOncePerEvent()
        {
            using var guard = FallbackGuard.Install();
            guard.Expect("views/x.heddle", PrecompiledFallbackReason.StaleContent);
            Raise("views/x.heddle", PrecompiledFallbackReason.StaleContent);
            Raise("views/x.heddle", PrecompiledFallbackReason.StaleContent);
            Assert.Throws<FallbackGuardException>(() => guard.Verify());
        }

        [Fact]
        public void DeclaredKeyWithADifferentReason_VerifyFails()
        {
            using var guard = FallbackGuard.Install();
            guard.Expect("views/x.heddle", PrecompiledFallbackReason.StaleContent);
            Raise("views/x.heddle", PrecompiledFallbackReason.OptionsMismatch);
            Assert.Throws<FallbackGuardException>(() => guard.Verify());
        }

        [Fact]
        public void GuardedOptions_SetStrictAndPreserveTheCallersSettings()
        {
            var options = FallbackGuard.GuardedOptions();
            Assert.Equal(PrecompiledMismatchPolicy.Strict, options.PrecompiledMismatchPolicy);

            var basis = new TemplateOptions { OutputProfile = OutputProfile.Text, TrimDirectiveLines = false };
            var guarded = FallbackGuard.GuardedOptions(basis);
            Assert.Equal(PrecompiledMismatchPolicy.Strict, guarded.PrecompiledMismatchPolicy);
            Assert.Equal(OutputProfile.Text, guarded.OutputProfile);
            Assert.False(guarded.TrimDirectiveLines);
            Assert.Equal(PrecompiledMismatchPolicy.Fallback, basis.PrecompiledMismatchPolicy); // the basis is untouched
        }
    }
}
