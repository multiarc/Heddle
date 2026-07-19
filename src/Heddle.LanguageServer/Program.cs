using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Heddle.LanguageServer.Protocol;
using StreamJsonRpc;

namespace Heddle.LanguageServer
{
    /// <summary>
    /// The <c>heddle-lsp</c> entry point (phase 6 D6): stdio, header-delimited, source-generated STJ over
    /// StreamJsonRpc. <c>--version</c> prints the informational version and exits 0.
    /// </summary>
    internal static class Program
    {
        internal static async Task<int> Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "--version" || args[0] == "-v"))
            {
                Console.Out.WriteLine(LspServer.InformationalVersion);
                return 0;
            }

            var server = new LspServer();
            var formatter = new SystemTextJsonFormatter();
            ConfigureFormatter(formatter);

            var handler = new HeaderDelimitedMessageHandler(
                Console.OpenStandardOutput(), Console.OpenStandardInput(), formatter);
            var rpc = new JsonRpc(handler);
            server.Attach(rpc);
            rpc.AddLocalRpcTarget(server, new JsonRpcTargetOptions { AllowNonPublicInvocation = false });
            rpc.StartListening();

            var exitCode = await server.ExitCode.ConfigureAwait(false);
            return exitCode;
        }

        internal static void ConfigureFormatter(SystemTextJsonFormatter formatter)
        {
            formatter.JsonSerializerOptions.TypeInfoResolver = LspJsonContext.Default;
            formatter.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            formatter.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            formatter.JsonSerializerOptions.AllowDuplicateProperties = false;
        }
    }
}
