using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Heddle.Data;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Reads the workspace <c>.heddle-lsp.json</c> (phase 6 D18) and produces a
    /// <see cref="HeddleLanguageServiceOptions"/>. Relative paths resolve against the workspace root. A present
    /// file wins field-by-field over any client-supplied defaults (applied by the caller by merging first).
    /// </summary>
    internal static class WorkspaceConfig
    {
        internal const string FileName = ".heddle-lsp.json";

        internal static HeddleLanguageServiceOptions Read(string workspaceRoot, string json)
        {
            var assemblies = new List<string>();
            string rootPath = workspaceRoot;
            var outputProfile = OutputProfile.Text;
            var expressionMode = ExpressionMode.Native;
            string fileNamePostfix = string.Empty;

            if (!string.IsNullOrWhiteSpace(json))
            {
                using var document = JsonDocument.Parse(json);
                var element = document.RootElement;

                if (element.TryGetProperty("assemblies", out var asm) && asm.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in asm.EnumerateArray())
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrEmpty(value))
                            assemblies.Add(Resolve(workspaceRoot, value));
                    }
                }

                if (element.TryGetProperty("rootPath", out var rp) && rp.ValueKind == JsonValueKind.String)
                    rootPath = Resolve(workspaceRoot, rp.GetString());

                if (element.TryGetProperty("outputProfile", out var op) && op.ValueKind == JsonValueKind.String)
                    outputProfile = string.Equals(op.GetString(), "html", StringComparison.OrdinalIgnoreCase)
                        ? OutputProfile.Html : OutputProfile.Text;

                if (element.TryGetProperty("expressionMode", out var em) && em.ValueKind == JsonValueKind.String)
                    expressionMode = ParseMode(em.GetString());

                if (element.TryGetProperty("fileNamePostfix", out var fp) && fp.ValueKind == JsonValueKind.String)
                    fileNamePostfix = fp.GetString() ?? string.Empty;
            }

            return new HeddleLanguageServiceOptions
            {
                AssemblyPaths = assemblies,
                RootPath = rootPath,
                OutputProfile = outputProfile,
                ExpressionMode = expressionMode,
                FileNamePostfix = fileNamePostfix
            };
        }

        internal static HeddleLanguageServiceOptions ReadFile(string workspaceRoot)
        {
            var path = string.IsNullOrEmpty(workspaceRoot) ? FileName : Path.Combine(workspaceRoot, FileName);
            var json = File.Exists(path) ? File.ReadAllText(path) : null;
            return Read(workspaceRoot, json);
        }

        private static ExpressionMode ParseMode(string value)
        {
            switch (value?.ToLowerInvariant())
            {
                case "memberpathsonly": return ExpressionMode.MemberPathsOnly;
                case "fullcsharp": return ExpressionMode.FullCSharp;
                default: return ExpressionMode.Native;
            }
        }

        private static string Resolve(string root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (Path.IsPathRooted(path) || string.IsNullOrEmpty(root))
                return path;
            return Path.Combine(root, path);
        }
    }
}
