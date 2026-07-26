using System;
using System.Text;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <para>The shared key-normalization rule for precompiled templates. One pure function,
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
        /// <summary>The template file extension; referenced by MSBuild glob <c>**\*.heddle</c> in <c>Heddle.Generator.targets</c>.</summary>
        public const string TemplateExtension = ".heddle";

        /// <summary>Whether a path or key carries the template extension. <b>Case-insensitive</b> — the policy both
        /// pre-existing sites (generator discovery, partial-name stripping) already used.</summary>
        public static bool HasTemplateExtension(string pathOrKey) =>
            pathOrKey != null &&
            pathOrKey.EndsWith(TemplateExtension, StringComparison.OrdinalIgnoreCase);

        /// <summary>Drops the template extension when present, yielding the template <i>name</i>; otherwise returns
        /// the input unchanged.</summary>
        public static string StripTemplateExtension(string pathOrKey) =>
            HasTemplateExtension(pathOrKey)
                ? pathOrKey.Substring(0, pathOrKey.Length - TemplateExtension.Length)
                : pathOrKey;

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

        /// <summary>
        /// <para>Derives the canonical key of a template file from its path and the template root — the build-side
        /// half of the key↔path pair. Returns <c>false</c> when <paramref name="path"/> is not under
        /// <paramref name="root"/>: there is <b>no</b> filename fallback here, because silently dropping the
        /// directory produces a key no runtime lookup can hit. A caller that wants the historical flattened
        /// key must ask for it explicitly, and say so.</para>
        /// <para><b>Two case domains, deliberately.</b> The root-prefix test is
        /// <see cref="StringComparison.OrdinalIgnoreCase"/> — it compares <i>filesystem paths</i>, and MSBuild
        /// routinely varies drive-letter and directory casing on Windows. Everything after the prefix — the key —
        /// preserves case exactly and compares <see cref="StringComparer.Ordinal"/>, per this type's contract.</para>
        /// </summary>
        /// <param name="path">The template file's path (either separator convention).</param>
        /// <param name="root">The template root the key is relative to.</param>
        /// <param name="key">The normalized key on success; <c>null</c> otherwise.</param>
        /// <returns><c>true</c> when the path is under the root and the relative part is a valid key.</returns>
        public static bool TryMakeRelative(string path, string root, out string key)
        {
            key = null;
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
                return false;

            var normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
            var normalizedPath = path.Replace('\\', '/');
            if (!normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            return TryNormalize(normalizedPath.Substring(normalizedRoot.Length + 1), out key);
        }

        /// <summary>The inverse of <see cref="TryMakeRelative"/>: the on-disk path a key names under a
        /// root, with <c>/</c> re-separated for the running platform. Used by the staleness check to find the file a
        /// manifest entry was built from.</summary>
        public static string ToPath(string key, string root)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            return System.IO.Path.Combine(root ?? string.Empty,
                key.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }

        private static bool TryNormalizeCore(string relativePath, out string key, out string error)
        {
            key = null;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                error = "A template key must be a non-empty, non-whitespace relative path.";
                return false;
            }

            var path = relativePath.Replace('\\', '/');

            path = CollapseSlashes(path);

            if (path.StartsWith("~/", StringComparison.Ordinal))
                path = path.Substring(2);
            while (path.StartsWith("./", StringComparison.Ordinal))
                path = path.Substring(2);
            while (path.Length > 0 && path[0] == '/')
                path = path.Substring(1);

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

            var last = segments[segments.Length - 1];
            if (last.IndexOf('.') < 0)
                segments[segments.Length - 1] = last + TemplateExtension;

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
