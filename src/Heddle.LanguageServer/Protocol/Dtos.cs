using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heddle.LanguageServer.Protocol
{
    // The phase 6 hand-written LSP 3.17 DTO subset (protocol.md): exactly the fields the server reads or writes.
    // camelCase policy + nulls omitted are configured on the serializer context; unknown incoming properties are
    // ignored by default. Records are immutable value carriers.

    public sealed record Position(int Line, int Character);

    public sealed record Range(Position Start, Position End);

    public sealed record Location(string Uri, Range Range);

    public sealed record TextDocumentIdentifier(string Uri);

    public sealed record VersionedTextDocumentIdentifier(string Uri, int Version);

    public sealed record TextDocumentItem(string Uri, string LanguageId, int Version, string Text);

    public sealed record WorkspaceFolder(string Uri, string Name);

    public sealed record TextDocumentPositionParams(TextDocumentIdentifier TextDocument, Position Position);

    // Lifecycle
    public sealed record InitializeParams
    {
        public int? ProcessId { get; init; }
        public string RootUri { get; init; }
        public JsonElement Capabilities { get; init; }
        public JsonElement? InitializationOptions { get; init; }
        public WorkspaceFolder[] WorkspaceFolders { get; init; }
    }

    public sealed record InitializeResult(ServerCapabilities Capabilities, ServerInfo ServerInfo);

    public sealed record ServerInfo(string Name, string Version);

    public sealed record ServerCapabilities
    {
        public TextDocumentSyncOptions TextDocumentSync { get; init; }
        public CompletionOptions CompletionProvider { get; init; }
        public bool HoverProvider { get; init; }
        public bool DefinitionProvider { get; init; }
        public SemanticTokensOptions SemanticTokensProvider { get; init; }
    }

    public sealed record TextDocumentSyncOptions
    {
        public bool OpenClose { get; init; }
        public int Change { get; init; }
        public SaveOptions Save { get; init; }
    }

    public sealed record SaveOptions;

    public sealed record CompletionOptions
    {
        public string[] TriggerCharacters { get; init; }
    }

    public sealed record SemanticTokensOptions
    {
        public SemanticTokensLegend Legend { get; init; }
        public bool Full { get; init; }
    }

    public sealed record SemanticTokensLegend
    {
        public string[] TokenTypes { get; init; }
        public string[] TokenModifiers { get; init; }
    }

    // Document sync
    public sealed record DidOpenTextDocumentParams(TextDocumentItem TextDocument);

    public sealed record DidChangeTextDocumentParams(VersionedTextDocumentIdentifier TextDocument,
        TextDocumentContentChangeEvent[] ContentChanges);

    public sealed record TextDocumentContentChangeEvent
    {
        public string Text { get; init; }
        public Range Range { get; init; }
        public int? RangeLength { get; init; }
    }

    public sealed record DidCloseTextDocumentParams(TextDocumentIdentifier TextDocument);

    public sealed record DidSaveTextDocumentParams(TextDocumentIdentifier TextDocument);

    // Diagnostics
    public sealed record PublishDiagnosticsParams
    {
        public string Uri { get; init; }
        public int? Version { get; init; }
        public Diagnostic[] Diagnostics { get; init; }
    }

    public sealed record Diagnostic
    {
        public Range Range { get; init; }
        public int Severity { get; init; }
        public string Code { get; init; }
        public string Source { get; init; }
        public string Message { get; init; }
    }

    // Completion
    public sealed record CompletionParams
    {
        public TextDocumentIdentifier TextDocument { get; init; }
        public Position Position { get; init; }
        public CompletionContext Context { get; init; }
    }

    public sealed record CompletionContext
    {
        public int TriggerKind { get; init; }
        public string TriggerCharacter { get; init; }
    }

    public sealed record CompletionItem
    {
        public string Label { get; init; }
        public int Kind { get; init; }
        public string Detail { get; init; }
        public string InsertText { get; init; }
    }

    // Hover / definition
    public sealed record HoverParams(TextDocumentIdentifier TextDocument, Position Position);

    public sealed record Hover(MarkupContent Contents, Range Range);

    public sealed record MarkupContent(string Kind, string Value);

    public sealed record DefinitionParams(TextDocumentIdentifier TextDocument, Position Position);

    // Semantic tokens
    public sealed record SemanticTokensParams(TextDocumentIdentifier TextDocument);

    public sealed record SemanticTokens(int[] Data);

    // Workspace / window
    public sealed record DidChangeConfigurationParams(JsonElement Settings);

    public sealed record LogMessageParams(int Type, string Message);

    public sealed record ShowMessageParams(int Type, string Message);
}
