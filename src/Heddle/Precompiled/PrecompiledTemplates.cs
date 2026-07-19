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
        internal const string Hed7102 = "HED7102";
        internal const string Hed7103 = "HED7103";
        // Phase 8 D7: the engine accepts schemaVersion 1 (phase 7 generator) and 2 (phase 8 generator, u8 twins +
        // WritePiece piece routing). A schema-1 assembly keeps registering and renders on all three sinks — its pieces
        // simply transcode via the sink adapters, exactly as a schema-2 template built without the u8 opt-in.
        private const int MinSupportedSchemaVersion = 1;
        private const int MaxSupportedSchemaVersion = 2;

        private sealed class Snapshot
        {
            public Snapshot(Dictionary<string, PrecompiledTemplateInfo> byKey,
                Dictionary<string, string> keyOwner,
                Dictionary<string, string> shadow,
                HashSet<string> assemblies)
            {
                ByKey = byKey;
                KeyOwner = keyOwner;
                Shadow = shadow;
                Assemblies = assemblies;
            }

            public Dictionary<string, PrecompiledTemplateInfo> ByKey { get; }
            public Dictionary<string, string> KeyOwner { get; }
            public Dictionary<string, string> Shadow { get; }
            public HashSet<string> Assemblies { get; }
        }

        private static readonly object RegistrationLock = new object();

        private static Snapshot _snapshot = new Snapshot(
            new Dictionary<string, PrecompiledTemplateInfo>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.Ordinal));

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

            var assemblyName = assembly.GetName().Name ?? assembly.FullName;

            if (attribute.SchemaVersion < MinSupportedSchemaVersion ||
                attribute.SchemaVersion > MaxSupportedSchemaVersion)
            {
                RaiseFallback(new PrecompiledFallbackEvent(assemblyName,
                    PrecompiledFallbackReason.SchemaVersionUnsupported,
                    $"SchemaVersion: manifest={attribute.SchemaVersion} supported={MinSupportedSchemaVersion}-{MaxSupportedSchemaVersion}",
                    Hed7102));
                return;
            }

            if (!IsEngineCompatible(attribute.EngineVersion, out var runtimeVersion))
            {
                RaiseFallback(new PrecompiledFallbackEvent(assemblyName,
                    PrecompiledFallbackReason.EngineVersionIncompatible,
                    $"EngineVersion: manifest={attribute.EngineVersion} runtime={runtimeVersion}", Hed7102));
                return;
            }

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

                // Stage transactionally: validate every key before publishing anything.
                foreach (var template in templates)
                {
                    var key = TemplateKey.Normalize(template.Key);
                    if (keyOwner.TryGetValue(key, out var existingOwner))
                        throw new PrecompiledRegistrationException(key, existingOwner, assemblyName);
                    byKey[key] = template;
                    keyOwner[key] = assemblyName;
                    shadow[key] = key;
                }

                var assemblies = new HashSet<string>(current.Assemblies, StringComparer.Ordinal) { assemblyName };
                Volatile.Write(ref _snapshot, new Snapshot(byKey, keyOwner, shadow, assemblies));
            }
        }

        /// <summary>Normalizes <paramref name="key"/> then performs an ordinal lookup. A case-only miss fires the
        /// shadow-index <see cref="PrecompiledFallbackReason.CaseMismatch"/> callback (HED7103) and returns false.</summary>
        public static bool TryGet(string key, out PrecompiledTemplateInfo entry)
        {
            entry = null;
            if (!TemplateKey.TryNormalize(key, out var normalized))
                return false;

            var snapshot = Volatile.Read(ref _snapshot);
            if (snapshot.ByKey.TryGetValue(normalized, out entry))
                return true;

            if (snapshot.Shadow.TryGetValue(normalized, out var actual) &&
                !string.Equals(actual, normalized, StringComparison.Ordinal))
            {
                RaiseFallback(new PrecompiledFallbackEvent(normalized, PrecompiledFallbackReason.CaseMismatch,
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
                Volatile.Write(ref _snapshot, new Snapshot(
                    new Dictionary<string, PrecompiledTemplateInfo>(StringComparer.Ordinal),
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    new HashSet<string>(StringComparer.Ordinal)));
            }
        }

        private static bool IsEngineCompatible(string manifestVersion, out Version runtimeVersion)
        {
            runtimeVersion = typeof(PrecompiledTemplates).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            if (!Version.TryParse(manifestVersion, out var parsed))
                return false;
            return parsed.Major == runtimeVersion.Major && parsed <= runtimeVersion;
        }

        private static void RaiseFallback(PrecompiledFallbackEvent evt)
        {
            OnFallback?.Invoke(evt);
        }
    }
}
