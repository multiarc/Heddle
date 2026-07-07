using System.Text.Json.Serialization;

namespace Heddle.LanguageServer.Protocol
{
    /// <summary>
    /// The source-generated STJ context over the DTO subset (phase 6 D6): no reflection-metadata warm-up on the
    /// first message. camelCase names, nulls omitted, non-strict unmapped-member handling.
    /// </summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(InitializeParams))]
    [JsonSerializable(typeof(InitializeResult))]
    [JsonSerializable(typeof(ServerCapabilities))]
    [JsonSerializable(typeof(DidOpenTextDocumentParams))]
    [JsonSerializable(typeof(DidChangeTextDocumentParams))]
    [JsonSerializable(typeof(DidCloseTextDocumentParams))]
    [JsonSerializable(typeof(DidSaveTextDocumentParams))]
    [JsonSerializable(typeof(DidChangeConfigurationParams))]
    [JsonSerializable(typeof(PublishDiagnosticsParams))]
    [JsonSerializable(typeof(CompletionParams))]
    [JsonSerializable(typeof(CompletionItem))]
    [JsonSerializable(typeof(CompletionItem[]))]
    [JsonSerializable(typeof(HoverParams))]
    [JsonSerializable(typeof(Hover))]
    [JsonSerializable(typeof(DefinitionParams))]
    [JsonSerializable(typeof(Location))]
    [JsonSerializable(typeof(SemanticTokensParams))]
    [JsonSerializable(typeof(SemanticTokens))]
    [JsonSerializable(typeof(LogMessageParams))]
    [JsonSerializable(typeof(ShowMessageParams))]
    internal partial class LspJsonContext : JsonSerializerContext
    {
    }
}
