using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Heddle.LanguageServer;
using Heddle.LanguageServer.Protocol;
using Nerdbank.Streams;
using StreamJsonRpc;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Phase 6 D4–D8/D17 contract tests: the real <c>LspServer</c> driven over an in-proc
    /// <c>FullDuplexStream.CreatePair()</c> (no process, no editor). Initialize handshake, didOpen →
    /// publishDiagnostics with the <c>HED*</c> code, didClose clears, completion, the semantic-token walkthrough,
    /// and shutdown/exit.
    /// </summary>
    public class ProtocolContractTests : IAsyncLifetime
    {
        private JsonRpc _clientRpc;
        private JsonRpc _serverRpc;
        private LspServer _server;
        private ClientSink _sink;

        private sealed class ClientSink
        {
            public readonly System.Collections.Concurrent.BlockingCollection<PublishDiagnosticsParams> Published =
                new System.Collections.Concurrent.BlockingCollection<PublishDiagnosticsParams>();

            [JsonRpcMethod("textDocument/publishDiagnostics", UseSingleObjectParameterDeserialization = true)]
            public void Publish(PublishDiagnosticsParams p) => Published.Add(p);

            [JsonRpcMethod("window/logMessage", UseSingleObjectParameterDeserialization = true)]
            public void Log(LogMessageParams p) { }
        }

        private PublishDiagnosticsParams TakePublished()
        {
            if (!_sink.Published.TryTake(out var value, TimeSpan.FromSeconds(15)))
                throw new TimeoutException("no publishDiagnostics received within 15s");
            return value;
        }

        public Task InitializeAsync()
        {
            var pair = FullDuplexStream.CreatePair();
            _server = new LspServer();
            var serverFormatter = new SystemTextJsonFormatter();
            Program.ConfigureFormatter(serverFormatter);
            _serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(pair.Item1, pair.Item1, serverFormatter));
            _server.Attach(_serverRpc);
            _serverRpc.AddLocalRpcTarget(_server, new JsonRpcTargetOptions { AllowNonPublicInvocation = false });
            _serverRpc.StartListening();

            _sink = new ClientSink();
            var clientFormatter = new SystemTextJsonFormatter();
            Program.ConfigureFormatter(clientFormatter);
            _clientRpc = new JsonRpc(new HeaderDelimitedMessageHandler(pair.Item2, pair.Item2, clientFormatter));
            _clientRpc.AddLocalRpcTarget(_sink, new JsonRpcTargetOptions { AllowNonPublicInvocation = false });
            _clientRpc.StartListening();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _clientRpc?.Dispose();
            _serverRpc?.Dispose();
            return Task.CompletedTask;
        }

        private static JsonElement EmptyObject() => JsonSerializer.Deserialize<JsonElement>("{}");

        private Task<InitializeResult> InitAsync() =>
            _clientRpc.InvokeWithParameterObjectAsync<InitializeResult>("initialize",
                new InitializeParams { Capabilities = EmptyObject() });

        [Fact]
        public async Task InitializeAdvertisesCapabilities()
        {
            var result = await InitAsync();
            Assert.Equal("heddle-lsp", result.ServerInfo.Name);
            Assert.Equal(1, result.Capabilities.TextDocumentSync.Change);
            Assert.True(result.Capabilities.HoverProvider);
            Assert.True(result.Capabilities.DefinitionProvider);
            Assert.Contains("@", result.Capabilities.CompletionProvider.TriggerCharacters);
            Assert.Contains(".", result.Capabilities.CompletionProvider.TriggerCharacters);
            Assert.Equal(6, result.Capabilities.SemanticTokensProvider.Legend.TokenTypes.Length);
        }

        [Fact]
        public async Task DidOpenPublishesDiagnosticsWithCode()
        {
            await InitAsync();
            await _clientRpc.NotifyWithParameterObjectAsync("textDocument/didOpen",
                new DidOpenTextDocumentParams(new TextDocumentItem("file:///doc.heddle", "heddle", 1, "@(Nonexistent)")));

            var published = TakePublished();
            Assert.Equal("file:///doc.heddle", published.Uri);
            Assert.Contains(published.Diagnostics, d => d.Code == "HED0001" && d.Severity == 1);
        }

        [Fact]
        public async Task DidCloseClearsDiagnostics()
        {
            await InitAsync();
            await _clientRpc.NotifyWithParameterObjectAsync("textDocument/didOpen",
                new DidOpenTextDocumentParams(new TextDocumentItem("file:///doc.heddle", "heddle", 1, "@(X)")));
            TakePublished(); // the open publish

            await _clientRpc.NotifyWithParameterObjectAsync("textDocument/didClose",
                new DidCloseTextDocumentParams(new TextDocumentIdentifier("file:///doc.heddle")));
            var cleared = TakePublished();
            Assert.Empty(cleared.Diagnostics);
        }

        [Fact]
        public async Task SemanticTokensWalkthroughMatchesSpec()
        {
            await InitAsync();
            await _clientRpc.NotifyWithParameterObjectAsync("textDocument/didOpen",
                new DidOpenTextDocumentParams(new TextDocumentItem("file:///t.heddle", "heddle", 1, "@(Name)\n@* hi *@")));
            TakePublished();

            var tokens = await _clientRpc.InvokeWithParameterObjectAsync<SemanticTokens>(
                "textDocument/semanticTokens/full",
                new SemanticTokensParams(new TextDocumentIdentifier("file:///t.heddle")));
            Assert.Equal(new[] { 0, 0, 1, 4, 0, 0, 2, 4, 0, 0, 1, 0, 8, 5, 0 }, tokens.Data);
        }

        [Fact]
        public async Task CompletionReturnsCallableNames()
        {
            await InitAsync();
            await _clientRpc.NotifyWithParameterObjectAsync("textDocument/didOpen",
                new DidOpenTextDocumentParams(new TextDocumentItem("file:///c.heddle", "heddle", 1, "@")));
            TakePublished();

            var items = await _clientRpc.InvokeWithParameterObjectAsync<Heddle.LanguageServer.Protocol.CompletionItem[]>(
                "textDocument/completion",
                new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier("file:///c.heddle"),
                    Position = new Position(0, 1)
                });
            Assert.Contains(items, i => i.Label == "list");
        }

        [Fact]
        public async Task ShutdownThenExitReturnsZero()
        {
            await InitAsync();
            await _clientRpc.InvokeAsync<object>("shutdown");
            Assert.True(_server.ShutdownReceived);
            await _clientRpc.NotifyAsync("exit");
            var code = await _server.ExitCode.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, code);
        }
    }
}
