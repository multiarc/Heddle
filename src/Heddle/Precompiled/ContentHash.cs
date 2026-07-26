using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The shared content-hash rule for precompiled templates. One pure, IO-free source file,
    /// compiled into both <c>Heddle</c> (runtime staleness check) and the <c>Heddle.Generator</c> analyzer
    /// (build-time manifest emission) so the two sides cannot hash different things.</para>
    /// <para><b>The staleness identity of a template is the lowercase-hex SHA-256 of its decoded text re-encoded
    /// as UTF-8 without a BOM.</b> The text domain — not the raw file bytes — is canonical on both sides: the
    /// parser and the emitter consume decoded text, so the compiled artifact depends on exactly that form, and
    /// re-saving a template with a BOM or as UTF-16 without changing a character does not change the compiled
    /// output and must not read as stale. It is also the only implementable choice on the build side, whose only
    /// input is Roslyn's decoded <c>SourceText</c> (direct file IO is a banned API in an analyzer).</para>
    /// <para><b>Encoding edge.</b> A file with no BOM that is not valid UTF-8 is outside the contract: the build
    /// may decode it through the system code page while the runtime decodes UTF-8 with replacement, so its
    /// staleness verdict is unspecified. The failure mode is the safe one — a <c>StaleContent</c> fallback to the
    /// byte-identical dynamic path.</para>
    /// </summary>
    public static class ContentHash
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>Hashes decoded template text — the canonical staleness identity.</summary>
        /// <param name="text">The template's decoded text (no BOM character).</param>
        /// <returns>The lowercase hex SHA-256 of the text's UTF-8 (BOM-less) encoding.</returns>
        public static string HashText(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            return HashBytes(Utf8NoBom.GetBytes(text));
        }

        /// <summary>Hashes a byte buffer with the same digest/hex rule as <see cref="HashText"/>.</summary>
        public static string HashBytes(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            using (var sha = SHA256.Create())
                return ToHex(sha.ComputeHash(bytes));
        }

        /// <summary>Lowercase, invariant-culture hex folding — the one hex rule both sides share.</summary>
        public static string ToHex(byte[] hash)
        {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }
}
