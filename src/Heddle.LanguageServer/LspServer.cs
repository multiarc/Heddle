using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Heddle.LanguageServices;
using StreamJsonRpc;
using LspProtocol = Heddle.LanguageServer.Protocol;

namespace Heddle.LanguageServer
{
    /// <summary>
    /// Thin StreamJsonRpc target methods, each a projection of a <see cref="HeddleLanguageService"/> call
    /// plus DTO mapping. Full-document sync, 300 ms debounce, request-forced analysis. No compiler logic lives here.
    /// </summary>
    internal sealed class LspServer
    {
        /// <summary>The version <c>heddle-lsp --version</c> prints and the LSP <c>initialize</c> response reports as
        /// <c>serverInfo.version</c>. It is read off this assembly's
        /// <see cref="AssemblyInformationalVersionAttribute"/>, with the source-revision suffix trimmed. Derived,
        /// not stated, so there is no second statement of the release line left here to drift.</summary>
        internal static readonly string InformationalVersion = ReadInformationalVersion();

        private static string ReadInformationalVersion()
        {
            var raw = typeof(LspServer).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(raw))
                return typeof(LspServer).Assembly.GetName().Version?.ToString() ?? "unknown";
            var plus = raw.IndexOf('+');
            return plus < 0 ? raw : raw.Substring(0, plus);
        }

        private const int DebounceMs = 300;

        private readonly ConcurrentDictionary<string, (string Text, int Version)> _buffers =
            new ConcurrentDictionary<string, (string, int)>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounce =
            new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);
        private readonly object _lifecycleGate = new object();

        private JsonRpc _rpc;
        private HeddleLanguageService _service;
        private string _workspaceRoot;
        private bool _initialized;
        private bool _shutdownReceived;
        private readonly TaskCompletionSource<int> _exit = new TaskCompletionSource<int>();

        internal Task<int> ExitCode => _exit.Task;
        internal bool ShutdownReceived => _shutdownReceived;

        internal void Attach(JsonRpc rpc)
        {
            _rpc = rpc;
        }

        [JsonRpcMethod("initialize", UseSingleObjectParameterDeserialization = true)]
        public LspProtocol.InitializeResult Initialize(LspProtocol.InitializeParams @params)
        {
            lock (_lifecycleGate)
            {
                _workspaceRoot = ResolveWorkspaceRoot(@params);
                var options = BuildOptions(_workspaceRoot, @params?.InitializationOptions);
                _service = new HeddleLanguageService(options) { LogSink = LogSink };
                LogConfigurationMessages(options);
                _initialized = true;
            }

            return new LspProtocol.InitializeResult(
                new LspProtocol.ServerCapabilities
                {
                    TextDocumentSync = new LspProtocol.TextDocumentSyncOptions
                    {
                        OpenClose = true,
                        Change = 1,
                        Save = new LspProtocol.SaveOptions()
                    },
                    CompletionProvider = new LspProtocol.CompletionOptions
                    {
                        TriggerCharacters = new[] { "@", "(", ".", ":", "," }
                    },
                    HoverProvider = true,
                    DefinitionProvider = true,
                    SemanticTokensProvider = new LspProtocol.SemanticTokensOptions
                    {
                        Legend = new LspProtocol.SemanticTokensLegend
                        {
                            TokenTypes = SemanticTokensBuilder.TokenTypes,
                            TokenModifiers = Array.Empty<string>()
                        },
                        Full = true
                    }
                },
                new LspProtocol.ServerInfo("heddle-lsp", InformationalVersion));
        }

        [JsonRpcMethod("initialized")]
        public void Initialized()
        {
        }

        [JsonRpcMethod("$/setTrace", UseSingleObjectParameterDeserialization = true)]
        public void SetTrace(JsonElement value)
        {
        }

        [JsonRpcMethod("shutdown")]
        public object Shutdown()
        {
            _shutdownReceived = true;
            _service?.Dispose();
            _service = null;
            return null;
        }

        [JsonRpcMethod("exit")]
        public void Exit()
        {
            _exit.TrySetResult(_shutdownReceived ? 0 : 1);
        }

        [JsonRpcMethod("workspace/didChangeConfiguration", UseSingleObjectParameterDeserialization = true)]
        public void DidChangeConfiguration(LspProtocol.DidChangeConfigurationParams @params)
        {
            // Re-read the workspace file/settings and rebuild on config changes.
            lock (_lifecycleGate)
            {
                if (!_initialized)
                    return;
                var options = BuildOptions(_workspaceRoot, @params?.Settings);
                _service?.Dispose();
                _service = new HeddleLanguageService(options) { LogSink = LogSink };
                LogConfigurationMessages(options);
            }

            foreach (var uri in _buffers.Keys.ToArray())
                AnalyzeAndPublish(uri);
        }

        [JsonRpcMethod("textDocument/didOpen", UseSingleObjectParameterDeserialization = true)]
        public void DidOpen(LspProtocol.DidOpenTextDocumentParams @params)
        {
            var doc = @params.TextDocument;
            _buffers[doc.Uri] = (doc.Text ?? string.Empty, doc.Version);
            AnalyzeAndPublish(doc.Uri);
        }

        [JsonRpcMethod("textDocument/didChange", UseSingleObjectParameterDeserialization = true)]
        public void DidChange(LspProtocol.DidChangeTextDocumentParams @params)
        {
            var uri = @params.TextDocument.Uri;
            string text = _buffers.TryGetValue(uri, out var current) ? current.Text : string.Empty;
            foreach (var change in @params.ContentChanges ?? Array.Empty<LspProtocol.TextDocumentContentChangeEvent>())
            {
                if (change.Range != null)
                {
                    LogSink($"Ignoring ranged change event under Full sync for {uri} (client contract violation).");
                    continue;
                }

                text = change.Text ?? string.Empty;
            }

            _buffers[uri] = (text, @params.TextDocument.Version);
            ScheduleDebounced(uri);
        }

        [JsonRpcMethod("textDocument/didSave", UseSingleObjectParameterDeserialization = true)]
        public void DidSave(LspProtocol.DidSaveTextDocumentParams @params)
        {
        }

        [JsonRpcMethod("textDocument/didClose", UseSingleObjectParameterDeserialization = true)]
        public void DidClose(LspProtocol.DidCloseTextDocumentParams @params)
        {
            var uri = @params.TextDocument.Uri;
            _buffers.TryRemove(uri, out _);
            _service?.Close(UriToPath(uri));
            PublishDiagnostics(uri, null, Array.Empty<LspProtocol.Diagnostic>());
        }

        [JsonRpcMethod("textDocument/completion", UseSingleObjectParameterDeserialization = true)]
        public LspProtocol.CompletionItem[] Completion(LspProtocol.CompletionParams @params, CancellationToken ct)
        {
            var uri = @params.TextDocument.Uri;
            var analysis = EnsureAnalyzed(uri);
            if (analysis == null)
                return Array.Empty<LspProtocol.CompletionItem>();
            int offset = analysis.Lines.PositionToOffset(@params.Position.Line, @params.Position.Character);
            var result = _service.GetCompletions(UriToPath(uri), offset, ct);
            return result.Items.Select(ToLspCompletion).ToArray();
        }

        [JsonRpcMethod("textDocument/hover", UseSingleObjectParameterDeserialization = true)]
        public LspProtocol.Hover Hover(LspProtocol.HoverParams @params)
        {
            var uri = @params.TextDocument.Uri;
            var analysis = EnsureAnalyzed(uri);
            if (analysis == null)
                return null;
            int offset = analysis.Lines.PositionToOffset(@params.Position.Line, @params.Position.Character);
            var hover = _service.GetHover(UriToPath(uri), offset);
            if (hover == null)
                return null;
            return new LspProtocol.Hover(new LspProtocol.MarkupContent("markdown", hover.Markdown),
                ToRange(analysis, hover.Offset, hover.Length));
        }

        [JsonRpcMethod("textDocument/definition", UseSingleObjectParameterDeserialization = true)]
        public LspProtocol.Location Definition(LspProtocol.DefinitionParams @params)
        {
            var uri = @params.TextDocument.Uri;
            var analysis = EnsureAnalyzed(uri);
            if (analysis == null)
                return null;
            int offset = analysis.Lines.PositionToOffset(@params.Position.Line, @params.Position.Character);
            var target = _service.GetDefinition(UriToPath(uri), offset);
            if (target == null)
                return null;

            var targetText = ReadDocument(target.SourcePath);
            var targetMap = new Heddle.LanguageServices.LineMap(targetText ?? string.Empty);
            var (sl, sc) = targetMap.OffsetToPosition(target.Offset);
            var (el, ec) = targetMap.OffsetToPosition(target.Offset + target.Length);
            return new LspProtocol.Location(PathToUri(target.SourcePath),
                new LspProtocol.Range(new LspProtocol.Position(sl, sc), new LspProtocol.Position(el, ec)));
        }

        [JsonRpcMethod("textDocument/semanticTokens/full", UseSingleObjectParameterDeserialization = true)]
        public LspProtocol.SemanticTokens SemanticTokensFull(LspProtocol.SemanticTokensParams @params)
        {
            var analysis = EnsureAnalyzed(@params.TextDocument.Uri);
            return new LspProtocol.SemanticTokens(analysis == null
                ? Array.Empty<int>()
                : SemanticTokensBuilder.Build(analysis));
        }

        private void ScheduleDebounced(string uri)
        {
            var cts = new CancellationTokenSource();
            var previous = _debounce.AddOrUpdate(uri, cts, (_, old) =>
            {
                old.Cancel();
                old.Dispose();
                return cts;
            });
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceMs, cts.Token).ConfigureAwait(false);
                    AnalyzeAndPublish(uri);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        private DocumentAnalysis EnsureAnalyzed(string uri)
        {
            if (_service == null || !_buffers.TryGetValue(uri, out var buffer))
                return _service?.GetAnalysis(UriToPath(uri));
            var path = UriToPath(uri);
            var existing = _service.GetAnalysis(path);
            if (existing != null && existing.Version == buffer.Version && ReferenceEquals(existing.Text, buffer.Text))
                return existing;
            return _service.Analyze(path, buffer.Text, buffer.Version);
        }

        private void AnalyzeAndPublish(string uri)
        {
            if (_service == null || !_buffers.TryGetValue(uri, out var buffer))
                return;
            var analysis = _service.Analyze(UriToPath(uri), buffer.Text, buffer.Version);
            var diagnostics = analysis.Diagnostics.Select(d => ToLspDiagnostic(analysis, d)).ToArray();
            PublishDiagnostics(uri, buffer.Version, diagnostics);
        }

        private void PublishDiagnostics(string uri, int? version, LspProtocol.Diagnostic[] diagnostics)
        {
            _rpc?.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics",
                new LspProtocol.PublishDiagnosticsParams { Uri = uri, Version = version, Diagnostics = diagnostics });
        }

        private LspProtocol.Diagnostic ToLspDiagnostic(DocumentAnalysis analysis, HeddleDiagnostic d)
        {
            var message = d.Fix != null ? $"{d.Message}\nFix: {d.Fix}" : d.Message;
            return new LspProtocol.Diagnostic
            {
                Range = ToRange(analysis, d.Offset, d.Length),
                Severity = d.Severity == HeddleDiagnosticSeverity.Warning ? 2 : 1,
                Code = d.Id,
                Source = "heddle",
                Message = message
            };
        }

        private static LspProtocol.CompletionItem ToLspCompletion(CompletionItem item)
        {
            int kind = item.Kind switch
            {
                CompletionItemKind.Property => 10,
                CompletionItemKind.Definition => 7,
                CompletionItemKind.Extension => 3,
                CompletionItemKind.Function => 3,
                CompletionItemKind.Prop => 5,
                _ => 14
            };
            return new LspProtocol.CompletionItem
            {
                Label = item.Label,
                Kind = kind,
                Detail = item.Detail,
                InsertText = item.InsertText
            };
        }

        private static LspProtocol.Range ToRange(DocumentAnalysis analysis, int offset, int length)
        {
            var (sl, sc) = analysis.Lines.OffsetToPosition(offset);
            var (el, ec) = analysis.Lines.OffsetToPosition(offset + length);
            return new LspProtocol.Range(new LspProtocol.Position(sl, sc), new LspProtocol.Position(el, ec));
        }

        /// <summary>Forwards workspace-configuration warnings to the client's log. A configuration error keeps
        /// the option's default and logs a message; it never becomes a diagnostic and never stops analysis.</summary>
        private void LogConfigurationMessages(HeddleLanguageServiceOptions options)
        {
            if (options?.ConfigurationMessages == null)
                return;
            foreach (var message in options.ConfigurationMessages)
                LogSink(message);
        }

        private void LogSink(string message)
        {
            _rpc?.NotifyWithParameterObjectAsync("window/logMessage",
                new LspProtocol.LogMessageParams(3, message));
        }

        private static HeddleLanguageServiceOptions BuildOptions(string root, JsonElement? settings)
        {
            // A present .heddle-lsp.json file is authoritative; otherwise forwarded client settings; otherwise a bare typeless workspace.
            var filePath = string.IsNullOrEmpty(root)
                ? WorkspaceConfig.FileName
                : Path.Combine(root, WorkspaceConfig.FileName);
            if (File.Exists(filePath))
                return WorkspaceConfig.ReadFile(root);
            if (settings.HasValue && settings.Value.ValueKind == JsonValueKind.Object)
                return WorkspaceConfig.Read(root, settings.Value.GetRawText());
            return new HeddleLanguageServiceOptions { RootPath = root };
        }

        private static string ResolveWorkspaceRoot(LspProtocol.InitializeParams p)
        {
            var folderUri = p?.WorkspaceFolders != null && p.WorkspaceFolders.Length > 0
                ? p.WorkspaceFolders[0].Uri
                : p?.RootUri;
            return string.IsNullOrEmpty(folderUri) ? null : UriToPath(folderUri);
        }

        internal static string UriToPath(string uri)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;
            if (uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                try { return new Uri(uri).LocalPath; }
                catch { return uri; }
            }

            return uri; // untitled / non-file — analyzed typelessly
        }

        internal static string PathToUri(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            try { return new Uri(Path.GetFullPath(path)).AbsoluteUri; }
            catch { return path; }
        }

        private static string ReadDocument(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
            catch { return string.Empty; }
        }
    }
}
