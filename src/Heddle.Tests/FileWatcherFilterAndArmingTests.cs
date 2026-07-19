using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Shared plumbing for the phase 1 file-watcher fixtures: a fresh temp directory per test (the
    /// committed <c>TestTemplate/</c> fixtures are read-only inputs and are never rewritten), reflection
    /// access to the private watcher/handler members (the deterministic tests drive the exact production
    /// <c>Reload()</c> path synchronously), and a polling <c>WaitFor</c> for the FSW-timed tests.
    /// </summary>
    internal static class FileWatcherTestSupport
    {
        public static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "heddle-fw-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void CleanupDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch (IOException)
            {
                // best-effort: a watcher handle may briefly outlive the test on slow teardown
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static TemplateOptions WatchOptions(string dir, string stem)
        {
            return new TemplateOptions(stem)
            {
                RootPath = dir,
                FileNamePostfix = ".heddle",
                EnableFileChangeCheck = true,
            };
        }

        public static FileSystemWatcher GetWatcher(HeddleTemplate template)
        {
            var field = typeof(HeddleTemplate).GetField("_watcher", BindingFlags.NonPublic | BindingFlags.Instance);
            return (FileSystemWatcher)field.GetValue(template);
        }

        /// <summary>Disables the real watcher so a deterministic test's file rewrite cannot race the
        /// reflection-invoked handler with an OS-delivered duplicate event.</summary>
        public static void Disarm(HeddleTemplate template)
        {
            var watcher = GetWatcher(template);
            if (watcher != null)
                watcher.EnableRaisingEvents = false;
        }

        private static void Invoke(HeddleTemplate template, string handler, params object[] args)
        {
            var method = typeof(HeddleTemplate).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(template, args);
        }

        public static void InvokeChanged(HeddleTemplate template, string dir, string fileName)
            => Invoke(template, "FileChanged", null, new FileSystemEventArgs(WatcherChangeTypes.Changed, dir, fileName));

        public static void InvokeCreated(HeddleTemplate template, string dir, string fileName)
            => Invoke(template, "FileCreated", null, new FileSystemEventArgs(WatcherChangeTypes.Created, dir, fileName));

        public static void InvokeDeleted(HeddleTemplate template, string dir, string fileName)
            => Invoke(template, "FileDeleted", null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, dir, fileName));

        public static void InvokeRenamed(HeddleTemplate template, string dir, string newName, string oldName)
            => Invoke(template, "FileRenamed", null, new RenamedEventArgs(WatcherChangeTypes.Renamed, dir, newName, oldName));

        public static System.Collections.Concurrent.ConcurrentQueue<RuntimeDocument> GetSupersededQueue(HeddleTemplate template)
        {
            var field = typeof(HeddleTemplate).GetField("_supersededDocs", BindingFlags.NonPublic | BindingFlags.Instance);
            return (System.Collections.Concurrent.ConcurrentQueue<RuntimeDocument>)field.GetValue(template);
        }

        public static RuntimeDocument GetRuntimeDocument(HeddleTemplate template)
        {
            var field = typeof(HeddleTemplate).GetField("_runtimeDocument", BindingFlags.NonPublic | BindingFlags.Instance);
            return (RuntimeDocument)field.GetValue(template);
        }

        /// <summary>Retries a file operation that can transiently collide with the engine's own reload read
        /// (the watcher callback holds the file open briefly while re-reading it) — the same sharing-violation
        /// window real editors face on Windows.</summary>
        public static void RetryIO(Action operation, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    operation();
                    return;
                }
                catch (IOException) when (sw.ElapsedMilliseconds < timeoutMs)
                {
                    Thread.Sleep(25);
                }
            }
        }

        /// <summary>Polls <paramref name="condition"/> until it holds or the timeout elapses; returns whether
        /// it held. FSW-timed tests assert the return with context.</summary>
        public static bool WaitFor(Func<bool> condition, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                    return true;
                Thread.Sleep(25);
            }
            return condition();
        }
    }

    /// <summary>
    /// Phase 1 D1/D2 — the watcher filter is built from the same file name the reader reads
    /// (<c>TemplateName + FileNamePostfix</c>) and the watcher is armed (<c>EnableRaisingEvents</c>);
    /// non-file and flag-off compiles install no watcher at all.
    /// </summary>
    public class FileWatcherFilterAndArmingTests
    {
        /// <summary>D1 + D2: a file compile of <c>home</c> + <c>.heddle</c> with the flag on watches
        /// <c>home.heddle</c> (not the postfix-less <c>home</c> of the old bug) and is armed.</summary>
        [Fact]
        public void FilterEqualsTemplateNamePlusPostfix()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "home.heddle"), "HELLO");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, "home"));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

                var watcher = FileWatcherTestSupport.GetWatcher(template);
                Assert.NotNull(watcher);
                Assert.Equal("home.heddle", watcher.Filter);
                Assert.True(watcher.EnableRaisingEvents, "the watcher must be armed (EnableRaisingEvents)");
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>An inline-string compile never constructs a watcher.</summary>
        [Fact]
        public void InlineCompileInstallsNoWatcher()
        {
            using var template = new HeddleTemplate("inline", new CompileContext());
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Null(FileWatcherTestSupport.GetWatcher(template));
        }

        /// <summary>A file compile with <c>EnableFileChangeCheck = false</c> never constructs a watcher.</summary>
        [Fact]
        public void FlagOffFileCompileInstallsNoWatcher()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "home.heddle"), "HELLO");
                var options = FileWatcherTestSupport.WatchOptions(dir, "home");
                options.EnableFileChangeCheck = false;
                using var template = new HeddleTemplate(options);
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Null(FileWatcherTestSupport.GetWatcher(template));
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }
    }
}
