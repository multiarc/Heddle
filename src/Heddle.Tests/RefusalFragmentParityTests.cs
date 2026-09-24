using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Refusal fragments on the two tiers. A site the build refused is recorded as source and
    /// recompiled by the loader, so the fragment has to compile under the same conditions the dynamic
    /// engine would give the same text — the byte comparison here is the whole assertion, not the shape
    /// of the context object that produces it. The shapes a corpus row can hold — a bodied refusal at a
    /// document's top level, and a bodied refusal inside a <c>@list</c> element body — are swept by
    /// <c>CompiledFormParityTests</c> through <c>refusal-fragment-body.heddle</c> and
    /// <c>refusal-nested-body.heddle</c>; what this class carries is what a corpus row cannot be — a
    /// fragment the loader cannot compile at all, where a fragment's own compile error is reported, the
    /// two nested reads (a <c>::</c> read and a <c>@prop</c> read from inside the fragment) that neither
    /// corpus row spells, and the <c>@&lt;&lt;</c> import shapes a single corpus directory cannot hold:
    /// an import served from the build host's item map with no file behind it, and an import file that is
    /// no row of the artifact at all.
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class RefusalFragmentParityTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public RefusalFragmentParityTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        /// <summary>A fragment whose recorded source does not compile at load faults the template instead of
        /// escaping as an exception or dropping the site's output: Strict names the reason, Fallback reports
        /// it once and declines the entry, and the text still renders — dynamically, and byte for byte what
        /// the precompiled tier would have produced. The corpus cannot hold this row, because every corpus
        /// row must materialize; the source is spoiled in the artifact instead, which is also what the loader
        /// sees whenever the build records a fragment it cannot rebuild.</summary>
        [Fact]
        public void AFragmentThatCannotCompileFaultsTheTemplateAndStillRendersTheSameBytes()
        {
            const string key = "refusal-uncompilable.heddle";
            const string text = "A@shroud(){{[@(Banner)]}}B\n";
            var model = new RefusalParityRoot();
            string expected = DynamicRender(text, model);
            Assert.Equal("A[ROOT]B\n", expected);

            var artifact = Record(key, text);
            Assert.True(SpoilRefusalSource(artifact, "@shroud(NoSuchMember){{[@(Banner)]}}"),
                "The recorded artifact carries no refusal source to spoil.");
            PrecompiledTemplates.ResetForTests();
            CompiledFormHarness.RegisterImage(CompiledFormWriter.Write(artifact),
                "HeddleTestAsm_RefusalUncompilable");

            using (var guard = FallbackGuard.Install())
            {
                var fallback = Options();
                fallback.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Fallback;
                Assert.False(PrecompiledTemplates.TryResolve(key, fallback, out _),
                    "A fragment that cannot compile must not resolve as a usable entry.");
                guard.Expect(key, PrecompiledFallbackReason.ExtensionInitCompileError);
                guard.Verify();
            }

            var strict = Options();
            strict.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict;
            var thrown = Assert.Throws<PrecompiledMismatchException>(
                () => { PrecompiledTemplates.TryResolve(key, strict, out _); });
            Assert.Equal(PrecompiledFallbackReason.ExtensionInitCompileError, thrown.Reason);
            Assert.Equal(key, thrown.Key);
            Assert.Equal(expected, DynamicRender(text, model));
        }

        /// <summary>The shapes the fragment's inherited context exists for: a refused site inside a
        /// <c>@list</c> element body reading the document root through <c>::</c>, and a refused site inside a
        /// props-declaring definition body reading one of its props. Both must render byte for byte what the
        /// dynamic engine renders.
        /// <para>The regression these pin: a nested body's recorded raw text is only that body's span of the
        /// template, while an item's position is absolute in the text the parser ran over, so a source sliced
        /// without translating between the two lands out of bounds and the recorded fragment degrades to a
        /// name with no data parameter. Neither degraded form parses — <c>@scanner</c> and
        /// <c>@scanner{{x}}</c> both raise HED0003 — so the whole template used to fault at load with
        /// ExtensionInitCompileError and fall back to the dynamic tier.</para></summary>
        [Fact]
        public void ARefusedSiteNestedInABodyRendersIdenticallyOnBothTiers()
        {
            AssertTierParity("refusal-nested-root-ref.heddle",
                "@list(Rows){{[@(Label)|@scanner(::Banner)]}}\n");
            AssertTierParity("refusal-nested-prop-read.heddle",
                "@%\n<panel(tone: string = \"prop-default\")>{{@scanner(tone)}} :: RefusalParityRow\n%@\n" +
                "@list(Rows){{[@(Label)|@panel()]}}\n");
        }

        /// <summary>A refused call an <c>@&lt;&lt;</c> composition import carried is recorded from the
        /// imported file's own text and materializes at load like any other refusal. An import expands
        /// inline: its chains compile into the importing document and get no document of their own, while
        /// their positions stay absolute in the imported file — text neither the owning document nor the
        /// root carries, which is why the build used to refuse the whole artifact. The parse now keeps the
        /// text it consumed at each expansion, so the call's source is sliced out of the file it was
        /// written in.
        /// <para>This row takes the import reader's disk ladder — nothing maps the import and the artifact
        /// carries a single row, so the imported file is not a row of it. That is the shape a fix resolving
        /// imports against the other rows at merge time could not have reached, and the likelier one in a
        /// real project: a shared partial nothing renders directly.</para></summary>
        [Fact]
        public void ARefusedSiteInsideAnImportIsRecordedFromTheImportedFilesOwnText()
        {
            const string key = "refusal-import-disk.heddle";
            const string text = "A@<<{{ext-site-fallback.heddle}}\nB\n";
            var artifact = Record(key, text);
            Assert.Single(artifact.Templates);
            Assert.Equal("@scanner()", SingleRefusal(artifact).SourceText);
            AssertAnchoredAtTheImportBlock(text, artifact);
            AssertTierParity(key, text);
        }

        /// <summary>The other half of the import reader's contract: an import the build host's item map
        /// answers for, which never reaches the disk. Neither file exists on disk here, so a recorder that
        /// re-read the import would find nothing; the text the parse consumed is the text the fragment is
        /// sliced from. The merged artifact carries the imported template as a row of its own, which is how
        /// the loader expands the same import back.</summary>
        [Fact]
        public void ARefusedSiteInsideAnImportServedFromTheItemMapRecordsAndRendersTheSame()
        {
            PrecompiledTemplates.ResetForTests();
            const string libText = "[lib @scanner(Banner)]";
            const string pageText = "A@<<{{refusal-map-lib.heddle}}\nB\n";
            string rootPath = Path.Combine(Path.GetTempPath(),
                "heddle-refusal-" + Guid.NewGuid().ToString("N"));
            var map = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "refusal-map-lib.heddle", libText },
                { "refusal-map-page.heddle", pageText }
            };
            Assert.False(Directory.Exists(rootPath),
                "The map has to be the only source of the import, or the row proves nothing.");

            var lib = RecordWithMap("refusal-map-lib.heddle", libText, map, rootPath);
            var page = RecordWithMap("refusal-map-page.heddle", pageText, map, rootPath);
            Assert.Equal("@scanner(Banner)", SingleRefusal(page).SourceText);
            AssertAnchoredAtTheImportBlock(pageText, page);

            var merged = CompiledArtifactMerger.Merge(new[] { lib, page });
            CompiledFormHarness.RegisterImage(CompiledFormWriter.Write(merged),
                "HeddleTestAsm_RefusalMapImport");
            var request = Options(rootPath);
            request.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict;
            PrecompiledTemplateInfo entry;
            Assert.True(PrecompiledTemplates.TryResolve("refusal-map-page.heddle", request, out entry) &&
                entry != null, "TryResolve refused the map-import row.");
            var strategy = entry.GetStrategy(request);
            Assert.True(strategy != null, "Materialization fault: " +
                (entry.TryGetRequestFault(request, out var reason, out var detail)
                    ? reason + ": " + detail : "<no fault recorded>") + ".");

            var model = new RefusalParityRoot();
            string expected = DynamicRenderWithMap(pageText, model, map, rootPath);
            // A composition import contributes its chains, not its static text, on both tiers alike:
            // the imported file's own brackets are not in either render.
            Assert.Equal("Ascanned:ROOT\nB\n", expected);
            Assert.Equal(expected, CompiledFormHarness.RenderStrategy(strategy, model));
        }

        /// <summary>A refused call nested in a body <b>inside</b> an imported file: the shape where the
        /// record's own body pass and the import describe the same characters, because a body's recorded
        /// raw text is a span of the file it was written in. What the row pins is that it renders byte for
        /// byte, and that the site is reported at the import block rather than at an offset into the
        /// imported file.
        /// <para>The reverse nesting — an <c>@&lt;&lt;</c> inside a body — is unreachable by construction:
        /// a composition import below a document's top level is refused at parse with
        /// <c>HED4004</c> and skipped, so no chain of it ever compiles and no refusal of it can exist.
        /// The second half of this test is that refusal, which is the evidence, not a fixture of it.</para></summary>
        [Fact]
        public void ARefusedSiteNestedInABodyInsideAnImportRendersIdenticallyOnBothTiers()
        {
            const string libText = "L@list(Rows){{[@(Label)|@shroud(){{@(::Banner)}}]}}M";
            const string pageText = "A@<<{{refusal-nest-lib.heddle}}\nB\n";
            WithImportOnDisk("refusal-nest-lib.heddle", libText, rootPath =>
            {
                var artifact = Record("refusal-nest-page.heddle", pageText, rootPath);
                Assert.Equal("@shroud(){{@(::Banner)}}", SingleRefusal(artifact).SourceText);
                AssertAnchoredAtTheImportBlock(pageText, artifact);
                AssertTierParity("refusal-nest-page.heddle", pageText, rootPath);
            });

            var nested = new HeddleTemplate("@list(Rows){{@<<{{refusal-nest-lib.heddle}}}}",
                new CompileContext(Options(), new ExType(typeof(RefusalParityRoot))));
            Assert.Contains(nested.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.ComposeImportNotTopLevel);
        }

        /// <summary>The shape an <c>@&lt;&lt;</c> library is usually for: a definition declared in the
        /// imported file, whose body holds a refused call, called from the importing document. The body
        /// compiles at the call site under the definition's own context, which the imported file's parse
        /// produced — so the call it carries is positioned in the imported file too, and the record reaches
        /// that file through the same inheritance the body and definition contexts already have.</summary>
        [Fact]
        public void ARefusedSiteInADefinitionDeclaredInAnImportRendersIdenticallyOnBothTiers()
        {
            const string libText = "@%\n<libpanel>{{[@shroud(){{@(::Banner)}}]}} :: RefusalParityRoot\n%@\n";
            const string pageText = "A@<<{{refusal-def-lib.heddle}}\n@libpanel()\nB\n";
            WithImportOnDisk("refusal-def-lib.heddle", libText, rootPath =>
            {
                var artifact = Record("refusal-def-page.heddle", pageText, rootPath);
                Assert.Equal("@shroud(){{@(::Banner)}}", SingleRefusal(artifact).SourceText);
                AssertTierParity("refusal-def-page.heddle", pageText, rootPath);
            });
        }

        /// <summary>A compile error inside a fragment whose source came from an imported file is reported
        /// at the import block, never at the refused call's own position. That position is an offset into a
        /// file this template does not contain, so reporting it here would name a place in this template
        /// that has nothing to do with the fault — the record's anchor is used instead, and the fragment's
        /// own offsets are deliberately left untranslated. The source is spoiled in the artifact, because a
        /// fragment that does not compile is what a changed environment produces and nothing else stages
        /// it.</summary>
        [Fact]
        public void AFragmentErrorFromAnImportedFileIsReportedAtTheImportBlock()
        {
            // Padded so the refused call sits past the end of the importing text: its position is in the
            // imported file's coordinates, and the padding is what makes reporting it here visibly wrong.
            const string libText = "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL@shroud(){{@(::Banner)}}M";
            const string pageText = "A@<<{{refusal-fault-lib.heddle}}\nB\n";
            WithImportOnDisk("refusal-fault-lib.heddle", libText, rootPath =>
            {
                var artifact = Record("refusal-fault-page.heddle", pageText, rootPath);
                var recorded = SingleRefusal(artifact);
                int importStart = recorded.Position.Start;
                var item = SingleRefusalItem(artifact);
                Assert.True(item.Position.Start > pageText.Length,
                    "The refused call must be positioned outside the importing text, or this row is not " +
                    "the case under test (item at " + item.Position.Start + ", importing text length " +
                    pageText.Length + ").");
                // Only the recorded source is spoiled. The imported file on disk is what the load
                // re-parses, so respelling the recorded item keys too would make the loader miss the
                // refusal entirely and fault on a body it could not serve instead of on the fragment.
                string spoiled = Respell(recorded.SourceText);
                Assert.NotEqual(recorded.SourceText, spoiled);
                recorded.SourceText = spoiled;

                var scope = new CompileScope(new CompileContext(Options(rootPath),
                    new ExType(typeof(RefusalParityRoot))));
                HeddleCompiler.Materialize(artifact, artifact.Templates[0], scope);
                var error = Assert.Single(scope.CompileErrors);
                Assert.True(error.Position.StartIndex == importStart,
                    "The fragment error (" + error.DiagnosticId + ": " + error.Error + ") is at " +
                    error.Position.StartIndex + "; the import block it has to name is at " + importStart +
                    " (the refused call's own position, " + item.Position.Start +
                    ", indexes the imported file, not this one).");
                Assert.True(error.Position.StartIndex + error.Position.Length <= pageText.Length,
                    "The fragment error names a span outside the importing text.");
            });
        }

        /// <summary>Two composition imports whose refused calls sit at the same offset of their own
        /// files. The imports expand inline, so both sites land in this one document carrying positions
        /// that index two different files — and a loader that correlated a site by position and template
        /// alone kept only the first, serving one import's call where the other's belonged. Neither tier
        /// reported anything: the gate passed, no fallback fired, and the bytes were simply wrong.
        /// <para>Two partials cut from one scaffold, or sharing a boilerplate prefix, is the ordinary way
        /// to land here, and it became reachable the moment such a template stopped failing the build.
        /// The byte comparison is the whole assertion; the recorded facts above it say why it holds.</para></summary>
        [Fact]
        public void TwoImportsRefusingAtTheSameOffsetEachKeepTheirOwnCall()
        {
            const string firstLib = "[@scanner(Banner)]";
            const string secondLib = "[@scanner(::Rows)]";
            const string pageText = "A@<<{{collide-one.heddle}}\nB@<<{{collide-two.heddle}}\nC\n";
            var libs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "collide-one.heddle", firstLib },
                { "collide-two.heddle", secondLib }
            };
            WithImportsOnDisk(libs, rootPath =>
            {
                var artifact = Record("refusal-collide-page.heddle", pageText, rootPath);
                var sites = RefusalItems(artifact);
                Assert.Equal(2, sites.Count);
                // The two calls are at one position in their own files, which is what used to collide.
                Assert.Equal(sites[0].Position.Start, sites[1].Position.Start);
                Assert.Equal(sites[0].Position.Length, sites[1].Position.Length);
                var sources = new SortedSet<string>(StringComparer.Ordinal)
                {
                    sites[0].Parameter.Refusal.SourceText,
                    sites[1].Parameter.Refusal.SourceText
                };
                Assert.Equal(new[] { "@scanner(::Rows)", "@scanner(Banner)" }, sources.ToArray());
                // Each site is reported at its own import block, which is also what tells them apart.
                var anchors = new SortedSet<int>
                {
                    sites[0].Parameter.Refusal.Position.Start,
                    sites[1].Parameter.Refusal.Position.Start
                };
                Assert.Equal(
                    new[]
                    {
                        pageText.IndexOf("@<<{{collide-one", StringComparison.Ordinal),
                        pageText.IndexOf("@<<{{collide-two", StringComparison.Ordinal)
                    },
                    anchors.ToArray());

                AssertTierParity("refusal-collide-page.heddle", pageText, rootPath);
            });
        }

        /// <summary>The same file imported twice. Both expansions refuse, each records its own site, and
        /// each is reported at the <c>@&lt;&lt;</c> block that expanded it — so a fault in the second
        /// expansion names the second import's line, not the first's. The two sites are identical in every
        /// way a position can express, which is what made them one key.</summary>
        [Fact]
        public void OneFileImportedTwiceKeepsASiteForEachExpansion()
        {
            const string libText = "[@scanner(Banner)]";
            const string pageText = "A@<<{{twice-lib.heddle}}\nB@<<{{twice-lib.heddle}}\nC\n";
            WithImportOnDisk("twice-lib.heddle", libText, rootPath =>
            {
                var artifact = Record("refusal-twice-page.heddle", pageText, rootPath);
                var sites = RefusalItems(artifact);
                Assert.Equal(2, sites.Count);
                Assert.Equal(sites[0].Position.Start, sites[1].Position.Start);
                var anchors = new SortedSet<int>
                {
                    sites[0].Parameter.Refusal.Position.Start,
                    sites[1].Parameter.Refusal.Position.Start
                };
                Assert.Equal(
                    new[]
                    {
                        pageText.IndexOf("@<<", StringComparison.Ordinal),
                        pageText.LastIndexOf("@<<", StringComparison.Ordinal)
                    },
                    anchors.ToArray());
                AssertTierParity("refusal-twice-page.heddle", pageText, rootPath);
            });
        }

        /// <summary>A refused call the template itself wrote, in a <c>-&gt;</c> default output chain
        /// declared before an <c>@&lt;&lt;</c> import. The expansion isolates the importing context and
        /// copies the importer's chains onto the copy before marking it, so a marker read off the
        /// compiling context claimed those chains for the imported file — and the record then cut this
        /// call's source out of a file it does not appear in and published its site at the import block.
        /// A chain carries the file it was built in, which a later expansion cannot retrofit; the byte
        /// comparison is what says the recompiled fragment is still this template's call.</summary>
        [Fact]
        public void ARefusedCallInADefaultChainBeforeAnImportIsCutFromTheTemplatesOwnText()
        {
            const string libText = "[@scanner(::Rows)]";
            const string pageText =
                "@%\n<lead> -> scanner(Banner)\n{{X}} :: RefusalParityRoot\n%@\n" +
                "A@<<{{lead-lib.heddle}}\nB\n";
            WithImportOnDisk("lead-lib.heddle", libText, rootPath =>
            {
                var artifact = Record("refusal-lead-page.heddle", pageText, rootPath);
                CompiledItem own = null;
                var sources = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var site in RefusalItems(artifact))
                {
                    sources.Add(site.Parameter.Refusal.SourceText);
                    if (string.Equals(site.Parameter.Refusal.SourceText, "@scanner(Banner)",
                        StringComparison.Ordinal))
                        own = site;
                }

                Assert.True(own != null,
                    "The template's own refused call was not cut from its own text; the record holds [" +
                    string.Join(", ", sources) + "].");
                // Its own position is a position in this template, so that is where it is reported —
                // an import block standing in for it would say the call came from the imported file.
                Assert.Equal(own.Position.Start, own.Parameter.Refusal.Position.Start);
                Assert.Equal(own.Position.Length, own.Parameter.Refusal.Position.Length);
                AssertTierParity("refusal-lead-page.heddle", pageText, rootPath);
            });
        }

        /// <summary>Nested imports: the page imports a middle file, which imports an inner one, and the
        /// refused call is in the inner file. Its source is cut from the inner file — the one its position
        /// indexes — while its site is reported at the page's own <c>@&lt;&lt;</c> block, the only one of
        /// the three the page contains. Reporting the middle file's import block would name a position
        /// inside a file the page does not carry.</summary>
        [Fact]
        public void ARefusedSiteInsideANestedImportIsReportedAtThePagesOwnImportBlock()
        {
            const string innerText = "[@scanner(Banner)]";
            const string midText = "MIDDLE PADDING TEXT\n@<<{{nest-inner.heddle}}\n";
            const string pageText = "A@<<{{nest-mid.heddle}}\nB\n";
            var libs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "nest-inner.heddle", innerText },
                { "nest-mid.heddle", midText }
            };
            WithImportsOnDisk(libs, rootPath =>
            {
                var artifact = Record("refusal-nest-page.heddle", pageText, rootPath);
                var refusal = SingleRefusal(artifact);
                Assert.Equal("@scanner(Banner)", refusal.SourceText);
                Assert.Equal(pageText.IndexOf("@<<", StringComparison.Ordinal), refusal.Position.Start);
                AssertAnchoredAtTheImportBlock(pageText, artifact);
                AssertTierParity("refusal-nest-page.heddle", pageText, rootPath);
            });
        }

        /// <summary>A fault inside a fragment whose source came from an imported file, where the refused
        /// call sits in a body <b>inside</b> that file. The body's recorded raw text is a span of the
        /// imported file, so the loader's slice search does find the fragment there and does offer an
        /// offset — an offset into a file this template does not contain, which published as a position
        /// here lands wherever it happens to land (the line lookup clamps rather than throwing, so the
        /// host prints a confidently wrong line and column). The anchor the record chose is used instead,
        /// and the search is not run at all for an imported source.
        /// <para>Staged by respelling one member read through the recorded texts and the recorded fragment
        /// alike, leaving every item key and every position exactly where the build put them: the loader
        /// still correlates every item, still serves the body, and the fragment it is handed still matches
        /// the body's text — which is what makes the slice search succeed, and what the fix has to
        /// override.</para></summary>
        [Fact]
        public void AFragmentErrorFromABodyInsideAnImportedFileIsReportedAtTheImportBlock()
        {
            const string libText = "LEAD@list(Rows){{[@(Label)|@shroud(::Banner){{Z}}]}}TAIL";
            const string pageText = "A@<<{{fault-body-lib.heddle}}\nB\n";
            WithImportOnDisk("fault-body-lib.heddle", libText, rootPath =>
            {
                var artifact = Record("refusal-fault-body.heddle", pageText, rootPath);
                var item = SingleRefusalItem(artifact);
                var recorded = item.Parameter.Refusal;
                int importStart = recorded.Position.Start;
                Assert.Equal("@shroud(::Banner){{Z}}", recorded.SourceText);
                Assert.Equal(pageText.IndexOf("@<<", StringComparison.Ordinal), importStart);
                Assert.True(item.Position.Start != importStart,
                    "The refused call must carry the imported file's own coordinates for this row to " +
                    "mean anything (item at " + item.Position.Start + ").");

                SpoilRecordedTexts(artifact);
                recorded.SourceText = Respell(recorded.SourceText);
                Assert.Equal("@shroud(::Bannnr){{Z}}", recorded.SourceText);

                var scope = new CompileScope(new CompileContext(Options(rootPath),
                    new ExType(typeof(RefusalParityRoot))));
                HeddleCompiler.Materialize(artifact, artifact.Templates[0], scope);
                var error = Assert.Single(scope.CompileErrors);
                Assert.True(error.Position.StartIndex == importStart,
                    "The fragment error (" + error.DiagnosticId + ": " + error.Error + ") is at " +
                    error.Position.StartIndex + "; the import block it has to name is at " + importStart +
                    ", and the refused call's own position, " + item.Position.Start +
                    ", indexes the imported file.");
                Assert.True(error.Position.StartIndex + error.Position.Length <= pageText.Length,
                    "The fragment error names a span outside the importing text.");
            });
        }

        /// <summary>Respells the member read through every recorded <b>text</b> — documents and served
        /// bodies — and through nothing else. Item templates and positions are what the loader correlates
        /// by, and the imported file on disk is what it re-parses, so respelling those too would make it
        /// miss the site and fault on something other than the fragment.</summary>
        private static void SpoilRecordedTexts(CompiledArtifact artifact)
        {
            foreach (var document in artifact.Documents)
            {
                document.RawText = Respell(document.RawText);
                document.ShapedText = Respell(document.ShapedText);
                foreach (var element in document.Elements)
                {
                    element.StaticPiece = Respell(element.StaticPiece);
                    if (element.Chain != null)
                        foreach (var item in element.Chain.Items)
                            SpoilItemTexts(item);
                }

                foreach (var removed in document.RemovedItems)
                    SpoilItemTexts(removed);
            }
        }

        private static void SpoilItemTexts(CompiledItem item)
        {
            if (item == null)
                return;
            SpoilBody(item.Body);
            foreach (var alt in item.AltBodies)
                SpoilBody(alt.Body);
            if (item.Parameter != null && item.Parameter.NestedChain != null)
                foreach (var nested in item.Parameter.NestedChain.Items)
                    SpoilItemTexts(nested);
        }

        /// <summary>Two composition imports putting a bodied call at the same offset of their own files.
        /// An import expands inline, so both items land in one document carrying positions that are offsets
        /// into two different files; each item records the <c>@&lt;&lt;</c> block that composed it, and that
        /// is what the loader correlates a body by, so the two never meet under one key. Both spellings
        /// render byte for byte what the dynamic engine renders \u2014 bodies that read alike and bodies that
        /// differ. Two partials cut from one scaffold are the ordinary way to share a position.</summary>
        [Fact]
        public void TwoImportsWhoseBodiedCallsShareAPositionKeepTheirOwnBodies()
        {
            const string plain = "[@list(Rows){{B}}]";
            const string commented = "[@list(Rows){{@*c*@B}}]";
            const string pageText = "A@<<{{body-one.heddle}}\nB@<<{{body-two.heddle}}\nC\n";
            var agreeing = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "body-one.heddle", plain },
                { "body-two.heddle", plain }
            };
            WithImportsOnDisk(agreeing, rootPath =>
            {
                var artifact = Record("refusal-bodykey-same.heddle", pageText, rootPath);
                var bodied = BodiedItems(artifact);
                Assert.Equal(2, bodied.Count);
                // One position in their own files; two anchors, each the import block that composed it.
                Assert.Equal(bodied[0].Position.Start, bodied[1].Position.Start);
                Assert.Equal(bodied[0].Position.Length, bodied[1].Position.Length);
                Assert.Equal(
                    new[]
                    {
                        pageText.IndexOf("@<<", StringComparison.Ordinal),
                        pageText.LastIndexOf("@<<", StringComparison.Ordinal)
                    },
                    new SortedSet<int> { bodied[0].ImportAnchor, bodied[1].ImportAnchor }.ToArray());
                AssertTierParity("refusal-bodykey-same.heddle", pageText, rootPath);
            });

            var differing = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "body-one.heddle", plain },
                { "body-two.heddle", commented }
            };
            WithImportsOnDisk(differing, rootPath =>
                AssertTierParity("refusal-bodykey-differ.heddle", pageText, rootPath));
        }

        /// <summary>Every recorded item that carries a body, in document and chain order.</summary>
        private static List<CompiledItem> BodiedItems(CompiledArtifact artifact)
        {
            var found = new List<CompiledItem>();
            foreach (var document in artifact.Documents)
            {
                foreach (var element in document.Elements)
                {
                    if (!element.IsChain || element.Chain == null)
                        continue;
                    foreach (var item in element.Chain.Items)
                        FindBodiedItems(item, found);
                }

                foreach (var removed in document.RemovedItems)
                    FindBodiedItems(removed, found);
            }

            return found;
        }

        private static void FindBodiedItems(CompiledItem item, List<CompiledItem> found)
        {
            if (item == null)
                return;
            if (item.Body != null)
                found.Add(item);
            if (item.Parameter != null && item.Parameter.NestedChain != null)
                foreach (var nested in item.Parameter.NestedChain.Items)
                    FindBodiedItems(nested, found);
        }

        /// <summary>The locate step is a verdict, not a containment test. Staged by moving the refused
        /// item onto a span of the template that mentions the call's name without being its site: the
        /// record refuses it rather than cutting a fragment out of text that merely happens to say
        /// <c>shroud</c>. Asking only whether the name appears somewhere in the span is what let an
        /// unrelated byte range of an imported file stand in for a call — which decides both the file a
        /// fragment is cut from and the line published as its site, and a wrong answer there is a
        /// confidently wrong build, not a failed one.</summary>
        [Fact]
        public void ASpanThatMerelyMentionsTheCallIsNotTakenForItsSite()
        {
            const string text = "A@shroud(){{[@(Banner)]}}B\n";
            var options = Options();
            var modelEx = new ExType(typeof(RefusalParityRoot));
            var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out var context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "The template must compile: " + CompiledFormHarness.Summarize(context) + ".");
            var refusal = Assert.Single(context.FormRecord.Refusals);
            refusal.SourceText = null;
            // Widened left to the start of the document: the span still contains 'shroud', and is no
            // longer the call site that begins with it.
            int length = refusal.Item.Position.StartIndex + refusal.Item.Position.Length;
            refusal.Item.Position = new Strings.Core.BlockPosition(0, length);
            Assert.Contains("shroud", text.Substring(0, length), StringComparison.Ordinal);

            var thrown = Assert.Throws<InvalidOperationException>(() => context.FormRecord.ToArtifact(
                "test", "test", "refusal-mentioned.heddle", ContentHash.HashText(text), null, null,
                modelEx, false, false, "Text", "Native", options.TrimDirectiveLines));
            Assert.Contains("no recordable source", thrown.Message, StringComparison.Ordinal);
        }

        /// <summary>The guard that outlived its stopgap. Every source of template text a compile reads is
        /// now offered to the record — the owning document, the root, and the file of an <c>@&lt;&lt;</c>
        /// import — so no template shape is known to reach this. What the build still refuses to record is
        /// a refusal whose position names the call in none of them: the slice check is what says so, and
        /// recording a name with no data parameter instead would ship a fragment that parses nowhere.
        /// Staged by moving the refused item past the end of every recorded text, because no template
        /// produces that state and a guard nothing exercises is a guard nothing notices losing.</summary>
        [Fact]
        public void ARefusalWhosePositionNamesNoRecordedTextIsRefusedByTheBuild()
        {
            const string text = "A@shroud(){{[@(Banner)]}}B\n";
            var options = Options();
            var modelEx = new ExType(typeof(RefusalParityRoot));
            var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out var context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "The template must compile: " + CompiledFormHarness.Summarize(context) + ".");
            var refusal = Assert.Single(context.FormRecord.Refusals);
            Assert.NotNull(refusal.SourceText);
            refusal.SourceText = null;
            refusal.Item.Position = new Strings.Core.BlockPosition(text.Length + 1000, 8);

            var thrown = Assert.Throws<InvalidOperationException>(() => context.FormRecord.ToArtifact(
                "test", "test", "refusal-unlocatable.heddle", ContentHash.HashText(text), null, null,
                modelEx, false, false, "Text", "Native", options.TrimDirectiveLines));
            Assert.Contains("@shroud", thrown.Message, StringComparison.Ordinal);
            Assert.Contains("no recordable source", thrown.Message, StringComparison.Ordinal);
        }

        /// <summary>The recorded source of the artifact's one refusal site.</summary>
        private static CompiledRefusalSource SingleRefusal(CompiledArtifact artifact) =>
            SingleRefusalItem(artifact).Parameter.Refusal;

        private static CompiledItem SingleRefusalItem(CompiledArtifact artifact)
        {
            var found = RefusalItems(artifact);
            Assert.True(found.Count == 1,
                "The recorded artifact carries " + found.Count + " refusal sites, not one.");
            return found[0];
        }

        /// <summary>Every recorded refusal site, in document and chain order.</summary>
        private static List<CompiledItem> RefusalItems(CompiledArtifact artifact)
        {
            var found = new List<CompiledItem>();
            foreach (var document in artifact.Documents)
            {
                foreach (var element in document.Elements)
                {
                    if (!element.IsChain || element.Chain == null)
                        continue;
                    foreach (var item in element.Chain.Items)
                        FindRefusalItems(item, found);
                }

                foreach (var removed in document.RemovedItems)
                    FindRefusalItems(removed, found);
            }

            Assert.True(found.Count != 0, "The recorded artifact carries no refusal site.");
            return found;
        }

        private static void FindRefusalItems(CompiledItem item, List<CompiledItem> found)
        {
            if (item == null || item.Parameter == null)
                return;
            if (item.Parameter.Refusal != null)
                found.Add(item);
            if (item.Parameter.NestedChain != null)
                foreach (var nested in item.Parameter.NestedChain.Items)
                    FindRefusalItems(nested, found);
        }

        /// <summary>The recorded site names the <c>@&lt;&lt;</c> block in the importing text — the one
        /// position in this template a reader can act on — and the row's own refusal list says the same.
        /// An imported file's offsets are never reported as positions in this template.</summary>
        private static void AssertAnchoredAtTheImportBlock(string text, CompiledArtifact artifact)
        {
            var refusal = SingleRefusal(artifact);
            int start = refusal.Position.Start;
            int length = refusal.Position.Length;
            Assert.True(start >= 0 && length > 0 && start + length <= text.Length,
                "The recorded refusal position " + start + ":" + length +
                " is not a span of the importing text (length " + text.Length + ").");
            Assert.Equal("@<<", text.Substring(start, 3));
            var row = artifact.Templates[artifact.Templates.Count - 1];
            var site = Assert.Single(row.RefusalSites);
            Assert.Equal(start, site.PositionStart);
            Assert.Equal(length, site.PositionLength);
        }

        /// <summary>Runs <paramref name="body"/> with <paramref name="importText"/> on disk in a root of
        /// its own, so the import resolves through the reader's file ladder and through nothing else.</summary>
        private static void WithImportOnDisk(string name, string importText, Action<string> body)
        {
            var one = new Dictionary<string, string>(StringComparer.Ordinal) { { name, importText } };
            WithImportsOnDisk(one, body);
        }

        private static void WithImportsOnDisk(Dictionary<string, string> imports, Action<string> body)
        {
            string rootPath = Path.Combine(Path.GetTempPath(),
                "heddle-refusal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            try
            {
                foreach (var import in imports)
                    File.WriteAllText(Path.Combine(rootPath, import.Key), import.Value,
                        new UTF8Encoding(false));
                body(rootPath);
            }
            finally
            {
                Directory.Delete(rootPath, true);
            }
        }

        /// <summary>Records the way the build host does, with an in-memory item map standing in for the
        /// host's: an import the map answers for never reaches the disk.</summary>
        private static CompiledArtifact RecordWithMap(string key, string text,
            Dictionary<string, string> map, string rootPath)
        {
            var options = Options(rootPath);
            var modelEx = new ExType(typeof(RefusalParityRoot));
            var context = new CompileContext(options, modelEx);
            context.DeferUnboundFunctions = true;
            context.RecordForm = true;
            context.ImportReader = ImportMap.ReaderFor(map, rootPath);
            context.ImportIdentifier = ImportMap.IdentifierFor(map, rootPath);
            var template = new HeddleTemplate(text, context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + key + ": " + CompiledFormHarness.Summarize(context) + ".");
            return context.FormRecord.ToArtifact("test", "test", key, ContentHash.HashText(text),
                null, null, modelEx, false, false, "Text", "Native", options.TrimDirectiveLines);
        }

        private static string DynamicRenderWithMap(string text, object model,
            Dictionary<string, string> map, string rootPath)
        {
            var context = new CompileContext(Options(rootPath), new ExType(typeof(RefusalParityRoot)));
            context.ImportReader = ImportMap.ReaderFor(map, rootPath);
            context.ImportIdentifier = ImportMap.IdentifierFor(map, rootPath);
            var template = new HeddleTemplate(text, context);
            Assert.True(template.CompileResult.Success,
                "Dynamic reference failed: " + template.CompileResult + ".");
            return template.Generate(model);
        }

        /// <summary>A compile error the loader finds inside a refused fragment is reported where the
        /// fragment really sits in the source, not at the refused call. The fragment compiles as its own
        /// unit, so its errors come back with fragment-local positions and are shifted home by the offset
        /// the recorded source maps to; a nested body's recorded text is only that body's span while an
        /// item position is absolute in the text the parser ran over, so that offset is found only after
        /// translating between the two. Untranslated, the lookup missed, every fragment error collapsed
        /// onto the refused call site, and nothing noticed — the fragment still compiled and still
        /// rendered the same bytes.
        /// <para>The second row is the case the translation alone does not reach: a comment ahead of the
        /// call is a hidden token, which the body's recorded text drops, so the arithmetic lands on the
        /// wrong index there and only the root text still carries the call where its position says. The
        /// record resolves that case at the root too.</para></summary>
        [Fact]
        public void ACompileErrorInARefusedFragmentIsReportedAtItsPlaceInTheSource()
        {
            AssertFragmentErrorPosition("refusal-nested-fault.heddle",
                "A@list(Rows){{[@(Label)|@shroud(){{@(::Banner)}}]}}B\n");
            AssertFragmentErrorPosition("refusal-nested-comment-fault.heddle",
                "A@list(Rows){{[@(Label)|@*note*@@shroud(){{@(::Banner)}}]}}B\n");
        }

        /// <summary>The member the spoiled artifact reads, and the one it is spelled over. Equal lengths:
        /// every other position in every recorded text stays exactly where the build put it, so the only
        /// thing the substitution changes is that the read no longer binds.</summary>
        private const string GoodRead = "::Banner";

        private const string BadRead = "::Bannnr";

        /// <summary>Records <paramref name="text"/>, rewrites the one member read it carries into one that
        /// does not bind, and materializes the result the way the loader does. The expected position is not
        /// computed here: the dynamic engine compiles the same spoiled text and its own error position is
        /// the answer the loader has to match.</summary>
        private static void AssertFragmentErrorPosition(string key, string text)
        {
            string spoiled = text.Replace(GoodRead, BadRead);
            Assert.NotEqual(text, spoiled);

            var reference = new HeddleTemplate(spoiled,
                new CompileContext(Options(), new ExType(typeof(RefusalParityRoot))));
            Assert.False(reference.CompileResult.Success,
                "The spoiled text must not compile, or there is no error to position: " + spoiled + ".");
            var expected = reference.CompileResult.ErrorList.First();

            var artifact = Record(key, text);
            Assert.True(SpoilRecordedText(artifact), "The recorded artifact carries no refusal source.");

            var scope = new CompileScope(new CompileContext(Options(), new ExType(typeof(RefusalParityRoot))));
            HeddleCompiler.Materialize(artifact, artifact.Templates[0], scope);

            var error = Assert.Single(scope.CompileErrors);
            Assert.Equal(expected.DiagnosticId, error.DiagnosticId);
            Assert.True(expected.Position.StartIndex == error.Position.StartIndex &&
                        expected.Position.Length == error.Position.Length,
                "The fragment error for " + key + " is at " + error.Position.StartIndex + ":" +
                error.Position.Length + "; the dynamic engine puts the same error at " +
                expected.Position.StartIndex + ":" + expected.Position.Length + ".");
        }

        /// <summary>Rewrites the member read through every recorded text of the artifact, so what the loader
        /// meets is the artifact a build would have produced for the spoiled template — which no build
        /// produces, because the spoiled template does not compile. Returns false when the record carried no
        /// refusal to spoil.</summary>
        private static bool SpoilRecordedText(CompiledArtifact artifact)
        {
            bool spoiled = false;
            foreach (var document in artifact.Documents)
            {
                document.RawText = Respell(document.RawText);
                document.ShapedText = Respell(document.ShapedText);
                foreach (var element in document.Elements)
                {
                    element.StaticPiece = Respell(element.StaticPiece);
                    if (element.Chain != null)
                        foreach (var item in element.Chain.Items)
                            spoiled |= SpoilItem(item);
                }

                foreach (var removed in document.RemovedItems)
                    spoiled |= SpoilItem(removed);
            }

            return spoiled;
        }

        private static bool SpoilItem(CompiledItem item)
        {
            if (item == null)
                return false;
            bool spoiled = false;
            item.ParameterTemplate = Respell(item.ParameterTemplate);
            SpoilBody(item.Body);
            foreach (var alt in item.AltBodies)
            {
                alt.Template = Respell(alt.Template);
                SpoilBody(alt.Body);
            }

            if (item.Parameter != null)
            {
                if (item.Parameter.Refusal != null && item.Parameter.Refusal.SourceText != null)
                {
                    item.Parameter.Refusal.SourceText = Respell(item.Parameter.Refusal.SourceText);
                    spoiled = true;
                }

                if (item.Parameter.NestedChain != null)
                    foreach (var nested in item.Parameter.NestedChain.Items)
                        spoiled |= SpoilItem(nested);
            }

            return spoiled;
        }

        private static void SpoilBody(CompiledBody body)
        {
            if (body == null)
                return;
            body.RawText = Respell(body.RawText);
            body.ShapedText = Respell(body.ShapedText);
        }

        private static string Respell(string text) =>
            text == null ? null : text.Replace(GoodRead, BadRead);

        /// <summary>Builds, registers and materializes one template the way the build and the loader do, then
        /// byte-compares all three sinks against the dynamic engine's render of the same text. The request
        /// renders under <see cref="PrecompiledMismatchPolicy.Strict"/> with the fallback sentinel armed, so a
        /// silent recompile cannot pass for parity.</summary>
        private static void AssertTierParity(string key, string text, string rootPath = null)
        {
            var model = new RefusalParityRoot();
            string expected = DynamicRender(text, model, rootPath);
            var artifact = Record(key, text, rootPath);
            PrecompiledTemplates.ResetForTests();
            CompiledFormHarness.RegisterImage(CompiledFormWriter.Write(artifact),
                "HeddleTestAsm_" + key.Replace(".", "_").Replace("-", "_"));
            using (var guard = FallbackGuard.Install())
            {
                var request = FallbackGuard.GuardedOptions(Options(rootPath));
                var report = PrecompiledTemplates.ValidateAll(request);
                Assert.True(report.Failures.Count == 0,
                    "Gate refused " + key + ": " + CompiledFormHarness.GateDetail(report) + ".");
                PrecompiledTemplateInfo entry;
                Assert.True(PrecompiledTemplates.TryResolve(key, request, out entry) && entry != null,
                    "TryResolve refused " + key + ".");
                var strategy = entry.GetStrategy(request);
                Assert.True(strategy != null, "Materialization fault for " + key + ": " +
                    (entry.TryGetRequestFault(request, out var reason, out var detail)
                        ? reason + ": " + detail : "<no fault recorded>") + ".");

                string fromString = CompiledFormHarness.RenderStrategy(strategy, model);
                string fromWriter;
                using (var writer = new StringWriter())
                {
                    CompiledFormHarness.RenderStrategy(strategy, model, writer);
                    fromWriter = writer.ToString();
                }
                var bytes = new Streaming.TestBufferWriter();
                CompiledFormHarness.RenderStrategy(strategy, model, bytes);
                string fromUtf8 = Encoding.UTF8.GetString(bytes.WrittenSpan.ToArray());

                Assert.True(fromString == expected && fromWriter == expected && fromUtf8 == expected,
                    "Byte divergence for " + key + ": string=" + (fromString == expected) +
                    " writer=" + (fromWriter == expected) + " utf8=" + (fromUtf8 == expected) +
                    ". Dynamic <<" + expected + ">> precompiled <<" + fromString + ">>.");
                guard.AssertQuiet();
            }
        }

        /// <summary>Text-compiles with recording and deferral armed, exactly as the build host does, and
        /// returns the artifact its record produces.</summary>
        private static CompiledArtifact Record(string key, string text, string rootPath = null)
        {
            var options = Options(rootPath);
            var modelEx = new ExType(typeof(RefusalParityRoot));
            var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out var context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + key + ": " + CompiledFormHarness.Summarize(context) + ".");
            return context.FormRecord.ToArtifact("test", "test", key, ContentHash.HashText(text),
                null, null, modelEx, false, false, "Text", "Native", options.TrimDirectiveLines);
        }

        /// <summary>Replaces the first recorded refusal source, so the loader meets a fragment it cannot
        /// rebuild. Returns false when the record carried none.</summary>
        private static bool SpoilRefusalSource(CompiledArtifact artifact, string sourceText)
        {
            foreach (var document in artifact.Documents)
                foreach (var element in document.Elements)
                {
                    if (!element.IsChain || element.Chain == null)
                        continue;
                    foreach (var item in element.Chain.Items)
                        if (Spoil(item, sourceText))
                            return true;
                }

            return false;
        }

        private static bool Spoil(CompiledItem item, string sourceText)
        {
            if (item == null || item.Parameter == null)
                return false;
            if (item.Parameter.Refusal != null)
            {
                item.Parameter.Refusal.SourceText = sourceText;
                return true;
            }

            if (item.Parameter.NestedChain != null)
                foreach (var nested in item.Parameter.NestedChain.Items)
                    if (Spoil(nested, sourceText))
                        return true;
            return false;
        }

        private static string DynamicRender(string text, object model, string rootPath = null)
        {
            var template = new HeddleTemplate(text,
                new CompileContext(Options(rootPath), new ExType(typeof(RefusalParityRoot))));
            Assert.True(template.CompileResult.Success,
                "Dynamic reference failed: " + template.CompileResult + ".");
            return template.Generate(model);
        }

        /// <summary>One options shape for both sides: the build records under it and the request
        /// materializes under it, so nothing but the tier differs between the two renders.</summary>
        private static TemplateOptions Options(string rootPath = null) => new TemplateOptions("refusal-fragment")
        {
            RootPath = rootPath ?? TestCorpusIndex.CorpusDir,
            FileNamePostfix = ".heddle",
            OutputProfile = OutputProfile.Text,
            EnableFileChangeCheck = false,
            ExpressionMode = ExpressionMode.Native
        };
    }
}
