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
    /// <summary>Test support for file-watcher fixtures: temp directories, reflection access to private members, and polling.</summary>
    internal static class FileWatcherTestSupport
    {
        public static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "heddle-fw-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Per-test stem unique across concurrent and parallel test hosts; prevents cross-process watcher collision.</summary>
        public static string NewStem() => "home-" + Guid.NewGuid().ToString("N").Substring(0, 8);

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

        /// <summary>Disables the watcher to prevent race between reflection-invoked and OS-delivered events.</summary>
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

        /// <summary>Retries file operations that collide with the engine's reload read.</summary>
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

        /// <summary>Polls until <paramref name="condition"/> or timeout; FSW-timed tests assert the result.</summary>
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

    /// <summary>Tests watcher filter initialization and arming rules.</summary>
    public class FileWatcherFilterAndArmingTests
    {
        // Per-test watched-file stem: isolates this test from concurrent tests and parallel TFM hosts.
        private readonly string _stem = FileWatcherTestSupport.NewStem();

        /// <summary>Watches <c>home.heddle</c> (not postfix-less) and is armed when flag on.</summary>
        [Fact]
        public void FilterEqualsTemplateNamePlusPostfix()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, _stem + ".heddle"), "HELLO");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, _stem));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

                var watcher = FileWatcherTestSupport.GetWatcher(template);
                Assert.NotNull(watcher);
                Assert.Equal(_stem + ".heddle", watcher.Filter);
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
                File.WriteAllText(Path.Combine(dir, _stem + ".heddle"), "HELLO");
                var options = FileWatcherTestSupport.WatchOptions(dir, _stem);
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
