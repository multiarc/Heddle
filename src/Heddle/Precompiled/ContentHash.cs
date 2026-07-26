using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Heddle.Precompiled
{
    /// <summary>Shared content-hash rule for both runtime and build-time staleness checks.
    /// The staleness identity is lowercase-hex SHA-256 of decoded text as UTF-8 without a BOM;
    /// re-encoding or BOM changes alone do not affect the compiled output.</summary>
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
