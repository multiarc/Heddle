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
    /// Lockstep test ensuring the generator's hash, runtime gauntlet, and pinned rule implementation
    /// agree for all supported on-disk encodings. A past divergence made BOM'd/UTF-16 templates permanently
    /// <c>StaleContent</c>.
    /// </summary>
    public class ContentHashLockstepTests
    {
        private const string Key = "hashlockstep.heddle";
        private const string Content = "@model(){{System.String}}@\\\nhash é中 @(this)\n";

        /// <summary>Supported on-disk encodings with their pinned contracts.</summary>
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

        /// <summary>Encoding-only re-save (same characters, different on-disk bytes) does not change the hash — hashing the text domain, not byte representation.</summary>
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

        /// <summary>The pinned rule restated independently: lowercase-hex SHA-256 of text decoded then re-encoded as UTF-8 without BOM. Independent implementation avoids circular proof (both sides calling the shared method).</summary>
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
