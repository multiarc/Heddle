using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Heddle.TestCorpus
{
    /// <summary>
    /// Phase 7 D2 — the single accessor for the shared test corpus, linked into every consuming test project by
    /// <c>src/TestCorpus/TestCorpus.props</c>.
    /// <para>This type replaces the triplicated <c>CorpusDir</c> / <c>HeddleTestsDll</c> / <c>LoadCorpus</c> helpers
    /// that rewrote the running assembly's path to name a sibling project and then climbed <c>../../..</c> out of
    /// <c>bin/&lt;cfg&gt;/&lt;tfm&gt;</c>. That shape encoded the configuration name, the TFM directory and the project
    /// nesting as four independent assumptions a build change could move, and its failure mode was a test that found
    /// nothing and passed. The fragility was the mechanism, not the missing assert: with the corpus
    /// <c>Content</c>-copied into each consumer's own output directory, locating it is
    /// <see cref="AppContext.BaseDirectory"/> and nothing else, and the assert becomes unnecessary rather than
    /// load-bearing (testing-standards — <i>Test-input single-sourcing</i>, ledger E9).</para>
    /// <para>The corpus files themselves stay in <c>src/Heddle.Tests/TestTemplate/</c> (D1 as revised): what each
    /// consumer holds is a build copy, which is not a second home for the input.</para>
    /// </summary>
    internal static class TestCorpusIndex
    {
        /// <summary>The output-directory folder the props file links the corpus into. Deliberately unchanged, so
        /// every existing <c>File.ReadAllText("TestTemplate/…")</c> and <c>RootPath = "TestTemplate"</c> still
        /// resolves exactly as before.</summary>
        public const string LinkFolder = "TestTemplate";

        /// <summary>The folder test-written debug output goes to. Deliberately NOT the corpus folder: the corpus is
        /// input, and a directory several projects copy from must not also be a directory tests write into
        /// (Q7.3 / phase 7 WI4). The checked-in historical artifacts live at <c>src/Heddle.Tests/TestOutput/</c>;
        /// they are preserved, excluded from the glob, and read by nothing.</summary>
        public const string WrittenArtifactFolder = "TestOutput";

        private static readonly object Gate = new object();
        private static List<CorpusFile> _templates;
        private static string _dir;

        /// <summary>One corpus template: its <c>/</c>-relativized key (the same key the generator's
        /// <c>AdditionalFiles</c> and the resolver use) and its decoded text.</summary>
        public sealed class CorpusFile
        {
            public CorpusFile(string key, string content)
            {
                Key = key;
                Content = content;
            }

            public string Key { get; }
            public string Content { get; }
            public string Name => Path.GetFileName(Key);
        }

        /// <summary>
        /// The corpus directory in <b>this</b> consumer's own output. Throws — never returns null, so no caller can
        /// silently skip — if the copy is missing, because "found nothing and reported a pass" is precisely the
        /// failure mode this accessor exists to delete. A miss here is a build-wiring failure (the consuming .csproj
        /// did not <c>&lt;Import&gt;</c> TestCorpus.props), not a skippable condition.
        /// </summary>
        public static string CorpusDir
        {
            get
            {
                if (_dir != null)
                    return _dir;
                var candidate = Path.Combine(AppContext.BaseDirectory, LinkFolder);
                if (!Directory.Exists(candidate))
                    throw new InvalidOperationException(
                        "The shared test corpus is not in this test project's output directory (" + candidate +
                        "). It is copied there by src/TestCorpus/TestCorpus.props, which the consuming .csproj must " +
                        "<Import>. This is a build-wiring failure, not a skippable condition.");
                _dir = candidate;
                return _dir;
            }
        }

        /// <summary>Every <c>.heddle</c> entry, ordinal-sorted by key. Enumerated once per process.</summary>
        public static IReadOnlyList<CorpusFile> Templates
        {
            get
            {
                if (_templates != null)
                    return _templates;
                lock (Gate)
                {
                    if (_templates == null)
                    {
                        var dir = CorpusDir;
                        var list = new List<CorpusFile>();
                        foreach (var path in Directory.EnumerateFiles(dir, "*.heddle", SearchOption.AllDirectories)
                                     .OrderBy(p => p, StringComparer.Ordinal))
                        {
                            var rel = path.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/');
                            list.Add(new CorpusFile(rel, File.ReadAllText(path)));
                        }

                        if (list.Count == 0)
                            throw new InvalidOperationException(
                                "The shared test corpus directory exists but holds no .heddle files: " + dir);
                        _templates = list;
                    }
                }

                return _templates;
            }
        }

        /// <summary>Every corpus <c>.heddle</c> file name, ordinal-sorted. The observed half of D3's completeness
        /// gates.</summary>
        public static IReadOnlyList<string> Names() =>
            Templates.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>The corpus as the differential harness consumes it: <c>(key, content)</c> tuples.
        /// <paramref name="includeFrontEndErrorFixtures"/> is the one filter every caller needed — the
        /// <c>Tier = FrontEndError</c> entries carry deliberate parse errors, so a run that wants the rest of the
        /// corpus to generate cleanly drops them (imports still resolve from what remains).</summary>
        public static List<(string key, string content)> Load(bool includeFrontEndErrorFixtures = true)
        {
            IEnumerable<CorpusFile> q = Templates;
            if (!includeFrontEndErrorFixtures)
                q = q.Where(t => CorpusIntent.For(t.Name).Tier != CorpusTier.FrontEndError);
            return q.Select(t => (t.Key, t.Content)).ToList();
        }

        /// <summary>The decoded text of one entry, by file name. Throws with the name on a miss — a migrated test
        /// that names a corpus key which no longer exists must fail loudly, not fall through to an empty string.
        /// </summary>
        public static string Text(string name)
        {
            var hit = Templates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
            if (hit == null)
                throw new InvalidOperationException(
                    "No such corpus template: " + name + ". The corpus is src/Heddle.Tests/TestTemplate/**; a test " +
                    "naming a key that is not there is a broken reference, not an empty input.");
            return hit.Content;
        }

        /// <summary>The absolute path of a corpus file (template or sibling golden) in this consumer's output.</summary>
        public static string FilePath(string name)
        {
            var p = Path.Combine(CorpusDir, name);
            if (!File.Exists(p))
                throw new InvalidOperationException("No such corpus file: " + p);
            return p;
        }

        /// <summary>The raw bytes of a corpus file — used by the encoding gate, which must see the byte-order mark
        /// that <see cref="File.ReadAllText(string)"/> strips.</summary>
        public static byte[] Bytes(string name) => File.ReadAllBytes(FilePath(name));

        /// <summary>True when the on-disk file starts with a UTF-8 byte-order mark. BOM presence and line-ending
        /// normalization are INDEPENDENT pins: <c>.gitattributes</c>' <c>eol=lf</c> governs the latter and says
        /// nothing at all about the former, which is why the pre-program BOM drift across the generator snapshots
        /// passed every <c>eol=lf</c> check.</summary>
        public static bool HasUtf8Bom(string name)
        {
            var b = Bytes(name);
            return b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
        }

        /// <summary>Every non-<c>.heddle</c> file in the corpus (the sibling output goldens), ordinal-sorted by
        /// name.</summary>
        public static IReadOnlyList<string> GoldenNames() =>
            Directory.EnumerateFiles(CorpusDir, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(n => !n.EndsWith(".heddle", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Where a test writes its rendered output when it wants the artifact on disk for eyeballing. Phase 7 WI4:
        /// these 25 writes used to land inside the corpus directory, which made the corpus simultaneously input and
        /// output and therefore un-checkable for byte neutrality — and, once the corpus is copied into several
        /// consumers' outputs, would have had one project's test run writing files into a directory another
        /// project's gate enumerates. The directory is created on demand in the writer's OWN output.
        /// </summary>
        public static string WrittenArtifactPath(string fileName)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, WrittenArtifactFolder);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
        }
    }
}
