using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Heddle.Data;

namespace Heddle.Precompiled
{
    /// <summary>Process-wide precompiled template registry (phase 7 D7). Registration is lock-guarded with
    /// copy-on-write publication; lookups are lock-free volatile reads. A registry <b>miss</b> is never a failure —
    /// the dynamic path proceeds untouched.</summary>
    public static class PrecompiledTemplates
    {
        internal const string Hed7102 = Data.HeddleDiagnosticIds.PrecompiledManifestRejected;
        internal const string Hed7103 = Data.HeddleDiagnosticIds.PrecompiledKeyCaseMismatch;
        internal const string Hed7104 = Data.HeddleDiagnosticIds.PrecompiledRegisteredNameUnavailable;
        // The accepted schema window lives in the shared PrecompiledSchema (phase 5 D5), which the generator also
        // emits from; see MinSupportedSchemaVersion for why 2.1 raises the floor to 3 (Q8.2 — the RELEASED schema 1–2
        // manifests reference a PrecompiledExtensionBinding constructor that no longer exists, so accepting them
        // faults here instead of falling back). The window is a point: {1, 2} shipped and are now excluded, the three
        // unreleased bumps above 2 were collapsed into one, so 3 is the only shape this engine reads.

        private sealed class Snapshot
        {
            public Snapshot(Dictionary<string, PrecompiledTemplateInfo> byKey,
                Dictionary<string, string> keyOwner,
                Dictionary<string, string> shadow,
                HashSet<string> assemblies,
                Dictionary<string, PrecompiledTemplateInfo> byName,
                Dictionary<string, string> nameOwner)
            {
                ByKey = byKey;
                KeyOwner = keyOwner;
                Shadow = shadow;
                Assemblies = assemblies;
                ByName = byName;
                NameOwner = nameOwner;
            }

            public Dictionary<string, PrecompiledTemplateInfo> ByKey { get; }
            public Dictionary<string, string> KeyOwner { get; }
            public Dictionary<string, string> Shadow { get; }
            public HashSet<string> Assemblies { get; }

            /// <summary>The registered-name index (Q8.30) — a <b>second</b> index rather than extra rows in
            /// <see cref="ByKey"/>, which is what makes key precedence structural. Invariant, enforced from both
            /// directions in <see cref="Register"/>: no spelling is present here and in <see cref="ByKey"/> at the
            /// same time.</summary>
            public Dictionary<string, PrecompiledTemplateInfo> ByName { get; }

            /// <summary>Which assembly a registered name belongs to, for first-come arbitration and for the
            /// <c>HED7104</c> detail.</summary>
            public Dictionary<string, string> NameOwner { get; }
        }

        private static readonly object RegistrationLock = new object();

        private static Snapshot _snapshot = EmptySnapshot();

        private static Snapshot EmptySnapshot() => new Snapshot(
            new Dictionary<string, PrecompiledTemplateInfo>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, PrecompiledTemplateInfo>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));

        /// <summary>Per-request/registration fallback and diagnostic callback (HED71xx). Invoked outside locks.</summary>
        public static Action<PrecompiledFallbackEvent> OnFallback { get; set; }

        /// <summary>Integration-supplied binding matcher; null = AQN-sans-version default (D9).</summary>
        public static Func<PrecompiledExtensionBinding, Type, bool> BindingResolver { get; set; }

        /// <summary>All registered entries (including fallback-marker entries, D21). Snapshot; safe to enumerate.</summary>
        public static IReadOnlyCollection<PrecompiledTemplateInfo> Entries =>
            Volatile.Read(ref _snapshot).ByKey.Values.ToArray();

        /// <summary>Reads the assembly's <see cref="HeddleCompiledTemplatesAttribute"/>, runs the schema/engine gate,
        /// instantiates the manifest once, and adds its entries transactionally (D2). Idempotent per assembly;
        /// thread-safe; repeatable — the correction to <c>AssemblyHelper.Configure</c>'s one-shot gate (D7).</summary>
        public static void Register(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var attribute = assembly.GetCustomAttribute<HeddleCompiledTemplatesAttribute>();
            if (attribute == null)
                return;

            // Never null in practice; defaulted rather than left null because the fallback event's assembly carrier
            // is now required to be populated (Q8.33) and a diagnostic must not become a throw.
            var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? "<unknown assembly>";

            if (!PrecompiledSchema.IsSupported(attribute.SchemaVersion))
            {
                RaiseFallback(PrecompiledFallbackEvent.ForAssembly(assemblyName,
                    PrecompiledFallbackReason.SchemaVersionUnsupported,
                    $"SchemaVersion: manifest={attribute.SchemaVersion} supported={PrecompiledSchema.MinSupportedSchemaVersion}-{PrecompiledSchema.MaxSupportedSchemaVersion}",
                    Hed7102));
                return;
            }

            if (!IsEngineCompatible(attribute.EngineVersion, out var runtimeVersion))
            {
                RaiseFallback(PrecompiledFallbackEvent.ForAssembly(assemblyName,
                    PrecompiledFallbackReason.EngineVersionIncompatible,
                    $"EngineVersion: manifest={attribute.EngineVersion} runtime={runtimeVersion}", Hed7102));
                return;
            }

            // HED7104 reports (Q8.30) are collected under the lock and raised after it: OnFallback is host code and
            // must never run while the registration lock is held.
            List<PrecompiledFallbackEvent> lostNames = null;

            lock (RegistrationLock)
            {
                var current = _snapshot;
                if (current.Assemblies.Contains(assemblyName))
                    return; // idempotent per assembly

                var manifest = (IHeddleTemplateManifest)Activator.CreateInstance(attribute.ManifestType);
                var templates = manifest.GetTemplates() ?? Array.Empty<PrecompiledTemplateInfo>();

                var byKey = new Dictionary<string, PrecompiledTemplateInfo>(current.ByKey, StringComparer.Ordinal);
                var keyOwner = new Dictionary<string, string>(current.KeyOwner, StringComparer.Ordinal);
                var shadow = new Dictionary<string, string>(current.Shadow, StringComparer.OrdinalIgnoreCase);
                var byName = new Dictionary<string, PrecompiledTemplateInfo>(current.ByName, StringComparer.Ordinal);
                var nameOwner = new Dictionary<string, string>(current.NameOwner, StringComparer.Ordinal);

                // Pass 1 — keys. Staged transactionally: every key is validated before anything is published, and a
                // duplicate throws, because two templates claiming one registration has no resolvable answer.
                foreach (var template in templates)
                {
                    var key = TemplateKey.Normalize(template.Key);
                    if (keyOwner.TryGetValue(key, out var existingOwner))
                        throw new PrecompiledRegistrationException(key, existingOwner, assemblyName);
                    byKey[key] = template;
                    keyOwner[key] = assemblyName;
                    shadow[key] = key;

                    // A key beats a name that was already answering to its spelling, even though that name got there
                    // first. This is the eviction half of the invariant: without it, key precedence would hold only
                    // when the key's assembly happened to register first, and which template a spelling meant would
                    // depend on host load order. The name's own template is untouched — it keeps its key.
                    if (byName.ContainsKey(key))
                    {
                        var displaced = nameOwner.TryGetValue(key, out var previous) ? previous : "<unknown>";
                        byName.Remove(key);
                        nameOwner.Remove(key);
                        (lostNames ?? (lostNames = new List<PrecompiledFallbackEvent>())).Add(
                            PrecompiledFallbackEvent.ForAssembly(assemblyName,
                                PrecompiledFallbackReason.RegisteredNameUnavailable,
                                $"Name: '{key}' registered by '{displaced}' is now the template key of '{assemblyName}'; " +
                                "the key wins and the name no longer resolves", Hed7104));
                    }

                }

                // Pass 2 — names, over the whole manifest's keys, mirroring the build tier's two-pass import map
                // (Q8.25). Keys-first is what makes `Name` additive rather than an override, at both tiers: every key
                // is already known when the first name is considered, so a name can never displace one.
                foreach (var template in templates)
                {
                    if (string.IsNullOrEmpty(template.RegisteredName))
                        continue;

                    var key = TemplateKey.Normalize(template.Key);

                    // A name the shared key rule refuses (Q8.32(b)). The generator cannot emit one — a malformed
                    // `Name` is HED7004 at build time and never reaches a manifest — so this arm is reached only by a
                    // manifest no build tier vetted, which is exactly the population that most needs telling. It used
                    // to `continue` in silence, the one wholly silent drop in registration; it now reports through the
                    // same HED7104 channel as the two collision arms, because from the host's side the outcome is the
                    // same: a name it expected to resolve does not, and the template is still reachable by its key.
                    if (!TemplateKey.TryNormalize(template.RegisteredName, out var name))
                    {
                        (lostNames ?? (lostNames = new List<PrecompiledFallbackEvent>())).Add(
                            PrecompiledFallbackEvent.ForAssembly(assemblyName,
                                PrecompiledFallbackReason.RegisteredNameUnavailable,
                                $"Name: '{template.RegisteredName}' requested by '{key}' is not a usable key " +
                                "spelling; the name is not registered and the template stays reachable by its key",
                                Hed7104));
                        continue;
                    }

                    // The name spells the template's own key: it asks for the spelling that already resolves to it, so
                    // there is nothing to add and nothing to report.
                    if (string.Equals(name, key, StringComparison.Ordinal))
                        continue;

                    // The insert-time half of the invariant: the spelling belongs to a key, or to a name that got
                    // there first. Either way the addition is refused and the loser is told — it is not a throw,
                    // because a broken addition costs the addition and nothing more (Q8.25's rule, applied here).
                    string owner = null;
                    if (keyOwner.TryGetValue(name, out var keyHolder))
                        owner = "the template key of '" + keyHolder + "'";
                    else if (nameOwner.TryGetValue(name, out var nameHolder))
                        owner = "a registered name of '" + nameHolder + "'";

                    if (owner != null)
                    {
                        (lostNames ?? (lostNames = new List<PrecompiledFallbackEvent>())).Add(
                            PrecompiledFallbackEvent.ForAssembly(assemblyName,
                                PrecompiledFallbackReason.RegisteredNameUnavailable,
                                $"Name: '{name}' requested by '{key}' is already {owner}; the name is not registered " +
                                "and the template stays reachable by its key", Hed7104));
                        continue;
                    }

                    byName[name] = template;
                    nameOwner[name] = assemblyName;
                }

                var assemblies = new HashSet<string>(current.Assemblies, StringComparer.Ordinal) { assemblyName };
                Volatile.Write(ref _snapshot,
                    new Snapshot(byKey, keyOwner, shadow, assemblies, byName, nameOwner));
            }

            if (lostNames != null)
            {
                foreach (var evt in lostNames)
                    RaiseFallback(evt);
            }
        }

        /// <summary>
        /// <para>Normalizes <paramref name="key"/> then performs an ordinal lookup — <b>keys first, registered names
        /// second</b> (Q8.30). A case-only miss fires the shadow-index
        /// <see cref="PrecompiledFallbackReason.CaseMismatch"/> callback (HED7103) and returns false.</para>
        /// <para>The parameter is still called <c>key</c> because that is what it is for every caller that has one; a
        /// registered name is an additional spelling of the same lookup, not a second lookup. The order is the
        /// decision, not an implementation detail: a spelling that names one template's key and another's registered
        /// name resolves to the <b>key</b> owner, always, and independently of the order the two assemblies
        /// registered in. A name is an addition, and an addition that displaced an existing spelling would be the
        /// override Q8.25 corrected; the build tier's import map resolves the same way round, so neither tier can
        /// disagree with the other about what a spelling means.</para>
        /// <para>The name index is consulted only after the key index misses <em>and</em> is guaranteed disjoint from
        /// it by <see cref="Register"/>, so the ordering here is belt-and-braces rather than the only guard — a
        /// shadowed name cannot be in the index to be found.</para>
        /// </summary>
        public static bool TryGet(string key, out PrecompiledTemplateInfo entry)
        {
            entry = null;
            if (!TemplateKey.TryNormalize(key, out var normalized))
                return false;

            var snapshot = Volatile.Read(ref _snapshot);
            if (snapshot.ByKey.TryGetValue(normalized, out entry))
                return true;

            if (snapshot.ByName.TryGetValue(normalized, out entry))
                return true;

            if (snapshot.Shadow.TryGetValue(normalized, out var actual) &&
                !string.Equals(actual, normalized, StringComparison.Ordinal))
            {
                RaiseFallback(PrecompiledFallbackEvent.ForTemplate(normalized,
                    PrecompiledFallbackReason.CaseMismatch,
                    $"Key: requested '{normalized}' registered '{actual}'", Hed7103));
            }

            entry = null;
            return false;
        }

        /// <summary>Runs the per-request validation gauntlet for a resolved entry (D7/D8). Returns the first
        /// failure as a <see cref="PrecompiledFallbackEvent"/> or <c>null</c> on success. Exposed so integration/host
        /// code can validate coverage; the resolver adapter calls this before rendering through
        /// <see cref="PrecompiledTemplateInfo.Strategy"/>.</summary>
        public static PrecompiledFallbackEvent? Validate(PrecompiledTemplateInfo entry, TemplateOptions options)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            return PrecompiledGauntlet.Validate(entry, options, BindingResolver);
        }

        /// <summary>
        /// <para>The aggregate <b>post-configuration</b> validation pass (Q8.32 subset A): runs the same gauntlet
        /// <see cref="Validate"/> runs over <b>every</b> registered entry and returns all failures together, before
        /// any render. Call it once the host has finished configuring — assemblies registered, extensions bound,
        /// functions registered — and log or fail the startup on the report.</para>
        /// <para>It exists because the recommended host API never reaches the gate. A typed entry point
        /// (<c>Templates_X.Generate(model)</c>) goes straight to <c>PrecompiledRuntime.GenerateString</c>: no
        /// lookup, no gauntlet, and — this being the point — <b>no fallback either</b>, so an extension- or
        /// function-binding mismatch on that path is not a silent degrade but a render against a stale binding. The
        /// gauntlet is reached only through <see cref="TryResolve"/>, i.e. only by dynamic call sites.</para>
        /// <para><b>It changes nothing per request.</b> The gauntlet still runs exactly where it ran before; this
        /// is an additional check the host asks for, not a moved one. It is a report and not a gate: it never
        /// raises <see cref="OnFallback"/> (nothing degraded — no render happened) and it does not throw under
        /// <see cref="PrecompiledMismatchPolicy.Strict"/>, which polices requests. Deciding what a failure costs is
        /// the caller's.</para>
        /// <para><b>The answer is scoped to <paramref name="options"/></b> and the report says so — see
        /// <see cref="PrecompiledValidationReport"/>. Four gauntlet inputs are per-request, so one pass cannot
        /// speak for a host that renders under several shapes; such a host calls this once per shape.</para>
        /// </summary>
        /// <param name="options">The options shape to validate against. Required: a verdict with no options to
        /// scope it would be unreadable, so there is no parameterless form.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        public static PrecompiledValidationReport ValidateAll(TemplateOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Entries is already a snapshot array; ordering is by key so the report does not inherit the
            // registry dictionary's enumeration order, which is an implementation accident.
            var entries = Entries.OrderBy(e => e.Key, StringComparer.Ordinal).ToList();

            List<PrecompiledFallbackEvent> failures = null;
            foreach (var entry in entries)
            {
                var failure = PrecompiledGauntlet.Validate(entry, options, BindingResolver);
                if (failure != null)
                    (failures ?? (failures = new List<PrecompiledFallbackEvent>())).Add(failure.Value);
            }

            return new PrecompiledValidationReport(
                new PrecompiledOptionsFingerprint(options.OutputProfile, options.ExpressionMode,
                    options.TrimDirectiveLines),
                options.Functions,
                options.EnableFileChangeCheck,
                entries.Count,
                (IReadOnlyList<PrecompiledFallbackEvent>)failures ?? Array.Empty<PrecompiledFallbackEvent>());
        }

        /// <summary>Resolves a precompiled entry for a request (phase 7 D7/D8): normalized lookup, then the
        /// per-request gauntlet under the request's <see cref="TemplateOptions"/>. On a gauntlet pass returns true
        /// with the entry; on a registry miss returns false (the dynamic path proceeds untouched). On a gauntlet
        /// failure the <see cref="OnFallback"/> callback fires; under <see cref="PrecompiledMismatchPolicy.Strict"/>
        /// it then throws <see cref="PrecompiledMismatchException"/>, under <c>Fallback</c> it returns false.</summary>
        public static bool TryResolve(string key, TemplateOptions options, out PrecompiledTemplateInfo entry)
        {
            entry = null;
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (!TryGet(key, out var found))
                return false;

            var failure = PrecompiledGauntlet.Validate(found, options, BindingResolver);
            if (failure == null)
            {
                entry = found;
                return true;
            }

            RaiseFallback(failure.Value);
            if (options.PrecompiledMismatchPolicy == PrecompiledMismatchPolicy.Strict)
                throw new PrecompiledMismatchException(found.Key, failure.Value.Reason, failure.Value.Detail);
            return false;
        }

        /// <summary>Test-only reset of registry state (does not touch <see cref="OnFallback"/>/<see cref="BindingResolver"/>).</summary>
        internal static void ResetForTests()
        {
            lock (RegistrationLock)
            {
                Volatile.Write(ref _snapshot, EmptySnapshot());
            }
        }

        private static bool IsEngineCompatible(string manifestVersion, out Version runtimeVersion)
        {
            runtimeVersion = typeof(PrecompiledTemplates).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            if (!Version.TryParse(manifestVersion, out var parsed))
                return false;
            return PrecompiledSchema.IsEngineCompatible(parsed, runtimeVersion);
        }

        private static void RaiseFallback(PrecompiledFallbackEvent evt)
        {
            OnFallback?.Invoke(evt);
        }
    }
}
