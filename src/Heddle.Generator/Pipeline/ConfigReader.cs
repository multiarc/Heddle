using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Heddle.Generator.Pipeline
{
    /// <summary>Reads the compilation-wide <c>build_property.Heddle*</c> options into a <see cref="GlobalConfig"/>.
    /// Unparsable enum/int values are collected as <c>HED7009</c> candidates (the generator never
    /// guesses a default from a typo).
    /// <para>A thin Roslyn-side adapter — one lookup lambda over
    /// <see cref="AnalyzerConfigOptions"/> feeding <see cref="HeddleBuildOptions"/>'s shared names, defaults and
    /// parse helpers, plus the translation of their parse-failure signal into an <see cref="OptionError"/>.
    /// <c>AnalyzerConfigOptions</c> never crosses into shared code, and no allowed-value list is hand-copied from an
    /// enum any more.</para></summary>
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
            Func<string, string> lookup = name =>
                options.TryGetValue(HeddleBuildOptions.BuildPropertyPrefix + name, out var value) ? value : null;

            var profile = ReadEnum<OutputProfile>(lookup, HeddleBuildOptions.OutputProfileProperty,
                HeddleBuildOptions.DefaultOutputProfile, errors);
            var mode = ReadEnum<ExpressionMode>(lookup, HeddleBuildOptions.ExpressionModeProperty,
                HeddleBuildOptions.DefaultExpressionMode, errors);
            var trim = ReadBool(lookup, HeddleBuildOptions.TrimDirectiveLinesProperty,
                HeddleBuildOptions.DefaultTrimDirectiveLines, errors);
            var maxRecursion = ReadPositiveInt(lookup, HeddleBuildOptions.MaxRecursionCountProperty,
                HeddleBuildOptions.DefaultMaxRecursionCount, errors);
            var root = HeddleBuildOptions.ReadString(lookup(HeddleBuildOptions.TemplateRootProperty),
                HeddleBuildOptions.DefaultTemplateRoot);
            var ns = HeddleBuildOptions.ReadString(lookup(HeddleBuildOptions.GeneratedNamespaceProperty),
                HeddleBuildOptions.DefaultGeneratedNamespace);
            var emitU8 = ReadBool(lookup, HeddleBuildOptions.EmitUtf8PiecesProperty,
                HeddleBuildOptions.DefaultEmitUtf8Pieces, errors);

            return new GlobalConfig(profile, mode, trim, maxRecursion, root, ns, emitU8);
        }

        private static TEnum ReadEnum<TEnum>(Func<string, string> lookup, string name, TEnum fallback,
            List<OptionError> errors) where TEnum : struct
        {
            var raw = lookup(name);
            if (HeddleBuildOptions.TryReadEnum(raw, fallback, out var value))
                return value;
            errors.Add(new OptionError(raw, name, HeddleBuildOptions.ExpectedValues<TEnum>()));
            return fallback;
        }

        private static bool ReadBool(Func<string, string> lookup, string name, bool fallback,
            List<OptionError> errors)
        {
            var raw = lookup(name);
            if (HeddleBuildOptions.TryReadBool(raw, fallback, out var value))
                return value;
            errors.Add(new OptionError(raw, name, HeddleBuildOptions.ExpectedBool));
            return fallback;
        }

        private static int ReadPositiveInt(Func<string, string> lookup, string name, int fallback,
            List<OptionError> errors)
        {
            var raw = lookup(name);
            if (HeddleBuildOptions.TryReadPositiveInt(raw, fallback, out var value))
                return value;
            errors.Add(new OptionError(raw, name, HeddleBuildOptions.ExpectedPositiveInt));
            return fallback;
        }
    }
}
