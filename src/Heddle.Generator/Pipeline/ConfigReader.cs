using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Heddle.Generator.Pipeline
{
    /// <summary>Reads the compilation-wide <c>build_property.Heddle*</c> options into a <see cref="GlobalConfig"/>
    /// (phase 7 D14/D16). Unparsable enum/int values are collected as <c>HED7009</c> candidates (the generator never
    /// guesses a default from a typo).</summary>
    internal static class ConfigReader
    {
        public struct OptionError
        {
            public OptionError(string value, string property, string expected)
            {
                Value = value;
                Property = property;
                Expected = expected;
            }

            public string Value { get; }
            public string Property { get; }
            public string Expected { get; }
        }

        public static GlobalConfig Read(AnalyzerConfigOptions options, List<OptionError> errors)
        {
            var profile = ReadEnum(options, "HeddleOutputProfile", "Html",
                new[] { "Text", "Html" }, errors);
            var mode = ReadEnum(options, "HeddleExpressionMode", "Native",
                new[] { "MemberPathsOnly", "Native", "FullCSharp" }, errors);
            var trim = ReadBool(options, "HeddleTrimDirectiveLines", true, errors);
            var maxRecursion = ReadPositiveInt(options, "HeddleMaxRecursionCount", 100, errors);
            var root = ReadString(options, "HeddleTemplateRoot", "");
            var ns = ReadString(options, "HeddleGeneratedNamespace", "");
            var emitU8 = ReadBool(options, "HeddleEmitUtf8Pieces", false, errors);

            return new GlobalConfig(profile, mode, trim, maxRecursion, root, ns, emitU8);
        }

        private static string ReadString(AnalyzerConfigOptions options, string name, string fallback)
        {
            return options.TryGetValue("build_property." + name, out var value) && !string.IsNullOrEmpty(value)
                ? value
                : fallback;
        }

        private static string ReadEnum(AnalyzerConfigOptions options, string name, string fallback,
            string[] allowed, List<OptionError> errors)
        {
            if (!options.TryGetValue("build_property." + name, out var value) || string.IsNullOrEmpty(value))
                return fallback;
            foreach (var candidate in allowed)
                if (string.Equals(candidate, value, System.StringComparison.OrdinalIgnoreCase))
                    return candidate;
            errors.Add(new OptionError(value, name, string.Join("|", allowed)));
            return fallback;
        }

        private static bool ReadBool(AnalyzerConfigOptions options, string name, bool fallback,
            List<OptionError> errors)
        {
            if (!options.TryGetValue("build_property." + name, out var value) || string.IsNullOrEmpty(value))
                return fallback;
            if (bool.TryParse(value, out var parsed))
                return parsed;
            errors.Add(new OptionError(value, name, "true|false"));
            return fallback;
        }

        private static int ReadPositiveInt(AnalyzerConfigOptions options, string name, int fallback,
            List<OptionError> errors)
        {
            if (!options.TryGetValue("build_property." + name, out var value) || string.IsNullOrEmpty(value))
                return fallback;
            if (int.TryParse(value, out var parsed) && parsed > 0)
                return parsed;
            errors.Add(new OptionError(value, name, "a positive integer"));
            return fallback;
        }
    }
}
