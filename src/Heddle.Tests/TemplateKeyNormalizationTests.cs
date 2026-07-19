using System;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The phase 7 D1 key-normalization gate: the pinned cross-OS round-trip table from
    /// identity-and-metadata.md § normalization test table. <see cref="TemplateKey.Normalize"/> is a pure
    /// function, so these rows hold identically on every OS by construction — the table proves it stays that
    /// way. Each row's rationale names the algorithm step(s) it exercises.
    /// </summary>
    public class TemplateKeyNormalizationTests
    {
        [Theory]
        // N01 — step 2 separator unification (Windows-shaped item spec).
        [InlineData(@"views\home\index.heddle", "views/home/index.heddle")]
        // N02 — the round-trip row: a Linux-shaped input produces the same key as N01.
        [InlineData("views/home/index.heddle", "views/home/index.heddle")]
        // N03 — step 4 (~/ strip) + step 3 (duplicate-separator collapse).
        [InlineData("~/views//home/index.heddle", "views/home/index.heddle")]
        // N04 — step 4 (./ strip) + step 6 (.heddle appended to an extension-less final segment).
        [InlineData("./views/home/index", "views/home/index.heddle")]
        // N05 — step 4 (leading / strip) + step 2 (mixed separators in one input).
        [InlineData(@"/views\home/index.heddle", "views/home/index.heddle")]
        // N06 — step 7 case preserved (≠ N01 ordinally).
        [InlineData("Views/Home/Index.heddle", "Views/Home/Index.heddle")]
        // N09 — step 6 no-op: an existing extension (any extension) is kept.
        [InlineData("emails/receipt.v2.heddle", "emails/receipt.v2.heddle")]
        // N11 — a root-level template (zero directory segments) is a legal key.
        [InlineData("home.heddle", "home.heddle")]
        // N12 — explicit Key metadata passes the same function; step 6 appends.
        [InlineData("key-from-integration", "key-from-integration.heddle")]
        public void NormalizesToPinnedKey(string input, string expected)
        {
            Assert.Equal(expected, TemplateKey.Normalize(input));

            Assert.True(TemplateKey.TryNormalize(input, out var key));
            Assert.Equal(expected, key);
        }

        [Theory]
        // N07 — step 5: '..' is a path escape.
        [InlineData("views/../secrets.heddle")]
        // N08 — step 5: '.' segments are rejected rather than silently collapsed.
        [InlineData("views/./home.heddle")]
        // N10 — step 1: whitespace input.
        [InlineData("   ")]
        [InlineData(null)]
        // A trailing separator leaves an empty segment (step 5).
        [InlineData("views/home/")]
        public void RejectsInvalidInput(string input)
        {
            Assert.Throws<ArgumentException>(() => TemplateKey.Normalize(input));

            Assert.False(TemplateKey.TryNormalize(input, out var key));
            Assert.Null(key);
        }

        /// <summary>
        /// N06 vs N01: case-only twins are distinct keys under ordinal comparison — the companion of the
        /// build-time <c>HED7003</c> / lookup-time <c>HED7103</c> case-mismatch diagnostics.
        /// </summary>
        [Fact]
        public void CaseOnlyTwinsAreDistinctKeys()
        {
            var lower = TemplateKey.Normalize("views/home/index.heddle");
            var mixed = TemplateKey.Normalize("Views/Home/Index.heddle");

            Assert.NotEqual(lower, mixed);
            Assert.Equal(lower, mixed, StringComparer.OrdinalIgnoreCase);
        }
    }
}
