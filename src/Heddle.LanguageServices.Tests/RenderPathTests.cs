using System;
using System.IO;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Validates refactored <c>RenderPath</c> against the pre-refactor implementation; three test vectors show
    /// intentional behavioral changes.
    /// </summary>
    public class RenderPathTests
    {
        #region Legacy body

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
            { "/root/a/b.heddle", "/ROOT", "a/b.heddle" },      // case-insensitive
            { "/other/b.heddle", "/root", "/other/b.heddle" },  // outside root
            { "/root/a/b.heddle", "", "/root/a/b.heddle" },     // empty root
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

        /// <summary>Sibling directories starting with root's prefix are no longer misidentified; <c>TryMakeRelative</c> requires separator.</summary>
        [Fact]
        public void ASiblingDirectorySharingTheRootsPrefixIsNoLongerRenderedAsAKey()
        {
            Assert.Equal("x/a.heddle", LegacyRenderPath("/rootx/a.heddle", "/root"));       // measured: the bug
            Assert.Equal("/rootx/a.heddle", DocumentAnalyzer.RenderPath("/rootx/a.heddle", "/root"));
        }

        /// <summary>Root directory renders as absolute path instead of empty string.</summary>
        [Fact]
        public void TheRootItselfRendersAbsoluteRatherThanEmpty()
        {
            Assert.Equal("", LegacyRenderPath("/root", "/root"));                            // measured
            Assert.Equal("/root", DocumentAnalyzer.RenderPath("/root", "/root"));
        }

        /// <summary>Path above root renders as written (separators normalized) rather than resolved; both implementations agree.</summary>
        [Fact]
        public void APathAboveTheRootRendersAbsolute()
        {
            Assert.Equal(LegacyRenderPath("/root/../a.heddle", "/root"),
                DocumentAnalyzer.RenderPath("/root/../a.heddle", "/root"));
            Assert.Equal("/root/../a.heddle", DocumentAnalyzer.RenderPath("/root/../a.heddle", "/root"));
        }
    }
}
