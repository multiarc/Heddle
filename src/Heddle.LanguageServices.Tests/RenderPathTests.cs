using System;
using System.IO;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Generator plan phase 6 D8 / WI10 — <c>DocumentAnalyzer.RenderPath</c> is now
    /// <c>TemplateKey.TryMakeRelative</c> (phase 5's shared relativization, with its documented two-case-domain
    /// policy) plus the LSP's own absolute-path fallback. The fourth hand-rolled prefix strip is deleted.
    /// <para>The legacy body below is the pre-change implementation transcribed <b>verbatim</b> — the
    /// characterization oracle. Every vector is checked against it, and the three vectors where the two
    /// deliberately differ are named, with the legacy answer recorded, so the delta this WI ships is measured
    /// rather than remembered.</para>
    /// </summary>
    public class RenderPathTests
    {
        #region Legacy body — verbatim pre-WI10 DocumentAnalyzer.RenderPath

        private static string LegacyRenderPath(string path, string root)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (!string.IsNullOrEmpty(root))
            {
                try
                {
                    var full = Path.GetFullPath(path);
                    var rootFull = Path.GetFullPath(root);
                    if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                    {
                        var rel = full.Substring(rootFull.Length).TrimStart('/', '\\');
                        return rel.Replace('\\', '/');
                    }
                }
                catch
                {
                    // fall through to absolute
                }
            }

            return path.Replace('\\', '/');
        }

        #endregion

        public static TheoryData<string, string, string> Agreeing => new TheoryData<string, string, string>
        {
            { "/root/a/b.heddle", "/root", "a/b.heddle" },
            { "/root/b.heddle", "/root/", "b.heddle" },
            { "/root/a/b.heddle", "/ROOT", "a/b.heddle" },      // the documented case-insensitive prefix domain
            { "/other/b.heddle", "/root", "/other/b.heddle" },  // outside the root — absolute, '/'-formed
            { "/root/a/b.heddle", "", "/root/a/b.heddle" },     // no root configured
            { "/root/a/b.heddle", null, "/root/a/b.heddle" },
            { "", "/root", "" },
            { null, "/root", null },
        };

        [Theory]
        [MemberData(nameof(Agreeing))]
        public void RenderPathMatchesTheLegacyBodyAndThePin(string path, string root, string expected)
        {
            Assert.Equal(expected, LegacyRenderPath(path, root));
            Assert.Equal(expected, DocumentAnalyzer.RenderPath(path, root));
        }

        /// <summary>The defect the shared rule fixes: <c>StartsWith(rootFull)</c> matched a <i>sibling</i>
        /// directory whose name merely began with the root's, and the LSP then displayed a file outside the
        /// workspace as if it were a key inside it. <c>TryMakeRelative</c> requires the separator.</summary>
        [Fact]
        public void ASiblingDirectorySharingTheRootsPrefixIsNoLongerRenderedAsAKey()
        {
            Assert.Equal("x/a.heddle", LegacyRenderPath("/rootx/a.heddle", "/root"));       // measured: the bug
            Assert.Equal("/rootx/a.heddle", DocumentAnalyzer.RenderPath("/rootx/a.heddle", "/root"));
        }

        /// <summary>The root itself is not a template under the root. Legacy returned the empty string — a
        /// display of nothing at all; the shared rule declines and the absolute path is shown.</summary>
        [Fact]
        public void TheRootItselfRendersAbsoluteRatherThanEmpty()
        {
            Assert.Equal("", LegacyRenderPath("/root", "/root"));                            // measured
            Assert.Equal("/root", DocumentAnalyzer.RenderPath("/root", "/root"));
        }

        /// <summary>A path that resolves above the root is outside the key domain, and the fallback shows it as
        /// written (only separators normalized) rather than as its resolved form. Both bodies agree; pinned
        /// because it is the case a naive prefix strip is most likely to regress on if anyone reintroduces one.</summary>
        [Fact]
        public void APathAboveTheRootRendersAbsolute()
        {
            Assert.Equal(LegacyRenderPath("/root/../a.heddle", "/root"),
                DocumentAnalyzer.RenderPath("/root/../a.heddle", "/root"));
            Assert.Equal("/root/../a.heddle", DocumentAnalyzer.RenderPath("/root/../a.heddle", "/root"));
        }
    }
}
