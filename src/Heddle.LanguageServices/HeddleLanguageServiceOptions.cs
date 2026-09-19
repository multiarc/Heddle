using System;
using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.LanguageServices
{
    /// <summary>Workspace-level configuration; immutable — construct anew to reconfigure.</summary>
    public sealed class HeddleLanguageServiceOptions
    {
        /// <summary>Model assemblies for typed completion/hover; empty = typeless analysis. Also the input of the
        /// one-shot export scan — extensions and declaratively exported functions.</summary>
        public IReadOnlyList<string> AssemblyPaths { get; init; } = Array.Empty<string>();

        /// <summary>Template root for import/partial resolution (<c>TemplateOptions.RootPath</c>).</summary>
        public string RootPath { get; init; }

        /// <summary>Output profile mirrored from the host. Default: the engine's
        /// (<see cref="Heddle.Precompiled.HeddleBuildOptions.DefaultOutputProfile"/> — <c>Html</c> since 2.0).
        /// It must not default to <c>Text</c> while the engine
        /// and the build tier default to <c>Html</c>, or an unconfigured workspace lints templates under a
        /// profile no one would render them with and never shows the <c>HED2004</c>-class encoding lints the
        /// build of record produces. Set <c>"outputProfile": "text"</c> in <c>.heddle-lsp.json</c> to opt out.</summary>
        public OutputProfile OutputProfile { get; init; } = Heddle.Precompiled.HeddleBuildOptions.DefaultOutputProfile;

        /// <summary>Expression tier mirrored from the host. Default:
        /// <see cref="Heddle.Precompiled.HeddleBuildOptions.DefaultExpressionMode"/>.</summary>
        public ExpressionMode ExpressionMode { get; init; } =
            Heddle.Precompiled.HeddleBuildOptions.DefaultExpressionMode;

        /// <summary>Template file name postfix mirrored from the host.</summary>
        public string FileNamePostfix { get; init; } = string.Empty;

        /// <summary>Whether whole-line directives swallow their line (<c>TemplateOptions.TrimDirectiveLines</c>).
        /// Wired for options parity: it changes compiled output, so an editor that assumed the default
        /// could not match a workspace that builds with it off. Default:
        /// <see cref="Heddle.Precompiled.HeddleBuildOptions.DefaultTrimDirectiveLines"/>.</summary>
        public bool TrimDirectiveLines { get; init; } =
            Heddle.Precompiled.HeddleBuildOptions.DefaultTrimDirectiveLines;

        /// <summary>The compile-time recursion bound (<c>TemplateOptions.MaxRecursionCount</c>). Wired for options
        /// parity. Default:
        /// <see cref="Heddle.Precompiled.HeddleBuildOptions.DefaultMaxRecursionCount"/>.</summary>
        public int MaxRecursionCount { get; init; } =
            Heddle.Precompiled.HeddleBuildOptions.DefaultMaxRecursionCount;

        /// <summary>Human-readable complaints raised while reading <c>.heddle-lsp.json</c> — an unrecognized token
        /// or a value of the wrong JSON kind. A workspace-config typo must never break editing, so these are
        /// tooling log lines (the server forwards them to <c>window/logMessage</c>), never compile diagnostics and
        /// never a thrown error; the option keeps its default. Empty for a clean or absent config.</summary>
        public IReadOnlyList<string> ConfigurationMessages { get; init; } = Array.Empty<string>();
    }
}
