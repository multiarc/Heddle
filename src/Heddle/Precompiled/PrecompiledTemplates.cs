using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Heddle.Data;
using Heddle.Precompiled.CompiledForm;

namespace Heddle.Precompiled
{
    /// <summary>Process-wide precompiled template registry. Registration is lock-guarded with
    /// copy-on-write publication; lookups are lock-free volatile reads. A registry <b>miss</b> is never a failure —
    /// the dynamic path proceeds untouched.</summary>
    public static class PrecompiledTemplates
    {
        internal const string Hed7102 = Data.HeddleDiagnosticIds.PrecompiledManifestRejected;
        internal const string Hed7103 = Data.HeddleDiagnosticIds.PrecompiledKeyCaseMismatch;
        internal const string Hed7104 = Data.HeddleDiagnosticIds.PrecompiledRegisteredNameUnavailable;

        private sealed class Snapshot
        {
            public Snapshot(Dictionary<string, PrecompiledTemplateInfo> byKey,
                Dictionary<string, string> keyOwner,
                Dictionary<string, string> shadow,
                HashSet<string> assemblies,
                HashSet<Assembly> instances,
                Dictionary<string, PrecompiledTemplateInfo> byName,
                Dictionary<string, string> nameOwner)
            {
                ByKey = byKey;
                KeyOwner = keyOwner;
                Shadow = shadow;
                Assemblies = assemblies;
                Instances = instances;
                ByName = byName;
                NameOwner = nameOwner;
            }

            public Dictionary<string, PrecompiledTemplateInfo> ByKey { get; }
            public Dictionary<string, string> KeyOwner { get; }
            public Dictionary<string, string> Shadow { get; }
            public HashSet<string> Assemblies { get; }

            /// <summary>The <see cref="Assembly"/> instances registered so far, for idempotence. Identity,
            /// not simple name: two assemblies sharing a simple name (versions, plugin load contexts) are
            /// two registrations, and their duplicate keys are diagnosed instead of the second one being
            /// dropped silently. <see cref="Assemblies"/> keeps the names for diagnostics.</summary>
            public HashSet<Assembly> Instances { get; }

            /// <summary>The registered-name index — a <b>second</b> index rather than extra rows in
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
            new HashSet<Assembly>(),
            new Dictionary<string, PrecompiledTemplateInfo>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));

        /// <summary>Per-request/registration fallback and diagnostic callback (HED71xx). Invoked outside locks.</summary>
        public static Action<PrecompiledFallbackEvent> OnFallback { get; set; }

        /// <summary>Integration-supplied binding matcher; null = AQN-sans-version default.</summary>
        public static Func<PrecompiledExtensionBinding, Type, bool> BindingResolver { get; set; }

        private static volatile TemplateOptions _defaultOptions;

        /// <summary>The process-wide options typed entry points render under and validate against; null means
        /// engine defaults. Assign before the first typed call — the same instance a host passes to ValidateAll.
        /// A typed entry renders its item's baked OutputProfile whatever this instance says.</summary>
        public static TemplateOptions DefaultOptions
        {
            get => _defaultOptions;
            set => _defaultOptions = value;
        }

        /// <summary>Binds a typed entry: registers the assembly, resolves the key, runs the gauntlet (model-type step
        /// against modelType; options step on ExpressionMode and TrimDirectiveLines only) under DefaultOptions,
        /// materializes, and returns a precompiled-adapter template rendering the item's baked profile.
        /// Throws PrecompiledMismatchException on a gauntlet failure and InvalidOperationException when the
        /// assembly's artifact has no such key. Thread-safe.</summary>
        public static HeddleTemplate BindTyped(Assembly assembly, string key,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type modelType)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            var effectiveModel = modelType ?? typeof(object);
            var defaults = DefaultOptions ?? new TemplateOptions();
            lock (RegistrationLock)
            {
                Register(assembly);
                if (!TryGet(key, out var entry) || entry == null)
                    throw new InvalidOperationException("Assembly '" + assembly.FullName +
                        "' contains no precompiled template with key '" + key + "'.");
                var failure = PrecompiledGauntlet.ValidateTyped(entry, defaults, BindingResolver,
                    effectiveModel);
                if (failure != null)
                    throw new PrecompiledMismatchException(entry.Key, failure.Value.Reason,
                        failure.Value.Detail);
                // Materialize under the baked profile: the strategy renders the item's own bytes,
                // so each profile memoizes its own materialization off the same entry.
                var effective = new TemplateOptions(defaults);
                effective.OutputProfile = entry.OptionsFingerprint.Profile;
                var strategy = entry.GetStrategy(effective);
                if (strategy == null)
                {
                    if (entry.TryGetRequestFault(effective, out var reason, out var detail))
                        throw new PrecompiledMismatchException(entry.Key, reason, detail);
                    throw new InvalidOperationException("Template '" + entry.Key +
                        "' failed to materialize without a recorded fault.");
                }

                return new HeddleTemplate(strategy, defaults.Encoder, defaults.RenderBudget, effective,
                    entry.ModelType);
            }
        }

        /// <summary>All registered entries (including fallback-marker entries). Snapshot; safe to enumerate.</summary>
        public static IReadOnlyCollection<PrecompiledTemplateInfo> Entries =>
            Volatile.Read(ref _snapshot).ByKey.Values.ToArray();

        /// <summary>Reads the assembly's <see cref="HeddleCompiledTemplatesAttribute"/>, runs the schema/engine gate,
        /// instantiates the manifest once, and adds its entries transactionally. Idempotent per assembly;
        /// thread-safe; repeatable — supports reconfiguration after the one-shot assembly-load gate.</summary>
        public static void Register(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var attribute = assembly.GetCustomAttribute<HeddleCompiledTemplatesAttribute>();
            if (attribute == null)
                return;

            var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? "<unknown assembly>";

            // The v3 cutover: a marker below the compiled-form schema is a 2.x manifest. It is refused
            // before ManifestType is touched — no member of this engine can read, adapt or bridge one.
            if (attribute.SchemaVersion < PrecompiledSchema.CompiledFormSchemaVersion)
                throw new PrecompiledRegistrationException(assemblyName, attribute.SchemaVersion);

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

            List<PrecompiledFallbackEvent> lostNames = null;

            lock (RegistrationLock)
            {
                var current = _snapshot;
                if (current.Instances.Contains(assembly))
                    return; // idempotent per assembly instance

                // A supported schema opens the artifact the marker names. Hand-written manifests went
                // with the 2.x generator: a marker that names anything else is rejected whole.
                IReadOnlyList<PrecompiledTemplateInfo> templates;
                var manifestType = attribute.ManifestType;
                if (manifestType != null && typeof(IHeddleCompiledArtifact).IsAssignableFrom(manifestType))
                {
                    try
                    {
                        templates = LoadCompiledRows(assembly, manifestType);
                    }
                    catch (InvalidDataException malformed)
                    {
                        // A malformed container is a registration fault naming the assembly, never a
                        // reader exception escaping the registry.
                        throw new PrecompiledRegistrationException(assemblyName, malformed);
                    }
                }
                else
                    throw new PrecompiledRegistrationException(assemblyName, attribute.SchemaVersion);

                var byKey = new Dictionary<string, PrecompiledTemplateInfo>(current.ByKey, StringComparer.Ordinal);
                var keyOwner = new Dictionary<string, string>(current.KeyOwner, StringComparer.Ordinal);
                var shadow = new Dictionary<string, string>(current.Shadow, StringComparer.OrdinalIgnoreCase);
                var byName = new Dictionary<string, PrecompiledTemplateInfo>(current.ByName, StringComparer.Ordinal);
                var nameOwner = new Dictionary<string, string>(current.NameOwner, StringComparer.Ordinal);

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

                foreach (var template in templates)
                {
                    if (string.IsNullOrEmpty(template.RegisteredName))
                        continue;

                    var key = TemplateKey.Normalize(template.Key);

                    // A name the shared key rule refuses. The build cannot emit one — a malformed `Name` is
                    // HED7004 at build time and never reaches a manifest — so this arm is reached only by a manifest
                    // no build tier vetted, which is exactly the population that most needs telling. It used to `continue`
                    // in silence, the one wholly silent drop in registration; it now reports through the same HED7104
                    // channel as the two collision arms, because from the host's side the outcome is the same: a name
                    // it expected to resolve does not, and the template is still reachable by its key.
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
                    // because a broken addition costs only the addition, not the template's registration.
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
                var instances = new HashSet<Assembly>(current.Instances) { assembly };
                Volatile.Write(ref _snapshot,
                    new Snapshot(byKey, keyOwner, shadow, assemblies, instances, byName, nameOwner));
            }

            if (lostNames != null)
            {
                foreach (var evt in lostNames)
                    RaiseFallback(evt);
            }
        }

        /// <summary>Opens the artifact a supported marker names, decodes its sections <b>once</b> into one
        /// loader-constructed <see cref="PrecompiledTemplateInfo"/> per template row, and shares that decoded
        /// graph across the rows for materialization. Rows carry no strategy yet: <see cref="Entries"/> reports
        /// them before any render, and <see cref="PrecompiledTemplateInfo.Strategy"/> materializes on first
        /// read. A structurally defective image surfaces from <see cref="Register"/> as
        /// <see cref="PrecompiledRegistrationException"/> naming the assembly.</summary>
        private static IReadOnlyList<PrecompiledTemplateInfo> LoadCompiledRows(Assembly assembly,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type artifactType)
        {
            var artifactInstance = (IHeddleCompiledArtifact)Activator.CreateInstance(artifactType);
            byte[] image;
            using (var stream = artifactInstance.OpenArtifact())
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        "IHeddleCompiledArtifact.OpenArtifact() returned null; each call must return a new " +
                        "readable stream positioned at the start.");
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    image = buffer.ToArray();
                }
            }

            var loaded = new LoadedArtifact(CompiledFormReader.Read(image));
            var templates = loaded.Artifact.Templates;
            var rows = new List<PrecompiledTemplateInfo>(templates.Count);
            for (int i = 0; i < templates.Count; i++)
            {
                // An import-only row (Precompile="false") carries text for @<< replay and no entry.
                if (templates[i].IsImportOnly)
                    continue;
                rows.Add(new PrecompiledTemplateInfo(assembly, loaded, i,
                    artifactInstance as IPrecompiledSiteTable));
            }
            return rows;
        }

        /// <summary>
        /// <para>Normalizes <paramref name="key"/> then performs an ordinal lookup — <b>keys first, registered names
        /// second</b>. A case-only miss fires the shadow-index
        /// <see cref="PrecompiledFallbackReason.CaseMismatch"/> callback (HED7103) and returns false.</para>
        /// <para>The parameter is still called <c>key</c> because that is what it is for every caller that has one; a
        /// registered name is an additional spelling of the same lookup, not a second lookup. The order is the
        /// decision, not an implementation detail: a spelling that names one template's key and another's registered
        /// name resolves to the <b>key</b> owner, always, and independently of the order the two assemblies
        /// registered in. A name is an addition, and an addition that displaced an existing spelling would be an
        /// override rather than an addition; the build tier's import map resolves the same way round, so neither tier
        /// can disagree with the other about what a spelling means.</para>
        /// <para>The name index is consulted only after the key index misses <em>and</em> is guaranteed disjoint from
        /// it by <see cref="Register"/>, so the ordering here is belt-and-braces rather than the only guard — a
        /// shadowed name cannot be in the index to be found.</para>
        /// </summary>
        public static bool TryGet(string key, out PrecompiledTemplateInfo entry)
        {
            entry = null;
            if (!TemplateKey.TryNormalize(key, out var normalized))
                return false;
            return TryGetNormalized(normalized, out entry);
        }

        /// <summary>The lookup for a caller that already holds the normalized spelling. Normalization splits and
        /// rejoins the path, so a caller that just produced the key does not pay for it a second time.</summary>
        internal static bool TryGetNormalized(string normalized, out PrecompiledTemplateInfo entry)
        {
            entry = null;
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

        /// <summary>Runs the per-request validation gauntlet for a resolved entry. Returns the first
        /// failure as a <see cref="PrecompiledFallbackEvent"/> or <c>null</c> on success. Exposed so integration/host
        /// code can validate coverage; the resolver adapter calls this before rendering through
        /// <see cref="PrecompiledTemplateInfo.Strategy"/>.
        /// <para>The model-type step is skipped, as it is in <see cref="ValidateAll"/> and for the same reason: a
        /// request's model type is not something <see cref="TemplateOptions"/> carries.</para></summary>
        public static PrecompiledFallbackEvent? Validate(PrecompiledTemplateInfo entry, TemplateOptions options)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            return PrecompiledGauntlet.Validate(entry, options, BindingResolver);
        }

        /// <summary>
        /// <para>The aggregate <b>post-configuration</b> validation pass: runs the same gauntlet
        /// <see cref="Validate"/> runs over <b>every</b> registered entry and returns all failures together, before
        /// any render. Call it once the host has finished configuring — assemblies registered, extensions bound,
        /// functions registered — and log or fail the startup on the report.</para>
        /// <para>It exists because a host that renders only through typed entry points never reaches the
        /// per-request gate: <see cref="BindTyped"/> runs the gauntlet once at bind time and throws rather
        /// than degrading, so a startup report is how a host learns an entry it has not bound yet would fail.
        /// The gauntlet is otherwise reached through <see cref="TryResolve"/>, i.e. by dynamic call sites.</para>
        /// <para><b>It changes nothing per request.</b> The gauntlet still runs exactly where it ran before; this
        /// is an additional check the host asks for, not a moved one. It is a report and not a gate: it never
        /// raises <see cref="OnFallback"/> (nothing degraded — no render happened) and it does not throw under
        /// <see cref="PrecompiledMismatchPolicy.Strict"/>, which polices requests. Deciding what a failure costs is
        /// the caller's.</para>
        /// <para><b>The answer is scoped to <paramref name="options"/></b> and the report says so — see
        /// <see cref="PrecompiledValidationReport"/>. Four gauntlet inputs are per-request, so one pass cannot
        /// speak for a host that renders under several shapes; such a host calls this once per shape.</para>
        /// <para>The gauntlet's model-type step is <b>not</b> among them and is skipped here: it judges the request's
        /// <c>CompileContext</c> model type, which options do not carry and a pre-render pass does not have. An entry
        /// with <see cref="PrecompiledTemplateInfo.ModelTypeIsAmbient"/> set can therefore still fall back at a
        /// request this report passed.</para>
        /// </summary>
        /// <param name="options">The options shape to validate against. Required: a verdict with no options to
        /// scope it would be unreadable, so there is no parameterless form.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
        public static PrecompiledValidationReport ValidateAll(TemplateOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

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

        /// <summary>Resolves a precompiled entry for a request: normalized lookup, then the
        /// per-request gauntlet under the request's <see cref="TemplateOptions"/>, then materialization. On a
        /// pass returns true with the entry, its strategy materialized; on a registry miss returns false (the
        /// dynamic path proceeds untouched). On a gauntlet failure or a materialization fault the
        /// <see cref="OnFallback"/> callback fires with the recorded reason; under
        /// <see cref="PrecompiledMismatchPolicy.Strict"/> it then throws <see cref="PrecompiledMismatchException"/>,
        /// under <c>Fallback</c> it returns false.</summary>
        public static bool TryResolve(string key, TemplateOptions options, out PrecompiledTemplateInfo entry) =>
            TryResolve(key, options, null, out entry);

        /// <summary>Resolves a precompiled entry for a request that also knows the model type it would compile
        /// the template against — the requesting <see cref="Runtime.CompileContext.RootScopeType"/>. Everything
        /// <see cref="TryResolve(string,TemplateOptions,out PrecompiledTemplateInfo)"/> does, plus the gauntlet's
        /// model-type step, which is the only check that can tell a template the host typed from one the build
        /// assumed was untyped.</summary>
        /// <param name="requestModelType">The request's model type, or <c>null</c> to make no claim — which leaves
        /// the model-type step skipped, exactly as the shorter overload leaves it.</param>
        public static bool TryResolve(string key, TemplateOptions options, Type requestModelType,
            out PrecompiledTemplateInfo entry)
        {
            entry = null;
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (!TemplateKey.TryNormalize(key, out var normalized))
                return false;
            return TryResolveNormalized(normalized, options, requestModelType, out entry);
        }

        /// <summary>Whether a normalized spelling could resolve at all — a membership test over the published
        /// snapshot and nothing else. A resolver probing several candidate locations asks this before it builds
        /// the request's <see cref="TemplateOptions"/>, so a location the registry never heard of costs a
        /// dictionary probe instead of an options instance. The shadow index is consulted too, so a case-only
        /// miss still reaches <see cref="TryResolveNormalized"/> and still reports <c>HED7103</c>.</summary>
        internal static bool CouldResolve(string normalized)
        {
            if (normalized == null)
                return false;
            var snapshot = Volatile.Read(ref _snapshot);
            return snapshot.ByKey.ContainsKey(normalized) || snapshot.ByName.ContainsKey(normalized) ||
                snapshot.Shadow.ContainsKey(normalized);
        }

        /// <summary>The resolve for a caller that already holds the normalized spelling.</summary>
        internal static bool TryResolveNormalized(string normalized, TemplateOptions options,
            Type requestModelType, out PrecompiledTemplateInfo entry)
        {
            entry = null;
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (!TryGetNormalized(normalized, out var found))
                return false;

            var failure = PrecompiledGauntlet.Validate(found, options, BindingResolver, requestModelType);
            if (failure == null)
            {
                // A gauntlet pass is not a render: materialize now, so a memoized materialization fault
                // reports through the same channel and policy as a gauntlet failure instead of surfacing as
                // a null strategy at the adapter.
                if (found.GetStrategy(options) == null)
                {
                    PrecompiledFallbackReason reason;
                    string detail;
                    if (!found.TryGetRequestFault(options, out reason, out detail))
                    {
                        reason = PrecompiledFallbackReason.ExtensionInitCompileError;
                        detail = "Template '" + found.Key + "' materialized no strategy and recorded no fault.";
                    }
                    RaiseFallback(PrecompiledFallbackEvent.ForTemplate(found.Key, reason, detail,
                        PrecompiledGauntlet.Hed7101));
                    if (options.PrecompiledMismatchPolicy == PrecompiledMismatchPolicy.Strict)
                        throw new PrecompiledMismatchException(found.Key, reason, detail);
                    return false;
                }
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
