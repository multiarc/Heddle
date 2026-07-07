using System;
using System.Text;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The shared key-normalization rule for precompiled templates (phase 7 D1). One pure function,
    /// one source file, compiled into both <c>Heddle</c> (runtime lookup) and the <c>Heddle.Generator</c>
    /// analyzer (build-time emit) so emit-time and lookup-time keys are byte-identical by construction.</para>
    /// <para>Keys are the template's path relative to the resolver root, with backslashes unified to
    /// <c>/</c>, duplicate separators collapsed, leading <c>~/</c>/<c>./</c>/<c>/</c> stripped, <c>.</c> and
    /// <c>..</c> segments rejected, <c>.heddle</c> appended when the final segment carries no extension, and
    /// <b>case preserved exactly</b>. Comparisons are <see cref="StringComparer.Ordinal"/> — case-sensitive,
    /// so two legitimately-distinct case-only file twins never collide, and a case-sloppy lookup misses
    /// (degrading safely to the dynamic path) rather than serving the wrong template.</para>
    /// </summary>
    public static class TemplateKey
    {
        /// <summary>
        /// Normalizes a resolver-relative path into the canonical precompiled key.
        /// </summary>
        /// <param name="relativePath">A resolver-relative path or host path idiom (<c>~/</c>, <c>\</c>, <c>/</c>).</param>
        /// <returns>The normalized, case-preserved, <c>/</c>-separated key.</returns>
        /// <exception cref="ArgumentException">
        /// The input is null/whitespace, contains a <c>.</c> or <c>..</c> segment, ends in a separator, or
        /// normalizes to an empty string.
        /// </exception>
        public static string Normalize(string relativePath)
        {
            if (!TryNormalizeCore(relativePath, out var key, out var error))
                throw new ArgumentException(error, nameof(relativePath));
            return key;
        }

        /// <summary>
        /// Attempts to normalize a resolver-relative path into the canonical precompiled key.
        /// </summary>
        /// <param name="relativePath">A resolver-relative path or host path idiom.</param>
        /// <param name="key">The normalized key on success; <c>null</c> on failure.</param>
        /// <returns><c>true</c> if the input is a valid key; otherwise <c>false</c>.</returns>
        public static bool TryNormalize(string relativePath, out string key)
        {
            return TryNormalizeCore(relativePath, out key, out _);
        }

        private static bool TryNormalizeCore(string relativePath, out string key, out string error)
        {
            key = null;

            // Step 1 — reject null/whitespace input.
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                error = "A template key must be a non-empty, non-whitespace relative path.";
                return false;
            }

            // Step 2 — unify separators.
            var path = relativePath.Replace('\\', '/');

            // Step 3 — collapse runs of '/'.
            path = CollapseSlashes(path);

            // Step 4 — strip a leading '~/', then every leading './', then any leading '/'.
            if (path.StartsWith("~/", StringComparison.Ordinal))
                path = path.Substring(2);
            while (path.StartsWith("./", StringComparison.Ordinal))
                path = path.Substring(2);
            while (path.Length > 0 && path[0] == '/')
                path = path.Substring(1);

            // Step 5 — split, reject '.'/'..' and empty (trailing-separator) segments.
            var segments = path.Split('/');
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment.Length == 0)
                {
                    error = $"Invalid template key '{relativePath}': it must not contain an empty segment or end in a separator.";
                    return false;
                }
                if (segment == "." || segment == "..")
                {
                    error = $"Invalid template key '{relativePath}': '.' and '..' segments are not allowed.";
                    return false;
                }
            }

            if (segments.Length == 0)
            {
                error = $"Invalid template key '{relativePath}': it normalizes to an empty key.";
                return false;
            }

            // Step 6 — append '.heddle' when the final segment carries no extension.
            var last = segments[segments.Length - 1];
            if (last.IndexOf('.') < 0)
                segments[segments.Length - 1] = last + ".heddle";

            // Steps 7 & 8 — case preserved; rejoin with '/'.
            key = string.Join("/", segments);
            error = null;
            return true;
        }

        private static string CollapseSlashes(string value)
        {
            if (value.IndexOf("//", StringComparison.Ordinal) < 0)
                return value;

            var builder = new StringBuilder(value.Length);
            var previousWasSlash = false;
            foreach (var c in value)
            {
                if (c == '/')
                {
                    if (previousWasSlash)
                        continue;
                    previousWasSlash = true;
                }
                else
                {
                    previousWasSlash = false;
                }
                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
