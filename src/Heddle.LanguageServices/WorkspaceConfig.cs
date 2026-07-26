using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Heddle.Data;
using Heddle.Precompiled;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// <para>Reads the workspace <c>.heddle-lsp.json</c> (phase 6 D18) and produces a
    /// <see cref="HeddleLanguageServiceOptions"/>. Relative paths resolve against the workspace root. A present
    /// file wins field-by-field over any client-supplied defaults (applied by the caller by merging first).</para>
    /// <para><b>Full options parity (generator plan phase 6 D10/WI9, ruling Q6.2).</b> The editor follows the same
    /// configuration surface the runtime permits: every analysis-applicable <see cref="TemplateOptions"/> option has
    /// a key here, with its <b>name</b> the camelCase of the option's own property name and its <b>default</b> taken
    /// from <see cref="HeddleBuildOptions"/> — the one names/defaults table the MSBuild props and
    /// <see cref="TemplateOptions"/>' own constructor are pinned against. Options with no analysis meaning are named
    /// exclusions in <c>WorkspaceOptionParityTests</c>, not silent omissions.</para>
    /// <para><b>Token parsing is shared, reactions are not.</b> Profile and mode values parse through
    /// <see cref="OutputProfileRules"/>, the same functions the engine and the build tier use, so the three hosts
    /// cannot disagree on which spellings exist. What each host <i>does</i> with a bad value legitimately differs:
    /// a template author's typo is a compile error (<c>HED2001</c>), a build property's is a build diagnostic
    /// (<c>HED7009</c>), and a workspace-config typo must never break editing — so it keeps the default and adds a
    /// line to <see cref="HeddleLanguageServiceOptions.ConfigurationMessages"/>, which the server logs. No
    /// <c>HED</c> id is minted for it; <c>HED6xxx</c> stays reserved-unclaimed per the registry.</para>
    /// </summary>
    internal static class WorkspaceConfig
    {
        internal const string FileName = ".heddle-lsp.json";

        /// <summary>The LSP-specific key with no <see cref="TemplateOptions"/> twin: the config-file form of
        /// <see cref="TemplateOptions.Functions"/> and of extension exports, which are object-valued and have no
        /// literal JSON representation — the one-shot export scan reads them out of these assemblies instead.</summary>
        internal const string AssembliesKey = "assemblies";

        internal static readonly string RootPathKey = ConfigKey(nameof(TemplateOptions.RootPath));
        internal static readonly string OutputProfileKey = ConfigKey(nameof(TemplateOptions.OutputProfile));
        internal static readonly string ExpressionModeKey = ConfigKey(nameof(TemplateOptions.ExpressionMode));
        internal static readonly string FileNamePostfixKey = ConfigKey(nameof(TemplateOptions.FileNamePostfix));
        internal static readonly string TrimDirectiveLinesKey = ConfigKey(nameof(TemplateOptions.TrimDirectiveLines));
        internal static readonly string MaxRecursionCountKey = ConfigKey(nameof(TemplateOptions.MaxRecursionCount));

        /// <summary>The key naming rule, stated once: the option's own property name, camelCased. It agrees with
        /// the <c>Heddle</c>-stripped, camelCased MSBuild property names in
        /// <see cref="HeddleBuildOptions"/> for every option both surfaces carry, which
        /// <c>WorkspaceOptionParityTests</c> asserts rather than assumes.</summary>
        internal static string ConfigKey(string optionName) =>
            string.IsNullOrEmpty(optionName)
                ? optionName
                : char.ToLowerInvariant(optionName[0]) + optionName.Substring(1);

        internal static HeddleLanguageServiceOptions Read(string workspaceRoot, string json)
        {
            var assemblies = new List<string>();
            var messages = new List<string>();
            string rootPath = workspaceRoot;
            var outputProfile = HeddleBuildOptions.DefaultOutputProfile;
            var expressionMode = HeddleBuildOptions.DefaultExpressionMode;
            string fileNamePostfix = string.Empty;
            var trimDirectiveLines = HeddleBuildOptions.DefaultTrimDirectiveLines;
            var maxRecursionCount = HeddleBuildOptions.DefaultMaxRecursionCount;

            if (!string.IsNullOrWhiteSpace(json))
            {
                using var document = JsonDocument.Parse(json);
                var element = document.RootElement;

                if (element.TryGetProperty(AssembliesKey, out var asm))
                {
                    if (asm.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in asm.EnumerateArray())
                        {
                            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                            if (!string.IsNullOrEmpty(value))
                                assemblies.Add(Resolve(workspaceRoot, value));
                        }
                    }
                    else
                    {
                        messages.Add(WrongKind(AssembliesKey, "an array of strings", asm.ValueKind));
                    }
                }

                if (TryReadString(element, RootPathKey, messages, out var rawRoot))
                    rootPath = Resolve(workspaceRoot, rawRoot);

                if (TryReadString(element, OutputProfileKey, messages, out var rawProfile))
                {
                    if (OutputProfileRules.TryParseProfile(rawProfile, out var parsed))
                        outputProfile = parsed;
                    else
                        messages.Add(Unknown(OutputProfileKey, rawProfile,
                            OutputProfileRules.ValidProfileValues, outputProfile));
                }

                if (TryReadString(element, ExpressionModeKey, messages, out var rawMode))
                {
                    if (OutputProfileRules.TryParseExpressionMode(rawMode, out var parsed))
                        expressionMode = parsed;
                    else
                        messages.Add(Unknown(ExpressionModeKey, rawMode,
                            HeddleBuildOptions.ExpectedValues<ExpressionMode>(), expressionMode));
                }

                if (TryReadString(element, FileNamePostfixKey, messages, out var rawPostfix))
                    fileNamePostfix = rawPostfix ?? string.Empty;

                trimDirectiveLines = ReadBool(element, TrimDirectiveLinesKey, trimDirectiveLines, messages);
                maxRecursionCount = ReadPositiveInt(element, MaxRecursionCountKey, maxRecursionCount, messages);
            }

            return new HeddleLanguageServiceOptions
            {
                AssemblyPaths = assemblies,
                RootPath = rootPath,
                OutputProfile = outputProfile,
                ExpressionMode = expressionMode,
                FileNamePostfix = fileNamePostfix,
                TrimDirectiveLines = trimDirectiveLines,
                MaxRecursionCount = maxRecursionCount,
                ConfigurationMessages = messages
            };
        }

        internal static HeddleLanguageServiceOptions ReadFile(string workspaceRoot)
        {
            var path = string.IsNullOrEmpty(workspaceRoot) ? FileName : Path.Combine(workspaceRoot, FileName);
            var json = File.Exists(path) ? File.ReadAllText(path) : null;
            return Read(workspaceRoot, json);
        }

        /// <summary><c>true</c> when the key is present and is a JSON string; a present key of the wrong kind is
        /// reported and treated as absent.</summary>
        private static bool TryReadString(JsonElement element, string key, ICollection<string> messages,
            out string value)
        {
            value = null;
            if (!element.TryGetProperty(key, out var property))
                return false;
            if (property.ValueKind != JsonValueKind.String)
            {
                messages.Add(WrongKind(key, "a string", property.ValueKind));
                return false;
            }

            value = property.GetString();
            return true;
        }

        /// <summary>A bool option: JSON <c>true</c>/<c>false</c>, or a string parsed by the shared
        /// <see cref="HeddleBuildOptions.TryReadBool"/> so the editor accepts exactly the spellings the build
        /// property does.</summary>
        private static bool ReadBool(JsonElement element, string key, bool fallback, ICollection<string> messages)
        {
            if (!element.TryGetProperty(key, out var property))
                return fallback;
            switch (property.ValueKind)
            {
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.String:
                    var raw = property.GetString();
                    if (!string.IsNullOrEmpty(raw) && HeddleBuildOptions.TryReadBool(raw, fallback, out var parsed))
                        return parsed;
                    messages.Add(Unknown(key, raw, HeddleBuildOptions.ExpectedBool, fallback));
                    return fallback;
                default:
                    messages.Add(WrongKind(key, HeddleBuildOptions.ExpectedBool, property.ValueKind));
                    return fallback;
            }
        }

        /// <summary>A positive-int option: a JSON number, or a string parsed by the shared
        /// <see cref="HeddleBuildOptions.TryReadPositiveInt"/>.</summary>
        private static int ReadPositiveInt(JsonElement element, string key, int fallback,
            ICollection<string> messages)
        {
            if (!element.TryGetProperty(key, out var property))
                return fallback;
            switch (property.ValueKind)
            {
                case JsonValueKind.Number:
                    if (property.TryGetInt32(out var number) && number > 0)
                        return number;
                    messages.Add(Unknown(key, property.GetRawText(), HeddleBuildOptions.ExpectedPositiveInt,
                        fallback));
                    return fallback;
                case JsonValueKind.String:
                    var raw = property.GetString();
                    if (!string.IsNullOrEmpty(raw) &&
                        HeddleBuildOptions.TryReadPositiveInt(raw, fallback, out var parsed))
                        return parsed;
                    messages.Add(Unknown(key, raw, HeddleBuildOptions.ExpectedPositiveInt, fallback));
                    return fallback;
                default:
                    messages.Add(WrongKind(key, HeddleBuildOptions.ExpectedPositiveInt, property.ValueKind));
                    return fallback;
            }
        }

        private static string Unknown(string key, string value, string accepted, object kept) =>
            string.Format(CultureInfo.InvariantCulture,
                "{0}: '{1}' has an unrecognized value '{2}'. Accepted: {3}. Keeping the default '{4}'.",
                FileName, key, value, accepted, kept);

        private static string WrongKind(string key, string accepted, JsonValueKind kind) =>
            string.Format(CultureInfo.InvariantCulture,
                "{0}: '{1}' must be {2} but is {3}. Ignoring it.",
                FileName, key, accepted, kind);

        private static string Resolve(string root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (Path.IsPathRooted(path) || string.IsNullOrEmpty(root))
                return path;
            return Path.Combine(root, path);
        }
    }
}
