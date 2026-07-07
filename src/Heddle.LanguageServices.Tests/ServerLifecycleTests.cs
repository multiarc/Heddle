using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Heddle.LanguageServer;
using Heddle.LanguageServer.Protocol;
using Nerdbank.Streams;
using StreamJsonRpc;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Phase 6 process/lifecycle checks: <c>--version</c> from the built server, and the CI cold-start guard
    /// (initialize → first publishDiagnostics on the benchmark-sized home page under the automated 5 s bound).
    /// </summary>
    public class ServerLifecycleTests
    {
        private static string ServerDll =>
            Path.Combine(AppContext.BaseDirectory, "Heddle.LanguageServer.dll");

        [Fact]
        public void VersionFlagPrintsAndExitsZero()
        {
            Assert.True(File.Exists(ServerDll), "the server DLL must be copied into the test output");
            var psi = new ProcessStartInfo("dotnet", $"exec \"{ServerDll}\" --version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            var output = process.StandardOutput.ReadToEnd();
            Assert.True(process.WaitForExit(15000));
            Assert.Equal(0, process.ExitCode);
            Assert.Contains(LspServer.InformationalVersion, output);
        }

        [Fact]
        public async Task ColdStartInitializeAndFirstDiagnosticsUnderFiveSeconds()
        {
            var pair = FullDuplexStream.CreatePair();
            var server = new LspServer();
            var sf = new SystemTextJsonFormatter();
            Program.ConfigureFormatter(sf);
            var serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(pair.Item1, pair.Item1, sf));
            server.Attach(serverRpc);
            serverRpc.AddLocalRpcTarget(server, new JsonRpcTargetOptions { AllowNonPublicInvocation = false });
            serverRpc.StartListening();

            var published = new TaskCompletionSource<PublishDiagnosticsParams>();
            var sink = new Sink(published);
            var cf = new SystemTextJsonFormatter();
            Program.ConfigureFormatter(cf);
            var clientRpc = new JsonRpc(new HeaderDelimitedMessageHandler(pair.Item2, pair.Item2, cf));
            clientRpc.AddLocalRpcTarget(sink, new JsonRpcTargetOptions { AllowNonPublicInvocation = false });
            clientRpc.StartListening();

            var stopwatch = Stopwatch.StartNew();
            await clientRpc.InvokeWithParameterObjectAsync<InitializeResult>("initialize",
                new InitializeParams { Capabilities = JsonSerializer.Deserialize<JsonElement>("{}") });
            await clientRpc.NotifyWithParameterObjectAsync("textDocument/didOpen",
                new DidOpenTextDocumentParams(new TextDocumentItem("file:///home.heddle", "heddle", 1,
                    "<html><body>@(Title)</body></html>")));
            await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < 5000,
                $"cold start took {stopwatch.ElapsedMilliseconds} ms (CI bound 5 s)");

            clientRpc.Dispose();
            serverRpc.Dispose();
        }

        private sealed class Sink
        {
            private readonly TaskCompletionSource<PublishDiagnosticsParams> _tcs;
            public Sink(TaskCompletionSource<PublishDiagnosticsParams> tcs) => _tcs = tcs;

            [JsonRpcMethod("textDocument/publishDiagnostics", UseSingleObjectParameterDeserialization = true)]
            public void Publish(PublishDiagnosticsParams p) => _tcs.TrySetResult(p);

            [JsonRpcMethod("window/logMessage", UseSingleObjectParameterDeserialization = true)]
            public void Log(LogMessageParams p) { }
        }
    }
}
