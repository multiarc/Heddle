using System;
using Heddle.Data;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The one option names + defaults table. The MSBuild property names the generator reads and
    /// the effective default of every option, stated once and compiled into both <c>Heddle</c> (which initializes
    /// <see cref="TemplateOptions"/> from it) and the <c>Heddle.Generator</c> analyzer (whose <c>ConfigReader</c> is
    /// a thin adapter over the parse helpers here). Before this file the table existed three times — the props XML,
    /// <c>ConfigReader</c>, and both <c>TemplateOptions</c> constructors — and a one-sided default change flips every
    /// template to <c>OptionsMismatch</c>.</para>
    /// <para><b>The props defaults stay.</b> <c>HeddleTemplateRoot</c>'s default is
    /// <c>$(MSBuildProjectDirectory)</c>, expressible only in MSBuild, and the properties are consumer-visible after
    /// import. The XML therefore remains a second physical statement of the five scalar defaults, guarded by the
    /// props↔code↔runtime lockstep test rather than by hand.</para>
    /// <para>Roslyn-free and IO-free by construction: the analyzer's <c>AnalyzerConfigOptions</c> never crosses into
    /// this file, and the parse helpers report failure as a <c>bool</c> rather than a diagnostic.</para>
    /// </summary>
    public static class HeddleBuildOptions
    {
        /// <summary>The <c>build_property.</c> prefix the compiler puts on a <c>CompilerVisibleProperty</c>.</summary>
        public const string BuildPropertyPrefix = "build_property.";

        public const string OutputProfileProperty = "HeddleOutputProfile";
        public const string ExpressionModeProperty = "HeddleExpressionMode";
        public const string TrimDirectiveLinesProperty = "HeddleTrimDirectiveLines";
        public const string MaxRecursionCountProperty = "HeddleMaxRecursionCount";
        public const string TemplateRootProperty = "HeddleTemplateRoot";
        public const string GeneratedNamespaceProperty = "HeddleGeneratedNamespace";
        public const string EmitUtf8PiecesProperty = "HeddleEmitUtf8Pieces";

        public const OutputProfile DefaultOutputProfile = OutputProfile.Html;
        public const ExpressionMode DefaultExpressionMode = ExpressionMode.Native;
        public const bool DefaultTrimDirectiveLines = true;
        public const int DefaultMaxRecursionCount = 100;
        public const bool DefaultEmitUtf8Pieces = false;

        /// <summary>The generator's blank fallback for the template root. The <i>effective</i> default is MSBuild's
        /// <c>$(MSBuildProjectDirectory)</c>; an empty root here means "no root", which flattens keys (and now draws
        /// HED7018).</summary>
        public const string DefaultTemplateRoot = "";

        /// <summary>Blank means the generator's own <c>Heddle.Generated</c> fallback.</summary>
        public const string DefaultGeneratedNamespace = "";

        /// <summary>The expected-values text for a bool option, as the build diagnostic prints it.</summary>
        public const string ExpectedBool = "true|false";

        /// <summary>The expected-values text for a positive-int option, as the build diagnostic prints it.</summary>
        public const string ExpectedPositiveInt = "a positive integer";

        /// <summary>The expected-values text for an enum option: its member names in declaration order. Built from
        /// the linked enum itself, so adding a member can never leave a hand-copied allow-list behind.</summary>
        public static string ExpectedValues<TEnum>() where TEnum : struct =>
            string.Join("|", Enum.GetNames(typeof(TEnum)));

        /// <summary>Parses an enum option value. A missing/blank value is the default and is <b>not</b> an error;
        /// anything that is not a member name (case-insensitively) is — the generator never guesses a default from a
        /// typo. Numeric spellings are rejected on purpose: <c>Enum.TryParse</c> would accept <c>"5"</c>.</summary>
        /// <returns><c>false</c> only when the value is present and unparsable.</returns>
        public static bool TryReadEnum<TEnum>(string raw, TEnum fallback, out TEnum value) where TEnum : struct
        {
            value = fallback;
            if (string.IsNullOrEmpty(raw))
                return true;
            foreach (var name in Enum.GetNames(typeof(TEnum)))
            {
                if (string.Equals(name, raw, StringComparison.OrdinalIgnoreCase))
                {
                    value = (TEnum)Enum.Parse(typeof(TEnum), name, ignoreCase: false);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Parses a bool option value; a missing/blank value is the default.</summary>
        /// <returns><c>false</c> only when the value is present and unparsable.</returns>
        public static bool TryReadBool(string raw, bool fallback, out bool value)
        {
            value = fallback;
            if (string.IsNullOrEmpty(raw))
                return true;
            if (!bool.TryParse(raw, out var parsed))
                return false;
            value = parsed;
            return true;
        }

        /// <summary>Parses a positive-int option value; a missing/blank value is the default.</summary>
        /// <returns><c>false</c> only when the value is present and not a positive integer.</returns>
        public static bool TryReadPositiveInt(string raw, int fallback, out int value)
        {
            value = fallback;
            if (string.IsNullOrEmpty(raw))
                return true;
            if (!int.TryParse(raw, out var parsed) || parsed <= 0)
                return false;
            value = parsed;
            return true;
        }

        /// <summary>Reads a string option; a missing/blank value is the fallback. Never an error.</summary>
        public static string ReadString(string raw, string fallback) =>
            string.IsNullOrEmpty(raw) ? fallback : raw;
    }
}
