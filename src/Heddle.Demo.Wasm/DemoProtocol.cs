using System.Collections.Generic;

namespace Heddle.Demo.Wasm
{
    // The facade-shaped worker protocol DTOs (phase 9 D6 / wasm-demo.md). Absolute UTF-16 offsets, facade
    // vocabulary — no LSP Position/Range. Serialized by the source-generated DemoJsonContext (camelCase, nulls
    // omitted). These are the .NET mirror of the TypeScript shapes the page and worker use.

    public sealed class DemoModelInfo
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string StarterTemplate { get; set; }
    }

    public sealed class InitResult
    {
        public string EngineVersion { get; set; }
        public List<DemoModelInfo> Models { get; set; }
    }

    public sealed class DemoDiagnostic
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public string Fix { get; set; }
        public string Severity { get; set; }   // "error" | "warning"
        public int Offset { get; set; }
        public int Length { get; set; }
    }

    public sealed class AnalyzeResult
    {
        public List<DemoDiagnostic> Diagnostics { get; set; }
        public bool CsharpTierUsed { get; set; }
    }

    public sealed class DemoCompletionItem
    {
        public string Label { get; set; }
        public string Kind { get; set; }
        public string Detail { get; set; }
        public string InsertText { get; set; }
    }

    public sealed class CompleteResult
    {
        public List<DemoCompletionItem> Items { get; set; }
    }

    public sealed class HoverResultDto
    {
        public string Markdown { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }

    /// <summary>{ html } on success, { error } on a render fault — nulls are omitted so exactly one appears.</summary>
    public sealed class RenderResult
    {
        public string Html { get; set; }
        public string Error { get; set; }
    }
}
