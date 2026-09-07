using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Heddle.Tool.Compile
{
    /// <summary>One <c>--template</c> item: the file plus the <c>path|key|name|modelType|outputProfile</c>
    /// fields (empty fields allowed; an empty key means path-derived).</summary>
    internal sealed class TemplateItem
    {
        internal string Path;
        internal string Key;
        internal string Name;
        internal string ModelType;
        internal string OutputProfile;
    }

    /// <summary>One <c>--import-only</c> item: the file plus the <c>path|key|name</c> fields.</summary>
    internal sealed class ImportOnlyItem
    {
        internal string Path;
        internal string Key;
        internal string Name;
    }

    /// <summary>The parsed <c>heddle compile</c> request: one argument per line, UTF-8, verbatim —
    /// a response file survives long item lists on Windows where a command line does not.</summary>
    internal sealed class CompileRequest
    {
        internal string Project;
        internal string Root;
        internal string OutputProfile;
        internal string ExpressionMode;
        internal string TrimDirectiveLines;
        internal string MaxRecursionCount;
        internal string GeneratedNamespace;
        internal readonly List<TemplateItem> Templates = new List<TemplateItem>();
        internal readonly List<ImportOnlyItem> ImportOnly = new List<ImportOnlyItem>();
        internal readonly List<string> References = new List<string>();
        internal string EngineReference;
        internal string BuildVersion;
        internal string ArtifactOut;
        internal string SourceOut;
        internal string Stamp;
        internal string Probe;
        internal string StubsOnly;
    }

    /// <summary>Parses <c>compile @&lt;rsp&gt;</c> and the direct-argument twin the tests drive.
    /// Throws <see cref="ResponseFileException"/> (exit 2) on usage/response-file errors.</summary>
    internal sealed class ResponseFileException : Exception
    {
        internal ResponseFileException(string message)
            : base(message)
        {
        }
    }

    internal static class ResponseFile
    {
        internal static CompileRequest Parse(string[] args)
        {
            if (args.Length == 2 && args[1].StartsWith("@", StringComparison.Ordinal))
                return ParseLines(ReadResponseFile(args[1].Substring(1)));
            var rest = new string[args.Length - 1];
            Array.Copy(args, 1, rest, 0, rest.Length);
            return ParseLines(rest);
        }

        private static string[] ReadResponseFile(string path)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new ResponseFileException("Response file '" + path + "' could not be read: " +
                    ex.Message + ".");
            }

            var result = new List<string>();
            foreach (var line in lines)
            {
                // Verbatim arguments: no quoting, no comments, no continuation. A blank line
                // carries no argument and would silently shift every --flag value pairing.
                if (line.Length == 0)
                    throw new ResponseFileException("Response file '" + path +
                        "' contains a blank line; one argument per line, no blanks.");
                result.Add(line);
            }

            return result.ToArray();
        }

        private static CompileRequest ParseLines(string[] tokens)
        {
            var request = new CompileRequest();
            for (int i = 0; i < tokens.Length; i++)
            {
                string flag = tokens[i];
                switch (flag)
                {
                    case "--project": request.Project = Take(tokens, ref i, flag); break;
                    case "--root": request.Root = Take(tokens, ref i, flag); break;
                    case "--output-profile": request.OutputProfile = Take(tokens, ref i, flag); break;
                    case "--expression-mode": request.ExpressionMode = Take(tokens, ref i, flag); break;
                    case "--trim-directive-lines":
                        request.TrimDirectiveLines = Take(tokens, ref i, flag);
                        break;
                    case "--max-recursion-count":
                        request.MaxRecursionCount = Take(tokens, ref i, flag);
                        break;
                    case "--generated-namespace":
                        request.GeneratedNamespace = Take(tokens, ref i, flag);
                        break;
                    case "--template": request.Templates.Add(ParseTemplate(Take(tokens, ref i, flag))); break;
                    case "--import-only":
                        request.ImportOnly.Add(ParseImportOnly(Take(tokens, ref i, flag)));
                        break;
                    case "--reference": request.References.Add(Take(tokens, ref i, flag)); break;
                    case "--engine-reference":
                        request.EngineReference = Take(tokens, ref i, flag);
                        break;
                    case "--build-version": request.BuildVersion = Take(tokens, ref i, flag); break;
                    case "--artifact-out": request.ArtifactOut = Take(tokens, ref i, flag); break;
                    case "--source-out": request.SourceOut = Take(tokens, ref i, flag); break;
                    case "--stamp": request.Stamp = Take(tokens, ref i, flag); break;
                    case "--probe": request.Probe = Take(tokens, ref i, flag); break;
                    case "--stubs-only": request.StubsOnly = Take(tokens, ref i, flag); break;
                    default:
                        throw new ResponseFileException("Unknown compile option '" + flag + "'.");
                }
            }

            return request;
        }

        private static string Take(string[] tokens, ref int i, string flag)
        {
            if (i + 1 >= tokens.Length)
                throw new ResponseFileException("Missing value for option '" + flag + "'.");
            return tokens[++i];
        }

        private static TemplateItem ParseTemplate(string raw)
        {
            // path|key|name|modelType|outputProfile; empty fields allowed, trailing fields omittable.
            var fields = raw.Split('|');
            if (fields.Length == 0 || fields.Length > 5 || string.IsNullOrEmpty(fields[0]))
                throw new ResponseFileException("Malformed --template item '" + raw +
                    "'; expected <path>|<key>|<name>|<modelType>|<outputProfile>.");
            var item = new TemplateItem { Path = fields[0] };
            if (fields.Length > 1)
                item.Key = fields[1];
            if (fields.Length > 2)
                item.Name = fields[2];
            if (fields.Length > 3)
                item.ModelType = fields[3];
            if (fields.Length > 4)
                item.OutputProfile = fields[4];
            return item;
        }

        private static ImportOnlyItem ParseImportOnly(string raw)
        {
            // path|key|name; empty fields allowed, trailing fields omittable.
            var fields = raw.Split('|');
            if (fields.Length == 0 || fields.Length > 3 || string.IsNullOrEmpty(fields[0]))
                throw new ResponseFileException("Malformed --import-only item '" + raw +
                    "'; expected <path>|<key>|<name>.");
            var item = new ImportOnlyItem { Path = fields[0] };
            if (fields.Length > 1)
                item.Key = fields[1];
            if (fields.Length > 2)
                item.Name = fields[2];
            return item;
        }
    }
}
