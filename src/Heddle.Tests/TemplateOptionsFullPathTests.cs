using System.IO;
using Heddle;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <c>TemplateOptions.FullPath</c> and <c>FileReader.GetFileName</c> must compose by the same rule; a
    /// shipped bug fixed this divergence. Expected values are pinned against both directions.
    /// </summary>
    public class TemplateOptionsFullPathTests
    {
        private static TemplateOptions Options(string root, string name, string postfix) =>
            new TemplateOptions(name) { RootPath = root, FileNamePostfix = postfix };

        [Theory]
        // Prior bug: root without trailing separator produced "/a/bt.heddle"
        [InlineData("/a/b", "t", ".heddle", "/a/b/t.heddle")]
        // Root with trailing separator — unchanged by the fix
        [InlineData("/a/b/", "t", ".heddle", "/a/b/t.heddle")]
        // Sub-path name against separator-less root: was "/a/bsub/t.heddle"
        [InlineData("/a/b", "sub/t", ".heddle", "/a/b/sub/t.heddle")]
        // Empty root — unchanged
        [InlineData("", "t", ".heddle", "t.heddle")]
        public void FullPathComposesThroughThePathCombineRule(string root, string name, string postfix,
            string expected)
        {
            // Separators are normalised because the rule under test is the COMPOSITION -- exactly one
            // separator between root and name, and none invented when the root already ends in one --
            // not which character the platform writes. Path.Combine inserts '\' on Windows, so pinning
            // the POSIX spelling failed there while the behaviour was correct. Normalising still
            // catches both shipped bugs these rows exist for: a MISSING separator ("/a/bt.heddle") and
            // a doubled one both survive it. That FullPath and the reader agree is pinned separately
            // by TheFileReaderResolvesExactlyFullPath, which compares them to each other.
            Assert.Equal(expected, Normalise(Options(root, name, postfix).FullPath));
        }

        private static string Normalise(string path) => path.Replace('\\', '/');

        /// <summary>Whatever <c>FullPath</c> says, the reader must open that exact file.</summary>
        [Theory]
        [InlineData("/a/b", "t", ".heddle")]
        [InlineData("/a/b/", "t", ".heddle")]
        [InlineData("/a/b", "sub/t", ".heddle")]
        public void TheFileReaderResolvesExactlyFullPath(string root, string name, string postfix)
        {
            var options = Options(root, name, postfix);
            Assert.Equal(options.FullPath, new FileReader(options).GetFileName());
        }
    }
}
