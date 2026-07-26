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
            Assert.Equal(expected, Options(root, name, postfix).FullPath);
        }

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
