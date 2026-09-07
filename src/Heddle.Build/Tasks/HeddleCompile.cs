using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Heddle.Build.Tasks
{
    /// <summary>The MSBuild <c>HeddleCompile</c> task: writes the <c>heddle compile</c> response file
    /// and runs the out-of-process host under a .NET 10 (or later) runtime. The host's stdout already
    /// speaks the canonical MSBuild diagnostic format, so <see cref="ToolTask"/> parses it with no
    /// custom code; a missing runtime surfaces as MSBuild's own MSB6006 with the runtime's message.
    /// Modes: full compile (artifact, source, stamp), <c>--probe</c> (probe JSON plus the unresolved
    /// count), and <c>--stubs-only</c> (wrapper stubs plus the intermediate-compile digest).</summary>
    public class HeddleCompile : ToolTask
    {
        /// <summary>Templates to precompile. Metadata: Key, Name, ModelType, OutputProfile (empty allowed).</summary>
        public ITaskItem[] Templates { get; set; }

        /// <summary><c>Precompile="false"</c> items: import map only, no entry. Metadata: Key, Name.</summary>
        public ITaskItem[] ImportOnly { get; set; }

        /// <summary>The compiling project file (response <c>--project</c>; project facts anchor here).</summary>
        public string ProjectPath { get; set; }

        /// <summary>Response <c>--root</c>; defaults to the project directory.</summary>
        public string TemplateRoot { get; set; }

        public string OutputProfile { get; set; }

        public string ExpressionMode { get; set; }

        public string TrimDirectiveLines { get; set; }

        public string MaxRecursionCount { get; set; }

        public string GeneratedNamespace { get; set; }

        /// <summary>Implementation images: project-reference outputs, package runtime images, declared
        /// assemblies, and the intermediate model assembly when present.</summary>
        public ITaskItem[] ImplementationImages { get; set; }

        /// <summary>The resolved <c>Heddle.dll</c> reference the version lock checks the host against.</summary>
        public string EngineReference { get; set; }

        /// <summary>Response <c>--build-version</c>; defaults to this task assembly's version (the
        /// <c>Heddle.Build</c> package version).</summary>
        public string BuildVersion { get; set; }

        /// <summary>Response <c>--artifact-out</c>.</summary>
        public string ArtifactPath { get; set; }

        /// <summary>Response <c>--source-out</c>.</summary>
        public string SourcePath { get; set; }

        /// <summary>Response <c>--stamp</c>.</summary>
        public string StampPath { get; set; }

        /// <summary>Response <c>--probe</c>. Probe mode: no artifact, source or stamp.</summary>
        public string ProbeJson { get; set; }

        /// <summary>Response <c>--stubs-only</c>. Stubs mode: no engine compile, image load, artifact,
        /// source or stamp.</summary>
        public string StubsOnly { get; set; }

        /// <summary>Sources the intermediate model compile covers (digest input).</summary>
        public ITaskItem[] CompileSources { get; set; }

        /// <summary>Reference images the intermediate digest covers (MVID input).</summary>
        public ITaskItem[] DigestReferences { get; set; }

        /// <summary>Analyzer paths the intermediate digest covers.</summary>
        public ITaskItem[] Analyzers { get; set; }

        /// <summary>The host tool assembly (<c>Heddle.Tool.dll</c> under <c>tools/net10.0/any/</c>).</summary>
        public string ToolPath { get; set; }

        /// <summary>Where to write the response file; defaults beside the stamp path.</summary>
        public string ResponseFilePath { get; set; }

        /// <summary>Override for the .NET host; defaults to <c>DOTNET_HOST_PATH</c>, else <c>dotnet</c>.</summary>
        public string DotnetPath { get; set; }

        /// <summary>Probe mode output: spellings no image resolves.</summary>
        [Output]
        public int UnresolvedCount { get; set; }

        /// <summary>Stubs mode output: the intermediate-compile content-addressed digest.</summary>
        [Output]
        public string IntermediateDigest { get; set; }

        protected override string ToolName => "dotnet";

        protected override string GenerateFullPathToTool()
        {
            string configured = DotnetPath;
            if (!string.IsNullOrEmpty(configured))
                return configured;
            string hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            return string.IsNullOrEmpty(hostPath) ? "dotnet" : hostPath;
        }

        protected override string GenerateCommandLineCommands()
        {
            var command = new StringBuilder();
            command.Append('"').Append(ToolPath).Append('"');
            command.Append(" compile @\"").Append(_responseFile).Append('"');
            return command.ToString();
        }

        private string _responseFile;

        public override bool Execute()
        {
            try
            {
                if (string.IsNullOrEmpty(ToolPath))
                {
                    Log.LogError("HeddleToolPath is not set; it must point at the built Heddle.Tool.dll.");
                    return false;
                }

                string version = BuildVersion;
                if (string.IsNullOrEmpty(version))
                    version = GetType().Assembly.GetName().Version?.ToString() ?? string.Empty;
                string root = TemplateRoot;
                if (string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(ProjectPath))
                    root = Path.GetDirectoryName(Path.GetFullPath(ProjectPath));

                // One argument per line, verbatim: the host reads the file, never a command line.
                // Empty optionals are omitted, never written as blank lines (the host rejects those).
                var args = new List<string>();
                Add(args, "--project", ProjectPath ?? string.Empty);
                Add(args, "--root", root ?? string.Empty);
                AddIfPresent(args, "--output-profile", OutputProfile);
                AddIfPresent(args, "--expression-mode", ExpressionMode);
                AddIfPresent(args, "--trim-directive-lines", TrimDirectiveLines);
                AddIfPresent(args, "--max-recursion-count", MaxRecursionCount);
                AddIfPresent(args, "--generated-namespace", GeneratedNamespace);
                Add(args, "--build-version", version);
                if (Templates != null)
                    foreach (var template in Templates)
                        Add(args, "--template", ItemLine(template, true));
                if (ImportOnly != null)
                    foreach (var import in ImportOnly)
                        Add(args, "--import-only", ItemLine(import, false));
                if (ImplementationImages != null)
                    foreach (var image in ImplementationImages)
                        Add(args, "--reference", FullPath(root, image.ItemSpec));
                if (!string.IsNullOrEmpty(EngineReference))
                    Add(args, "--engine-reference", EngineReference);
                if (ProbeJson != null)
                    Add(args, "--probe", ProbeJson);
                else if (StubsOnly != null)
                    Add(args, "--stubs-only", StubsOnly);
                else
                {
                    Add(args, "--artifact-out", ArtifactPath);
                    Add(args, "--source-out", SourcePath);
                    Add(args, "--stamp", StampPath);
                }

                _responseFile = ResponseFilePath;
                if (string.IsNullOrEmpty(_responseFile))
                {
                    string anchor = StampPath ?? ProbeJson ?? StubsOnly ?? Path.GetTempPath();
                    string directory = Directory.Exists(anchor) ? anchor : Path.GetDirectoryName(anchor);
                    _responseFile = Path.Combine(directory ?? Path.GetTempPath(), "compile.rsp");
                }

                string responseDirectory = Path.GetDirectoryName(_responseFile);
                if (!string.IsNullOrEmpty(responseDirectory))
                    Directory.CreateDirectory(responseDirectory);
                File.WriteAllLines(_responseFile, args.ToArray(), new UTF8Encoding(false));

                if (StubsOnly != null)
                    IntermediateDigest = ComputeIntermediateDigest(root);
                bool ok = base.Execute();
                if (ok && ProbeJson != null)
                    UnresolvedCount = CountUnresolved(ProbeJson);
                return ok;
            }
            catch (Exception ex)
            {
                Log.LogError("HeddleCompile failed before the host ran: {0}", ex.Message);
                return false;
            }
        }

        private static void Add(List<string> args, string flag, string value)
        {
            args.Add(flag);
            args.Add(value ?? string.Empty);
        }

        private static void AddIfPresent(List<string> args, string flag, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            args.Add(flag);
            args.Add(value);
        }

        private static string ItemLine(ITaskItem item, bool withProfile)
        {
            var line = new StringBuilder(item.ItemSpec);
            line.Append('|').Append(item.GetMetadata("Key") ?? string.Empty);
            line.Append('|').Append(item.GetMetadata("Name") ?? string.Empty);
            if (withProfile)
            {
                line.Append('|').Append(item.GetMetadata("ModelType") ?? string.Empty);
                line.Append('|').Append(item.GetMetadata("OutputProfile") ?? string.Empty);
            }

            return line.ToString();
        }

        private static string FullPath(string root, string path) =>
            Path.IsPathRooted(path) ? path : Path.Combine(root ?? string.Empty, path);

        /// <summary>SHA-256 over the source paths and content hashes, the reference MVIDs and the
        /// analyzer paths: the intermediate-compile content address.</summary>
        private string ComputeIntermediateDigest(string root)
        {
            var text = new StringBuilder();
            if (CompileSources != null)
                foreach (var source in CompileSources)
                {
                    string full = FullPath(root, source.ItemSpec);
                    text.Append("source=").Append(source.ItemSpec).Append('|')
                        .Append(FileHash(full)).Append('\n');
                }

            if (DigestReferences != null)
                foreach (var reference in DigestReferences)
                    text.Append("reference=").Append(reference.ItemSpec).Append('|')
                        .Append(MvidHex(FullPath(root, reference.ItemSpec))).Append('\n');
            if (Analyzers != null)
                foreach (var analyzer in Analyzers)
                    text.Append("analyzer=").Append(analyzer.ItemSpec).Append('\n');
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var hex = new StringBuilder(digest.Length * 2);
                foreach (var b in digest)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        private static string FileHash(string path)
        {
            try
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var digest = sha.ComputeHash(File.ReadAllBytes(path));
                    var hex = new StringBuilder(digest.Length * 2);
                    foreach (var b in digest)
                        hex.Append(b.ToString("x2"));
                    return hex.ToString();
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

        private static string MvidHex(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new PEReader(stream))
                {
                    var metadata = reader.GetMetadataReader();
                    return metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString("N");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                ex is BadImageFormatException || ex is ArgumentException)
            {
                return "file:" + FileHash(path);
            }
        }

        /// <summary>Counts the top-level <c>"unresolved"</c> string array of the probe JSON the host
        /// just wrote. Minimal on purpose: the host owns the schema, the task only needs the count
        /// that decides the intermediate compile.</summary>
        private static int CountUnresolved(string probeJson)
        {
            string json;
            try
            {
                json = File.ReadAllText(probeJson, Encoding.UTF8);
            }
            catch (IOException)
            {
                return 0;
            }

            int anchor = json.IndexOf("\"unresolved\":[", StringComparison.Ordinal);
            if (anchor < 0)
                return 0;
            int count = 0;
            bool inString = false;
            for (int i = anchor + "\"unresolved\":[".Length; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\')
                        i++;
                    else if (c == '"')
                        inString = false;
                }
                else if (c == '"')
                {
                    inString = true;
                    count++;
                }
                else if (c == ']')
                {
                    break;
                }
            }

            return count;
        }
    }
}
