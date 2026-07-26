using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Language.Binding;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests.RegistrationPrecedence
{
    /// <summary>Base of the assignability pair: the incumbent a derived candidate may take over from.</summary>
    [ExtensionName("regbase")]
    public class RegBaseExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => "base";
        public override void RenderData(in Scope scope) => scope.Renderer.Render("base");
    }

    /// <summary>Derived from the incumbent, so <c>incumbent.IsAssignableFrom(candidate)</c> holds and the
    /// registration overrides without <c>[ExtensionReplace]</c>.</summary>
    public sealed class RegDerivedExtension : RegBaseExtension
    {
        public override object ProcessData(in Scope scope) => "derived";
    }

    /// <summary>Unrelated to <see cref="RegBaseExtension"/> — the collision the runtime refuses.</summary>
    public sealed class RegUnrelatedExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => "unrelated";
        public override void RenderData(in Scope scope) => scope.Renderer.Render("unrelated");
    }

    /// <summary>Unrelated <b>and</b> declaring <c>[ExtensionReplace]</c> — displaces regardless.</summary>
    [ExtensionReplace]
    public sealed class RegReplacerExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => "replacer";
        public override void RenderData(in Scope scope) => scope.Renderer.Render("replacer");
    }
}

namespace Heddle.Tests
{
    /// <summary>
    /// Q8.3 — the registration-precedence rule, asserted on the <b>run tier</b>.
    /// <para>Phase 3 extracted the rule into <see cref="ExtensionRegistrationRules"/>, but only the generator
    /// called it: <c>TemplateFactory.AddExtensions</c> kept its own inlined copy, so mutating the shared file
    /// reddened one generator test and zero runtime tests. A shared file that only one side calls is a
    /// transcription that <em>reads</em> as a source of truth, which is worse than no extraction, because the
    /// record claims otherwise.</para>
    /// <para>These are the runtime-side assertions that had no existence before: the three verdicts through the
    /// public <see cref="TemplateFactory.AddExtensions"/> seam, plus the ordering rule that decides which candidate
    /// is the incumbent in the first place. Mutating <see cref="ExtensionRegistrationRules"/> now reddens them.</para>
    /// </summary>
    public class ExtensionRegistrationPrecedenceTests
    {
        /// <summary>A fresh name per test: the registry is process-global and append-only, so tests must not reuse
        /// each other's names.</summary>
        private static string FreshName() => "reg" + Guid.NewGuid().ToString("N").Substring(0, 12);

        private static Type Registered(string name)
        {
            Assert.True(TemplateFactory.TryGetExtensionType(name, out var type),
                $"'{name}' is not registered.");
            return type;
        }

        [Fact]
        public void AFreeNameRegisters()
        {
            var name = FreshName();
            TemplateFactory.AddExtensions(new[]
            {
                new ExtensionType(name, typeof(RegistrationPrecedence.RegBaseExtension), false)
            });

            Assert.Equal(typeof(RegistrationPrecedence.RegBaseExtension), Registered(name));
        }

        [Fact]
        public void AnAssignableCandidateOverridesTheIncumbent()
        {
            // The rule that makes `class MyIf : IfExtension` take over "if" — incumbent.IsAssignableFrom(candidate).
            var name = FreshName();
            TemplateFactory.AddExtensions(new[]
            {
                new ExtensionType(name, typeof(RegistrationPrecedence.RegBaseExtension), false),
                new ExtensionType(name, typeof(RegistrationPrecedence.RegDerivedExtension), false)
            });

            Assert.Equal(typeof(RegistrationPrecedence.RegDerivedExtension), Registered(name));
        }

        [Fact]
        public void AnUnrelatedCandidateIsRefused()
        {
            var name = FreshName();
            TemplateFactory.AddExtensions(new[]
            {
                new ExtensionType(name, typeof(RegistrationPrecedence.RegBaseExtension), false)
            });

            var ex = Assert.Throws<TemplateOverrideException>(() => TemplateFactory.AddExtensions(new[]
            {
                new ExtensionType(name, typeof(RegistrationPrecedence.RegUnrelatedExtension), false)
            }));
            Assert.Contains(name, ex.Message);
            // The incumbent survives the refusal.
            Assert.Equal(typeof(RegistrationPrecedence.RegBaseExtension), Registered(name));
        }

        [Fact]
        public void AReplaceCandidateOverridesAnUnrelatedIncumbent()
        {
            var name = FreshName();
            TemplateFactory.AddExtensions(new[]
            {
                new ExtensionType(name, typeof(RegistrationPrecedence.RegBaseExtension), false),
                new ExtensionType(name, typeof(RegistrationPrecedence.RegReplacerExtension), true)
            });

            Assert.Equal(typeof(RegistrationPrecedence.RegReplacerExtension), Registered(name));
        }

        [Fact]
        public void ReplaceCandidatesAreOrderedLastSoTheyWinRegardlessOfInputOrder()
        {
            // The replacer is offered FIRST here. Without the Replace-last ordering the unrelated non-replacing
            // candidate would arrive second and throw; with it, the replacer lands last and displaces.
            var name = FreshName();
            TemplateFactory.AddExtensions(new[]
            {
                new ExtensionType(name, typeof(RegistrationPrecedence.RegReplacerExtension), true),
                new ExtensionType(name, typeof(RegistrationPrecedence.RegBaseExtension), false)
            });

            Assert.Equal(typeof(RegistrationPrecedence.RegReplacerExtension), Registered(name));
        }

        [Fact]
        public void ANullNameOrTypeIsRejected()
        {
            Assert.Throws<ArgumentException>(() => TemplateFactory.AddExtensions(new[]
            {
                new ExtensionType(null, typeof(RegistrationPrecedence.RegBaseExtension), false)
            }));
            Assert.Throws<ArgumentException>(() => TemplateFactory.AddExtensions(new[]
            {
                new ExtensionType(FreshName(), null, false)
            }));
        }

        /// <summary>The shared verdict table itself, so the rule's own truth table is pinned next to the behaviour
        /// it produces — including the build-tier-only <see cref="ExtensionRegistrationVerdict.KeepIncumbent"/>
        /// relaxation, which the runtime never reaches.</summary>
        [Fact]
        public void TheSharedVerdictTableIsTheRuntimesRule()
        {
            Assert.Equal(ExtensionRegistrationVerdict.Register,
                ExtensionRegistrationRules.Resolve(false, false, false));
            Assert.Equal(ExtensionRegistrationVerdict.Replace,
                ExtensionRegistrationRules.Resolve(true, true, false));
            Assert.Equal(ExtensionRegistrationVerdict.Replace,
                ExtensionRegistrationRules.Resolve(true, false, true));
            Assert.Equal(ExtensionRegistrationVerdict.Conflict,
                ExtensionRegistrationRules.Resolve(true, false, false));

            // ResolveForBuild differs from Resolve on exactly one input, and only in the direction that makes the
            // build tier order-insensitive over the inheritance relation.
            Assert.Equal(ExtensionRegistrationVerdict.KeepIncumbent,
                ExtensionRegistrationRules.ResolveForBuild(true, false, false, true));
            Assert.Equal(ExtensionRegistrationVerdict.Conflict,
                ExtensionRegistrationRules.ResolveForBuild(true, false, false, false));
        }

        /// <summary>The ordering key both tiers sort candidates by before offering them.</summary>
        [Fact]
        public void TheOrderingKeyRanksInterfaceDataTypeAfterConcrete()
        {
            Assert.Equal(0, ExtensionRegistrationRules.OrderingKey(false, false));
            Assert.Equal(1, ExtensionRegistrationRules.OrderingKey(false, true));
            Assert.Equal(2, ExtensionRegistrationRules.OrderingKey(true, false));
            Assert.Equal(3, ExtensionRegistrationRules.OrderingKey(true, true));
        }
    }
}
