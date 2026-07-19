using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Demo.Models;
using Heddle.LanguageServices;
using Heddle.Runtime;

namespace Heddle.Demo.Wasm
{
    /// <summary>
    /// All demo behavior (phase 9 D5/D6/D14). Plain net10.0 code with no browser dependency: the WASM
    /// <c>[JSExport]</c> shell (DemoInterop) contains only marshalling, and this class is exercised directly on
    /// CoreCLR by <c>DemoContractTests</c>. Single-threaded by contract (one worker); analyze/render are
    /// serialized per document by construction.
    /// </summary>
    public sealed class DemoHost
    {
        private readonly HeddleLanguageService _service;
        private readonly Dictionary<string, string> _lastText = new Dictionary<string, string>(StringComparer.Ordinal);

        public DemoHost()
        {
            // The compiled-in models are name-resolvable because the host seeds the engine assembly list. The
            // browser cannot load user assemblies, so AssemblyPaths stays empty and the facade's ALC path is unused.
            HeddleTemplate.Configure(typeof(Blog).Assembly);
            _service = new HeddleLanguageService(new HeddleLanguageServiceOptions
            {
                AssemblyPaths = Array.Empty<string>(),
                RootPath = "/",
                OutputProfile = OutputProfile.Html,
                ExpressionMode = ExpressionMode.Native,
                FileNamePostfix = string.Empty
            });
        }

        public InitResult Init()
        {
            var models = new List<DemoModelInfo>();
            foreach (var set in DemoCatalog.Sets)
                models.Add(new DemoModelInfo { Id = set.Id, DisplayName = set.DisplayName, StarterTemplate = set.StarterTemplate });

            return new InitResult
            {
                EngineVersion = typeof(HeddleTemplate).Assembly.GetName().Version?.ToString() ?? "unknown",
                Models = models
            };
        }

        public AnalyzeResult Analyze(string path, string text, int version)
        {
            _lastText[path] = text;
            var analysis = _service.Analyze(path, text, version);
            var diagnostics = new List<DemoDiagnostic>(analysis.Diagnostics.Count);
            foreach (var d in analysis.Diagnostics)
            {
                diagnostics.Add(new DemoDiagnostic
                {
                    Id = d.Id,
                    Message = d.Message,
                    Fix = d.Fix,
                    Severity = d.Severity == HeddleDiagnosticSeverity.Warning ? "warning" : "error",
                    Offset = d.Offset,
                    Length = d.Length
                });
            }

            return new AnalyzeResult { Diagnostics = diagnostics, CsharpTierUsed = analysis.CSharpTierUsed };
        }

        public CompleteResult Complete(string path, int offset)
        {
            var completions = _service.GetCompletions(path, offset);
            var items = new List<DemoCompletionItem>(completions.Items.Count);
            foreach (var item in completions.Items)
            {
                items.Add(new DemoCompletionItem
                {
                    Label = item.Label,
                    Kind = KindName(item.Kind),
                    Detail = item.Detail,
                    InsertText = item.InsertText
                });
            }

            return new CompleteResult { Items = items };
        }

        public HoverResultDto Hover(string path, int offset)
        {
            var hover = _service.GetHover(path, offset);
            if (hover == null)
                return null;
            return new HoverResultDto { Markdown = hover.Markdown, Offset = hover.Offset, Length = hover.Length };
        }

        public RenderResult Render(string path, string modelId)
        {
            if (!_lastText.TryGetValue(path, out var text))
                return new RenderResult { Error = "Nothing to render yet — analyze first." };

            try
            {
                var options = new TemplateOptions
                {
                    OutputProfile = OutputProfile.Html,
                    ExpressionMode = ExpressionMode.Native,
                    TrimDirectiveLines = true,
                    RootPath = "/"
                    // ProvideLanguageFeatures stays off: this is the render compile, not the tooling analysis.
                };

                using var template = new HeddleTemplate(text, new CompileContext(options));
                if (!template.CompileResult.Success)
                {
                    // A C#-tier construct declined by ExpressionMode.Native (or any compile error) surfaces here;
                    // the page shows it in the pane note (D8).
                    return new RenderResult { Error = FirstError(template.CompileResult) };
                }

                var model = DemoCatalog.Get(modelId).CreateInstance();
                return new RenderResult { Html = template.Generate(model) };
            }
            catch (Exception ex)
            {
                // Render faults (e.g. a :: dynamic template exercising trimmed runtime-binder paths) never crash
                // the page — the pane shows the message (D8).
                return new RenderResult { Error = ex.Message };
            }
        }

        private static string FirstError(HeddleCompileResult result)
        {
            foreach (var error in result.ErrorList)
            {
                var id = error.DiagnosticId;
                return id != null ? $"[{id}] {error.Error}" : error.Error;
            }

            return "Template failed to compile.";
        }

        private static string KindName(CompletionItemKind kind)
        {
            switch (kind)
            {
                case CompletionItemKind.Property: return "property";
                case CompletionItemKind.Definition: return "definition";
                case CompletionItemKind.Extension: return "extension";
                case CompletionItemKind.Function: return "function";
                case CompletionItemKind.Prop: return "prop";
                case CompletionItemKind.Keyword: return "keyword";
                default: return kind.ToString().ToLowerInvariant();
            }
        }
    }
}
