using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Precompiled;

namespace Heddle.Runtime
{
    /// <summary>Builds the <c>@&lt;&lt;</c> import reader pair over an in-memory spelling map with the
    /// file ladder as fallback. Spellings normalize through <see cref="TemplateKey"/> before the map
    /// lookup — the spelling a template writes (<c>Banner</c>) meets the key a row carries
    /// (<c>Banner.heddle</c>) — while the disk fallback reads the raw spelling, because the ladder
    /// resolves files, not keys. The build host maps every item's key and registered name to its
    /// text; the loader maps every artifact row's. Internal: the build host is the only other caller, and
    /// it duplicates these lines against the same contract rather than widen the public surface for one
    /// consumer.</summary>
    internal static class ImportMap
    {
        internal static Func<string, string> ReaderFor(IReadOnlyDictionary<string, string> contents,
            string rootPath) =>
            spelling =>
            {
                if (spelling != null && TemplateKey.TryNormalize(spelling, out string key) &&
                    contents.TryGetValue(key, out string content))
                    return content;
                using (var file = File.OpenText(Path.Combine(rootPath ?? string.Empty, spelling)))
                    return file.ReadToEnd();
            };

        internal static Func<string, string> IdentifierFor(IReadOnlyDictionary<string, string> contents,
            string rootPath) =>
            spelling =>
            {
                if (spelling != null && TemplateKey.TryNormalize(spelling, out string key) &&
                    contents.ContainsKey(key))
                    return "import:" + key;
                try
                {
                    return Path.GetFullPath(Path.Combine(rootPath ?? string.Empty, spelling));
                }
                catch (Exception)
                {
                    return spelling;
                }
            };
    }
}
