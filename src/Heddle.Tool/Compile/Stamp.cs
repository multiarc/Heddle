using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;

namespace Heddle.Tool.Compile
{
    /// <summary>The incrementality stamp: SHA-256 over the build version, every option value — the
    /// template root included, which decides every key the artifact records and every file the import
    /// disk fallback reaches — one row per <c>--template</c>/<c>--import-only</c> item carrying its
    /// content hash, one row per file the compile read off disk to serve an <c>@&lt;&lt;</c> import,
    /// and the MVID of every <c>--reference</c> image. Item and disk-import rows are ordinal-sorted,
    /// so neither item nor response-file order can change the digest. A rebuilt referenced project
    /// with an unchanged MVID (deterministic build) runs the host but compiles nothing; artifact bytes
    /// are therefore a function of exactly these inputs.</summary>
    internal static class Stamp
    {
        /// <param name="diskImports">The files served off disk. A row carrying a content hash is used as
        /// given — that is the text the compile was handed — and one carrying none is read now, which is
        /// what makes a check a check.</param>
        /// <param name="unrecorded">True when the compile read something those rows do not describe. The
        /// digest is salted, so it certifies nothing and the next compile runs.</param>
        internal static string Compute(string buildVersion, CompileRequest request,
            IReadOnlyList<TemplateInput> templates, IReadOnlyList<DiskImportRead> diskImports,
            bool unrecorded)
        {
            var text = new StringBuilder();
            text.Append("build-version=").Append(buildVersion ?? string.Empty).Append('\n');
            text.Append("root=").Append(request.Root ?? string.Empty).Append('\n');
            text.Append("output-profile=").Append(request.OutputProfile ?? string.Empty).Append('\n');
            text.Append("expression-mode=").Append(request.ExpressionMode ?? string.Empty).Append('\n');
            text.Append("trim-directive-lines=").Append(request.TrimDirectiveLines ?? string.Empty).Append('\n');
            text.Append("max-recursion-count=").Append(request.MaxRecursionCount ?? string.Empty).Append('\n');
            text.Append("generated-namespace=").Append(request.GeneratedNamespace ?? string.Empty).Append('\n');
            text.Append("assembly-name=").Append(request.AssemblyName ?? string.Empty).Append('\n');
            bool unreadable = false;
            var rows = new List<string>();
            // Every item once, opted-out ones under their own prefix: the two populations differ by an
            // entry point, which no other field on the row records. The file is read for its content
            // hash exactly once, at intake.
            foreach (var template in templates)
            {
                var row = new StringBuilder();
                row.Append(template.Item.IsImportOnly ? "import-only=" : "template=")
                    .Append(template.Item.Path).Append('|')
                    .Append(template.Item.Key ?? string.Empty).Append('|')
                    .Append(template.Item.Name ?? string.Empty).Append('|')
                    .Append(template.Item.ModelType ?? string.Empty).Append('|')
                    .Append(template.Item.OutputProfile ?? string.Empty).Append('|')
                    .Append(template.ContentHash ?? string.Empty);
                rows.Add(row.ToString());
            }

            if (diskImports != null)
                foreach (var import in diskImports)
                    rows.Add("disk-import=" + import.Path + "|" +
                        (import.ContentHash ?? DiskImportHash(import.Path, ref unreadable)));
            rows.Sort(StringComparer.Ordinal);
            foreach (var row in rows)
                text.Append(row).Append('\n');
            foreach (var reference in request.References)
                text.Append("reference=").Append(reference).Append('|')
                    .Append(MvidHex(reference, ref unreadable)).Append('\n');
            // An input that exists but cannot be read is never hashed to a placeholder, which would make
            // every such input look unchanged for ever: the digest is salted instead, so the compile runs.
            if (unreadable)
                text.Append("unreadable=").Append(Guid.NewGuid().ToString("N")).Append('\n');
            if (unrecorded)
                text.Append("unrecorded=").Append(Guid.NewGuid().ToString("N")).Append('\n');
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var hex = new StringBuilder(digest.Length * 2);
                foreach (var b in digest)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>The content hash of a file the compile reached only through the import disk fallback,
        /// over the decoded text — the same identity the item rows carry, and the same text the reader
        /// hands the parse, so the check and the record describe one thing.</summary>
        private static string DiskImportHash(string path, ref bool unreadable)
        {
            try
            {
                return Heddle.Precompiled.ContentHash.HashText(File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return UnreadableToken(ex, ref unreadable);
            }
        }

        /// <summary>What a file that would not be read contributes. Absence is a fact and keeps a stable
        /// token — it changes the moment the file appears, so a build whose input is simply not there can
        /// still go up to date. Anything else is not a fact at all: it salts the digest rather than
        /// standing in for one, because a placeholder would make every such input look unchanged for
        /// ever. Every site that hashes a file in this stamp classifies through here.</summary>
        private static string UnreadableToken(Exception ex, ref bool unreadable)
        {
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                return "<missing>";
            unreadable = true;
            return "<unreadable>";
        }

        private static string Hex(byte[] digest)
        {
            var hex = new StringBuilder(digest.Length * 2);
            foreach (var b in digest)
                hex.Append(b.ToString("x2"));
            return hex.ToString();
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

        /// <summary>Writes the digest — or, when the compile could not describe what it read, removes the
        /// stamp instead. The target's outputs are then incomplete, so the next build runs it again
        /// rather than inheriting an artifact no digest describes; a salted digest alone would stop the
        /// host trusting the artifact without ever getting the host invoked. A stamp that will not be
        /// removed keeps the salted digest, which matches no later check either.</summary>
        internal static void Write(string stampPath, string digest, bool certified)
        {
            string directory = Path.GetDirectoryName(stampPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            if (!certified)
            {
                try
                {
                    File.Delete(stampPath);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }

            File.WriteAllText(stampPath, digest + "\n", new UTF8Encoding(false));
        }

        private static string MvidHex(string path, ref bool unreadable)
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
                        return "file:" + Hex(sha.ComputeHash(File.ReadAllBytes(path)));
                }
                catch (Exception inner) when (inner is IOException || inner is UnauthorizedAccessException)
                {
                    return UnreadableToken(inner, ref unreadable);
                }
            }
        }
    }
}
