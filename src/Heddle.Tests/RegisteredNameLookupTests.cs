using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para><b>Q8.30: the engine answers to a registered name.</b> The <c>Name</c> item metadatum has now changed
    /// scope three times, and the history is worth stating because each step invalidated the previous step's rules
    /// rather than extending them:</para>
    /// <list type="number">
    /// <item><b>Override</b> (Q8.12, landing 1) — <c>Name</c> was a second spelling of <c>Key</c>. It replaced the
    /// path-derived key, so every <c>@&lt;&lt;</c> that named a file by its path stopped resolving. Wrong.</item>
    /// <item><b>Additive, import-only</b> (Q8.25) — the template keeps its key <em>and</em> gains the name; both
    /// spellings resolve. Scoped to build-time import resolution on the ruling's words, so the manifest carried keys
    /// only and a name was invisible at run time.</item>
    /// <item><b>Additive, plus runtime</b> (Q8.30, this fixture) — the import-only boundary was an artifact of the
    /// wiring, not a design: if a name is a useful key for an import it is a useful key full stop. The manifest
    /// carries the name and the registry answers to it.</item>
    /// </list>
    ///
    /// <para><b>The resolution-order decision, which this fixture is mostly about.</b> A lookup string can match one
    /// template's key and another's registered name. <b>The key wins</b>, always, and independently of the order the
    /// assemblies registered in. Three reasons, in order of weight:</para>
    /// <list type="bullet">
    /// <item><b>Additivity.</b> A name is an addition. An addition that displaces a spelling which already resolved
    /// is precisely the override Q8.25 corrected, and re-introducing it at the runtime tier would undo that
    /// correction on the surface where it is hardest to see.</item>
    /// <item><b>The match principle.</b> The build tier's import map is already two passes, keys first, names second
    /// (<c>HeddleTemplateGenerator.Emit</c>). The runtime uses the same order, so the two tiers cannot disagree about
    /// which template a spelling means — which is the whole point of the shared <see cref="TemplateKey"/> rule.</item>
    /// <item><b>Determinism.</b> Keys and names in one dictionary would make the winner depend on which assembly
    /// registered first, and registration order is the host's business. Two indexes consulted in a fixed order have
    /// no such dependency, which is why the invariant below is structural rather than a check.</item>
    /// </list>
    ///
    /// <para><b>The invariant:</b> a spelling that is a key is never present in the name index. It is enforced from
    /// both directions, because either can happen first across assemblies — a name that finds its spelling already
    /// taken by a key is not registered, and a key that arrives later evicts the name that was shadowing its
    /// spelling. Both report <c>HED7104</c>.</para>
    ///
    /// <para><b>Why a collision does not throw.</b> Duplicate <em>keys</em> throw
    /// (<see cref="PrecompiledRegistrationException"/>) because two templates claiming one registration is
    /// unresolvable — either could be the one the host meant, and picking silently is exactly the illegitimate
    /// fallback the taxonomy forbids. A name colliding with a key is fully resolved by the ordering rule with no
    /// ambiguity about which template renders, so there is nothing to refuse. And Q8.25's principle applies at both
    /// tiers: <b>a broken addition costs the addition and nothing more</b> — throwing would take a whole assembly's
    /// registration down over an alias.</para>
    /// </summary>
    [Collection("PrecompiledRegistrySerial")]
    public class RegisteredNameLookupTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public RegisteredNameLookupTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        // ---- Fixture plumbing --------------------------------------------------------------------------------

        private sealed class NameFakeStrategy : IProcessStrategy
        {
            public string Execute(in Scope scope) => string.Empty;
            public void Render(in Scope scope) { }
        }

        private static readonly IProcessStrategy Strategy = new NameFakeStrategy();

        /// <summary>An entry carrying a key and, optionally, a registered name. The name is passed already
        /// normalized, as the generator emits it.</summary>
        private static PrecompiledTemplateInfo Entry(string key, string registeredName = null) =>
            new PrecompiledTemplateInfo(
                key, typeof(object), null, false, "0",
                Array.Empty<PrecompiledImport>(),
                new PrecompiledOptionsFingerprint(OutputProfile.Text, ExpressionMode.Native, false),
                Array.Empty<PrecompiledExtensionBinding>(),
                Array.Empty<PrecompiledFunctionBinding>(),
                PrecompiledCapabilities.StringOutput, Strategy,
                registeredName: registeredName,
                linePathForm: PrecompiledLinePathForm.RootRelative);

        /// <summary>Manifest entries are handed in through a static slot because the manifest type is instantiated
        /// reflectively by <c>Register</c> and cannot take constructor arguments.</summary>
        private static readonly Dictionary<string, PrecompiledTemplateInfo[]> Pending =
            new Dictionary<string, PrecompiledTemplateInfo[]>(StringComparer.Ordinal);

        public sealed class SlotManifestA : IHeddleTemplateManifest
        {
            public IReadOnlyList<PrecompiledTemplateInfo> GetTemplates() => Pending["A"];
        }

        public sealed class SlotManifestB : IHeddleTemplateManifest
        {
            public IReadOnlyList<PrecompiledTemplateInfo> GetTemplates() => Pending["B"];
        }

        private static Version RuntimeVersion =>
            typeof(PrecompiledTemplates).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

        private static string CompatibleVersion =>
            $"{RuntimeVersion.Major}.{Math.Max(RuntimeVersion.Minor, 0)}.{Math.Max(RuntimeVersion.Build, 0)}";

        /// <summary>Registers a manifest carrying <paramref name="entries"/> as a fresh dynamic assembly, and returns
        /// its simple name (the string the registry attributes ownership to).</summary>
        private static string Register(string slot, params PrecompiledTemplateInfo[] entries)
        {
            Pending[slot] = entries;
            var manifestType = slot == "A" ? typeof(SlotManifestA) : typeof(SlotManifestB);
            var name = "HeddleNameAsm_" + slot + "_" + Guid.NewGuid().ToString("N");
            var ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
            var ctor = typeof(HeddleCompiledTemplatesAttribute)
                .GetConstructor(new[] { typeof(Type), typeof(int), typeof(string) });
            ab.SetCustomAttribute(new CustomAttributeBuilder(ctor,
                new object[] { manifestType, PrecompiledSchema.CurrentSchemaVersion, CompatibleVersion }));
            PrecompiledTemplates.Register(ab);
            return name;
        }

        private static TemplateOptions Options() =>
            new TemplateOptions("x")
            {
                OutputProfile = OutputProfile.Text,
                ExpressionMode = ExpressionMode.Native,
                TrimDirectiveLines = false
            };

        // ---- The behaviour Q8.30 asks for ------------------------------------------------------------------

        /// <summary>
        /// <para><b>The assertion the ruling asked for.</b> A template registered under a name resolves by that name,
        /// and it resolves to <em>the same entry</em> a lookup by key returns — asserted as reference identity, not as
        /// "some entry came back", because two indexes that happened to hold different objects for one template would
        /// satisfy the weaker check while being exactly the drift the manifest field exists to prevent.</para>
        /// <para>This is the test that was red before the feature: <c>Name</c> reached the import map and stopped, so
        /// the name lookup missed and returned false.</para>
        /// </summary>
        [Fact]
        public void ALookupByRegisteredNameFindsTheSameEntryAsALookupByKey()
        {
            Register("A", Entry("templates/report.heddle", "BuildReport.heddle"));

            Assert.True(PrecompiledTemplates.TryGet("templates/report.heddle", out var byKey));
            Assert.True(PrecompiledTemplates.TryGet("BuildReport.heddle", out var byName));
            Assert.Same(byKey, byName);
            // The entry keeps reporting its key, never the name it was reached through: the key is the identity the
            // staleness check and every diagnostic message are written against.
            Assert.Equal("templates/report.heddle", byName.Key);
            Assert.Equal("BuildReport.heddle", byName.RegisteredName);
        }

        /// <summary>A name goes through the same normalization a key does, on both sides of the lookup — it lives in
        /// the same namespace, so <c>BuildReport</c>, <c>BuildReport.heddle</c> and a backslashed spelling are one
        /// spelling. Without this the runtime and the build tier would disagree about what the author wrote.</summary>
        [Theory]
        [InlineData("BuildReport")]
        [InlineData("BuildReport.heddle")]
        [InlineData("~/BuildReport")]
        public void ARegisteredNameLookupNormalizesLikeAKeyLookup(string spelling)
        {
            Register("A", Entry("templates/report.heddle", "BuildReport.heddle"));

            Assert.True(PrecompiledTemplates.TryGet(spelling, out var entry));
            Assert.Equal("templates/report.heddle", entry.Key);
        }

        /// <summary>The name reaches the full request path, not just the raw index: <c>TryResolve</c> runs the
        /// per-request gauntlet on the entry a name found, exactly as it does for a key. A name that resolved only
        /// through <c>TryGet</c> would be invisible to every real caller, since the resolver and
        /// <c>PrecompiledRuntime</c> go through <c>TryResolve</c>.</summary>
        [Fact]
        public void TryResolveByRegisteredNameRunsTheGauntletAndReturnsTheEntry()
        {
            Register("A", Entry("templates/report.heddle", "BuildReport.heddle"));

            Assert.True(PrecompiledTemplates.TryResolve("BuildReport", Options(), out var entry));
            Assert.Equal("templates/report.heddle", entry.Key);
        }

        /// <summary>A name adds a lookup spelling and nothing else: it is not a registry <em>entry</em>, so
        /// <see cref="PrecompiledTemplates.Entries"/> — what a host enumerates to see what is precompiled — must not
        /// double-count it.</summary>
        [Fact]
        public void ARegisteredNameAddsNoEntryToTheEnumeration()
        {
            Register("A", Entry("templates/report.heddle", "BuildReport.heddle"));

            Assert.Single(PrecompiledTemplates.Entries);
        }

        /// <summary>An entry with no registered name is every pre-existing project: nothing new resolves, and in
        /// particular the null name must not become a lookup spelling of its own.</summary>
        [Fact]
        public void AnEntryWithNoRegisteredNameAddsNoSpelling()
        {
            Register("A", Entry("templates/report.heddle"));

            Assert.True(PrecompiledTemplates.TryGet("templates/report.heddle", out _));
            Assert.False(PrecompiledTemplates.TryGet("BuildReport", out _));
        }

        // ---- Resolution order: keys win, whatever the registration order ----------------------------------

        /// <summary>
        /// <para>The resolution-order decision, in the direction where the <b>name registers first</b>: assembly A's
        /// template is named <c>shared/banner.heddle</c>, then assembly B turns up owning that spelling as its real
        /// key. The key wins, and A's template stays reachable by its own key — the addition is what breaks, not the
        /// registration.</para>
        /// </summary>
        [Fact]
        public void AKeyArrivingLaterWinsOverAnAlreadyRegisteredName()
        {
            Register("A", Entry("templates/report.heddle", "shared/banner.heddle"));
            Assert.True(PrecompiledTemplates.TryGet("shared/banner.heddle", out var beforeB));
            Assert.Equal("templates/report.heddle", beforeB.Key);

            Register("B", Entry("shared/banner.heddle"));

            Assert.True(PrecompiledTemplates.TryGet("shared/banner.heddle", out var afterB));
            Assert.Equal("shared/banner.heddle", afterB.Key);
            // A's template is untouched: its own key still resolves to it.
            Assert.True(PrecompiledTemplates.TryGet("templates/report.heddle", out var a));
            Assert.Equal("templates/report.heddle", a.Key);
        }

        /// <summary>The same decision in the opposite registration order — the <b>key registers first</b> and the
        /// name that wants its spelling never gets it. Asserted separately because a one-sided implementation (a
        /// keys-first lookup with no eviction, or an eviction with no insert-time check) passes exactly one of these
        /// two tests, and which one it passes depends on host load order.</summary>
        [Fact]
        public void ANameNeverTakesASpellingAKeyAlreadyOwns()
        {
            Register("A", Entry("shared/banner.heddle"));
            Register("B", Entry("templates/report.heddle", "shared/banner.heddle"));

            Assert.True(PrecompiledTemplates.TryGet("shared/banner.heddle", out var entry));
            Assert.Equal("shared/banner.heddle", entry.Key);
        }

        /// <summary>Both collision directions are order-independent <em>in the same process state</em>: whichever way
        /// round they registered, the resolved template is the key owner. Stated as one assertion over both orders so
        /// the property is pinned as a property rather than as two anecdotes.</summary>
        [Fact]
        public void KeyPrecedenceIsIndependentOfRegistrationOrder()
        {
            Register("A", Entry("templates/report.heddle", "shared/banner.heddle"));
            Register("B", Entry("shared/banner.heddle"));
            Assert.True(PrecompiledTemplates.TryGet("shared/banner.heddle", out var nameFirst));

            PrecompiledTemplates.ResetForTests();

            Register("A", Entry("shared/banner.heddle"));
            Register("B", Entry("templates/report.heddle", "shared/banner.heddle"));
            Assert.True(PrecompiledTemplates.TryGet("shared/banner.heddle", out var keyFirst));

            Assert.Equal(nameFirst.Key, keyFirst.Key);
            Assert.Equal("shared/banner.heddle", keyFirst.Key);
        }

        // ---- HED7104: the new collision class ------------------------------------------------------------

        /// <summary>
        /// <para><b>The collision class the build tier cannot see.</b> Within one compilation a name that collides
        /// with another template's key is <c>HED7004</c>. Across assemblies nothing at build time can know: the
        /// generator reads referenced assemblies' symbols, but a manifest's rows live in a
        /// <c>GetTemplates</c> method <em>body</em>, which is IL and not symbol metadata. So the collision is
        /// detectable only where both manifests are present — at registration — and it reports through the fallback
        /// callback, the channel the runtime tier already uses for everything a host needs told about its
        /// precompiled assemblies.</para>
        /// <para>Here the key registers first, so the name is refused at insert.</para>
        /// </summary>
        [Fact]
        public void ANameCollidingWithAnotherAssemblysKeyReportsHed7104()
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            var a = Register("A", Entry("shared/banner.heddle"));
            var b = Register("B", Entry("templates/report.heddle", "shared/banner.heddle"));

            var evt = Assert.Single(events);
            Assert.Equal(PrecompiledFallbackReason.RegisteredNameUnavailable, evt.Reason);
            Assert.Equal("HED7104", evt.DiagnosticId);
            Assert.Equal(b, evt.AssemblyName);
            Assert.Contains("shared/banner.heddle", evt.Detail);
            // The detail names who owns the spelling, which is the only actionable part of the report.
            Assert.Contains(a, evt.Detail);
        }

        /// <summary>The eviction direction reports too, and names the same spelling: a name that was working stops
        /// working because a key claimed its spelling, and a host that never hears about that sees a template quietly
        /// change identity between deployments.</summary>
        [Fact]
        public void AKeyEvictingAShadowedNameReportsHed7104()
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            Register("A", Entry("templates/report.heddle", "shared/banner.heddle"));
            Assert.Empty(events);

            var b = Register("B", Entry("shared/banner.heddle"));

            var evt = Assert.Single(events);
            Assert.Equal(PrecompiledFallbackReason.RegisteredNameUnavailable, evt.Reason);
            Assert.Equal("HED7104", evt.DiagnosticId);
            Assert.Equal(b, evt.AssemblyName);
            Assert.Contains("shared/banner.heddle", evt.Detail);
        }

        /// <summary>Two templates in different assemblies claiming one name: first-come keeps it, the loser is told,
        /// and neither registration throws. Unlike a duplicate key there is no reason to refuse the assembly — no
        /// existing resolution changes meaning, and the template whose name lost is still fully reachable by its
        /// key.</summary>
        [Fact]
        public void TwoAssembliesClaimingOneNameReportHed7104AndKeepFirstCome()
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            Register("A", Entry("a/first.heddle", "Shared.heddle"));
            var b = Register("B", Entry("b/second.heddle", "Shared.heddle"));

            var evt = Assert.Single(events);
            Assert.Equal(PrecompiledFallbackReason.RegisteredNameUnavailable, evt.Reason);
            Assert.Equal(b, evt.AssemblyName);

            // First-come kept it.
            Assert.True(PrecompiledTemplates.TryGet("Shared", out var entry));
            Assert.Equal("a/first.heddle", entry.Key);
            // And the loser is still reachable by its own key.
            Assert.True(PrecompiledTemplates.TryGet("b/second.heddle", out _));
        }

        /// <summary>The contrast that justifies the severity choice, asserted rather than argued: a duplicate
        /// <b>key</b> still throws, and a colliding <b>name</b> still does not. Both in one test so an implementation
        /// that unified them — in either direction — cannot pass.</summary>
        [Fact]
        public void ANameCollisionDegradesWhileADuplicateKeyStillThrows()
        {
            PrecompiledTemplates.OnFallback = _ => { };

            Register("A", Entry("shared/banner.heddle", "Shared.heddle"));

            // A colliding name: no throw.
            Register("B", Entry("b/second.heddle", "Shared.heddle"));

            // A duplicate key: still a throw, and still transactional.
            Pending["B"] = new[] { Entry("shared/banner.heddle") };
            var ab = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("HeddleNameAsm_Dup_" + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.Run);
            var ctor = typeof(HeddleCompiledTemplatesAttribute)
                .GetConstructor(new[] { typeof(Type), typeof(int), typeof(string) });
            ab.SetCustomAttribute(new CustomAttributeBuilder(ctor,
                new object[] { typeof(SlotManifestB), PrecompiledSchema.CurrentSchemaVersion, CompatibleVersion }));

            Assert.Throws<PrecompiledRegistrationException>(() => PrecompiledTemplates.Register(ab));
        }

        /// <summary>A name equal to the template's <b>own</b> key is a redundant request, not a collision: the same
        /// string is already the spelling that resolves, so there is nothing to add and nothing to report. The
        /// build tier makes the same call for the same reason.</summary>
        [Fact]
        public void ANameEqualToItsOwnKeyIsSilent()
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            Register("A", Entry("templates/report.heddle", "templates/report.heddle"));

            Assert.Empty(events);
            Assert.True(PrecompiledTemplates.TryGet("templates/report.heddle", out var entry));
            Assert.Equal("templates/report.heddle", entry.Key);
        }

        /// <summary>
        /// <para><b>Q8.32(b): the registration path has no silent drop left.</b> A <c>RegisteredName</c> the shared
        /// key rule refuses — a <c>..</c> segment, a trailing separator, whitespace — used to be <c>continue</c>d past
        /// with no event at all: the one place in registration where a manifest row was discarded and nothing was
        /// said, while both <em>collision</em> arms already reported <c>HED7104</c>. The generator cannot emit such a
        /// name (a name that fails normalization is <c>HED7004</c> at build time and never reaches a manifest), so the
        /// population is exactly the manifests no build tier vetted — hand-written, third-party, or emitted by a tool
        /// that skipped the rule. That is the population most in need of being told.</para>
        /// <para>It reports through the same channel and the same id as a collision, deliberately: from the host's
        /// side the outcome is identical — a name it expected to resolve does not, and the template is still reachable
        /// by its key — so a second id would split one situation across two rows of the registry. The
        /// <em>sub-question (a)</em> half of Q8.32, a per-request gauntlet arm for <c>RegisteredName</c>, was rejected
        /// as over-engineering and is deliberately absent: a name resolves to an entry whose every row the gauntlet
        /// already re-checks, so re-validating the name would re-validate nothing.</para>
        /// <para><b>One mutation survives here, and it is recorded rather than papered over.</b> Indexing the refused
        /// spelling anyway — <c>byName[template.RegisteredName] = template</c> alongside the report — passes every
        /// test, and provably must: <see cref="PrecompiledTemplates.TryGet"/> normalizes before it consults the name
        /// index, so a spelling outside the range of <c>TryNormalize</c> is unreachable by any lookup; the eviction
        /// and arbitration arms compare against <em>normalized</em> keys and names only; and
        /// <see cref="PrecompiledTemplates.Entries"/> reads the key index. The mutant is therefore extensionally
        /// equal to the code — unreachable state, not a defect — and the observable half of this arm is the report,
        /// which two other mutants (dropping the event, dropping the requesting key from its detail) do kill.</para>
        /// </summary>
        [Theory]
        [InlineData("../escape")]
        [InlineData("nested/../escape")]
        [InlineData("trailing/")]
        [InlineData("   ")]
        [InlineData("./")]
        public void AnUnnormalizableRegisteredNameReportsHed7104(string unusable)
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            var a = Register("A", Entry("templates/report.heddle", unusable));

            var evt = Assert.Single(events);
            Assert.Equal(PrecompiledFallbackReason.RegisteredNameUnavailable, evt.Reason);
            Assert.Equal("HED7104", evt.DiagnosticId);
            Assert.Equal(a, evt.AssemblyName);
            // The report has to name the spelling that was refused and the template that asked for it; otherwise a
            // host with fifty templates is told only that something, somewhere, lost a name.
            Assert.Contains(unusable, evt.Detail);
            Assert.Contains("templates/report.heddle", evt.Detail);

            // The addition is what was lost, and nothing else: the template is still reachable by its key, and the
            // refused spelling resolves to nothing.
            Assert.True(PrecompiledTemplates.TryGet("templates/report.heddle", out var entry));
            Assert.Equal("templates/report.heddle", entry.Key);
            Assert.False(PrecompiledTemplates.TryGet(unusable, out _));
        }

        /// <summary>The complement, so the new report cannot be satisfied by reporting on every row: an
        /// <em>absent</em> name is not a refused name. Every pre-existing project's manifest is entirely rows like
        /// this, and a registration that raised an event per row would make <c>OnFallback</c> useless as a
        /// signal.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void AnAbsentRegisteredNameStaysSilent(string absent)
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            Register("A", Entry("templates/report.heddle", absent));

            Assert.Empty(events);
        }

        /// <summary>
        /// <para>Two templates in the <em>same</em> manifest, one naming the other's key. The build tier refuses this
        /// at <c>HED7004</c>, so a first-party manifest cannot carry it — but a hand-written or third-party manifest
        /// can, and the registry must be order-independent within one assembly rather than depending on the order
        /// <c>GetTemplates</c> happened to list its rows. That is why keys are staged for the whole manifest before
        /// any name is considered.</para>
        /// <para><b>The report is asserted, not just the resolution</b>, and that distinction was found by mutation
        /// testing. Checking a name against the pre-registration keys instead of this manifest's staged ones
        /// <em>survived</em> while only the resolution was asserted: the name went into the name index in violation
        /// of the disjointness invariant, and the lookup still returned the key owner because
        /// <see cref="PrecompiledTemplates.TryGet"/> consults keys first. Two redundant guards masking each other is
        /// exactly the shape a mutation survives, so the invariant is now pinned where it is actually established —
        /// the name is refused, and the host is told.</para>
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WithinOneManifestKeysAreStagedBeforeNames(bool nameRowFirst)
        {
            var events = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = e => events.Add(e);

            var named = Entry("templates/report.heddle", "shared/banner.heddle");
            var keyed = Entry("shared/banner.heddle");
            Register("A", nameRowFirst ? new[] { named, keyed } : new[] { keyed, named });

            Assert.True(PrecompiledTemplates.TryGet("shared/banner.heddle", out var entry));
            Assert.Equal("shared/banner.heddle", entry.Key);

            // The name was refused rather than parked in the index behind a key that shadows it.
            var evt = Assert.Single(events);
            Assert.Equal(PrecompiledFallbackReason.RegisteredNameUnavailable, evt.Reason);
            Assert.Contains("shared/banner.heddle", evt.Detail);
            Assert.Contains("the template key of", evt.Detail);
        }

        /// <summary>A case-only miss on a <em>name</em> behaves like a case-only miss on a key: the lookup misses
        /// rather than serving the wrong template, because the whole namespace is ordinal by contract.</summary>
        [Fact]
        public void ARegisteredNameLookupIsOrdinal()
        {
            PrecompiledTemplates.OnFallback = _ => { };
            Register("A", Entry("templates/report.heddle", "BuildReport.heddle"));

            Assert.False(PrecompiledTemplates.TryGet("buildreport", out _));
        }

        /// <summary>Every fallback reason this fixture raises must be a real enum member with a catalogued id —
        /// cheap, and it is what stops a new reason from being added without its registry row.</summary>
        [Fact]
        public void TheNewFallbackReasonAndIdAreDeclared()
        {
            Assert.Contains(PrecompiledFallbackReason.RegisteredNameUnavailable,
                Enum.GetValues(typeof(PrecompiledFallbackReason)).Cast<PrecompiledFallbackReason>());
            Assert.Equal("HED7104", HeddleDiagnosticIds.PrecompiledRegisteredNameUnavailable);
            Assert.True(HeddleDiagnosticCatalog.TryGet(
                HeddleDiagnosticIds.PrecompiledRegisteredNameUnavailable, out _));
        }
    }
}
