using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Documentation links resolve, and a <c>name.cs:NN</c> citation names a line the file has. A citation is a
    /// promise a reader follows, and following one into a deleted file or past the end of a shortened one costs the
    /// reader the trust they extended.
    /// <para>Every documentation tree is covered, published and internal alike — <c>docs/**</c>, the benchmark
    /// docs under <c>benchmarks/**</c>, the repository README and the CHANGELOG — so no tree's links can rot
    /// unnoticed.</para>
    /// </summary>
    public class DocumentationLinkTests
    {
        /// <summary>Link targets that name a repository file, by extension — prose links to headings are covered by
        /// the docs site build, which fails on a dead intra-site link.</summary>
        private static readonly Regex FileLink = new Regex(
            @"\]\((?<link>[^)\s#]+\.(?:cs|csproj|props|targets|json|tcs|xml|snk|sh|ps1|py|rs|go|ts|js|mjs|md|heddle|txt))(?:#L(?<anchor>\d+))?\)",
            RegexOptions.Compiled);

        /// <summary>A <c>[File.cs:12](path)</c>, <c>[File.cs:12-20](path)</c> or <c>[File.cs:12,91](path)</c>
        /// citation. Every number is captured, not just the first: the defect that prompted this gate was a
        /// <c>99-100</c> range against a 93-line file, whose <b>start</b> was in range.</summary>
        private static readonly Regex LineCitation = new Regex(
            @"\[`?(?<name>[A-Za-z0-9_.]+\.(?:cs|csproj|props|targets|tcs|json|md))`?:(?<line>\d+)(?:[-,](?<line>\d+))*`?\]\((?<link>[^)\s#]+)\)",
            RegexOptions.Compiled);

        [Fact]
        public void EveryDocumentedFileLinkResolves()
        {
            var broken = new List<string>();
            var checkedCount = 0;

            foreach (var doc in Documents())
            {
                var directory = Path.GetDirectoryName(doc);
                foreach (Match match in FileLink.Matches(File.ReadAllText(doc)))
                {
                    var link = match.Groups["link"].Value;
                    if (!IsRepositoryPath(link))
                        continue;

                    checkedCount++;
                    var target = Path.GetFullPath(Path.Combine(directory, link));
                    if (!File.Exists(target))
                    {
                        broken.Add(Relative(doc) + " → " + link);
                        continue;
                    }

                    // The #L anchor is a line number too, and was captured but never read.
                    var anchor = match.Groups["anchor"];
                    if (anchor.Success)
                    {
                        var lines = File.ReadAllLines(target).Length;
                        if (int.Parse(anchor.Value) > lines)
                            broken.Add(Relative(doc) + " → " + link + "#L" + anchor.Value +
                                       " but the file has " + lines + " lines");
                    }
                }
            }

            // Floors sit just under the current counts, so a convention change that silently drops most of the
            // surface reddens instead of passing on a remnant. Re-pinned when the plan/spec trees were retired
            // and the benchmark contract docs moved under benchmarks/docs/.
            Assert.True(checkedCount > 360,
                "Only " + checkedCount + " file links were checked — the link convention changed and this gate is " +
                "no longer covering the documentation.");
            Assert.True(broken.Count == 0,
                "Documentation links pointing at files that do not exist: " + string.Join("; ", broken));
        }

        [Fact]
        public void EveryLineCitationNamesALineTheFileHas()
        {
            var stale = new List<string>();
            var checkedCount = 0;

            foreach (var doc in Documents())
            {
                var directory = Path.GetDirectoryName(doc);
                foreach (Match match in LineCitation.Matches(File.ReadAllText(doc)))
                {
                    var link = match.Groups["link"].Value;
                    if (!IsRepositoryPath(link))
                        continue;

                    var target = Path.GetFullPath(Path.Combine(directory, link));
                    if (!File.Exists(target))
                        continue;   // the missing-file case is the other test's, reported once

                    checkedCount++;
                    var lines = File.ReadAllLines(target).Length;
                    foreach (Capture capture in match.Groups["line"].Captures)
                    {
                        var cited = int.Parse(capture.Value);
                        if (cited > lines)
                            stale.Add(Relative(doc) + " cites " + match.Groups["name"].Value + ":" + cited +
                                      " but the file has " + lines + " lines");
                    }
                }
            }

            // No count floor: the line-citation population went to zero when the generator-plan tree (which held
            // every citation) was retired. The staleness check stays armed for any citation that returns;
            // reinstate a floor once a tree adopts the convention again.
            Assert.True(stale.Count == 0, "Stale line citations: " + string.Join("; ", stale));
        }

        /// <summary>A link is a repository path if it is relative and not a placeholder or an external URL. Placeholder
        /// text appears in this program's own plans, which quote example citations verbatim.</summary>
        private static bool IsRepositoryPath(string link)
        {
            if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                link.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                return false;
            if (link.IndexOf('<') >= 0 || link.IndexOf('\u2026') >= 0)
                return false;   // a documented example path, not a link: "../<run-date>/index.md"
            // A same-directory link is still a path. Requiring a slash once skipped more than half the links —
            // same-directory forms dominate the internal trees, so the gate would be checking the easier half
            // of the documentation it claims to cover.
            return true;
        }

        private static IEnumerable<string> Documents()
        {
            var root = RepoRoot();
            var docs = Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
                .Where(p => !p.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar))
                .Where(p => !p.Contains(Path.DirectorySeparatorChar + ".vitepress" + Path.DirectorySeparatorChar))
                .ToList();

            // The benchmark trees carry the harness contract docs and per-ecosystem reproduce books; build
            // output and vendored material are not documentation.
            var benchmarks = Path.Combine(root, "benchmarks");
            if (Directory.Exists(benchmarks))
                docs.AddRange(Directory.GetFiles(benchmarks, "*.md", SearchOption.AllDirectories)
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar))
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "third-party" + Path.DirectorySeparatorChar))
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "out" + Path.DirectorySeparatorChar))
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "target" + Path.DirectorySeparatorChar))
                    .Where(p => !p.Contains("target.rootowned-preserved"))
                    .Where(p => !p.Contains("BenchmarkDotNet.Artifacts")));

            docs.Add(Path.Combine(root, "README.md"));
            docs.Add(Path.Combine(root, "CHANGELOG.md"));
            return docs.Where(File.Exists);
        }

        private static string Relative(string path)
        {
            var root = RepoRoot();
            return path.StartsWith(root, StringComparison.Ordinal) ? path.Substring(root.Length + 1) : path;
        }

        private static string RepoRoot([CallerFilePath] string here = null)
        {
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
        }
    }
}
