using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 5 WI1 (D1) lockstep gate for the content-hash rule. The generator's emitted manifest hash, the runtime
    /// gauntlet's <c>HashFile</c>, and the pinned rule restated in this file must agree for every supported
    /// on-disk encoding — the drift that made every BOM'd/UTF-16 template permanently <c>StaleContent</c>
    /// (05 F1). The generator half runs the <b>real</b> generator over the same characters; the runtime half reads
    /// real bytes off disk, so a decoder divergence is a red test rather than a silent per-request fallback.
    /// </summary>
    public class ContentHashLockstepTests
    {
        private const string Key = "hashlockstep.heddle";
        private const string Content = "@model(){{System.String}}@\\\nhash é中 @(this)\n";

        /// <summary>The on-disk encodings inside the pinned contract: a BOM is honored and stripped, and a
        /// BOM-less file is UTF-8.</summary>
        public static IEnumerable<object[]> Encodings => new[]
        {
            new object[] { "utf8-no-bom", (object)new UTF8Encoding(false) },
            new object[] { "utf8-bom", (object)new UTF8Encoding(true) },
            new object[] { "utf16-le-bom", (object)new UnicodeEncoding(false, true) },
            new object[] { "utf16-be-bom", (object)new UnicodeEncoding(true, true) },
        };

        [Theory]
        [MemberData(nameof(Encodings))]
        public void GeneratorHashRuntimeHashAndSharedRuleAgreeForEveryEncoding(string name, Encoding encoding)
        {
            var generated = ManifestContentHash(Content);
            var path = WriteFixture(name, Content, encoding, out var dir);
            try
            {
                Assert.Equal(ExpectedHash(Content), generated);
                Assert.Equal(generated, PrecompiledGauntlet.HashFile(path));
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(dir);
            }
        }

        [Theory]
        [MemberData(nameof(Encodings))]
        public void OneCharacterEditChangesAllThreeHashes(string name, Encoding encoding)
        {
            var edited = Content.Replace("hash ", "hasH ");
            var generated = ManifestContentHash(edited);
            var path = WriteFixture(name + "-edited", edited, encoding, out var dir);
            try
            {
                Assert.NotEqual(ExpectedHash(Content), ExpectedHash(edited));
                Assert.Equal(ExpectedHash(edited), generated);
                Assert.Equal(generated, PrecompiledGauntlet.HashFile(path));
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(dir);
            }
        }

        /// <summary>An encoding-only re-save (the same characters, a different on-disk byte shape) is not an edit —
        /// the whole point of hashing the text domain (D1).</summary>
        [Fact]
        public void EncodingOnlyResaveDoesNotChangeTheRuntimeHash()
        {
            var utf8 = WriteFixture("resave-utf8", Content, new UTF8Encoding(false), out var dir1);
            var utf16 = WriteFixture("resave-utf16", Content, new UnicodeEncoding(false, true), out var dir2);
            try
            {
                Assert.Equal(PrecompiledGauntlet.HashFile(utf8), PrecompiledGauntlet.HashFile(utf16));
            }
            finally
            {
                DifferentialHarness.TryDeleteDirectory(dir1);
                DifferentialHarness.TryDeleteDirectory(dir2);
            }
        }

        /// <summary>The rule, restated independently of both implementations: lowercase-hex SHA-256 of the decoded
        /// text re-encoded as UTF-8 without a BOM. A lockstep test that called the shared helper would only prove the
        /// two sides call the same method, not that the method computes the pinned identity.</summary>
        private static string ExpectedHash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder(64);
                foreach (var b in sha.ComputeHash(new UTF8Encoding(false).GetBytes(text)))
                    sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static string ManifestContentHash(string content)
        {
            var gen = DifferentialHarness.Generate(new[] { (Key, content) });
            DifferentialHarness.ExpectPrecompiled(gen, Key);
            var match = Regex.Match(gen.ManifestSource, "contentHash:\\s*\"(?<h>[0-9a-f]{64})\"");
            Assert.True(match.Success, "No contentHash literal in the emitted manifest:\n" + gen.ManifestSource);
            return match.Groups["h"].Value;
        }

        private static string WriteFixture(string name, string content, Encoding encoding, out string dir)
        {
            dir = Path.Combine(Path.GetTempPath(), "heddle-hash-" + name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, Key);
            using (var stream = File.Create(path))
            {
                var preamble = encoding.GetPreamble();
                stream.Write(preamble, 0, preamble.Length);
                var body = encoding.GetBytes(content);
                stream.Write(body, 0, body.Length);
            }

            return path;
        }
    }
}
