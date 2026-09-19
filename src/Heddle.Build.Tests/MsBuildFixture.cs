using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>The outcome of one fixture <c>dotnet</c> invocation: exit code plus the merged
    /// stdout/stderr text the assertions read.</summary>
    public sealed class BuildResult
    {
        public int Exit;
        public string Output = string.Empty;

        public void AssertSuccess(string what)
        {
            Assert.True(Exit == 0, what + " failed with exit " + Exit + ":\n" + Output);
        }
    }

    /// <summary>Writes fixture projects to a fresh temp directory and runs <c>dotnet build</c> against
    /// the in-repo <c>Heddle.Build</c> props/targets and the built <c>Heddle.Tool</c>. Every build goes
    /// through one process-wide gate: concurrent fixture builds would otherwise contend on the shared
    /// in-repo outputs (the referenced <c>Heddle</c> build) and interleave their logs.</summary>
    public sealed class MsBuildFixture : IDisposable
    {
        private static readonly object Gate = new object();

        /// <summary>The fixture root: one fresh temp directory per fixture, removed on dispose.</summary>
        public string Root { get; }

        public MsBuildFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "heddle-build-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Writes <paramref name="content"/> under the fixture root with LF newlines.</summary>
        public string Write(string relative, string content)
        {
            string full = Path.Combine(Root, relative);
            string directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(full, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
            return full;
        }

        /// <summary>Runs <c>dotnet</c> with <paramref name="arguments"/> in <paramref name="workdir"/>
        /// (the fixture root by default) and captures the merged output.</summary>
        public BuildResult Dotnet(string arguments, string workdir = null)
        {
            lock (Gate)
            {
                var start = new ProcessStartInfo("dotnet", arguments)
                {
                    WorkingDirectory = workdir ?? Root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                var output = new StringBuilder();
                using (var process = new Process { StartInfo = start })
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                            output.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                            output.AppendLine(e.Data);
                    };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    return new BuildResult { Exit = process.ExitCode, Output = output.ToString() };
                }
            }
        }

        /// <summary>Builds <paramref name="projectPath"/> with the in-repo task and tool pinned, at
        /// normal verbosity so target skip lines are visible. <c>-m:1</c> keeps the build on one node
        /// for deterministic output.</summary>
        public BuildResult Build(string projectPath, params string[] extraArgs)
        {
            string args = "build \"" + projectPath + "\" -c " + Configuration + " -m:1 -v:n"
                + " /p:HeddleBuildTasksPath=\"" + TasksDll + "\""
                + " /p:HeddleToolPath=\"" + ToolDll + "\"";
            foreach (string extra in extraArgs)
                args += " " + extra;
            return Dotnet(args, Path.GetDirectoryName(projectPath));
        }

        /// <summary>Builds like <see cref="Build"/> and also writes an MSBuild binary log at
        /// <paramref name="binlogPath"/> (P2-R11: the incrementality facts are asserted on the binary
        /// log, replayed through <see cref="ReplayBinlog"/>, not only on console text).</summary>
        public BuildResult BuildWithBinlog(string projectPath, string binlogPath, params string[] extraArgs)
        {
            var args = new string[extraArgs.Length + 1];
            extraArgs.CopyTo(args, 0);
            args[extraArgs.Length] = "-bl:\"" + binlogPath + "\"";
            return Build(projectPath, args);
        }

        /// <summary>Replays a binary log through MSBuild's console logger at detailed verbosity (the
        /// level at which a replay surfaces target-skip messages) and returns the text, so the
        /// incrementality facts are asserted on what the log recorded.</summary>
        public BuildResult ReplayBinlog(string binlogPath)
        {
            return Dotnet("msbuild \"" + binlogPath + "\" -v:d -nologo", Path.GetDirectoryName(binlogPath));
        }

        /// <summary>The fixture's built assembly for <paramref name="assemblyName"/> under
        /// <paramref name="dir"/>: the one <c>bin/**/*.dll</c> with that name.</summary>
        public static string BuiltAssembly(string dir, string assemblyName)
        {
            string[] found = Directory.GetFiles(Path.Combine(dir, "bin"), assemblyName + ".dll", SearchOption.AllDirectories);
            Assert.True(found.Length == 1, "Expected one built " + assemblyName + ".dll under " + dir + "/bin, found " + found.Length + ".");
            return found[0];
        }

        /// <summary>The single <c>Heddle.CompiledForm.bin</c> under <paramref name="dir"/>.</summary>
        public static string Artifact(string dir)
        {
            string[] found = Directory.GetFiles(dir, "Heddle.CompiledForm.bin", SearchOption.AllDirectories);
            Assert.True(found.Length == 1, "Expected one artifact under " + dir + ", found " + found.Length + ".");
            return found[0];
        }

        /// <summary>The single <c>stamp.txt</c> under <paramref name="dir"/>.</summary>
        public static string Stamp(string dir)
        {
            string[] found = Directory.GetFiles(dir, "stamp.txt", SearchOption.AllDirectories);
            Assert.True(found.Length == 1, "Expected one stamp under " + dir + ", found " + found.Length + ".");
            return found[0];
        }

        /// <summary>The single <c>Heddle.CompiledForm.g.cs</c> under <paramref name="dir"/>.</summary>
        public static string GeneratedSource(string dir)
        {
            string[] found = Directory.GetFiles(dir, "Heddle.CompiledForm.g.cs", SearchOption.AllDirectories);
            Assert.True(found.Length == 1, "Expected one generated source under " + dir + ", found " + found.Length + ".");
            return found[0];
        }

        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var digest = sha.ComputeHash(stream);
                var hex = new StringBuilder(digest.Length * 2);
                foreach (byte b in digest)
                    hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>Composes a fixture SDK project: the in-repo props/targets imported by absolute
        /// path, an optional project reference to the in-repo engine, and the caller's items.</summary>
        public static string ProjectXml(string targetFrameworks, string properties, string items, bool heddleReference = true)
        {
            string tfm = targetFrameworks.Contains(";")
                ? "<TargetFrameworks>" + targetFrameworks + "</TargetFrameworks>"
                : "<TargetFramework>" + targetFrameworks + "</TargetFramework>";
            string reference = heddleReference
                ? "<ProjectReference Include=\"" + HeddleProject + "\" />"
                : string.Empty;
            return "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
                + "  <PropertyGroup>\n"
                + "    " + tfm + "\n"
                + "    <EnableDefaultHeddleTemplates>false</EnableDefaultHeddleTemplates>\n"
                + "    <Nullable>disable</Nullable>\n"
                + "    <LangVersion>latest</LangVersion>\n"
                + properties
                + "  </PropertyGroup>\n"
                + "  <Import Project=\"" + PropsPath + "\" />\n"
                + "  <Import Project=\"" + TargetsPath + "\" />\n"
                + "  <ItemGroup>\n"
                + "    " + reference + "\n"
                + items
                + "  </ItemGroup>\n"
                + "</Project>\n";
        }

        public static string RepoRoot
        {
            get
            {
                string dir = AppContext.BaseDirectory;
                for (int i = 0; i < 12; i++)
                {
                    if (File.Exists(Path.Combine(dir, "Heddle.sln"))
                        && Directory.Exists(Path.Combine(dir, "src", "Heddle.Build")))
                        return dir;
                    string parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
                    if (string.IsNullOrEmpty(parent) || parent == dir)
                        break;
                    dir = parent;
                }

                throw new InvalidOperationException(
                    "Could not locate the repository root from " + AppContext.BaseDirectory + ".");
            }
        }

        public static string PropsPath => ForOs(Path.Combine(RepoRoot, "src", "Heddle.Build", "build", "Heddle.Build.props"));

        public static string TargetsPath => ForOs(Path.Combine(RepoRoot, "src", "Heddle.Build", "build", "Heddle.Build.targets"));

        public static string HeddleProject => ForOs(Path.Combine(RepoRoot, "src", "Heddle", "Heddle.csproj"));

        public static string Configuration =>
            typeof(MsBuildFixture).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";

        public static string TasksDll => ToolOrTasks("Heddle.Build", "netstandard2.0", "Heddle.Build.Tasks.dll");

        public static string ToolDll => ToolOrTasks("Heddle.Tool", "net10.0", "Heddle.Tool.dll");

        private static string ToolOrTasks(string project, string tfm, string file)
        {
            foreach (string candidate in new[] { Configuration, "Release", "Debug" }.Distinct())
            {
                string path = Path.Combine(RepoRoot, "src", project, "bin", candidate, tfm, file);
                if (File.Exists(path))
                    return ForOs(path);
            }

            throw new InvalidOperationException("Could not find " + file + " under src/" + project + "/bin.");
        }

        private static string ForOs(string path) => path.Replace('\\', '/');
    }
}
