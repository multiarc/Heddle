using System.IO;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 1 D4/D5 — a failed watcher recompile (edited source that no longer compiles) keeps the
    /// last-good document published and renderable, and surfaces the failure on <c>CompileResult</c>
    /// (no new diagnostic channel); a subsequent good edit recovers.
    /// </summary>
    public class FileWatcherFailedRecompileTests
    {
        /// <summary>The pinned scenario: break the file, reload → <c>CompileResult.Success == false</c> with
        /// the error in <c>ErrorList</c> while <c>Generate</c> still renders the previous content; fix the
        /// file, reload → success again with the new content (the edit-to-fix loop D2 arms for).</summary>
        [Fact]
        public void FailedRecompileKeepsLastGoodAndSurfacesErrorOnCompileResult()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                var path = Path.Combine(dir, "home.heddle");
                File.WriteAllText(path, "GOOD");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, "home"));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);

                File.WriteAllText(path, "@profile(){{pdf}}broken");   // deterministic compile error (unknown profile)
                FileWatcherTestSupport.InvokeChanged(template, dir, "home.heddle");

                Assert.False(template.CompileResult.Success, "the broken edit must surface a failed CompileResult");
                Assert.NotEmpty(template.CompileResult.ErrorList);
                Assert.Equal("GOOD", template.Generate(null));        // last-good stays published and renderable

                File.WriteAllText(path, "FIXED");                     // the edit-to-fix save recovers
                FileWatcherTestSupport.InvokeChanged(template, dir, "home.heddle");
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal("FIXED", template.Generate(null));
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }
    }
}
