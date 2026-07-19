using System;
using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.LanguageServices
{
    /// <summary>Workspace-level configuration (phase 6 D18); immutable — construct anew to reconfigure.</summary>
    public sealed class HeddleLanguageServiceOptions
    {
        /// <summary>Model assemblies for typed completion/hover; empty = typeless analysis. Also the input of the
        /// one-shot export scan — extensions (D23) and declaratively exported functions (D24).</summary>
        public IReadOnlyList<string> AssemblyPaths { get; init; } = Array.Empty<string>();

        /// <summary>Template root for import/partial resolution (<c>TemplateOptions.RootPath</c>).</summary>
        public string RootPath { get; init; }

        /// <summary>Output profile mirrored from the host (D18).</summary>
        public OutputProfile OutputProfile { get; init; } = OutputProfile.Text;

        /// <summary>Expression tier mirrored from the host (D18).</summary>
        public ExpressionMode ExpressionMode { get; init; } = ExpressionMode.Native;

        /// <summary>Template file name postfix mirrored from the host (D18).</summary>
        public string FileNamePostfix { get; init; } = string.Empty;
    }
}
