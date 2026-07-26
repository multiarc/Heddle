using System.IO;
using Heddle;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 6 D8 / WI10 — <c>TemplateOptions.FullPath</c> composes a path by the same rule
    /// <c>FileReader.GetFileName</c> resolves one with. The two used to differ (naive concatenation vs
    /// <see cref="Path.Combine"/>), and the comment at <c>HeddleTemplate.cs</c> records a shipped bug from a
    /// previous divergence of exactly this pair — so the fix is stated as "one of them <i>is</i> the other",
    /// pinned here from both directions.
    /// <para>The vectors below were measured against the pre-change concatenation first; the "was" column in each
    /// case comment is that measurement, so the delta this WI ships is recorded rather than asserted from
    /// memory.</para>
    /// </summary>
    public class TemplateOptionsFullPathTests
    {
        private static TemplateOptions Options(string root, string name, string postfix) =>
            new TemplateOptions(name) { RootPath = root, FileNamePostfix = postfix };

        [Theory]
        // root without a trailing separator — the recorded prior-bug shape (was "/a/bt.heddle")
        [InlineData("/a/b", "t", ".heddle", "/a/b/t.heddle")]
        // root with a trailing separator — unchanged by the fix (was "/a/b/t.heddle")
        [InlineData("/a/b/", "t", ".heddle", "/a/b/t.heddle")]
        // a keyed sub-path name against a separator-less root (was "/a/bsub/t.heddle")
        [InlineData("/a/b", "sub/t", ".heddle", "/a/b/sub/t.heddle")]
        // empty root — unchanged (was "t.heddle")
        [InlineData("", "t", ".heddle", "t.heddle")]
        public void FullPathComposesThroughThePathCombineRule(string root, string name, string postfix,
            string expected)
        {
            Assert.Equal(expected, Options(root, name, postfix).FullPath);
        }

        /// <summary>The load-bearing half: whatever <c>FullPath</c> says, that is the file the reader opens. This
        /// is the invariant the previous divergence broke.</summary>
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
