using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Diagnostics
{
    /// <summary>The generator's Roslyn <see cref="DiagnosticDescriptor"/>s (phase 7 D13): the <c>HED7xxx</c> block
    /// used directly as the diagnostic <c>Id</c>, category <c>"Heddle.Precompile"</c>. Forwarded front-end errors
    /// keep their own IDs; an ID-less forwarded error is wrapped as <c>HED7012</c>/<c>HED7013</c>.</summary>
    internal static class GeneratorDiagnostics
    {
        private const string Category = "Heddle.Precompile";

        /// <summary>An <c>AdditionalFiles</c> <c>.heddle</c> source could not be read at generation time.</summary>
        public static readonly DiagnosticDescriptor UnreadableFile = new DiagnosticDescriptor(
            "HED7001", "Unreadable Heddle template",
            "Heddle template '{0}' could not be read: {1}.",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        /// <summary>Two templates in one compilation normalize to the same key (position: the second file).</summary>
        public static readonly DiagnosticDescriptor DuplicateKey = new DiagnosticDescriptor(
            "HED7002", "Duplicate Heddle template key",
            "Duplicate Heddle template key '{0}': '{1}' and '{2}' normalize to the same key. Set an explicit Key " +
            "metadata on one, or exclude it from pre-compilation.",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        /// <summary>Explicit <c>Key</c> metadata is empty/whitespace, contains a <c>.</c>/<c>..</c> segment, or
        /// normalizes to an empty string.</summary>
        public static readonly DiagnosticDescriptor InvalidKeyMetadata = new DiagnosticDescriptor(
            "HED7004", "Invalid Heddle template Key metadata",
            "Invalid Key metadata '{0}' on Heddle template '{1}': keys must be non-empty relative paths without " +
            "'.' or '..' segments.",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        /// <summary>A static piece contains an unpaired surrogate; the u8 twin is suppressed for the template
        /// (string output unaffected — D15).</summary>
        public static readonly DiagnosticDescriptor SurrogatePiece = new DiagnosticDescriptor(
            "HED7005", "Unpaired surrogate in static text",
            "Static text in '{0}' contains an unpaired surrogate; UTF-8 pre-encoded pieces are disabled for this " +
            "template. Byte-sink renders will transcode at run time.",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        /// <summary>D9 / WI6: a named extension resolves to no <c>[ExtensionName]</c> type in any referenced assembly
        /// (position: the call). Fires only for an extension-only call shape — a bodied call — so a function-compatible
        /// bare call is never misclassified (that path draws HED7014).</summary>
        public static readonly DiagnosticDescriptor ExtensionNotBindable = new DiagnosticDescriptor(
            "HED7006", "Extension not bindable",
            "Cannot find extension <{0}> in the referenced assemblies. Reference the assembly that defines it, or " +
            "correct the name.",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        /// <summary>D22 / WI6: a bound extension outside the engine assembly overrides <c>InitStart</c>/
        /// <c>CompleteInit</c> — compile-time logic the generator cannot evaluate; precompiled binding would silently
        /// skip it (position: the call).</summary>
        public static readonly DiagnosticDescriptor ExtensionOverridesHook = new DiagnosticDescriptor(
            "HED7015", "Extension overrides a compile-time hook",
            "Extension <{0}> ({1}) overrides {2}, which runs compile-time logic the generator cannot evaluate at " +
            "build time; precompiled binding would silently skip it. Exclude this template from pre-compilation " +
            "(Precompile=\"false\" or <HeddleTemplate Remove=\"…\" />), or keep the extension's compile-time behavior " +
            "in the base implementation.",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        /// <summary>Milestone 2 (D3): the <c>@model</c>/<c>::</c> type name does not resolve in the compilation or
        /// its references — a genuine typo/unresolvable symbol reported natively before the C# compiler sees the
        /// generated code (position: the directive).</summary>
        public static readonly DiagnosticDescriptor UnresolvableModelType = new DiagnosticDescriptor(
            "HED7007", "Unresolvable model type",
            "Model type '{0}' is not defined in this compilation or its references.",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        /// <summary>Milestone 2 (D3): a member path does not resolve on the model type, mirroring the runtime member
        /// tier order (position: the path). Only fires for a genuine property-not-found on a resolved, non-dynamic
        /// model — the same failure the runtime raises as HED0001, surfaced natively at the template span.</summary>
        public static readonly DiagnosticDescriptor UnresolvableMember = new DiagnosticDescriptor(
            "HED7008", "Unresolvable member path",
            "'{0}' does not contain an accessible member '{1}' (member path '{2}').",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OptionParseError = new DiagnosticDescriptor(
            "HED7009", "Invalid Heddle build option",
            "Invalid value '{0}' for build option '{1}'; expected {2}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        /// <summary>An <c>@&lt;&lt;</c> import path is not among the compilation's <c>.heddle</c> <c>AdditionalFiles</c>.</summary>
        public static readonly DiagnosticDescriptor ImportNotIncluded = new DiagnosticDescriptor(
            "HED7011", "Heddle import not included in compilation",
            "Import '{0}' is not included in this compilation. Add it as a <HeddleTemplate> item (use " +
            "Precompile=\"false\" for import-only files).",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ForwardedError = new DiagnosticDescriptor(
            "HED7012", "Heddle template error",
            "{0}",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ForwardedWarning = new DiagnosticDescriptor(
            "HED7013", "Heddle template warning",
            "{0}",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateSanitizedName = new DiagnosticDescriptor(
            "HED7010", "Duplicate generated entry-class name",
            "Templates '{0}' and '{1}' sanitize to the same entry-class identifier '{2}'",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CaseOnlyKeyTwin = new DiagnosticDescriptor(
            "HED7003", "Case-only template key twin",
            "Templates '{0}' and '{1}' differ only by case; ordinal-case-sensitive keys make one shadow the other",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        /// <summary>D21 / OQ1 remainder: a called function name is neither a default built-in nor exported by any
        /// referenced assembly (a delegate-only registration, not representable in assembly metadata), so there is
        /// nothing for build-time discovery to bind against. The template is left un-precompiled with a
        /// fallback-marker manifest entry; it renders through the dynamic path at run time.</summary>
        public static readonly DiagnosticDescriptor UnresolvableFunction = new DiagnosticDescriptor(
            "HED7014", "Unresolvable function in precompiled template",
            "Function '{0}' is neither a default built-in nor exported by a referenced assembly, so it cannot be " +
            "precompiled (delegate-only registrations are not representable in metadata). Export it with " +
            "[ExportFunctions] on a public static container to precompile it; otherwise this template renders through " +
            "the dynamic path at run time.",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
    }
}
