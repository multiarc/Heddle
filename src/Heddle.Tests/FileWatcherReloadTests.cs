using System;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Top-level so a template's <c>@model(){{FileWatcherConcreteModel}}</c> resolves it by simple
    /// name through the configured test-assembly namespaces (phase 1 model-directive reload test).</summary>
    public class FileWatcherConcreteModel
    {
        public string Name { get; set; }
    }

    /// <summary>
    /// Phase 1 D3/D4 — a watcher reload recompiles the freshly-read file into a from-scratch-equivalent
    /// fresh context honoring the original options and pre-<c>@model</c> root model type; the event/reload
    /// mapping (edit/create reload, rename-onto-target reload, delete keeps last-good, empty save no-op)
    /// matches the documented contract. Deterministic tests drive the wired private handlers by reflection
    /// (the exact production <c>Reload()</c> path, synchronously); two FSW-timed tests witness the true
    /// end-to-end wiring.
    /// </summary>
    public class FileWatcherReloadTests
    {
        // xunit news up the class per test: every test watches its own uniquely named file, isolating it
        // from concurrent tests and from the other TFMs' test hosts running the same suite in parallel.
        private readonly string _stem = FileWatcherTestSupport.NewStem();

        private static string WriteFile(string dir, string stem, string content)
        {
            var path = Path.Combine(dir, stem + ".heddle");
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>End-to-end (test-with, FSW): an armed watcher sees an in-place edit, raises
        /// <c>OnFileChanged</c> with the template as sender, and the next render reflects the new content —
        /// filter-match + arming + reload proven against the real OS watcher.</summary>
        [Fact]
        public void ArmedWatcherRaisesOnFileChangedAndRecompiles()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                var path = WriteFile(dir, _stem, "ONE");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, _stem));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal("ONE", template.Generate(null));

                using var changed = new ManualResetEventSlim(false);
                object observedSender = null;
                template.OnFileChanged += (s, e) => { observedSender = s; changed.Set(); };

                File.WriteAllText(path, "TWO");

                Assert.True(changed.Wait(10000), "OnFileChanged did not fire within the timeout");
                Assert.Same(template, observedSender);
                Assert.True(FileWatcherTestSupport.WaitFor(() => template.Generate(null) == "TWO"),
                    "the render never reflected the edited content; last render: " + template.Generate(null));
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>D3 (deterministic): a <c>FullCSharp</c> template re-runs <c>CompleteInit</c> + the Roslyn
        /// pass on every reload — each of three successive edits renders exactly what a fresh from-scratch
        /// compile of the same content renders (the pre-fix reused scope skipped finalization from the 2nd
        /// reload on).</summary>
        [Fact]
        public void SuccessiveEditsEachRenderFromScratchEquivalentFinalizedDocument()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                WriteFile(dir, _stem, "@(@ 1 + 1 )");
                var options = FileWatcherTestSupport.WatchOptions(dir, _stem);
                options.ExpressionMode = ExpressionMode.FullCSharp;
                using var template = new HeddleTemplate(options);
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                Assert.Equal("2", template.Generate(null));

                foreach (var edit in new[] { "@(@ 2 * 3 )", "@(@ 10 - 4 )", "@(@ 7 + 1 )" })
                {
                    WriteFile(dir, _stem, edit);
                    FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");
                    Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

                    using var fresh = new HeddleTemplate(edit,
                        new CompileContext(new TemplateOptions(options)));
                    Assert.True(fresh.CompileResult.Success, fresh.CompileResult.ToString());
                    Assert.Equal(fresh.Generate(null), template.Generate(null));
                }
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>D3 (deterministic): the reload honors the original options — the configured modern
        /// <c>HtmlEncoder</c> (distinguishable from the legacy <c>WebUtility</c> path by the apostrophe),
        /// <c>FullCSharp</c>, and the <c>RenderBudget</c> all still apply to the reloaded document.</summary>
        [Fact]
        public void RecompiledDocumentHonorsOriginalOptions()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                WriteFile(dir, _stem, "@(@ 1 + 1 )|@()");
                var options = FileWatcherTestSupport.WatchOptions(dir, _stem);
                options.ExpressionMode = ExpressionMode.FullCSharp;
                options.Encoder = HtmlEncoder.Create(UnicodeRanges.All);
                options.RenderBudget = new RenderBudget { MaxOutputChars = 32 };
                using var template = new HeddleTemplate(options);
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                Assert.Equal("2|&#x27;", template.Generate("'"));   // modern encoder: ' → &#x27; (WebUtility yields &#39;)

                WriteFile(dir, _stem, "@(@ 40 + 2 )|@()");
                FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

                Assert.Equal("42|&#x27;", template.Generate("'"));
                Assert.Throws<TemplateRenderBudgetException>(
                    () => template.Generate(new string('x', 64)));   // the original budget still caps the reload
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>D3 regression (deterministic): <c>OutputProfile</c> is re-derived from the options plus
        /// the freshly-parsed <c>@profile()</c> on every reload — removing the directive restores the options
        /// default (Html-encoded), adding it flips to Text — never the stale directive-flipped profile a
        /// post-compile capture would have carried.</summary>
        [Fact]
        public void ProfileDirectiveEditReloadsFromScratchEquivalent()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                WriteFile(dir, _stem, "@profile(){{text}}\n@()");
                var options = FileWatcherTestSupport.WatchOptions(dir, _stem);   // OutputProfile default: Html
                using var template = new HeddleTemplate(options);
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                Assert.Equal("<b>", template.Generate("<b>"));      // directive flips the first compile to Text

                WriteFile(dir, _stem, "@()");                      // directive removed
                FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                using (var fresh = new HeddleTemplate("@()", new CompileContext(new TemplateOptions(options))))
                {
                    Assert.Equal(fresh.Generate("<b>"), template.Generate("<b>"));
                }
                Assert.Equal("&lt;b&gt;", template.Generate("<b>"));   // options default (Html), not the stale Text

                WriteFile(dir, _stem, "@profile(){{text}}\n@()");  // directive re-added
                FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal("<b>", template.Generate("<b>"));
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>D3 regression (deterministic): the root model type is captured PRE-compile — a file whose
        /// first compile flipped <c>RootScopeType</c> via a root <c>@model</c> reloads under the ORIGINAL
        /// ctor model when the directive is edited away (a post-compile capture would seed the stale
        /// directive-flipped type and reject the payload the from-scratch compile accepts), and a re-added
        /// directive takes effect again.</summary>
        [Fact]
        public void ModelDirectiveEditReloadsFromScratchEquivalent()
        {
            HeddleTemplate.Configure(typeof(FileWatcherReloadTests).GetTypeInfo().Assembly);
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                WriteFile(dir, _stem, "@model(){{FileWatcherConcreteModel}}\n@(Name)");
                var options = FileWatcherTestSupport.WatchOptions(dir, _stem);   // default object model
                using var template = new HeddleTemplate(options);
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                Assert.Equal("n1", template.Generate(new FileWatcherConcreteModel { Name = "n1" }));

                WriteFile(dir, _stem, "@()");                      // @model removed → back to the original object model
                FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                // A string payload is NOT a FileWatcherConcreteModel: this render succeeds only because the
                // reload seeded the pre-@model original (object), like a from-scratch compile of "@()".
                Assert.Equal("payload", template.Generate("payload"));

                WriteFile(dir, _stem, "@model(){{FileWatcherConcreteModel}}\n@(Name)");   // directive re-added
                FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal("n2", template.Generate(new FileWatcherConcreteModel { Name = "n2" }));
#if DEBUG
                // DEBUG model-type guard: the re-applied @model rejects a mistyped payload again.
                Assert.Throws<TemplateProcessingException>(() => template.Generate("payload"));
#endif
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>D3 (deterministic): a code-typed template (model type passed at construction) reloads
        /// under that same model — the edit renders with the original typed member access.</summary>
        [Fact]
        public void RecompiledTypedTemplateRendersWithOriginalModel()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                WriteFile(dir, _stem, "@(Name)");
                var options = FileWatcherTestSupport.WatchOptions(dir, _stem);
                using var template = new HeddleTemplate(options, typeof(FileWatcherConcreteModel));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                Assert.Equal("a", template.Generate(new FileWatcherConcreteModel { Name = "a" }));

                WriteFile(dir, _stem, "X:@(Name)");
                FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal("X:b", template.Generate(new FileWatcherConcreteModel { Name = "b" }));
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>D4 end-to-end (test-with, FSW): the atomic-save idiom — write a temp file, then land it
        /// ON the watched name — recompiles (rename-onto-target / delete-then-create both covered by the
        /// Created wiring + rename predicate).</summary>
        [Fact]
        public void AtomicSaveViaTempThenRenameRecompiles()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                var target = WriteFile(dir, _stem, "ONE");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, _stem));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal("ONE", template.Generate(null));

                var temp = Path.Combine(dir, _stem + ".heddle.tmp");
                File.WriteAllText(temp, "TWO");
                FileWatcherTestSupport.RetryIO(() => File.Delete(target));
                FileWatcherTestSupport.RetryIO(() => File.Move(temp, target));   // rename lands the new content ON the watched name

                Assert.True(FileWatcherTestSupport.WaitFor(() => template.Generate(null) == "TWO"),
                    "the atomic save never reloaded; last render: " + template.Generate(null));
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>P1-Q2 (deterministic): a delete raises <c>OnFileDeleted</c> (sender = the template), does
        /// NOT recompile, and the last-good document stays renderable.</summary>
        [Fact]
        public void DeleteRaisesOnFileDeletedAndKeepsLastGood()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                var path = WriteFile(dir, _stem, "GOOD");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, _stem));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                var lastGoodResult = template.CompileResult;

                object observedSender = null;
                var deleted = 0;
                template.OnFileDeleted += (s, e) => { observedSender = s; deleted++; };

                File.Delete(path);
                FileWatcherTestSupport.InvokeDeleted(template, dir, _stem + ".heddle");

                Assert.Equal(1, deleted);
                Assert.Same(template, observedSender);
                Assert.Same(lastGoodResult, template.CompileResult);   // no recompile happened
                Assert.Equal("GOOD", template.Generate(null));         // last-good stays renderable
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>D4 empty-content policy (deterministic): an empty (then whitespace-only) save is a no-op
        /// — no recompile, <c>CompileResult</c> untouched (still the prior success), last-good renderable.
        /// <c>OnFileChanged</c> still fires (before the guard) for hosts that want to observe the save.</summary>
        [Fact]
        public void EmptyOrWhitespaceSaveIsNoOpAndKeepsLastGood()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                var path = WriteFile(dir, _stem, "GOOD");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, _stem));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                FileWatcherTestSupport.Disarm(template);
                var lastGoodResult = template.CompileResult;

                var changedEvents = 0;
                template.OnFileChanged += (s, e) => changedEvents++;

                foreach (var truncated in new[] { string.Empty, " \t\r\n " })
                {
                    File.WriteAllText(path, truncated);
                    FileWatcherTestSupport.InvokeChanged(template, dir, _stem + ".heddle");
                    Assert.Same(lastGoodResult, template.CompileResult);   // no recompile, no failure surfaced
                    Assert.Equal("GOOD", template.Generate(null));
                }
                Assert.Equal(2, changedEvents);
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }

        /// <summary>D4 end-to-end (test-with, FSW): over a scripted edit → delete → recreate-by-rename
        /// sequence the right public events fire, and touching an unrelated sibling (not matching the
        /// filter) raises nothing.</summary>
        [Fact]
        public void ScriptedEditDeleteRenameRaiseCorrectEvents()
        {
            var dir = FileWatcherTestSupport.NewTempDir();
            try
            {
                var target = WriteFile(dir, _stem, "ONE");
                using var template = new HeddleTemplate(FileWatcherTestSupport.WatchOptions(dir, _stem));
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

                int changed = 0, deletedCount = 0, renamed = 0;
                using var changedEvt = new ManualResetEventSlim(false);
                using var deletedEvt = new ManualResetEventSlim(false);
                using var renamedEvt = new ManualResetEventSlim(false);
                template.OnFileChanged += (s, e) => { Interlocked.Increment(ref changed); changedEvt.Set(); };
                template.OnFileDeleted += (s, e) => { Interlocked.Increment(ref deletedCount); deletedEvt.Set(); };
                template.OnFileRenamed += (s, e) => { Interlocked.Increment(ref renamed); renamedEvt.Set(); };

                // Unrelated sibling: must raise nothing (the filter is home.heddle).
                File.WriteAllText(Path.Combine(dir, "other.txt"), "noise");
                Thread.Sleep(300);
                Assert.Equal(0, Volatile.Read(ref changed));
                Assert.Equal(0, Volatile.Read(ref deletedCount));
                Assert.Equal(0, Volatile.Read(ref renamed));

                File.WriteAllText(target, "TWO");                      // edit
                Assert.True(changedEvt.Wait(10000), "OnFileChanged did not fire for the edit");

                // Retry: the delete can transiently collide with the engine's own reload read of the edit above.
                FileWatcherTestSupport.RetryIO(() => File.Delete(target));     // delete
                Assert.True(deletedEvt.Wait(10000), "OnFileDeleted did not fire for the delete");

                var temp = Path.Combine(dir, "staged.tmp");            // recreate by rename-onto-target
                File.WriteAllText(temp, "THREE");
                FileWatcherTestSupport.RetryIO(() => File.Move(temp, target));
                Assert.True(renamedEvt.Wait(10000), "OnFileRenamed did not fire for the rename");

                Assert.True(FileWatcherTestSupport.WaitFor(() => template.Generate(null) == "THREE"),
                    "the rename-onto-target never reloaded; last render: " + template.Generate(null));
            }
            finally
            {
                FileWatcherTestSupport.CleanupDir(dir);
            }
        }
    }
}
