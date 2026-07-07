using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// <para>Import-provenance marker (phase 6 D25): stamped, only when
    /// <see cref="Data.TemplateOptions.ProvideLanguageFeatures"/> is on, onto diagnostics and parse contexts
    /// produced by an imported/partial file's parse or call-site compile. The LSP facade re-anchors such
    /// diagnostics to the import site (a zero-width range) and prefixes their message with the origin path.</para>
    /// <para><see cref="Path"/> is the resolved absolute path of the file whose parse/compile produced the
    /// artifact (the deepest origin); <see cref="Site"/> is the import/partial site span in the coordinate space
    /// of the document under analysis. Deliberately mutable — instance <b>sharing</b> is the nested re-anchoring
    /// mechanism (an outer import re-anchors <see cref="Site"/> while <see cref="Path"/> keeps the deepest file).</para>
    /// </summary>
    internal sealed class ImportOrigin
    {
        internal ImportOrigin(string path, BlockPosition site)
        {
            Path = path;
            Site = site;
        }

        internal string Path { get; set; }

        internal BlockPosition Site { get; set; }
    }
}
