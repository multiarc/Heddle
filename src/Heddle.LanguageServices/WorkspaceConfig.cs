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
    /// Reads <c>.heddle-lsp.json</c> and produces <see cref="HeddleLanguageServiceOptions"/>. The file wins
    /// field-by-field over client defaults. Every analysis-applicable <see cref="TemplateOptions"/> option maps
    /// here with camelCased names and defaults from <see cref="HeddleBuildOptions"/> — the single table MSBuild
    /// props and <see cref="TemplateOptions"/> are pinned against. Profile/mode parse through
    /// <see cref="OutputProfileRules"/> shared with engine/build tier. Config typos keep defaults and log to
    /// <see cref="HeddleLanguageServiceOptions.ConfigurationMessages"/> (no HED diagnostic, HED6xxx reserved).
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

        /// <summary>Naming rule: option's property name, camelCased, asserted to match MSBuild by WorkspaceOptionParityTests.</summary>
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

        /// <summary><c>true</c> when key is present and is a JSON string; wrong kind is reported and treated as absent.</summary>
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

        /// <summary>Bool option: JSON true/false or string parsed by shared <see cref="HeddleBuildOptions.TryReadBool"/>.</summary>
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

        /// <summary>Positive-int option: JSON number or string parsed by shared <see cref="HeddleBuildOptions.TryReadPositiveInt"/>.</summary>
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
