using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Heddle.Benchmarks.Dotnet.Engines
{
    /// <summary>
    /// Template-source loader. Templates live as files under
    /// <c>benchmarks/dotnet/templates/&lt;track&gt;/&lt;engine&gt;/</c>, matching every other
    /// ecosystem harness, rather than as C# string constants the way the retired project stored
    /// them. Files make the idiomatic track reviewable as templates instead of as escaped literals,
    /// and they let a reader diff one engine's authoring against another's.
    ///
    /// Read once at startup and cached: template *parse* cost belongs to the cold-compile sidebar,
    /// never to a render measurement.
    /// </summary>
    public static class Templates
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static string _root;

        /// <summary>The templates root, resolved by the same walk-up the corpus loader uses.</summary>
        public static string Root()
        {
            if (_root != null) return _root;
            var probe = new DirectoryInfo(AppContext.BaseDirectory);
            while (probe != null)
            {
                var candidate = Path.Combine(probe.FullName, "benchmarks", "dotnet", "templates");
                if (System.IO.Directory.Exists(candidate)) return _root = candidate;
                probe = probe.Parent;
            }
            throw new InvalidOperationException(
                "templates/ not found: walked up from " + AppContext.BaseDirectory);
        }

        /// <summary>Loads one template source, e.g. <c>Load("controlled", "liquid", "layout.liquid")</c>.</summary>
        public static string Load(string track, string engine, string file)
        {
            var key = track + "/" + engine + "/" + file;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var path = Path.Combine(Root(), track, engine, file);
            if (!File.Exists(path))
                throw new FileNotFoundException($"template not found: templates/{key}", path);

            // Templates are stored exactly as the engine must see them. No trimming, no line-ending
            // rewriting: whitespace is erased by N3b at comparison time, so the harness has no
            // business normalizing a source file and hiding what the engine was actually given.
            var text = File.ReadAllText(path, Utf8NoBom);
            Cache[key] = text;
            return text;
        }
    }
}
