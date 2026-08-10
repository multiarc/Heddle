using System;
using Heddle.Data;

namespace Heddle.Precompiled
{
    /// <summary>The centralized option names and defaults table, stated once and used by both <c>Heddle</c> and
    /// <c>Heddle.Generator</c> analyzer. MSBuild property defaults stay in XML (guarded by lockstep test); this file
    /// is Roslyn-free and IO-free by construction.</summary>
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
        public const string NodeFallbackProperty = "HeddleNodeFallback";
        public const string ObserveEngineProperty = "HeddleObserveEngine";

        /// <summary>Where the build may write and load the content-addressed intermediate assemblies engine
        /// observation needs. MSBuild owns the value because only MSBuild knows the intermediate output path, and
        /// only MSBuild can create the directory: an analyzer may not touch <c>System.IO.Directory</c>.</summary>
        public const string ObserveIntermediatePathProperty = "HeddleObserveIntermediatePath";

        /// <summary>The implementation image behind every reference the compiler was handed as a reference
        /// assembly, which is what observation loads and executes. MSBuild owns the value because only MSBuild sees
        /// both halves of a project reference: <c>@(ReferencePath)</c> is the implementation and
        /// <c>%(ReferenceAssembly)</c> is what <c>CoreCompile</c> compiles against.</summary>
        public const string ObserveImplementationPathProperty = "HeddleObserveImplementationPath";

        public const OutputProfile DefaultOutputProfile = OutputProfile.Html;
        public const ExpressionMode DefaultExpressionMode = ExpressionMode.Native;
        public const bool DefaultTrimDirectiveLines = true;
        public const int DefaultMaxRecursionCount = 100;
        public const bool DefaultEmitUtf8Pieces = false;

        /// <summary>Per-node engine-accessor fallback, on by default. Not identity-bearing and so not a
        /// fingerprint input: it changes whether a template precompiles, never a rendered byte.</summary>
        public const bool DefaultNodeFallback = true;

        /// <summary>Observation is on by default and forgiving by default: it is an optimisation, so the cost of
        /// not getting it is a type-agnostic body rather than a failed build.</summary>
        public const ObserveMode DefaultObserveMode = ObserveMode.Auto;

        /// <summary>The generator's blank fallback for the observe directory. An empty path means "nowhere to write
        /// an intermediate assembly", which is observation being unavailable rather than an error.</summary>
        public const string DefaultObserveIntermediatePath = "";

        /// <summary>The generator's blank fallback for the implementation reference list. Empty means "no reference
        /// the compiler holds has a separate implementation on record", which is what a build that resolves no
        /// reference assemblies at all declares.</summary>
        public const string DefaultObserveImplementationPath = "";

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
