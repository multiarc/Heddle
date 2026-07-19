using System;
using System.IO;

namespace Heddle.Samples
{
    /// <summary>
    /// Shared capture-mode plumbing for every gallery sample (phase 9 D11). Linked into each sample csproj so the
    /// `--capture out` convention resolves identically everywhere: the directory is taken relative to the sample's
    /// own source folder (discovered from the running assembly), not the shell's current directory — so
    /// `dotnet run --project samples/X -- --capture out` writes to `samples/X/out` no matter where it was invoked.
    /// </summary>
    internal static class SampleCapture
    {
        /// <summary>Returns the absolute capture directory (created) when `--capture &lt;dir&gt;` is present, else null.</summary>
        public static string Resolve(string[] args)
        {
            string value = null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--capture")
                {
                    value = args[i + 1];
                    break;
                }
            }

            if (value == null)
                return null;

            var dir = Path.IsPathRooted(value) ? value : Path.Combine(SampleRoot(), value);
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>The sample's source directory — the nearest ancestor of the running assembly that contains a
        /// <c>*.csproj</c> (bin/Release/&lt;tfm&gt; walks up to the project folder).</summary>
        public static string SampleRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (dir.GetFiles("*.csproj").Length > 0)
                    return dir.FullName;
                dir = dir.Parent;
            }

            // Fallback: the app base directory (keeps capture working even from an unexpected layout).
            return AppContext.BaseDirectory;
        }

        public static void Write(string captureDir, string relativeName, string content)
        {
            var path = Path.Combine(captureDir, relativeName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }
    }
}
