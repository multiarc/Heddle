using System.IO;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 1 back-compat gate (R5): templates that never take the watcher path — inline compiles and
    /// flag-off file compiles — render byte-identically to the pre-phase baseline; no watcher is installed
    /// and no reload state is allocated for them.
    /// </summary>
    public class FileWatcherByteIdentityTests
    {
        private const string Source = "<p>@()</p>|@raw()";
        // Pre-recorded baseline for Source with model "<b>x</b>" under the default options (Html profile,
        // legacy WebUtility encoder): the unnamed sink encodes, @raw does not.
        private const string Baseline = "<p>&lt;b&gt;x&lt;/b&gt;</p>|<b>x</b>";

        /// <summary>An inline compile and a flag-off file compile of the same source both render the exact
        /// recorded baseline bytes — the watcher fix changes nothing for non-watching templates.</summary>
        [Fact]
        public void FlagOffAndInlineOutputsAreByteIdentical()
        {
            using (var inline = new HeddleTemplate(Source, new CompileContext()))
            {
                Assert.True(inline.CompileResult.Success, inline.CompileResult.ToString());
                Assert.Equal(Baseline, inline.Generate("<b>x</b>"));
                Assert.Null(FileWatcherTestSupport.GetWatcher(inline));
            }

            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "home.heddle"), Source);
                var options = FileWatcherTestSupport.WatchOptions(dir, "home");
                options.EnableFileChangeCheck = false;
                using var fromFile = new HeddleTemplate(options);
                Assert.True(fromFile.CompileResult.Success, fromFile.CompileResult.ToString());
                Assert.Equal(Baseline, fromFile.Generate("<b>x</b>"));
                Assert.Null(FileWatcherTestSupport.GetWatcher(fromFile));
                Assert.Null(FileWatcherTestSupport.GetSupersededQueue(fromFile));
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }
    }
}
