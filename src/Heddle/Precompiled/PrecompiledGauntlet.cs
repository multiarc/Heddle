using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;

namespace Heddle.Precompiled
{
    /// <summary>The per-request validation gauntlet (phase 7 D7/D8/D9/D21). Runs the pinned ordered checks against a
    /// resolved <see cref="PrecompiledTemplateInfo"/> and the request's effective <see cref="TemplateOptions"/>,
    /// returning the first failure as a <see cref="PrecompiledFallbackEvent"/> (with the pinned detail string) or
    /// <c>null</c> when every check passes. Pure apart from the optional staleness step's file reads.</summary>
    internal static class PrecompiledGauntlet
    {
        internal const string Hed7101 = "HED7101";

        internal static PrecompiledFallbackEvent? Validate(PrecompiledTemplateInfo entry, TemplateOptions options,
            Func<PrecompiledExtensionBinding, Type, bool> bindingResolver)
        {
            // Step 0 — marker short-circuit (D21).
            if (!entry.IsPrecompiled)
            {
                var name = entry.FunctionBindings.FirstOrDefault(r => r.TargetTypeName == null).Name ?? "?";
                return Fail(entry.Key, PrecompiledFallbackReason.UnsupportedFunction,
                    $"Function '{name}': not precompiled (no default or exported binding; build warning HED7014)");
            }

            // Step 1 — options fingerprint.
            var optionsFailure = CheckOptions(entry, options);
            if (optionsFailure != null)
                return optionsFailure;

            // Step 2 — extension bindings vs the live registry.
            var extensionFailure = CheckExtensions(entry, bindingResolver);
            if (extensionFailure != null)
                return extensionFailure;

            // Step 3 — function bindings vs the request's effective registry.
            var functionFailure = CheckFunctions(entry, options);
            if (functionFailure != null)
                return functionFailure;

            // Step 4 — staleness (only under EnableFileChangeCheck).
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

            var registry = options.Functions;
            var allBuiltIn = rows.All(r => r.TargetTypeName == DefaultFunctionTable.ShimTargetTypeName);

            if (registry == null)
            {
                // The request compiles against the frozen FunctionRegistry.Default — it cannot have diverged from
                // the default rows, but it equally cannot contain an export.
                if (allBuiltIn)
                    return null;
                var exportRow = rows.First(r => r.TargetTypeName != DefaultFunctionTable.ShimTargetTypeName &&
                                                r.TargetTypeName != null);
                return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                    $"Function '{exportRow.Name}': manifest={exportRow.TargetTypeName} live=<missing>");
            }

            // Distinct called names, in manifest (row) order.
            var names = new List<string>();
            foreach (var r in rows)
                if (!names.Contains(r.Name))
                    names.Add(r.Name);

            foreach (var name in names)
            {
                var recordedForName = rows.Where(r => r.Name == name && r.TargetTypeName != null).ToList();
                if (recordedForName.Count == 0)
                    continue;
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
            var contentPath = Path.Combine(rootPath, entry.Key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(contentPath))
                return Fail(entry.Key, PrecompiledFallbackReason.StaleContent, $"Content: '{entry.Key}' missing");
            if (!string.Equals(HashFile(contentPath), entry.ContentHash, StringComparison.Ordinal))
                return Fail(entry.Key, PrecompiledFallbackReason.StaleContent, $"Content: '{entry.Key}' hash mismatch");

            foreach (var import in entry.Imports)
            {
                var importPath = Path.Combine(rootPath, import.Key.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(importPath))
                    return Fail(entry.Key, PrecompiledFallbackReason.StaleImport, $"Import: '{import.Key}' missing");
                if (!string.Equals(HashFile(importPath), import.ContentHash, StringComparison.Ordinal))
                    return Fail(entry.Key, PrecompiledFallbackReason.StaleImport,
                        $"Import: '{import.Key}' hash mismatch");
            }

            return null;
        }

        internal static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return ToHex(sha.ComputeHash(stream));
        }

        internal static string HashBytes(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return ToHex(sha.ComputeHash(bytes));
        }

        private static string ToHex(byte[] hash)
        {
            var builder = new System.Text.StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        internal static string AqnSansVersion(Type type)
        {
            if (type == null)
                return "<unknown>";
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }

        private static string Lower(bool value) => value ? "true" : "false";

        private static PrecompiledFallbackEvent Fail(string key, PrecompiledFallbackReason reason, string detail)
        {
            return new PrecompiledFallbackEvent(key, reason, detail, Hed7101);
        }
    }
}
