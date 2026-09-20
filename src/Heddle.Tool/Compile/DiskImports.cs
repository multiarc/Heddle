using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Heddle.Tool.Compile
{
    /// <summary>One file a compile read off disk to serve an <c>@&lt;&lt;</c> import: the path the
    /// reader resolved, and the hash of the text the reader returned — or no hash, when the caller
    /// holds only the path a previous compile recorded and the content is to be read now.</summary>
    internal sealed class DiskImportRead
    {
        internal DiskImportRead(string path, string contentHash)
        {
            Path = path;
            ContentHash = contentHash;
        }

        internal string Path { get; }

        internal string ContentHash { get; }
    }

    /// <summary>The files a compile read off disk to serve an <c>@&lt;&lt;</c> import — a library no
    /// <c>--template</c> or <c>--import-only</c> item declares, reached through the reader's disk
    /// fallback. The compile writes the paths beside the stamp: the next run folds their content into
    /// the stamp, and the targets read the same list back as compile inputs, so a shared library
    /// outside the item set decides the artifact as visibly as a declared template does.
    /// <para>The hash is taken from the text the reader handed the parse, never re-read afterwards:
    /// a file saved while the compile was reading it would otherwise be certified by content the
    /// artifact was never built from.</para></summary>
    internal sealed class DiskImports
    {
        private static readonly DiskImportRead[] None = new DiskImportRead[0];

        private readonly SortedDictionary<string, string> _reads =
            new SortedDictionary<string, string>(StringComparer.Ordinal);

        private bool _unrecorded;

        /// <summary>Records one disk-served import by the full path the reader resolved it to and the
        /// hash of the text it returned.</summary>
        internal void Record(string path, string contentHash)
        {
            if (string.IsNullOrEmpty(path))
                return;
            // One path per line is the shape the targets read back as items, and a path carrying a line
            // separator does not survive it: written, it would hand MSBuild two inputs naming nothing and
            // leave the stamp unable ever to match its own record. It is left out and said so instead.
            if (path.IndexOf('\n') >= 0 || path.IndexOf('\r') >= 0)
            {
                _unrecorded = true;
                return;
            }

            string first;
            if (_reads.TryGetValue(path, out first))
            {
                // The same file answered twice with different text: it was saved while this compile was
                // reading it, so the artifact carries both and no single hash describes what was built.
                if (!string.Equals(first, contentHash, StringComparison.Ordinal))
                    _unrecorded = true;
                return;
            }

            _reads.Add(path, contentHash);
        }

        /// <summary>Whether the compile read something this record does not describe. The stamp is then
        /// salted rather than written over a half-truth, so it certifies nothing and the next compile
        /// runs instead of finding itself up to date.</summary>
        internal bool Unrecorded => _unrecorded;

        /// <summary>The recorded reads, deduplicated and ordinal-sorted by path: one import reached from
        /// several documents is one dependency, and the order the parse happened to reach them in is
        /// not one.</summary>
        internal IReadOnlyList<DiskImportRead> Reads
        {
            get
            {
                var reads = new List<DiskImportRead>(_reads.Count);
                foreach (var read in _reads)
                    reads.Add(new DiskImportRead(read.Key, read.Value));
                return reads;
            }
        }

        /// <summary>The paths the previous compile wrote, carrying no hash — their content is read now,
        /// which is what makes the check a check. Nothing when there is no list: the first build, or one
        /// whose outputs a clean removed, both of which run the compile anyway. A list that cannot be
        /// read also reads as nothing, which differs from whatever the stamp recorded and so recompiles
        /// rather than trusting a file it could not see.</summary>
        internal static IReadOnlyList<DiskImportRead> Read(string listPath)
        {
            if (string.IsNullOrEmpty(listPath))
                return None;
            try
            {
                var lines = File.ReadAllLines(listPath, new UTF8Encoding(false));
                var reads = new List<DiskImportRead>(lines.Length);
                foreach (var line in lines)
                    if (line.Length != 0)
                        reads.Add(new DiskImportRead(line, null));
                return reads;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return None;
            }
        }

        /// <summary>Writes the paths, one per line, LF-terminated — the shape an MSBuild
        /// <c>ReadLinesFromFile</c> turns straight back into items. Written on every compile that
        /// produced outputs, empty list included, so a library that stopped being imported stops
        /// being an input.</summary>
        internal static void Write(string listPath, IReadOnlyList<DiskImportRead> reads)
        {
            if (string.IsNullOrEmpty(listPath))
                return;
            string directory = Path.GetDirectoryName(listPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var text = new StringBuilder();
            foreach (var read in reads)
                text.Append(read.Path).Append('\n');
            File.WriteAllText(listPath, text.ToString(), new UTF8Encoding(false));
        }
    }
}
