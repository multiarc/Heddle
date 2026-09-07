using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;

namespace Heddle.Tool.Compile
{
    /// <summary>The incrementality stamp: SHA-256 over the build version, every option value, every
    /// <c>--template</c>/<c>--import-only</c> line with the item's content hash, and the MVID of every
    /// <c>--reference</c> image, in response-file order. A rebuilt referenced project with an unchanged
    /// MVID (deterministic build) runs the host but compiles nothing; artifact bytes are therefore a
    /// function of exactly these inputs.</summary>
    internal static class Stamp
    {
        internal static string Compute(string buildVersion, CompileRequest request,
            IReadOnlyList<TemplateInput> templates, IReadOnlyList<ImportOnlyItem> imports)
        {
            var text = new StringBuilder();
            text.Append("build-version=").Append(buildVersion ?? string.Empty).Append('\n');
            text.Append("output-profile=").Append(request.OutputProfile ?? string.Empty).Append('\n');
            text.Append("expression-mode=").Append(request.ExpressionMode ?? string.Empty).Append('\n');
            text.Append("trim-directive-lines=").Append(request.TrimDirectiveLines ?? string.Empty).Append('\n');
            text.Append("max-recursion-count=").Append(request.MaxRecursionCount ?? string.Empty).Append('\n');
            text.Append("generated-namespace=").Append(request.GeneratedNamespace ?? string.Empty).Append('\n');
            foreach (var template in templates)
            {
                text.Append("template=").Append(template.Item.Path).Append('|')
                    .Append(template.Item.Key ?? string.Empty).Append('|')
                    .Append(template.Item.Name ?? string.Empty).Append('|')
                    .Append(template.Item.ModelType ?? string.Empty).Append('|')
                    .Append(template.Item.OutputProfile ?? string.Empty).Append('|')
                    .Append(template.ContentHash ?? string.Empty).Append('\n');
            }

            foreach (var import in imports)
            {
                string hash;
                try
                {
                    hash = ContentHash(request, import.Path);
                }
                catch (IOException)
                {
                    hash = "<unreadable>";
                }

                text.Append("import-only=").Append(import.Path).Append('|')
                    .Append(import.Key ?? string.Empty).Append('|')
                    .Append(import.Name ?? string.Empty).Append('|')
                    .Append(hash).Append('\n');
            }

            foreach (var reference in request.References)
                text.Append("reference=").Append(reference).Append('|')
                    .Append(MvidHex(reference)).Append('\n');
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var hex = new StringBuilder(digest.Length * 2);
                foreach (var b in digest)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        internal static string ContentHash(CompileRequest request, string path)
        {
            string full = Resolve(request, path);
            byte[] bytes = File.ReadAllBytes(full);
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(bytes);
                var hex = new StringBuilder(digest.Length * 2);
                foreach (var b in digest)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        internal static string Resolve(CompileRequest request, string path) =>
            Path.IsPathRooted(path) ? path : Path.Combine(request.Root ?? string.Empty, path);

        internal static string Read(string stampPath)
        {
            try
            {
                return File.ReadAllText(stampPath, Encoding.UTF8).Trim();
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        internal static void Write(string stampPath, string digest)
        {
            string directory = Path.GetDirectoryName(stampPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(stampPath, digest + "\n", new UTF8Encoding(false));
        }

        private static string MvidHex(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new PEReader(stream))
                {
                    var module = reader.GetMetadataReader().GetModuleDefinition();
                    var mvid = reader.GetMetadataReader().GetGuid(module.Mvid);
                    return mvid.ToString("N");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                ex is BadImageFormatException)
            {
                // Not a managed image (or unreadable): fall back to the file's own hash so a
                // changed native image still invalidates the stamp.
                try
                {
                    using (var sha = SHA256.Create())
                    {
                        var digest = sha.ComputeHash(File.ReadAllBytes(path));
                        var hex = new StringBuilder(digest.Length * 2);
                        foreach (var b in digest)
                            hex.Append(b.ToString("x2"));
                        return "file:" + hex;
                    }
                }
                catch (IOException)
                {
                    return "<unreadable>";
                }
                catch (UnauthorizedAccessException)
                {
                    return "<unreadable>";
                }
            }
        }
    }
}
