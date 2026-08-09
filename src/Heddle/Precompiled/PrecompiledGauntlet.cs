using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;

namespace Heddle.Precompiled
{
    /// <summary>The per-request validation gauntlet. Runs the pinned ordered checks against a
    /// resolved <see cref="PrecompiledTemplateInfo"/> and the request's effective <see cref="TemplateOptions"/>,
    /// returning the first failure as a <see cref="PrecompiledFallbackEvent"/> (with the pinned detail string) or
    /// <c>null</c> when every check passes. Pure apart from the optional staleness step's file reads.</summary>
    internal static class PrecompiledGauntlet
    {
        internal const string Hed7101 = Data.HeddleDiagnosticIds.PrecompiledGauntletFallback;

        internal static PrecompiledFallbackEvent? Validate(PrecompiledTemplateInfo entry, TemplateOptions options,
            Func<PrecompiledExtensionBinding, Type, bool> bindingResolver)
        {
            if (!entry.IsPrecompiled)
            {
                var name = entry.FunctionBindings.FirstOrDefault(r => r.TargetTypeName == null).Name ?? "?";
                return Fail(entry.Key, PrecompiledFallbackReason.UnsupportedFunction,
                    $"Function '{name}': not precompiled (no default or exported binding; build warning HED7014)");
            }

            var optionsFailure = CheckOptions(entry, options);
            if (optionsFailure != null)
                return optionsFailure;

            var extensionFailure = CheckExtensions(entry, bindingResolver);
            if (extensionFailure != null)
                return extensionFailure;

            var functionFailure = CheckFunctions(entry, options);
            if (functionFailure != null)
                return functionFailure;

            if (options.EnableFileChangeCheck)
            {
                var staleFailure = CheckStaleness(entry, options);
                if (staleFailure != null)
                    return staleFailure;
            }

            return null;
        }

        private static PrecompiledFallbackEvent? CheckOptions(PrecompiledTemplateInfo entry, TemplateOptions options)
        {
            var fp = entry.OptionsFingerprint;
            if (fp.Profile != options.OutputProfile)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch,
                    $"OutputProfile: manifest={fp.Profile} request={options.OutputProfile}");
            if (fp.ExpressionMode != options.ExpressionMode)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch,
                    $"ExpressionMode: manifest={fp.ExpressionMode} request={options.ExpressionMode}");
            if (fp.TrimDirectiveLines != options.TrimDirectiveLines)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch,
                    $"TrimDirectiveLines: manifest={Lower(fp.TrimDirectiveLines)} request={Lower(options.TrimDirectiveLines)}");
            return null;
        }

        private static PrecompiledFallbackEvent? CheckExtensions(PrecompiledTemplateInfo entry,
            Func<PrecompiledExtensionBinding, Type, bool> bindingResolver)
        {
            foreach (var binding in entry.ExtensionBindings)
            {
                if (!TemplateFactory.TryGetExtensionType(binding.Name, out var liveType))
                    return Fail(entry.Key, PrecompiledFallbackReason.ExtensionBindingMismatch,
                        $"Extension '{binding.Name}': manifest={binding.ExtensionTypeName} live=<unresolved>");

                var matches = bindingResolver != null
                    ? bindingResolver(binding, liveType)
                    : DefaultBindingMatch(binding, liveType);
                if (!matches)
                    return Fail(entry.Key, PrecompiledFallbackReason.ExtensionBindingMismatch,
                        $"Extension '{binding.Name}': manifest={binding.ExtensionTypeName} live={AqnSansVersion(liveType)}");

                // Identity match (same AQN) does not guarantee [Prop] slots remain unchanged; extension packages
                // can gain/reorder slots while keeping AQN, so validate the fingerprint.
                if (!string.IsNullOrEmpty(binding.PropLayoutFingerprint))
                {
                    var liveFingerprint = PropLayout.Fingerprint(liveType);
                    if (!string.Equals(binding.PropLayoutFingerprint, liveFingerprint, StringComparison.Ordinal))
                        return Fail(entry.Key, PrecompiledFallbackReason.ExtensionBindingMismatch,
                            $"Extension '{binding.Name}': prop layout manifest={binding.PropLayoutFingerprint} " +
                            $"live={liveFingerprint ?? "<none>"}");
                }
            }

            return null;
        }

        internal static bool DefaultBindingMatch(PrecompiledExtensionBinding binding, Type liveType)
        {
            return liveType != null &&
                   string.Equals(binding.ExtensionTypeName, AqnSansVersion(liveType), StringComparison.Ordinal);
        }

        private static PrecompiledFallbackEvent? CheckFunctions(PrecompiledTemplateInfo entry, TemplateOptions options)
        {
            var rows = entry.FunctionBindings;
            if (rows.Count == 0)
                return null;

            // A request with no registry compiles against the frozen default set — and so does a late-bound site
            // (PrecompiledRuntime.EffectiveFunctions), which is why a null-target row still has to be checked
            // where the all-built-in shortcut used to answer for the whole entry.
            var registry = options.Functions ?? FunctionRegistry.Default;
            var allBuiltIn = rows.All(r => r.TargetTypeName == DefaultFunctionTable.ShimTargetTypeName);

            if (options.Functions == null)
            {
                if (allBuiltIn)
                    return null;
                var exportRow = rows.FirstOrDefault(r =>
                    r.TargetTypeName != DefaultFunctionTable.ShimTargetTypeName && r.TargetTypeName != null);
                if (exportRow.TargetTypeName != null)
                    return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                        $"Function '{exportRow.Name}': manifest={exportRow.TargetTypeName} live=<missing>");
            }

            var names = new List<string>();
            foreach (var r in rows)
                if (!names.Contains(r.Name))
                    names.Add(r.Name);

            foreach (var name in names)
            {
                var recordedForName = rows.Where(r => r.Name == name && r.TargetTypeName != null).ToList();
                if (recordedForName.Count == 0)
                {
                    // A null-target row on a PRECOMPILED entry is a late-bound call site: the build knew the call
                    // shape but not the target, and PrecompiledFunctionSite resolves it at first render through
                    // the engine's own ranker. Two render-time configurations are outside what that site can
                    // reproduce, and both are the dynamic tier's to answer, so they fall back here rather than at
                    // a render: a name that is a registered EXTENSION (whose render protocol is not a value), and
                    // a name registered nowhere (whose engine answer is a compile error, and a compile error the
                    // dynamic tier raises is the parity contract).
                    if (TemplateFactory.Exists(name))
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest=<late-bound> live=<extension>");
                    if (!registry.Contains(name))
                        return Fail(entry.Key, PrecompiledFallbackReason.UnsupportedFunction,
                            $"Function '{name}': manifest=<late-bound> live=<unregistered>");
                    continue;
                }
                var recordedTargets = new HashSet<string>(recordedForName.Select(r => r.TargetTypeName),
                    StringComparer.Ordinal);
                var manifestTarget = recordedForName[0].TargetTypeName;
                var live = registry.GetOverloads(name);

                foreach (var reg in live)
                {
                    if (reg.Method == null) // a delegate registration under a bound name
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest={manifestTarget} live=<delegate>");
                    var liveAqn = AqnSansVersion(reg.Method.DeclaringType);
                    if (!recordedTargets.Contains(liveAqn))
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest={manifestTarget} live={liveAqn}");
                }

                foreach (var row in recordedForName)
                {
                    var liveCount = live.Count(reg => reg.Method != null &&
                                                      string.Equals(AqnSansVersion(reg.Method.DeclaringType),
                                                          row.TargetTypeName, StringComparison.Ordinal));
                    if (liveCount > row.OverloadCount)
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest={row.TargetTypeName} live=<overloads added>");
                    if (liveCount < row.OverloadCount)
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest={row.TargetTypeName} live=<missing>");
                }
            }

            return null;
        }

        private static PrecompiledFallbackEvent? CheckStaleness(PrecompiledTemplateInfo entry, TemplateOptions options)
        {
            var rootPath = options.RootPath ?? string.Empty;
            var contentPath = TemplateKey.ToPath(entry.Key, rootPath);
            if (!File.Exists(contentPath))
                return Fail(entry.Key, PrecompiledFallbackReason.StaleContent, $"Content: '{entry.Key}' missing");
            if (!string.Equals(HashFile(contentPath), entry.ContentHash, StringComparison.Ordinal))
                return Fail(entry.Key, PrecompiledFallbackReason.StaleContent, $"Content: '{entry.Key}' hash mismatch");

            foreach (var import in entry.Imports)
            {
                var importPath = TemplateKey.ToPath(import.Key, rootPath);
                if (!File.Exists(importPath))
                    return Fail(entry.Key, PrecompiledFallbackReason.StaleImport, $"Import: '{import.Key}' missing");
                if (!string.Equals(HashFile(importPath), import.ContentHash, StringComparison.Ordinal))
                    return Fail(entry.Key, PrecompiledFallbackReason.StaleImport,
                        $"Import: '{import.Key}' hash mismatch");
            }

            return null;
        }

        /// <summary>Decode-then-hash: the file is read with BOM detection (a BOM is honored and
        /// stripped; no BOM means UTF-8) and its <b>decoded text</b> is hashed through the shared
        /// <see cref="ContentHash"/> rule — the same input the generator hashed at build time. Hashing the raw byte
        /// stream here is what made every BOM'd/UTF-16 template permanently <c>StaleContent</c>.</summary>
        internal static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
                return ContentHash.HashText(reader.ReadToEnd());
        }

        /// <summary>The manifest identity string, produced by the shared <see cref="AqnFormatter"/>
        /// through its reflection adapter — the same rule the generator's Roslyn adapter applies, so a nested or
        /// generic container can no longer spell its identity two different ways.</summary>
        internal static string AqnSansVersion(Type type) => ReflectionTypeIdentity.AqnSansVersion(type);

        private static string Lower(bool value) => value ? "true" : "false";

        private static PrecompiledFallbackEvent Fail(string key, PrecompiledFallbackReason reason, string detail)
        {
            return PrecompiledFallbackEvent.ForTemplate(key, reason, detail, Hed7101);
        }
    }
}
